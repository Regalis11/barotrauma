﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class Item : MapEntity, IDamageable, ISerializableEntity, IServerSerializable, IClientSerializable
    {
        private List<ItemComponent> activeHUDs = new List<ItemComponent>();

        public override Sprite Sprite
        {
            get { return prefab.GetActiveSprite(condition); }
        }

        public Color GetSpriteColor()
        {
            Color color = spriteColor;
            if (prefab.UseContainedSpriteColor && ownInventory != null)
            {
                for (int i = 0; i < ownInventory.Items.Length; i++)
                {
                    if (ownInventory.Items[i] != null)
                    {
                        color = ownInventory.Items[i].spriteColor;
                        break;
                    }
                }
            }
            return color;
        }

        public override void Draw(SpriteBatch spriteBatch, bool editing, bool back = true)
        {
            if (!Visible) return;
            
            Color color = (IsSelected && editing) ? Color.Red : GetSpriteColor();
            if (isHighlighted) color = Color.Orange;

            Sprite activeSprite = prefab.sprite;
            BrokenItemSprite fadeInBrokenSprite = null;
            float fadeInBrokenSpriteAlpha = 0.0f;
            if (condition < 100.0f)
            {
                for (int i = 0; i < prefab.BrokenSprites.Count; i++)
                {
                    if (condition <= prefab.BrokenSprites[i].MaxCondition)
                    {
                        activeSprite = prefab.BrokenSprites[i].Sprite;
                        break;
                    }

                    if (prefab.BrokenSprites[i].FadeIn)
                    {
                        float min = i > 0 ? prefab.BrokenSprites[i].MaxCondition : 0.0f;
                        float max = i < prefab.BrokenSprites.Count - 1 ? prefab.BrokenSprites[i + 1].MaxCondition : 100.0f;
                        fadeInBrokenSpriteAlpha = 1.0f - ((condition - min) / (max - min));
                        if (fadeInBrokenSpriteAlpha > 0.0f && fadeInBrokenSpriteAlpha < 1.0f)
                        {
                            fadeInBrokenSprite = prefab.BrokenSprites[i];
                        }
                    }
                }
            }

            Sprite selectedSprite = prefab.GetActiveSprite(condition);

            if (selectedSprite != null)
            {
                SpriteEffects oldEffects = selectedSprite.effects;
                selectedSprite.effects ^= SpriteEffects;

                float depth = GetDrawDepth();

                if (body == null)
                {
                    if (prefab.ResizeHorizontal || prefab.ResizeVertical || SpriteEffects.HasFlag(SpriteEffects.FlipHorizontally) || SpriteEffects.HasFlag(SpriteEffects.FlipVertically))
                    {
                        selectedSprite.DrawTiled(spriteBatch, new Vector2(DrawPosition.X - rect.Width / 2, -(DrawPosition.Y + rect.Height / 2)), new Vector2(rect.Width, rect.Height), color: color);
                        fadeInBrokenSprite?.Sprite.DrawTiled(spriteBatch, new Vector2(DrawPosition.X - rect.Width / 2, -(DrawPosition.Y + rect.Height / 2)), new Vector2(rect.Width, rect.Height), color: color * fadeInBrokenSpriteAlpha,
                            depth: selectedSprite.Depth - 0.000001f);

                    }
                    else
                    {
                        selectedSprite.Draw(spriteBatch, new Vector2(DrawPosition.X, -DrawPosition.Y), color, 0.0f, 1.0f, SpriteEffects.None, depth);
                        fadeInBrokenSprite?.Sprite.Draw(spriteBatch, new Vector2(DrawPosition.X, -DrawPosition.Y), color * fadeInBrokenSpriteAlpha, 0.0f, 1.0f, SpriteEffects.None, depth - 0.000001f);
                    }

                }
                else if (body.Enabled)
                {
                    var holdable = GetComponent<Holdable>();
                    if (holdable != null && holdable.Picker?.AnimController != null)
                    {
                        if (holdable.Picker.SelectedItems[0] == this)
                        {
                            Limb holdLimb = holdable.Picker.AnimController.GetLimb(LimbType.RightHand);
                            depth = holdLimb.sprite.Depth + 0.000001f;
                            foreach (WearableSprite wearableSprite in holdLimb.WearingItems)
                            {
                                if (!wearableSprite.InheritLimbDepth && wearableSprite.Sprite != null) depth = Math.Min(wearableSprite.Sprite.Depth, depth);
                            }
                        }
                        else if (holdable.Picker.SelectedItems[1] == this)
                        {
                            Limb holdLimb = holdable.Picker.AnimController.GetLimb(LimbType.LeftHand);
                            depth = holdLimb.sprite.Depth - 0.000001f;
                            foreach (WearableSprite wearableSprite in holdLimb.WearingItems)
                            {
                                if (!wearableSprite.InheritLimbDepth && wearableSprite.Sprite != null) depth = Math.Max(wearableSprite.Sprite.Depth, depth);
                            }
                        }
                    }
                    body.Draw(spriteBatch, selectedSprite, color, depth);
                    if (fadeInBrokenSprite != null) body.Draw(spriteBatch, fadeInBrokenSprite.Sprite, color * fadeInBrokenSpriteAlpha, depth - 0.000001f);
                }

                selectedSprite.effects = oldEffects;
            }


            List<IDrawableComponent> staticDrawableComponents = new List<IDrawableComponent>(drawableComponents); //static list to compensate for drawable toggling
            for (int i = 0; i < staticDrawableComponents.Count; i++)
            {
                staticDrawableComponents[i].Draw(spriteBatch, editing);
            }

            if (GameMain.DebugDraw)
            {
                aiTarget?.Draw(spriteBatch);
                var containedItems = ContainedItems;
                if (containedItems != null)
                {
                    foreach (Item item in containedItems)
                    {
                        item.AiTarget?.Draw(spriteBatch);
                    }
                }
            }

            if (!editing || (body != null && !body.Enabled))
            {
                return;
            }

            if (IsSelected || isHighlighted)
            {
                GUI.DrawRectangle(spriteBatch, new Vector2(DrawPosition.X - rect.Width / 2, -(DrawPosition.Y + rect.Height / 2)), new Vector2(rect.Width, rect.Height), Color.Green, false, 0, (int)Math.Max((1.5f / GameScreen.Selected.Cam.Zoom), 1.0f));

                foreach (Rectangle t in prefab.Triggers)
                {
                    Rectangle transformedTrigger = TransformTrigger(t);

                    Vector2 rectWorldPos = new Vector2(transformedTrigger.X, transformedTrigger.Y);
                    if (Submarine != null) rectWorldPos += Submarine.Position;
                    rectWorldPos.Y = -rectWorldPos.Y;

                    GUI.DrawRectangle(spriteBatch,
                        rectWorldPos,
                        new Vector2(transformedTrigger.Width, transformedTrigger.Height),
                        Color.Green,
                        false,
                        0,
                        (int)Math.Max((1.5f / GameScreen.Selected.Cam.Zoom), 1.0f));
                }
            }

            if (!ShowLinks) return;

            foreach (MapEntity e in linkedTo)
            {
                GUI.DrawLine(spriteBatch,
                    new Vector2(WorldPosition.X, -WorldPosition.Y),
                     new Vector2(e.WorldPosition.X, -e.WorldPosition.Y),
                    Color.Red * 0.3f);
            }
        }

        public override void UpdateEditing(Camera cam)
        {
            if (editingHUD == null || editingHUD.UserData as Item != this)
            {
                editingHUD = CreateEditingHUD(Screen.Selected != GameMain.SubEditorScreen);
            }

            editingHUD.UpdateManually((float)Timing.Step);

            if (Screen.Selected != GameMain.SubEditorScreen) return;

            if (!Linkable) return;

            if (!PlayerInput.KeyDown(Keys.Space)) return;
            bool lClick = PlayerInput.LeftButtonClicked();
            bool rClick = PlayerInput.RightButtonClicked();
            if (!lClick && !rClick) return;

            Vector2 position = cam.ScreenToWorld(PlayerInput.MousePosition);

            if (lClick)
            {
                foreach (MapEntity entity in mapEntityList)
                {
                    if (entity == this || !entity.IsHighlighted) continue;
                    if (linkedTo.Contains(entity)) continue;
                    if (!entity.IsMouseOn(position)) continue;

                    linkedTo.Add(entity);
                    if (entity.Linkable && entity.linkedTo != null) entity.linkedTo.Add(this);
                }
            }
            else
            {
                foreach (MapEntity entity in mapEntityList)
                {
                    if (entity == this || !entity.IsHighlighted) continue;
                    if (!linkedTo.Contains(entity)) continue;
                    if (!entity.IsMouseOn(position)) continue;

                    linkedTo.Remove(entity);
                    if (entity.linkedTo != null && entity.linkedTo.Contains(this)) entity.linkedTo.Remove(this);
                }
            }
        }

        public override void DrawEditing(SpriteBatch spriteBatch, Camera cam)
        {
            if (editingHUD != null && editingHUD.UserData == this) editingHUD.DrawManually(spriteBatch);
        }

        private GUIComponent CreateEditingHUD(bool inGame = false)
        {
            int width = 450, height = 150;
            int x = GameMain.GraphicsWidth / 2 - width / 2, y = 30;
            
            editingHUD = new GUIListBox(new RectTransform(new Point(width, height), GUI.Canvas) { ScreenSpaceOffset = new Point(x, y) })
            {
                UserData = this
            };

            GUIListBox listBox = (GUIListBox)editingHUD;
            listBox.Spacing = 5;
            
            var itemEditor = new SerializableEntityEditor(listBox.Content.RectTransform, this, inGame, showName: true);
            
            if (!inGame && Linkable)
            {
                itemEditor.AddCustomContent(new GUITextBlock(new RectTransform(new Point(editingHUD.Rect.Width, 20)), 
                    TextManager.Get("HoldToLink"), font: GUI.SmallFont), 1);
            }            

            foreach (ItemComponent ic in components)
            {
                if (ic.requiredItems.Count == 0)
                {
                    if (inGame)
                    {
                        if (SerializableProperty.GetProperties<InGameEditable>(ic).Count == 0) continue;
                    }
                    else
                    {
                        if (SerializableProperty.GetProperties<Editable>(ic).Count == 0) continue;
                    }
                }

                var componentEditor = new SerializableEntityEditor(listBox.Content.RectTransform, ic, inGame, showName: !inGame);
                
                if (inGame) continue;

                foreach (RelatedItem relatedItem in ic.requiredItems)
                {
                    var textBlock = new GUITextBlock(new RectTransform(new Point(editingHUD.Rect.Width, 20)),
                        relatedItem.Type.ToString() + " required", font: GUI.SmallFont)
                    {
                        Padding = new Vector4(10.0f, 0.0f, 10.0f, 0.0f)
                    };
                    componentEditor.AddCustomContent(textBlock, 1);

                    GUITextBox namesBox = new GUITextBox(new RectTransform(new Vector2(0.5f, 1.0f), textBlock.RectTransform, Anchor.CenterRight))
                    {
                        Font = GUI.SmallFont,
                        Text = relatedItem.JoinedNames
                    };

                    namesBox.OnDeselected += (textBox, key) =>
                    {
                        relatedItem.JoinedNames = textBox.Text;
                        textBox.Text = relatedItem.JoinedNames;
                    };

                    namesBox.OnEnterPressed += (textBox, text) =>
                    {
                        relatedItem.JoinedNames = text;
                        textBox.Text = relatedItem.JoinedNames;
                        return true;
                    };
                }
            }

            int contentHeight = editingHUD.Children.Sum(c => c.Rect.Height) + (listBox.CountChildren - 1) * listBox.Spacing;
            editingHUD.RectTransform.NonScaledSize =
                new Point(editingHUD.RectTransform.NonScaledSize.X, MathHelper.Clamp(contentHeight, 50, editingHUD.RectTransform.NonScaledSize.Y));

            return editingHUD;
        }
        
        public virtual void UpdateHUD(Camera cam, Character character, float deltaTime)
        {
            if (condition <= 0.0f)
            {
                FixRequirement.UpdateHud(this, character);
                return;
            }

            if (HasInGameEditableProperties)
            {
                UpdateEditing(cam);
            }

            activeHUDs.Clear();
            //the HUD of the component with the highest priority will be drawn
            //if all components have a priority of 0, all of them are drawn
            ItemComponent maxPriorityHUD = null;            
            foreach (ItemComponent ic in components)
            {
                if (ic.CanBeSelected && ic.HudPriority > 0 && ic.ShouldDrawHUD(character) &&
                    (maxPriorityHUD == null || ic.HudPriority > maxPriorityHUD.HudPriority))
                {
                    maxPriorityHUD = ic;
                }
            }

            if (maxPriorityHUD != null)
            {
                activeHUDs.Add(maxPriorityHUD);
            }
            else
            {
                foreach (ItemComponent ic in components)
                {
                    if (ic.CanBeSelected && ic.ShouldDrawHUD(character)) activeHUDs.Add(ic);
                }
            }

            foreach (ItemComponent ic in activeHUDs)
            {
                if (ic.CanBeSelected) ic.UpdateHUD(character, deltaTime);
            }
        }

        public virtual void DrawHUD(SpriteBatch spriteBatch, Camera cam, Character character)
        {
            if (HasInGameEditableProperties)
            {
                DrawEditing(spriteBatch, cam);
            }
            
            foreach (ItemComponent ic in activeHUDs)
            {
                if (ic.CanBeSelected) ic.DrawHUD(spriteBatch, character);
            }            
        }

        public override void AddToGUIUpdateList()
        {
            if (Screen.Selected is SubEditorScreen)
            {
                if (editingHUD != null) editingHUD.AddToGUIUpdateList();
            }
            else
            {
                if (HasInGameEditableProperties)
                {
                    if (editingHUD != null) editingHUD.AddToGUIUpdateList();
                }
            }

            if (Character.Controlled != null && Character.Controlled.SelectedConstruction == this)
            {
                if (condition <= 0.0f)
                {
                    FixRequirement.AddToGUIUpdateList();
                    return;
                }

                foreach (ItemComponent ic in activeHUDs)
                {
                    if (ic.CanBeSelected) ic.AddToGUIUpdateList();
                }
            }
        }
        
        public void ClientRead(ServerNetObject type, NetBuffer msg, float sendingTime)
        {
            if (type == ServerNetObject.ENTITY_POSITION)
            {
                ClientReadPosition(type, msg, sendingTime);
                return;
            }

            NetEntityEvent.Type eventType =
                (NetEntityEvent.Type)msg.ReadRangedInteger(0, Enum.GetValues(typeof(NetEntityEvent.Type)).Length - 1);

            switch (eventType)
            {
                case NetEntityEvent.Type.ComponentState:
                    int componentIndex = msg.ReadRangedInteger(0, components.Count - 1);
                    (components[componentIndex] as IServerSerializable).ClientRead(type, msg, sendingTime);
                    break;
                case NetEntityEvent.Type.InventoryState:
                    ownInventory.ClientRead(type, msg, sendingTime);
                    break;
                case NetEntityEvent.Type.Repair:
                    for (int i = 0; i < FixRequirements.Count; i++)
                    {
                        ushort fixerID = msg.ReadUInt16();
                        FixRequirements[i].CurrentFixer = fixerID == 0 ? null : FindEntityByID(fixerID) as Character;
                        FixRequirements[i].FixProgress = msg.ReadRangedSingle(0.0f, 1.0f, 8);
                    }
                    break;
                case NetEntityEvent.Type.Status:
                    condition = msg.ReadRangedSingle(0.0f, prefab.Health, 8);

                    if (FixRequirements.Count > 0)
                    {
                        if (Condition <= 0.0f)
                        {
                            for (int i = 0; i < FixRequirements.Count; i++)
                                FixRequirements[i].FixProgress = msg.ReadRangedSingle(0.0f, 1.0f, 8);
                        }
                        else
                        {
                            for (int i = 0; i < FixRequirements.Count; i++)
                                FixRequirements[i].FixProgress = 1.0f;
                        }
                    }
                    break;
                case NetEntityEvent.Type.ApplyStatusEffect:
                    ActionType actionType = (ActionType)msg.ReadRangedInteger(0, Enum.GetValues(typeof(ActionType)).Length - 1);
                    ushort targetID = msg.ReadUInt16();
                    byte targetLimbID = msg.ReadByte();

                    Character target = FindEntityByID(targetID) as Character;
                    Limb targetLimb = targetLimbID < target.AnimController.Limbs.Length ? target.AnimController.Limbs[targetLimbID] : null;

                    ApplyStatusEffects(actionType, (float)Timing.Step, target, targetLimb, true);
                    break;
                case NetEntityEvent.Type.ChangeProperty:
                    ReadPropertyChange(msg);
                    break;
            }
        }

        public void ClientWrite(NetBuffer msg, object[] extraData = null)
        {
            if (extraData == null || extraData.Length == 0 || !(extraData[0] is NetEntityEvent.Type))
            {
                return;
            }

            NetEntityEvent.Type eventType = (NetEntityEvent.Type)extraData[0];
            msg.WriteRangedInteger(0, Enum.GetValues(typeof(NetEntityEvent.Type)).Length - 1, (int)eventType);
            switch (eventType)
            {
                case NetEntityEvent.Type.ComponentState:
                    int componentIndex = (int)extraData[1];
                    msg.WriteRangedInteger(0, components.Count - 1, componentIndex);

                    (components[componentIndex] as IClientSerializable).ClientWrite(msg, extraData);
                    break;
                case NetEntityEvent.Type.InventoryState:
                    ownInventory.ClientWrite(msg, extraData);
                    break;
                case NetEntityEvent.Type.Repair:
                    if (FixRequirements.Count > 0)
                    {
                        int requirementIndex = (int)extraData[1];
                        msg.WriteRangedInteger(0, FixRequirements.Count - 1, requirementIndex);
                    }
                    break;
                case NetEntityEvent.Type.ApplyStatusEffect:
                    UInt16 characterID = (UInt16)extraData[1];
                    Limb targetLimb = (Limb)extraData[2];

                    Character targetCharacter = FindEntityByID(characterID) as Character;

                    msg.Write(characterID);
                    msg.Write(targetCharacter == null ? (byte)255 : (byte)Array.IndexOf(targetCharacter.AnimController.Limbs, targetLimb));               
                    break;
                case NetEntityEvent.Type.ChangeProperty:
                    WritePropertyChange(msg, extraData);
                    break;
            }
            msg.WritePadBits();
        }
        
        public void ClientReadPosition(ServerNetObject type, NetBuffer msg, float sendingTime)
        {
            Vector2 newPosition = new Vector2(msg.ReadFloat(), msg.ReadFloat());
            float newRotation = msg.ReadRangedSingle(0.0f, MathHelper.TwoPi, 7);
            bool awake = msg.ReadBoolean();
            Vector2 newVelocity = Vector2.Zero;

            if (awake)
            {
                newVelocity = new Vector2(
                    msg.ReadRangedSingle(-MaxVel, MaxVel, 12),
                    msg.ReadRangedSingle(-MaxVel, MaxVel, 12));
            }

            if (body == null)
            {
                DebugConsole.ThrowError("Received a position update for an item with no physics body (" + Name + ")");
                return;
            }

            body.FarseerBody.Awake = awake;
            if (body.FarseerBody.Awake)
            {
                if ((newVelocity - body.LinearVelocity).Length() > 8.0f) body.LinearVelocity = newVelocity;
            }
            else
            {
                try
                {
                    body.FarseerBody.Enabled = false;
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Exception in PhysicsBody.Enabled = false (" + body.PhysEnabled + ")", e);
                    if (body.UserData != null) DebugConsole.NewMessage("PhysicsBody UserData: " + body.UserData.GetType().ToString(), Color.Red);
                    if (GameMain.World.ContactManager == null) DebugConsole.NewMessage("ContactManager is null!", Color.Red);
                    else if (GameMain.World.ContactManager.BroadPhase == null) DebugConsole.NewMessage("Broadphase is null!", Color.Red);
                    if (body.FarseerBody.FixtureList == null) DebugConsole.NewMessage("FixtureList is null!", Color.Red);
                }
            }

            if ((newPosition - SimPosition).Length() > body.LinearVelocity.Length() * 2.0f)
            {
                body.SetTransform(newPosition, newRotation);

                Vector2 displayPos = ConvertUnits.ToDisplayUnits(body.SimPosition);
                rect.X = (int)(displayPos.X - rect.Width / 2.0f);
                rect.Y = (int)(displayPos.Y + rect.Height / 2.0f);
            }
        }

        public void CreateClientEvent<T>(T ic) where T : ItemComponent, IClientSerializable
        {
            if (GameMain.Client == null) return;

            int index = components.IndexOf(ic);
            if (index == -1) return;

            GameMain.Client.CreateEntityEvent(this, new object[] { NetEntityEvent.Type.ComponentState, index });
        }
    }
}