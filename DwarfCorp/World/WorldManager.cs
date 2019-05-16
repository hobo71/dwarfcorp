using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using BloomPostprocess;
using DwarfCorp.Gui;
using DwarfCorp.Gui.Widgets;
using DwarfCorp.Tutorial;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using DwarfCorp.GameStates;
using Newtonsoft.Json;
using DwarfCorp.Events;

namespace DwarfCorp
{
    // Todo: Split into WorldManager and WorldRenderer.
    /// <summary>
    /// This is the main game state for actually playing the game.
    /// </summary>
    public partial class WorldManager : IDisposable
    {
        #region fields

        public WorldRenderer Renderer;
        public OverworldGenerationSettings Settings = null;
        public ChunkManager ChunkManager = null;
        public ComponentManager ComponentManager = null;
        public Yarn.MemoryVariableStore ConversationMemory = new Yarn.MemoryVariableStore();
        public FactionLibrary Factions = null;
        public ParticleManager ParticleManager = null;
        public GameMaster Master = null;
        public Events.Scheduler EventScheduler;
        public int MaxViewingLevel = 0;

        #region Tutorial Hooks

        public Tutorial.TutorialManager TutorialManager;
        
        public void Tutorial(String Name)
        {
            if (TutorialManager != null)
                TutorialManager.ShowTutorial(Name);
        }

        #endregion

        public Diplomacy Diplomacy;
        public Scripting.Gambling GamblingState = new Scripting.Gambling();

        public ContentManager Content;
        public DwarfGame Game;
        public GraphicsDevice GraphicsDevice { get { return GameState.Game.GraphicsDevice; } }
        

        private bool paused_ = false;
        // True if the game's update loop is paused, false otherwise
        public bool Paused
        {
            get { return paused_; }
            set
            {
                paused_ = value;

                if (DwarfTime.LastTime != null)
                    DwarfTime.LastTime.IsPaused = paused_;
            }
        }

        // Handles a thread which constantly runs A* plans for whoever needs them.
        public PlanService PlanService = null;

        // Contains the storm forecast
        public Weather Weather = new Weather();

        // The current calendar date/time of the game.
        public WorldTime Time = new WorldTime();
        
        private SaveGame gameFile;

        public Point3 WorldSizeInChunks { get; set; }

        [JsonIgnore]
        public Point3 WorldSizeInVoxels
        {
            get
            {
                return new Point3(WorldSizeInChunks.X * VoxelConstants.ChunkSizeX, WorldSizeInChunks.Y * VoxelConstants.ChunkSizeY, WorldSizeInChunks.Z * VoxelConstants.ChunkSizeZ);
            }
        }


        public EventLog EventLog = new EventLog();

        public StatsTracker Stats = new StatsTracker();

        public void LogEvent(EventLog.LogEntry entry)
        {
            EventLog.AddEntry(entry);
        }

        public void LogEvent(String Message, String Details = "")
        {
            LogEvent(Message, Color.Black, Details);
        }


        public void LogEvent(String Message, Color textColor, String Details = "")
        {
            LogEvent(new EventLog.LogEntry()
            {
                TextColor = textColor,
                Text = Message,
                Details = Details,
                Date = Time.CurrentDate
            });
        }

        public void LogStat(String stat, float value)
        {
            Stats.AddStat(stat, Time.CurrentDate, value);
        }

        

        public MonsterSpawner MonsterSpawner { get; set; }

        public Faction PlayerFaction;

        public List<Faction> Natives { get; set; } // Todo: To be rid of this; two concepts of faction - The owner faction in the overworld, and the instance in this game.

        public struct Screenshot
        {
            public string FileName { get; set; }
            public Point Resolution { get; set; }
        }

        public bool ShowingWorld { get; set; }

        public GameState gameState;

        public Gui.Root Gui;
        private QueuedAnnouncement SleepPrompt = null;

        public Action<String> ShowTooltip = null;
        public Action<String> ShowInfo = null;
        public Action<String> ShowToolPopup = null;
        
        public Action<Gui.MousePointer> SetMouse = null;
        public Action<String, int> SetMouseOverlay = null;
        public Gui.MousePointer MousePointer = new Gui.MousePointer("mouse", 1, 0);
        public bool IsMouseOverGui
        {
            get
            {
                return Gui.HoverItem != null;
                // Don't detect tooltips and tool popups.
            }
        }

        // event that is called when the world is done loading
        public delegate void OnLoaded();
        public event OnLoaded OnLoadedEvent;

        // event that is called when the player loses in the world
        public delegate void OnLose();
        public event OnLose OnLoseEvent;

        // Lazy actions - needed occasionally to spawn entities from threads among other things.
        private static List<Action> LazyActions = new List<Action>();

        public static void DoLazy(Action action)
        {
            LazyActions.Add(action);
        }
        
        public class WorldPopup
        {
            public Widget Widget;
            public GameComponent BodyToTrack;
            public Vector2 ScreenOffset;

