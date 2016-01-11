﻿using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma
{
    class SalvageMission : Mission
    {
        private ItemPrefab itemPrefab;

        private Item item;

        private int state;

        public override Vector2 RadarPosition
        {
            get
            {
                return ConvertUnits.ToDisplayUnits(item.SimPosition);
            }
        }

        public SalvageMission(XElement element)
            : base(element)
        {
            string itemName = ToolBox.GetAttributeString(element, "itemname", "");

            itemPrefab = ItemPrefab.list.Find(ip => ip.Name == itemName) as ItemPrefab;

            if (itemPrefab == null)
            {
                DebugConsole.ThrowError("Error in SalvageMission: couldn't find an item prefab with the name "+itemName);
            }
        }

        public override void Start(Level level)
        {
            Vector2 position = Vector2.Zero;

            int tries = 0;
            do
            {
                Vector2 tryPos = level.PositionsOfInterest[Rand.Int(level.PositionsOfInterest.Count, false)];
                
                if (Submarine.PickBody(
                    ConvertUnits.ToSimUnits(tryPos), 
                    ConvertUnits.ToSimUnits(tryPos - Vector2.UnitY*level.Size.Y), 
                    null, Physics.CollisionLevel) != null)
                {
                    position = ConvertUnits.ToDisplayUnits(Submarine.LastPickedPosition);
                    break;
                }

                tries++;

                if (tries==10)
                {
                    position = level.EndPosition - Vector2.UnitY*300.0f;
                }

            } while (tries < 10);


            item = new Item(itemPrefab, position, null);
            item.MoveWithLevel = true;
            item.body.FarseerBody.IsKinematic = true;
            //item.MoveWithLevel = true;
        }

        public override void Update(float deltaTime)
        {
            switch (state)
            {
                case 0:
                    //item.body.LinearVelocity = Vector2.Zero;
                    if (item.Inventory!=null) item.body.FarseerBody.IsKinematic = false;
                    if (item.CurrentHull == null) return;
                    
                    ShowMessage(state);
                    state = 1;
                    break;
                case 1:
                    if (!Submarine.Loaded.AtEndPosition && !Submarine.Loaded.AtStartPosition) return;
                    ShowMessage(state);
                    state = 2;
                    break;
            }    
        }

        public override void End()
        {
            item.Remove();
            if (item.CurrentHull == null) return;

            GiveReward();

            completed = true;
        }
    }
}
