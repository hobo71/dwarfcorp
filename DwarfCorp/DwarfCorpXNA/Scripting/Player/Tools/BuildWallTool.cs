// BuildTool.cs
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using DwarfCorp.Gui.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Input;

namespace DwarfCorp
{
    public class BuildWallTool : PlayerTool
    {
        public Shader Effect;
        public byte CurrentVoxelType { get; set; }
        private List<VoxelHandle> Selected { get; set; }
        public bool BuildFloor = false;

        public override void OnVoxelsSelected(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
            var Faction = Player.Faction;

            if (CurrentVoxelType == 0)
            {
                return;
            }
            if (Selected == null)
            {
                Selected = new List<VoxelHandle>();
            }
            Selected.Clear();
            switch (button)
            {
                case (InputManager.MouseButton.Left):
                    {

                        List<Task> assignments = new List<Task>();
                        var validRefs = voxels.Where(r => !Faction.Designations.IsVoxelDesignation(r, DesignationType.Put)
                            && Player.World.Master.VoxSelector.SelectionType == VoxelSelectionType.SelectEmpty ? r.IsEmpty : !r.IsEmpty).ToList();

                        foreach (var r in voxels)
                        {
                            // Todo: Mode should be a property of the tool, not grabbed out of the vox selector.
                            if (Player.World.Master.VoxSelector.SelectionType == VoxelSelectionType.SelectEmpty && !r.IsEmpty) continue;
                            if (Player.World.Master.VoxSelector.SelectionType == VoxelSelectionType.SelectFilled && r.IsEmpty) continue;

                            var existingDesignation = Player.Faction.Designations.GetVoxelDesignation(r, DesignationType.Put);
                            if (existingDesignation != null)
                                Player.TaskManager.CancelTask(existingDesignation.Task);

                            var above = VoxelHelpers.GetVoxelAbove(r);

                            if (above.IsValid && above.LiquidType != LiquidType.None)
                            {
                                continue;
                            }

                            assignments.Add(new BuildVoxelTask(r, VoxelLibrary.GetVoxelType(CurrentVoxelType).Name));
                        }

                        //TaskManager.AssignTasks(assignments, Faction.FilterMinionsWithCapability(Player.World.Master.SelectedMinions, GameMaster.ToolMode.BuildZone));
                        Player.TaskManager.AddTasks(assignments);
                        break;
                    }
                case (InputManager.MouseButton.Right):
                    {
                        foreach (var r in voxels)
                        {
                            var designation = Faction.Designations.GetVoxelDesignation(r, DesignationType.Put);
                            if (designation != null)
                                Player.TaskManager.CancelTask(designation.Task);
                        }
                        break;
                    }
            }
        }

        public override void OnBegin()
        {

        }

        public override void OnEnd()
        {
            if (Selected != null)
                Selected.Clear();
            CurrentVoxelType = 0;
            Player.VoxSelector.Clear();
        }

        public override void OnMouseOver(IEnumerable<Body> bodies)
        {
            
        }

        public override void Update(DwarfGame game, DwarfTime time)
        {
            if (Player.IsCameraRotationModeActive())
            {
                Player.VoxSelector.Enabled = false;
                Player.World.SetMouse(null);
                return;
            }

            Player.VoxSelector.Enabled = true;
            Player.BodySelector.Enabled = false;

            if (Player.World.IsMouseOverGui)
                Player.World.SetMouse(Player.World.MousePointer);
            else
                Player.World.SetMouse(new Gui.MousePointer("mouse", 1, 4));

            MouseState mouse = Mouse.GetState();
            if (mouse.RightButton == ButtonState.Pressed)
                Player.VoxSelector.SelectionType = VoxelSelectionType.SelectFilled;
            else
                Player.VoxSelector.SelectionType = BuildFloor ? VoxelSelectionType.SelectFilled : VoxelSelectionType.SelectEmpty;

        }

        private void DrawVoxels(float alpha, IEnumerable<VoxelHandle> selected)
        {
            Effect.VertexColorTint = new Color(0.5f, 1.0f, 0.5f, alpha);
            Vector3 offset = Player.World.Master.VoxSelector.SelectionType == VoxelSelectionType.SelectEmpty ? Vector3.Zero : Vector3.Up * 0.15f;

            foreach (var voxel in selected)
            {
                Effect.World = Matrix.CreateTranslation(voxel.WorldPosition + offset);
                foreach (EffectPass pass in Effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    VoxelLibrary.GetPrimitive(CurrentVoxelType).Render(GameState.Game.GraphicsDevice);
                }
            }
        }

        private void DrawVoxels(DwarfTime time, IEnumerable<VoxelHandle> selected)
        {
            GameState.Game.GraphicsDevice.DepthStencilState = DepthStencilState.None;
            Effect = Player.World.DefaultShader;

            float t = (float)time.TotalGameTime.TotalSeconds;
            float st = (float)Math.Sin(t * 4) * 0.5f + 0.5f;
            float alpha = 0.25f * st + 0.6f;

            Effect.MainTexture = AssetManager.GetContentTexture(ContentPaths.Terrain.terrain_tiles);
            Effect.LightRamp = Color.White;
            Effect.SetTexturedTechnique();
            DrawVoxels(MathFunctions.Clamp(alpha * 0.5f, 0.25f, 1.0f), selected);
            GameState.Game.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            DrawVoxels(MathFunctions.Clamp(alpha, 0.25f, 1.0f), selected);
            Effect.LightRamp = Color.White;
            Effect.VertexColorTint = Color.White;
            Effect.World = Matrix.Identity;
        }

        public override void Render2D(DwarfGame game, DwarfTime time)
        {

        }


        public override void Render3D(DwarfGame game, DwarfTime time)
        {
            var mouse = Mouse.GetState();

            if (Selected == null)
            {
                Selected = new List<VoxelHandle>();
            }

            if (CurrentVoxelType == 0)
            {
                Selected.Clear();
            }

            if (mouse.LeftButton == ButtonState.Pressed)
            {
                DrawVoxels(time, Selected);
            }
            else if (mouse.RightButton != ButtonState.Pressed)
            {
                var underMouse = Player.VoxSelector.VoxelUnderMouse;
                if (underMouse.IsValid)
                {
                    DrawVoxels(time, new List<VoxelHandle>() { Player.VoxSelector.VoxelUnderMouse });
                }
            }
        }

        public override void OnBodiesSelected(List<Body> bodies, InputManager.MouseButton button)
        {
            
        }

        public override void OnVoxelsDragged(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
            if (CurrentVoxelType == 0)
                return;

            if (Mouse.GetState().LeftButton == ButtonState.Pressed)
                Player.World.ShowTooltip("Release to build.");
            else
                Player.World.ShowTooltip("Release to cancel.");

            if (Selected == null)
            {
                Selected = new List<VoxelHandle>();
            }
            Selected.Clear();

            foreach (var voxel in voxels)
            {
                Selected.Add(voxel);
            }
        }
    }
}