            public void Update(DwarfTime time, Camera camera, Viewport viewport)
            {
                if (Widget == null || BodyToTrack == null || BodyToTrack.IsDead)
                {
                    return;
                }

                var projectedPosition = viewport.Project(BodyToTrack.Position, camera.ProjectionMatrix, camera.ViewMatrix, Matrix.Identity);
                if (projectedPosition.Z > 0.999f)
                {
                    Widget.Hidden = true;
                    return;
                }

                Vector2 projectedCenter = new Vector2(projectedPosition.X / DwarfGame.GuiSkin.CalculateScale(), projectedPosition.Y / DwarfGame.GuiSkin.CalculateScale()) + ScreenOffset - new Vector2(0, Widget.Rect.Height);
                if ((new Vector2(Widget.Rect.Center.X, Widget.Rect.Center.Y) - projectedCenter).Length() < 0.1f)
                {
                    return;
                }

                Widget.Rect = new Rectangle((int)projectedCenter.X - Widget.Rect.Width / 2, 
                    (int)projectedCenter.Y - Widget.Rect.Height / 2, Widget.Rect.Width, Widget.Rect.Height);

                if (!viewport.Bounds.Intersects(Widget.Rect))
                {
                    Widget.Hidden = true;
                }
                else
                {
                    Widget.Hidden = false;
                }
                Widget.Layout();
                Widget.Invalidate();
            }
        }

        private Dictionary<uint, WorldPopup> LastWorldPopup = new Dictionary<uint, WorldPopup>();
        private Splasher Splasher;
        #endregion

        [JsonIgnore]
        public List<EngineModule> UpdateSystems = new List<EngineModule>();

        public T FindSystem<T>() where T: EngineModule
        {
            return UpdateSystems.FirstOrDefault(s => s is T) as T;
        }

        /// <summary>
        /// Creates a new play state
        /// </summary>
        /// <param name="Game">The program currently running</param>
        public WorldManager(DwarfGame Game)
        {
            this.Game = Game;
            Content = Game.Content;
            Time = new WorldTime();
            Renderer = new WorldRenderer(Game, this);
        }

        public void PauseThreads()
        {
            ChunkManager.PauseThreads = true;
        }

        public void UnpauseThreads()
        {
            if (ChunkManager != null)
            {
                ChunkManager.PauseThreads = false;
            }

            if (Renderer.Camera != null)
                Renderer.Camera.LastWheel = Mouse.GetState().ScrollWheelValue;
        }

        private void TrackStats()
        {
            LogStat("Money", (float)(decimal)PlayerFaction.Economy.Funds);

            var resources = PlayerFaction.ListResourcesInStockpilesPlusMinions();
            LogStat("Resources", resources.Values.Select(r => r.First.Count + r.Second.Count).Sum());
            LogStat("Resource Value", (float)resources.Values.Select(r =>
            {
                var value = ResourceLibrary.GetResourceByName(r.First.Type).MoneyValue.Value;
                return (r.First.Count * value) + (r.Second.Count * value);
            }).Sum());
            LogStat("Employees", PlayerFaction.Minions.Count);
            LogStat("Employee Pay", (float)PlayerFaction.Minions.Select(m => m.Stats.CurrentLevel.Pay.Value).Sum());
            LogStat("Furniture",  PlayerFaction.OwnedObjects.Count);
            LogStat("Zones", PlayerFaction.GetRooms().Count);
            LogStat("Employee Level", PlayerFaction.Minions.Sum(r => r.Stats.LevelIndex));
            LogStat("Employee Happiness", (float)PlayerFaction.Minions.Sum(m => m.Stats.Happiness.Percentage) / Math.Max(PlayerFaction.Minions.Count, 1));
        }

