﻿using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Barotrauma
{
    class AIObjectiveFixLeak : AIObjective
    {
        private Gap leak;

        public Gap Leak
        {
            get { return leak; }
        }

        public AIObjectiveFixLeak(Gap leak, Character character)
            : base (character, "")
        {
            this.leak = leak;
        }

        public override float GetPriority(Character character)
        {
            float leakSize = (leak.isHorizontal ? leak.Rect.Height : leak.Rect.Width) * leak.Open;

            float dist = Vector2.DistanceSquared(character.SimPosition, leak.SimPosition);
            dist = Math.Max(dist/1000.0f, 1.0f);
            return Math.Min(leakSize/dist, 40.0f);
        }

        public override bool IsDuplicate(AIObjective otherObjective)
        {
            AIObjectiveFixLeak fixLeak = otherObjective as AIObjectiveFixLeak;
            if (fixLeak == null) return false;
            return fixLeak.leak == leak;
        }

        protected override void Act(float deltaTime)
        {
            var weldingTool = character.Inventory.FindItem("Welding Tool");

            if (weldingTool == null)
            {
                subObjectives.Add(new AIObjectiveGetItem(character, "Welding Tool", true));
                return;
            }
            else
            {
                var containedItems = weldingTool.ContainedItems;
                if (containedItems == null) return;
                
                var fuelTank = Array.Find(containedItems, i => i.Name == "Welding Fuel Tank" && i.Condition > 0.0f);

                if (fuelTank == null)
                {
                    AddSubObjective(new AIObjectiveContainItem(character, "Welding Fuel Tank", weldingTool.GetComponent<ItemContainer>()));
                }
            }

            if (Vector2.Distance(character.Position, leak.Position) > 300.0f)
            {
                AddSubObjective(new AIObjectiveGoTo(ConvertUnits.ToSimUnits(GetStandPosition()), character));
            }
            else
            {
                var repairTool = weldingTool.GetComponent<RepairTool>();
                if (repairTool == null) return;
                AddSubObjective(new AIObjectiveOperateItem(repairTool, character, ""));
            }            
        }

        private Vector2 GetStandPosition()
        {
            Vector2 standPos = leak.Position;
            var hull = leak.FlowTargetHull;

            if (hull == null) return standPos;
            
            if (leak.isHorizontal)
            {
                standPos += Vector2.UnitX * Math.Sign(hull.Position.X - leak.Position.X) * leak.Rect.Width;
            }
            else
            {
                standPos += Vector2.UnitY * Math.Sign(hull.Position.Y - leak.Position.Y) * leak.Rect.Height;
            }

            return standPos;            
        }
    }
}
