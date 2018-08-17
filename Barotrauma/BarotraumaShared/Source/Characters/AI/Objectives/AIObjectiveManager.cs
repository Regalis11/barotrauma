﻿using Barotrauma.Items.Components;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class AIObjectiveManager
    {
        public const float OrderPriority = 50.0f;

        private List<AIObjective> objectives;

        private Character character;

        private AIObjective currentOrder;
        
        public AIObjective CurrentOrder
        {
            get { return currentOrder; }
        }

        public AIObjective CurrentObjective
        {
            get;
            private set;
        }

        public AIObjectiveManager(Character character)
        {
            this.character = character;

            objectives = new List<AIObjective>();
        }

        public void AddObjective(AIObjective objective)
        {
            if (objectives.Find(o => o.IsDuplicate(objective)) != null) return;

            objectives.Add(objective);
        }

        public T GetObjective<T>() where T : AIObjective
        {
            foreach (AIObjective objective in objectives)
            {
                if (objective is T) return (T)objective;
            }
            return null;
        }

        public float GetCurrentPriority(Character character)
        {
            if (CurrentOrder != null &&
                (objectives.Count == 0 || currentOrder.GetPriority(this) > objectives[0].GetPriority(this)))
            {
                return CurrentOrder.GetPriority(this);
            }

            return objectives.Count == 0 ? 0.0f : objectives[0].GetPriority(this);
        }

        public void UpdateObjectives()
        {
            if (!objectives.Any()) return;

            //remove completed objectives and ones that can't be completed
            objectives = objectives.FindAll(o => !o.IsCompleted() && o.CanBeCompleted);

            //sort objectives according to priority
            objectives.Sort((x, y) => y.GetPriority(this).CompareTo(x.GetPriority(this)));
        }

        public void DoCurrentObjective(float deltaTime)
        {
            if (currentOrder != null && (!objectives.Any() || objectives[0].GetPriority(this) < currentOrder.GetPriority(this)))
            {
                CurrentObjective = currentOrder;
                currentOrder.TryComplete(deltaTime);
                return;
            }

            if (!objectives.Any()) return;
            objectives[0].TryComplete(deltaTime);

            CurrentObjective = objectives[0];
        }

        public void SetOrder(Order order, string option, Character orderGiver)
        {
            currentOrder = null;
            if (order == null) return;

            switch (order.AITag.ToLowerInvariant())
            {
                case "follow":
                    currentOrder = new AIObjectiveGoTo(orderGiver, character, true);
                    break;
                case "wait":
                    currentOrder = new AIObjectiveGoTo(character, character, true);
                    break;
                case "fixleaks":
                    currentOrder = new AIObjectiveFixLeaks(character);
                    break;
                case "chargebatteries":
                    currentOrder = new AIObjectiveChargeBatteries(character, option);
                    break;
                case "rescue":
                    currentOrder = new AIObjectiveRescueAll(character);
                    break;
                case "repairsystems":
                    currentOrder = new AIObjectiveRepairItems(character);
                    break;
                case "pumpwater":
                    currentOrder = new AIObjectivePumpWater(character, option);
                    break;
                case "extinguishfires":
                    currentOrder = new AIObjectiveExtinguishFires(character);
                    break;
                case "steer":
                    var steering = (order?.TargetEntity as Item)?.GetComponent<Steering>();
                    if (steering != null) steering.PosToMaintain = steering.Item.Submarine?.WorldPosition;
                    if (order.TargetItemComponent == null) return;
                    currentOrder = new AIObjectiveOperateItem(order.TargetItemComponent, character, option, false, null, order.UseController);
                    break;
                default:
                    if (order.TargetItemComponent == null) return;
                    currentOrder = new AIObjectiveOperateItem(order.TargetItemComponent, character, option, false, null, order.UseController);
                    break;
            }
        }
    }
}
