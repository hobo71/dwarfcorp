// GrassLibrary.cs
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{
    public class GrassLibrary
    {
        public static GrassType emptyType = null;

        public static Dictionary<string, GrassType> Types = new Dictionary<string, GrassType>();
        public static List<GrassType> TypeList;

        public GrassLibrary()
        {
        }

        private static GrassType.FringeTileUV[] CreateFringeUVs(Point[] Tiles)
        {
            System.Diagnostics.Debug.Assert(Tiles.Length == 3);

            var r = new GrassType.FringeTileUV[8];

            // North
            r[0] = new GrassType.FringeTileUV(Tiles[0].X, (Tiles[0].Y * 2) + 1, 16, 32);
            // East
            r[1] = new GrassType.FringeTileUV((Tiles[1].X * 2) + 1, Tiles[1].Y, 32, 16);
            // South
            r[2] = new GrassType.FringeTileUV(Tiles[0].X, (Tiles[0].Y * 2), 16, 32);
            // West
            r[3] = new GrassType.FringeTileUV(Tiles[1].X * 2, Tiles[1].Y, 32, 16);

            // NW
            r[4] = new GrassType.FringeTileUV((Tiles[2].X * 2) + 1, (Tiles[2].Y * 2) + 1, 32, 32);
            // NE
            r[5] = new GrassType.FringeTileUV((Tiles[2].X * 2), (Tiles[2].Y * 2) + 1, 32, 32);
            // SE
            r[6] = new GrassType.FringeTileUV((Tiles[2].X * 2), (Tiles[2].Y * 2), 32, 32);
            // SW
            r[7] = new GrassType.FringeTileUV((Tiles[2].X * 2) + 1, (Tiles[2].Y * 2), 32, 32);

            return r;
        }


        public static void InitializeDefaultLibrary()
        {
            TypeList = FileUtils.LoadJsonListFromMultipleSources<GrassType>(ContentPaths.grass_types, null, g => g.Name);
            emptyType = TypeList[0];

            byte ID = 0;
            foreach (var type in TypeList)
            {
                type.ID = ID;
                ++ID;

                Types[type.Name] = type;

                if (type.FringeTiles != null)
                    type.FringeTransitionUVs = CreateFringeUVs(type.FringeTiles);

                if (type.InitialDecayValue > VoxelConstants.MaximumGrassDecay)
                {
                    type.InitialDecayValue = VoxelConstants.MaximumGrassDecay;
                    Console.WriteLine("Grass type " + type.Name + " with invalid InitialDecayValue");
                }
            }

            if (ID > 16)
            {
                Console.WriteLine("Allowed number of grass types exceeded. Limit is " + VoxelConstants.MaximumGrassTypes);
            }
        }

        public static GrassType GetGrassType(byte id)
        {
            return TypeList[id];
        }

        public static GrassType GetGrassType(string name)
        {
            if (name == null)
            {
                return null;
            }
            GrassType r = null;
            Types.TryGetValue(name, out r);
            return r;
        }
    }
}