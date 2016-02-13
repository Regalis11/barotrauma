﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Barotrauma.Networking;
using Lidgren.Network;
using System.Collections.Generic;

namespace Barotrauma
{
    [Flags]
    public enum LimbSlot
    {
        None = 0, Any = 1, RightHand = 2, LeftHand = 4, Head = 8, Torso = 16, Legs = 32
    };

    class CharacterInventory : Inventory
    {
        private static Texture2D icons;

        private Character character;

        public static LimbSlot[] limbSlots = new LimbSlot[] { 
            LimbSlot.Head, LimbSlot.Torso, LimbSlot.Legs, LimbSlot.LeftHand, LimbSlot.RightHand,
            LimbSlot.Any, LimbSlot.Any, LimbSlot.Any, LimbSlot.Any, LimbSlot.Any,
            LimbSlot.Any, LimbSlot.Any, LimbSlot.Any, LimbSlot.Any, LimbSlot.Any};

        public Vector2[] SlotPositions;

        public CharacterInventory(int capacity, Character character)
            : base(character, capacity)
        {
            this.character = character;

            if (icons == null) icons = TextureLoader.FromFile("Content/UI/inventoryIcons.png");

            SlotPositions = new Vector2[limbSlots.Length];
            
            int rectWidth = 40, rectHeight = 40;
            int spacing = 10;
            for (int i = 0; i < SlotPositions.Length; i++)
            {
                switch (i)
                {
                    //head, torso, legs
                    case 0:
                    case 1:
                    case 2:
                        SlotPositions[i] = new Vector2(
                            spacing, 
                            GameMain.GraphicsHeight - (spacing + rectHeight) * (3 - i));
                        break;
                    //lefthand, righthand
                    case 3:
                    case 4:
                        SlotPositions[i] = new Vector2(
                            spacing * 2 + rectWidth + (spacing + rectWidth) * (i - 3),
                            GameMain.GraphicsHeight - (spacing + rectHeight)*3);
                        break;
                    default:
                        SlotPositions[i] = new Vector2(
                            spacing * 2 + rectWidth + (spacing + rectWidth) * ((i - 3)%5),
                            GameMain.GraphicsHeight - (spacing + rectHeight) * ((i>9) ? 2 : 1));
                        break;
                }
            }
        }

        protected override void DropItem(Item item)
        {
            bool enabled = item.body!=null && item.body.Enabled;
            item.Drop(character);

            if (!enabled)
            {
                item.SetTransform(character.SimPosition, 0.0f);
            }
        }

