// Snake.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp
{
    public class Snake : Creature
    {
        [EntityFactory("Snake")]
        private static GameComponent __factory0(ComponentManager Manager, Vector3 Position, Blackboard Data)
        {
            return new Snake(false, Position, Manager, "Snake").Physics;
        }

        [EntityFactory("Necrosnake")]
        private static GameComponent __factory1(ComponentManager Manager, Vector3 Position, Blackboard Data)
        {
            return new Snake(true, Position, Manager, "Snake");
        }
        
        public class TailSegment
        {
            public Body Sprite;
            public Vector3 Target;
        }

        [JsonIgnore]
        public List<TailSegment> Tail;

        public bool Bonesnake = false;
        
        public Snake()
        {
        }

        public Snake(bool Bone, Vector3 position, ComponentManager manager, string name):
            base
            (
                manager,
                new CreatureStats
                {
                    Dexterity = 4,
                    Constitution = 6,
                    Strength = 9,
                    Wisdom = 2,
                    Charisma = 1,
                    Intelligence = 3,
                    Size = 3
                },
                "Evil",
                manager.World.PlanService,
                manager.World.Factions.Factions["Evil"],
                name
            )
        {
            UpdateRate = 1;
            Bonesnake = Bone;
            HasMeat = !Bone;
            HasBones = true;
            _maxPerSpecies = 4;
            Physics = new Physics
                (
                    manager,
                    name,
                    Matrix.CreateTranslation(position),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    new Vector3(0, 0, 0),
                    1.0f, 1.0f, 0.999f, 0.999f,
                    new Vector3(0, -10, 0)
                );

            Physics.AddChild(this);

            Physics.Orientation = Physics.OrientMode.Fixed;
            Species = "Snake";

            Attacks = new List<Attack>()
            {
                new Attack("Bite", 50.0f, 1.0f, 3.0f, SoundSource.Create(ContentPaths.Audio.Oscar.sfx_oc_giant_snake_attack_1), ContentPaths.Effects.bite)
                {
                    TriggerMode = Attack.AttackTrigger.Animation,
                    TriggerFrame = 2,
                }
            };

            CreateCosmeticChildren(Manager);

            Physics.AddChild(new EnemySensor(Manager, "EnemySensor", Matrix.Identity, new Vector3(20, 5, 20), Vector3.Zero));

            Physics.AddChild(new PacingCreatureAI(Manager, "snake AI", Sensors));

            Physics.AddChild(new Inventory(Manager, "Inventory", Physics.BoundingBox.Extents(), Physics.LocalBoundingBoxOffset));

            Physics.Tags.Add("Snake");
            Physics.Tags.Add("Animal");
            AI.Movement.SetCan(MoveType.ClimbWalls, true);
            AI.Movement.SetCan(MoveType.Dig, true);
            AI.Stats.FullName = "Giant Snake";
            AI.Stats.CurrentClass = SharedClass;
            AI.Stats.LevelIndex = 0;

            Physics.AddChild(new Flammable(Manager, "Flames"));
        }

        public override void CreateCosmeticChildren(ComponentManager Manager)
        {
            Stats.CurrentClass = SharedClass;

            CreateSprite(Bonesnake ? ContentPaths.Entities.Animals.Snake.bonesnake_animation : ContentPaths.Entities.Animals.Snake.snake_animation, Manager, 0.35f);

            #region Create Tail Pieces

            Tail = new List<TailSegment>();

            for (int i = 0; i < 10; ++i)
            {
                var tailPiece = CreateSprite(Bonesnake ? ContentPaths.Entities.Animals.Snake.bonetail_animation : ContentPaths.Entities.Animals.Snake.tail_animation, Manager, 0.25f, false);
                tailPiece.Name = "Snake Tail";
                Tail.Add(
                    new TailSegment()
                    {
                        Sprite = Manager.RootComponent.AddChild(tailPiece) as Body,
                        Target = Physics.LocalTransform.Translation
                    });


                tailPiece.AddChild(new Shadow(Manager));

                var inventory = tailPiece.AddChild(new Inventory(Manager, "Inventory", Physics.BoundingBox.Extents(), Physics.LocalBoundingBoxOffset)) as Inventory;
                inventory.SetFlag(Flag.ShouldSerialize, false);

                if (HasMeat)
                {
                    ResourceType type = Species + " " + ResourceType.Meat;

                    if (!ResourceLibrary.Resources.ContainsKey(type))
                    {
                        ResourceLibrary.Add(new Resource(ResourceLibrary.Resources[ResourceType.Meat])
                        {
                            Name = type,
                            ShortName = type
                        });
                    }

                    inventory.AddResource(new ResourceAmount(type, 1));
                }

                if (HasBones)
                {
                    ResourceType type = Name + " " + ResourceType.Bones;

                    if (!ResourceLibrary.Resources.ContainsKey(type))
                    {
                        ResourceLibrary.Add(new Resource(ResourceLibrary.Resources[ResourceType.Bones])
                        {
                            Name = type,
                            ShortName = type
                        });
                    }

                    inventory.AddResource(new ResourceAmount(type, 1));
                }
            }

            #endregion

            Physics.AddChild(Shadow.Create(0.75f, Manager));

            if (Bonesnake)
            {
                this.Attacks[0].DiseaseToSpread = "Necrorot";
                Physics.AddChild(new MinimapIcon(Manager, new NamedImageFrame(ContentPaths.GUI.map_icons, 16, 1, 4))).SetFlag(Flag.ShouldSerialize, false);
            }
            else
                Physics.AddChild(new MinimapIcon(Manager, new NamedImageFrame(ContentPaths.GUI.map_icons, 16, 2, 4))).SetFlag(Flag.ShouldSerialize, false);

            NoiseMaker = new NoiseMaker();
            NoiseMaker.Noises["Hurt"] = new List<string>() { ContentPaths.Audio.Oscar.sfx_oc_giant_snake_hurt_1 };
            NoiseMaker.Noises["Chirp"] = new List<string>()
            {
                ContentPaths.Audio.Oscar.sfx_oc_giant_snake_neutral_1,
                ContentPaths.Audio.Oscar.sfx_oc_giant_snake_neutral_2
            };

            Physics.AddChild(new ParticleTrigger("blood_particle", Manager, "Death Gibs", Matrix.Identity, Vector3.One, Vector3.Zero)
            {
                TriggerOnDeath = true,
                TriggerAmount = 1,
                BoxTriggerTimes = 10,
                SoundToPlay = ContentPaths.Audio.Oscar.sfx_oc_giant_snake_hurt_1,
            }).SetFlag(Flag.ShouldSerialize, false);

            base.CreateCosmeticChildren(Manager);
        }

        public override void Die()
        {
            foreach (var tail in Tail)
            {
                tail.Sprite.Die();
            }
            base.Die();
        }

        public override void Delete()
        {
            foreach (var tail in Tail)
            {
                tail.Sprite.GetRoot().Delete();
            }
            base.Delete();
        
        }

        public override void Update(DwarfTime gameTime, ChunkManager chunks, Camera camera)
        {
            if ((Physics.Position - Tail.First().Target).LengthSquared() > 0.5f)
            {
                for (int i = Tail.Count  - 1; i > 0; i--)
                {
                    Tail[i].Target = Tail[i - 1].Target;
                }
                Tail[0].Target = Physics.Position;
            }

            int k = 0;
            foreach (var tail in Tail)
            {
                Vector3 diff = Vector3.UnitX;
                if (k == Tail.Count - 1)
                {
                    diff = AI.Position - tail.Sprite.LocalPosition;
                }
                else
                {
                    diff = Tail[k + 1].Sprite.LocalPosition - tail.Sprite.LocalPosition;
                }
                var mat = Matrix.CreateRotationY((float)Math.Atan2(diff.X, -diff.Z));
                mat.Translation = 0.9f*tail.Sprite.LocalPosition + 0.1f*tail.Target;
                tail.Sprite.LocalTransform = mat;
                tail.Sprite.UpdateTransform();
                tail.Sprite.PropogateTransforms();
                k++;
            }
            base.Update(gameTime, chunks, camera);
        }

        private static EmployeeClass SharedClass = new EmployeeClass()
        {
            Name = "Giant Snake",
                Levels = new List<EmployeeClass.Level>()
                {
                    new EmployeeClass.Level()
                    {
                        Name = "Giant Snake",
                        Index = 0
                    },
                }
            };
    }
}