        private int _prevHour = 0;
        /// <summary>
        /// Called every frame
        /// </summary>
        /// <param name="gameTime">The current time</param>
        public void Update(DwarfTime gameTime)
        {
            int MAX_LAZY_ACTIONS = 32;
            int action = 0;
            foreach (var func in LazyActions)
            {
                if (func != null)
                    func.Invoke();
                action++;
                if (action > MAX_LAZY_ACTIONS)
                {
                    break;
                }
            }
            if (action > 0)
            {
                LazyActions.RemoveRange(0, action);
            }
            if (FastForwardToDay)
            {
                if (Time.IsDay())
                {
                    FastForwardToDay = false;
                    foreach (CreatureAI minion in PlayerFaction.Minions)
                        minion.Stats.Energy.CurrentValue = minion.Stats.Energy.MaxValue;
                    Time.Speed = 100;
                }
                else
                {
                    Time.Speed = 1000;
                }
            }

            IndicatorManager.Update(gameTime);
            HandleAmbientSound();

            Master.Update(Game, gameTime);
            GamblingState.Update(gameTime);
            EventScheduler.Update(this, Time.CurrentDate);

            Time.Update(gameTime);

            if (LastWorldPopup != null)
            {
                List<uint> removals = new List<uint>();
                foreach (var popup in LastWorldPopup)
                {
                    popup.Value.Update(gameTime, Renderer.Camera, GraphicsDevice.Viewport);
                    if (popup.Value.Widget == null || !Gui.RootItem.Children.Contains(popup.Value.Widget) 
                        || popup.Value.BodyToTrack == null || popup.Value.BodyToTrack.IsDead)
                    {
                        removals.Add(popup.Key);
                    }
                }

                foreach (var removal in removals)
                {
                    if (LastWorldPopup[removal].Widget != null && Gui.RootItem.Children.Contains(LastWorldPopup[removal].Widget))
                    {
                        Gui.DestroyWidget(LastWorldPopup[removal].Widget);
                    }
                    LastWorldPopup.Remove(removal);
                }
            }

            if (Paused)
            {
                ComponentManager.UpdatePaused(gameTime, ChunkManager, Renderer.Camera);
                TutorialManager.Update(Gui);
            }
            // If not paused, we want to just update the rest of the game.
            else
            {
                ParticleManager.Update(gameTime, this);
                TutorialManager.Update(Gui);

                foreach (var updateSystem in UpdateSystems)
                {
                    try
                    {
                        updateSystem.Update(gameTime);
                    }
                    catch (Exception) { }
                }

                Diplomacy.Update(gameTime, Time.CurrentDate, this);
                Factions.Update(gameTime);
                ComponentManager.Update(gameTime, ChunkManager, Renderer.Camera);
                MonsterSpawner.Update(gameTime);
                bool allAsleep = PlayerFaction.AreAllEmployeesAsleep();

#if !UPTIME_TEST
                if (SleepPrompt == null && allAsleep && !FastForwardToDay && Time.IsNight())
                {
                    SleepPrompt = new QueuedAnnouncement()
                    {
                        Text = "All your employees are asleep. Click here to skip to day.",
                        ClickAction = (sender, args) =>
                        {
                            FastForwardToDay = true;
                            SleepPrompt = null;
                        },
                        ShouldKeep = () =>
                        {
                            return FastForwardToDay == false && Time.IsNight() && PlayerFaction.AreAllEmployeesAsleep();
                        }
                    };
                    MakeAnnouncement(SleepPrompt);
                }
                else if (!allAsleep)
                {
                    Time.Speed = 100;
                    FastForwardToDay = false;
                    SleepPrompt = null;
                }
#endif
            }

            // These things are updated even when the game is paused

            Splasher.Splash(gameTime, ChunkManager.Water.GetSplashQueue());

            ChunkManager.Update(gameTime, Renderer.Camera, GraphicsDevice);
            SoundManager.Update(gameTime, Renderer.Camera, Time);
            Weather.Update(this.Time.CurrentDate, this);

            if (gameFile != null)
            {
                // Cleanup game file.
                gameFile = null;
            }

#if DEBUG
            KeyboardState k = Keyboard.GetState();
            if (k.IsKeyDown(Keys.Home))
            {
                try
                {
                    GameState.Game.GraphicsDevice.Reset();
                }
                catch (Exception exception)
                {

                }
            }
#endif

            if (Time.CurrentDate.Hour != _prevHour)
            {
                TrackStats();
            }
            _prevHour = Time.CurrentDate.Hour;
        }

        public bool FastForwardToDay { get; set; }

        public void Quit()
        {
            ChunkManager.Destroy();
            ComponentManager.Destroy();
            ComponentManager = null;

            Master.Destroy();
            Master = null;

            ChunkManager = null;
            GC.Collect();
            PlanService.Die();
        }

        public void Dispose()
        {
            foreach(var composite in CompositeLibrary.Composites)
                composite.Value.Dispose();
            CompositeLibrary.Composites.Clear();

            if (LoadingThread != null && LoadingThread.IsAlive)
                LoadingThread.Abort();
        }

        public void InvokeLoss()
        {
            OnLoseEvent();
        }

        public WorldPopup MakeWorldPopup(string text, GameComponent body, float screenOffset = -10, float time = 30.0f)
        {
            return MakeWorldPopup(new TimedIndicatorWidget() { Text = text, DeathTimer = new Timer(time, true, Timer.TimerMode.Real) }, body, new Vector2(0, screenOffset));
        }

        public WorldPopup MakeWorldPopup(Widget widget, GameComponent body, Vector2 ScreenOffset)
        {
            if (LastWorldPopup.ContainsKey(body.GlobalID))
                Gui.DestroyWidget(LastWorldPopup[body.GlobalID].Widget);

            Gui.RootItem.AddChild(widget);

            // Todo: Uh - what cleans these up if the body is destroyed?
            LastWorldPopup[body.GlobalID] = new WorldPopup()
            {
                Widget = widget,
                BodyToTrack = body,
                ScreenOffset = ScreenOffset 
            };

            Gui.RootItem.SendToBack(widget);

            return LastWorldPopup[body.GlobalID];
        }
    }
}
