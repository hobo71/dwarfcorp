// CreatureAI.cs
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
using System.Runtime.Serialization;
//using System.Windows.Forms;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class MateTask : Task
    {
        public CreatureAI Them;

        public MateTask()
        {
            ReassignOnDeath = false;
        }
        public MateTask(CreatureAI closestMate)
        {
            Them = closestMate;
            Name = "Mate with " + closestMate.GlobalID;
            ReassignOnDeath = false;
        }

        public override bool ShouldRetry(Creature agent)
        {
            return false;
        }

        public override bool ShouldDelete(Creature agent)
        {
            if (Them == null || Them.IsDead || agent == null || agent.AI == null || agent.IsDead)
            {
                return true;
            }

            return base.ShouldDelete(agent);
        }

        public override bool IsComplete(Faction faction)
        {
            if (Them == null || Them.IsDead)
            {
                return true;
            }
            return false;
        }

        public override float ComputeCost(Creature agent, bool alreadyCheckedFeasible = false)
        {
            if (Them == null || Them.IsDead ||  agent == null || agent.IsDead || agent.AI == null)
            {
                return float.MaxValue;
            }

            return (Them.Position - agent.AI.Position).LengthSquared();
        }

        public override Feasibility IsFeasible(Creature agent)
        {
            if (Them == null || Them.IsDead || agent == null || agent.IsDead || agent.AI == null)
            {
                return Feasibility.Infeasible;
            }
            return Mating.CanMate(agent, Them.Creature) ? Feasibility.Feasible : Feasibility.Infeasible ;
        }

        public IEnumerable<Act.Status> Mate(Creature me)
        {
            Timer mateTimer = new Timer(5.0f, true);
            while (!mateTimer.HasTriggered)
            {
                if (me.IsDead || Them == null || Them.IsDead)
                {
                    yield return Act.Status.Fail;
                    yield break;
                }

                me.Physics.Velocity = Vector3.Zero;
                Them.Physics.Velocity = Vector3.Zero;
                Them.Physics.LocalPosition = me.Physics.Position*0.1f + Them.Physics.Position*0.9f;
                if (MathFunctions.RandEvent(0.01f))
                {
                    me.NoiseMaker.MakeNoise("Hurt", me.AI.Position, true, 0.1f);
                    me.World.ParticleManager.Trigger("puff", me.AI.Position, Color.White, 1);
                }
                mateTimer.Update(DwarfTime.LastTime);
                yield return Act.Status.Running;
            }

            if (me.IsDead || Them == null || Them.IsDead)
            {
                yield return Act.Status.Fail;
                yield break;
            }


            if (Mating.CanMate(me, Them.Creature))
            {
                Mating.Mate(me, Them.Creature, me.World.Time);
            }
            else
            {
                AutoRetry = false;
                me.AI.SetMessage("Failed to mate.");
                yield return Act.Status.Fail;
            }

            yield return Act.Status.Success;
        }

        public override Act CreateScript(Creature agent)
        {
            return new Sequence(new GoToEntityAct(Them.Physics, agent.AI),
                                new Wrap(() => Mate(agent)));
        }
    }
}
