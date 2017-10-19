﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace DwarfCorp
{
    public partial class VoxelHelpers
    {
        public static void Reveal(
            ChunkData Data,
            VoxelHandle Start)
        {
            Reveal(Data, new VoxelHandle[] { Start });
        }

        public static void Reveal(
            ChunkData Data,
            IEnumerable<VoxelHandle> voxels)
        {
            //if (!GameSettings.Default.FogofWar) return;

            var queue = new Queue<VoxelHandle>(128);

            foreach (var voxel in voxels)
                if (voxel.IsValid)
                    queue.Enqueue(voxel);

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (!v.IsValid) continue;

                foreach (var neighborCoordinate in VoxelHelpers.EnumerateManhattanNeighbors(v.Coordinate))
                {
                    var neighbor = new VoxelHandle(Data, neighborCoordinate);
                    if (!neighbor.IsValid) continue;
                    if (neighbor.IsExplored) continue;

                    neighbor.Chunk.NotifyExplored(neighbor.Coordinate.GetLocalVoxelCoordinate());
                    neighbor.IsExplored = true;

                    if (neighbor.IsEmpty)
                        queue.Enqueue(neighbor);
                }

                v.IsExplored = true;
            }
        }

        /// <summary>
        /// Run the reveal algorithm without invoking the invalidation mechanism.
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="voxel"></param>
        public static void InitialReveal(
            ChunkData Data,
            VoxelHandle voxel)
        {
            // Fog of war must be on for the initial reveal to avoid artifacts.
            bool fogOfWar = GameSettings.Default.FogofWar;
            GameSettings.Default.FogofWar = true;

            var queue = new Queue<VoxelHandle>(128);
            queue.Enqueue(voxel);

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (!v.IsValid) continue;

                foreach (var neighborCoordinate in VoxelHelpers.EnumerateManhattanNeighbors(v.Coordinate))
                {
                    var neighbor = new VoxelHandle(Data, neighborCoordinate);
                    if (!neighbor.IsValid) continue;
                    if (neighbor.IsExplored) continue;

                    neighbor.Chunk.NotifyExplored(neighbor.Coordinate.GetLocalVoxelCoordinate());
                    neighbor.RawSetIsExplored(true);

                    if (neighbor.IsEmpty)
                        queue.Enqueue(neighbor);
                }

                v.RawSetIsExplored(true);
            }

            GameSettings.Default.FogofWar = fogOfWar;
        }

        private struct VisitedVoxel
        {
            public int Depth;
            public VoxelHandle Voxel;
        }

        public static void RadiusReveal(
            ChunkData Data,
            VoxelHandle Voxel,
            int Radius)
        {
            var queue = new Queue<VisitedVoxel>(128);

            if (Voxel.IsValid)
                queue.Enqueue(new VisitedVoxel { Depth = 1, Voxel = Voxel });

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (!v.Voxel.IsValid) continue;
                if (v.Depth >= Radius) continue;

                foreach (var neighborCoordinate in VoxelHelpers.EnumerateManhattanNeighbors(v.Voxel.Coordinate))
                {
                    var neighbor = new VoxelHandle(Data, neighborCoordinate);
                    if (!neighbor.IsValid) continue;
                    
                    neighbor.Chunk.NotifyExplored(neighbor.Coordinate.GetLocalVoxelCoordinate());
                    neighbor.IsExplored = true;

                    if (neighbor.IsEmpty)
                        queue.Enqueue(new VisitedVoxel
                        {
                            Depth = v.Depth + 1,
                            Voxel = neighbor
                        });
                }

                v.Voxel.IsExplored = true;
            }
        }

    }
}