        public int FindLimbSlot(LimbSlot limbSlot)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (limbSlots[i] == limbSlot) return i;
            }
            return -1;
        }

        public bool IsInLimbSlot(Item item, LimbSlot limbSlot)
        {
            for (int i = 0; i<Items.Length; i++)
            {
                if (Items[i] == item && limbSlots[i] == limbSlot) return true;
            }
            return false;
        }

        /// <summary>
        /// If there is room, puts the item in the inventory and returns true, otherwise returns false
        /// </summary>
        public override bool TryPutItem(Item item, List<LimbSlot> allowedSlots, bool createNetworkEvent = true)
        {
            //try to place the item in LimBlot.Any slot if that's allowed
            if (allowedSlots.Contains(LimbSlot.Any))
            {
                for (int i = 0; i < capacity; i++)
                {
                    if (Items[i] != null || limbSlots[i] != LimbSlot.Any) continue;
                    PutItem(item, i, createNetworkEvent);
                    item.Unequip(character);
                    return true;                   
                }
            }

            bool placed = false;
            foreach (LimbSlot allowedSlot in allowedSlots)
            {
                //check if all the required slots are free
                bool free = true;
                for (int i = 0; i < capacity; i++)
                {
                    if (allowedSlot.HasFlag(limbSlots[i]) && Items[i]!=null && Items[i]!=item)
                    {
                        free = false;
                        break;
                    }
                }

                if (!free) continue;

                for (int i = 0; i < capacity; i++)
                {
                    if (allowedSlot.HasFlag(limbSlots[i]) && Items[i] == null)
                    {
                        PutItem(item, i, createNetworkEvent, !placed);
                        item.Equip(character);
                        placed = true;
                    }
                }

                if (placed) return true;
            }


            return placed;
        }

        public override bool TryPutItem(Item item, int index, bool createNetworkEvent)
        {
            //there's already an item in the slot
            if (Items[index] != null)
            {
                if (Items[index] == item) return false;

                bool combined = false;
                if (Items[index].Combine(item))
                {
                    if (Items[index]==null)
                    {
                        System.Diagnostics.Debug.Assert(false);
                        return false;
                    }
                    Inventory otherInventory = Items[index].Inventory;
                    if (otherInventory != null && createNetworkEvent)
                    {
                        new Networking.NetworkEvent(Networking.NetworkEventType.InventoryUpdate, otherInventory.Owner.ID, true, true);
                    }

                    combined = true;
                }

                return combined;
            }

            if (limbSlots[index] == LimbSlot.Any)
            {
                if (!item.AllowedSlots.Contains(LimbSlot.Any)) return false;
                if (Items[index] != null) return Items[index] == item;

                PutItem(item, index, createNetworkEvent, true);
                return true;
            }

            LimbSlot placeToSlots = LimbSlot.None;

            bool slotsFree = true;
            List<LimbSlot> allowedSlots = item.AllowedSlots;
            foreach (LimbSlot allowedSlot in allowedSlots)
            {
                if (!allowedSlot.HasFlag(limbSlots[index])) continue;

                for (int i = 0; i < capacity; i++)
                {
                    if (allowedSlot.HasFlag(limbSlots[i]) && Items[i] != null && Items[i] != item)
                    {
                        slotsFree = false;
                        break;
                    }

                    placeToSlots = allowedSlot;
                }
            }

            if (!slotsFree) return false;
            
            return TryPutItem(item, new List<LimbSlot>() {placeToSlots}, createNetworkEvent);
        }
         
        public void DrawOwn(SpriteBatch spriteBatch)
        {
            string toolTip = "";
            Rectangle highlightedSlot = Rectangle.Empty;

            if (doubleClickedItem!=null &&  doubleClickedItem.Inventory!=this)
            {
                TryPutItem(doubleClickedItem, doubleClickedItem.AllowedSlots, true);
            }
            doubleClickedItem = null;

            int rectWidth = 40, rectHeight = 40;
            Rectangle slotRect = new Rectangle(0, 0, rectWidth, rectHeight);
            Rectangle draggingItemSlot = slotRect;

            for (int i = 0; i < capacity; i++)
            {
                slotRect.X = (int)SlotPositions[i].X;
                slotRect.Y = (int)SlotPositions[i].Y;

                if (i==1) //head
                {
                    spriteBatch.Draw(icons, new Vector2(slotRect.Center.X, slotRect.Center.Y), 
                        new Rectangle(0,0,56,128), Color.White*0.7f, 0.0f, 
                        new Vector2(28.0f, 64.0f), Vector2.One, 
                        SpriteEffects.None, 0.1f);
                } 
                else if (i==3 || i==4)
                {
                    spriteBatch.Draw(icons, new Vector2(slotRect.Center.X, slotRect.Center.Y),
                        new Rectangle(92, 41*(4-i), 36, 40), Color.White * 0.7f, 0.0f,
                        new Vector2(18.0f, 20.0f), Vector2.One,
                        SpriteEffects.None, 0.1f);
                }
            }
            
            for (int i = 0; i < capacity; i++)
            {
                slotRect.X = (int)SlotPositions[i].X;
                slotRect.Y = (int)SlotPositions[i].Y;

                bool multiSlot = false;
                //skip if the item is in multiple slots
                if (Items[i]!=null)
                {                    
                    for (int n = 0; n < capacity; n++ )
                    {
                        if (i==n || Items[n] != Items[i]) continue;
                        multiSlot = true;
                        break;
                    }
                }


                if (multiSlot) continue;

                if (Items[i] != null && slotRect.Contains(PlayerInput.MousePosition))
                {
                    if (GameMain.DebugDraw)
                    {
                        toolTip = Items[i].ToString();
                    }
                    else
                    {
                        toolTip = string.IsNullOrEmpty(Items[i].Description) ? Items[i].Name : Items[i].Name + '\n' + Items[i].Description;                   
                    }

                    highlightedSlot = slotRect;
                }

                UpdateSlot(spriteBatch, slotRect, i, Items[i], false, i>4 ? 0.2f : 0.4f);
                
                if (draggingItem!=null && draggingItem == Items[i]) draggingItemSlot = slotRect;
            }


            for (int i = 0; i < capacity; i++)
            {

                //Rectangle multiSlotRect = Rectangle.Empty;
                bool multiSlot = false;

                //check if the item is in multiple slots
                if (Items[i] != null)
                {
                    slotRect.X = (int)SlotPositions[i].X;
                    slotRect.Y = (int)SlotPositions[i].Y;
                    slotRect.Width = 40;
                    slotRect.Height = 40;

                    for (int n = 0; n < capacity; n++)
                    {
                        if (Items[n] != Items[i]) continue;

                        if (!multiSlot && i > n) break;
                        
                        if (i!=n)
                        {
                            multiSlot = true;
                            slotRect = Rectangle.Union(
                                new Rectangle((int)SlotPositions[n].X, (int)SlotPositions[n].Y, rectWidth, rectHeight), slotRect);
                        }
                    }
                }

                if (!multiSlot) continue;

                if (Items[i] != null && slotRect.Contains(PlayerInput.MousePosition))
                {
                    toolTip = string.IsNullOrEmpty(Items[i].Description) ? Items[i].Name : Items[i].Name + '\n' + Items[i].Description;
                    highlightedSlot = slotRect;
                }

                UpdateSlot(spriteBatch, slotRect, i, Items[i], i > 4);
            }

            slotRect.Width = rectWidth;
            slotRect.Height = rectHeight;

            if (!string.IsNullOrWhiteSpace(toolTip))
            {
                DrawToolTip(spriteBatch, toolTip, highlightedSlot);
            }

            if (draggingItem != null && !draggingItemSlot.Contains(PlayerInput.MousePosition))
            {
                if (PlayerInput.LeftButtonHeld())
                {
                    slotRect.X = (int)PlayerInput.MousePosition.X - slotRect.Width / 2;
                    slotRect.Y = (int)PlayerInput.MousePosition.Y - slotRect.Height / 2;

                    DrawSlot(spriteBatch, slotRect, draggingItem, false, false);
                }
                else
                {                    
                    DropItem(draggingItem);

                    new Networking.NetworkEvent(Barotrauma.Networking.NetworkEventType.DropItem, draggingItem.ID, true);
                    //draggingItem = null;
                }
            }                       
        }

        public override bool FillNetworkData(NetworkEventType type, NetBuffer message, object data)
        {
            for (int i = 0; i < capacity; i++)
            {
                message.Write(Items[i]==null ? (ushort)0 : (ushort)Items[i].ID);
            }
            
            return true;
        }

        public override void ReadNetworkData(NetworkEventType type, NetBuffer message, float sendingTime)
        {
            if (sendingTime < lastUpdate) return;

            character.ClearInput(InputType.Use);

            for (int i = 0; i<capacity; i++)
            {
                ushort itemId = message.ReadUInt16();
                if (itemId==0)
                {
                    if (Items[i] != null) Items[i].Drop(character, false);
                }
                else
                {
                    Item item = Entity.FindEntityByID(itemId) as Item;
                    if (item == null) continue;

                    if (Items[i] != item && Items[i] != null) Items[i].Drop(character, false);
                    TryPutItem(item, i, false);
                }
            }

            lastUpdate = sendingTime;
        }

    }
}
