// ChunkUpdate.cs
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
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Threading;

namespace DwarfCorp
{
    public class ChunkUpdate
    {
        private static int CurrentUpdateChunk = 0;

        public static void RunUpdate(ChunkManager Chunks)
        {
            if (CurrentUpdateChunk < 0 || CurrentUpdateChunk >= Chunks.ChunkData.ChunkMap.Length)
            {
                return;
            }

            var chunk = Chunks.ChunkData.ChunkMap[CurrentUpdateChunk];
            CurrentUpdateChunk += 1;
            if (CurrentUpdateChunk >= Chunks.ChunkData.ChunkMap.Length)
                CurrentUpdateChunk = 0;

            UpdateChunk(chunk);
        }

        private static void UpdateChunk(VoxelChunk chunk)
        {
            var addGrassToThese = new List<Tuple<VoxelHandle, byte>>();
            for (var y = 0; y < VoxelConstants.ChunkSizeY; ++y)
            {
                // Skip empty slices.
                if (chunk.Data.VoxelsPresentInSlice[y] == 0) continue;

                for (var x = 0; x < VoxelConstants.ChunkSizeX; ++x)
                    for (var z = 0; z < VoxelConstants.ChunkSizeZ; ++z)
                    {
                        var voxel = new VoxelHandle(chunk, new LocalVoxelCoordinate(x, y, z));

                        // Allow grass to decay
                        if (voxel.GrassType != 0)
                        {
                            var decal = GrassLibrary.GetGrassType(voxel.GrassType);

                            if (decal.NeedsSunlight && !voxel.Sunlight)
                                voxel.GrassType = 0;
                            else if (decal.Decay)
                            {                                
                                if (voxel.GrassDecay == 0)
                                {
                                    var newDecal = GrassLibrary.GetGrassType(decal.BecomeWhenDecays);
                                    if (newDecal != null)
                                        voxel.GrassType = newDecal.ID;
                                    else
                                        voxel.GrassType = 0;
                                }
                                else
                                    voxel.GrassDecay -= 1;
                            } 
                        }
//#if false
                        else if (voxel.Type.GrassSpreadsHere)
                        {
                            // Spread grass onto this tile - but only from the same biome.

                            // Don't spread if there's an entity here.
                             var entityPresent = chunk.Manager.World.EnumerateIntersectingObjects(
                                new BoundingBox(voxel.WorldPosition + new Vector3(0.1f, 1.1f, 0.1f), voxel.WorldPosition + new Vector3(0.9f, 1.9f, 0.9f)),
                                CollisionType.Static).Any();
                            if (entityPresent) continue;

                            var biome = Overworld.GetBiomeAt(voxel.Coordinate.ToVector3(), chunk.Manager.World.WorldScale, chunk.Manager.World.WorldOrigin);

                            var grassyNeighbors = VoxelHelpers.EnumerateManhattanNeighbors2D(voxel.Coordinate)
                                .Select(c => new VoxelHandle(voxel.Chunk.Manager.ChunkData, c))
                                .Where(v => v.IsValid && v.GrassType != 0)
                                .Where(v => GrassLibrary.GetGrassType(v.GrassType).Spreads)
                                .Where(v => biome == Overworld.GetBiomeAt(v.Coordinate.ToVector3(), chunk.Manager.World.WorldScale, chunk.Manager.World.WorldOrigin))
                                .ToList();

                            if (grassyNeighbors.Count > 0)
                                if (MathFunctions.RandEvent(0.1f))
                                    addGrassToThese.Add(Tuple.Create(voxel, grassyNeighbors[MathFunctions.RandInt(0, grassyNeighbors.Count)].GrassType));
                        }
//#endif
                    }
            }

            foreach (var v in addGrassToThese)
            {
                var l = v.Item1;
                var grassType = GrassLibrary.GetGrassType(v.Item2);
                if (grassType.NeedsSunlight && !l.Sunlight)
                    continue;
                l.GrassType = v.Item2;
            }
        }
    }
}
