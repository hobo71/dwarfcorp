// Camera.cs
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{

    [JsonObject(IsReference = true)]
    public class Camera
    {
        [JsonIgnore]
        public WorldManager World { get; set; }
        public Vector3 Target { get; set; }
        private Vector3 p = Vector3.Zero;

        private void setP(Vector3 v)
        {
            p = v;
        }

        public Vector3 Position
        {
            get { return p; }
            set { setP(value); }
        }

        public float FOV { get; set; }
        public float AspectRatio { get; set; }
        public float NearPlane { get; set; }
        public float FarPlane { get; set; }
        public Vector3 UpVector { get; set; }
        public Matrix ViewMatrix { get; set; }
        public Matrix ProjectionMatrix { get; set; }
        public Vector3 Velocity { get; set; }
        private Vector3 lastPosition = Vector3.Zero;


        public enum ProjectionMode
        {
            Orthographic,
            Perspective
        }

        public ProjectionMode Projection { get; set; }
        public int LastWheel { get; set; }


        public Camera(WorldManager world, Vector3 target, Vector3 position, float fov, float aspectRatio, float nearPlane, float farPlane)
        {
            World = world;
            UpVector = Vector3.Up;
            Target = target;
            Position = position;
            NearPlane = nearPlane;
            FarPlane = farPlane;
            AspectRatio = aspectRatio;
            FOV = fov;
            Velocity = Vector3.Zero;
            Projection = ProjectionMode.Perspective;
            UpdateViewMatrix();
            UpdateProjectionMatrix();
        }

        public Camera()
        {
  
        }


        public Vector3 Project(Vector3 pos)
        {
            return GameState.Game.Graphics.GraphicsDevice.Viewport.Project(pos, ProjectionMatrix, ViewMatrix, Matrix.Identity);
        }

        public Vector3 UnProject(Vector3 pos)
        {
            return GameState.Game.Graphics.GraphicsDevice.Viewport.Unproject(pos, ProjectionMatrix, ViewMatrix, Matrix.Identity);
        }


        public virtual void Update(DwarfTime time, ChunkManager chunks)
        {
            lastPosition = Position;
            UpdateViewMatrix();
            UpdateProjectionMatrix();
        }

        public virtual void UpdateViewMatrix()
        {
            ViewMatrix = Matrix.CreateLookAt(Position, Target, UpVector);
        }

        public void UpdateProjectionMatrix()
        {
            if(Projection == ProjectionMode.Perspective)
            {
                ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(FOV, AspectRatio, NearPlane, FarPlane);
            }
            else
            {
                float height = (float) (30.0f * Math.Tan(FOV / 2));
                float width = (float) (AspectRatio * 30.0f * Math.Tan(FOV / 2));
                ProjectionMatrix = Matrix.CreateOrthographic(width, height, NearPlane, FarPlane);
            }
        }

        public BoundingFrustum GetFrustum()
        {
            return new BoundingFrustum(ViewMatrix * ProjectionMatrix);
        }

        public BoundingFrustum GetDrawFrustum()
        {
            var projection = Matrix.CreatePerspectiveFieldOfView(FOV, AspectRatio, NearPlane, GameSettings.Default.ChunkDrawDistance);
            return new BoundingFrustum(ViewMatrix * projection);
        }
    }

    public class FPSCamera : Camera
    {
        public override void Update(DwarfTime time, ChunkManager chunks)
        {
            base.Update(time, chunks);
        }

        public override void UpdateViewMatrix()
        {
            ViewMatrix = Matrix.CreateLookAt(Position, Target, Vector3.UnitY);
        }
    }
}