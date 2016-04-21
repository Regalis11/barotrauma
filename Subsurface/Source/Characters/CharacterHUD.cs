﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Barotrauma.Items.Components;

namespace Barotrauma
{
    class CharacterHUD
    {
        private static Sprite statusIcons;

        private static Sprite noiseOverlay, damageOverlay;

        private static GUIButton cprButton;

        private static GUIButton suicideButton;

        private static GUIProgressBar drowningBar, healthBar;

        private static float damageOverlayTimer;

        public static void TakeDamage(float amount)
        {
            healthBar.Flash();

            damageOverlayTimer = MathHelper.Clamp(amount * 0.1f, 0.2f, 5.0f);
        }

        public static void Update(float deltaTime, Character character)
        {
            if (drowningBar != null)
            {
                drowningBar.Update(deltaTime);
                if (character.Oxygen < 10.0f) drowningBar.Flash();
            }
            if (healthBar != null) healthBar.Update(deltaTime);

            if (cprButton != null && cprButton.Visible) cprButton.Update(deltaTime);

            if (GameMain.NetworkMember != null && suicideButton != null && suicideButton.Visible) suicideButton.Update(deltaTime);

            if (damageOverlayTimer > 0.0f) damageOverlayTimer -= deltaTime;
        }

        public static void Draw(SpriteBatch spriteBatch, Character character, Camera cam)
        {
            if (statusIcons == null)
            {
                statusIcons = new Sprite("Content/UI/statusIcons.png", Vector2.Zero);
            }

            if (noiseOverlay == null)
            {
                noiseOverlay = new Sprite("Content/UI/noise.png", Vector2.Zero);
            }

            if (damageOverlay == null)
            {
                damageOverlay = new Sprite("Content/UI/damageOverlay.png", Vector2.Zero);
            }

            if (character.Inventory != null)
            {
                for (int i = 0; i< character.Inventory.Items.Length-1; i++)
                {
                    var item = character.Inventory.Items[i];
                    if (item == null || CharacterInventory.limbSlots[i]==InvSlotType.Any) continue;
                    //var statusHUD = item.GetComponent<StatusHUD>();
                    //if (statusHUD == null) continue;
                    foreach (ItemComponent ic in item.components)
                    {
                        if (ic.DrawHudWhenEquipped) ic.DrawHUD(spriteBatch, character);
                    }
                }
            }

            DrawStatusIcons(spriteBatch, character);

            if (!character.IsUnconscious)
            {
                if (character.Inventory != null && !character.LockHands &&
                    character.Stun >= -0.1f) character.Inventory.DrawOwn(spriteBatch, Vector2.Zero);

                if (character.SelectedCharacter != null && character.SelectedCharacter.Inventory != null)
                {
                    character.SelectedCharacter.Inventory.DrawOwn(spriteBatch, new Vector2(320.0f, 0.0f));

                    if (cprButton == null)
                    {
                        cprButton = new GUIButton(
                            new Rectangle(character.SelectedCharacter.Inventory.SlotPositions[0].ToPoint() + new Point(320, -30), new Point(130, 20)), "Perform CPR", GUI.Style);

                        cprButton.OnClicked = (button, userData) =>
                        {
                            if (Character.Controlled == null || Character.Controlled.SelectedCharacter == null) return false;

                            Character.Controlled.AnimController.Anim = (Character.Controlled.AnimController.Anim == AnimController.Animation.CPR) ?
                                AnimController.Animation.None : AnimController.Animation.CPR;

                            foreach (Limb limb in Character.Controlled.SelectedCharacter.AnimController.Limbs)
                            {
                                limb.pullJoint.Enabled = false;
                            }

                            new NetworkEvent(NetworkEventType.SelectCharacter, Character.Controlled.ID, true, Character.Controlled.SelectedCharacter.ID);

                            return true;
                        };
                    }

                    //cprButton.Visible = character.GetSkillLevel("Medical") > 20.0f;

                    if (cprButton.Visible) cprButton.Draw(spriteBatch);
                }

                if (character.ClosestCharacter != null && character.ClosestCharacter.CanBeSelected)
                {
                    Vector2 startPos = character.DrawPosition + (character.ClosestCharacter.DrawPosition - character.DrawPosition) * 0.7f;
                    startPos = cam.WorldToScreen(startPos);

                    Vector2 textPos = startPos;
                    textPos -= new Vector2(GUI.Font.MeasureString(character.ClosestCharacter.Info.Name).X / 2, 20);

                    GUI.DrawString(spriteBatch, textPos, character.ClosestCharacter.Info.Name, Color.White, Color.Black, 2);
                }
                else if (character.SelectedCharacter == null && character.ClosestItem != null && character.SelectedConstruction == null)
                {
                    var hudTexts = character.ClosestItem.GetHUDTexts(character);

                    Vector2 startPos = new Vector2((int)(GameMain.GraphicsWidth / 2.0f), GameMain.GraphicsHeight);
                    startPos.Y -= 50 + hudTexts.Count * 25;

                    Vector2 textPos = startPos;
                    textPos -= new Vector2((int)GUI.Font.MeasureString(character.ClosestItem.Name).X / 2, 20);

                    GUI.DrawString(spriteBatch, textPos, character.ClosestItem.Name, Color.White, Color.Black * 0.7f, 2);

                    textPos.Y += 30.0f;
                    foreach (ColoredText coloredText in hudTexts)
                    {
                        textPos.X = (int)(startPos.X - GUI.SmallFont.MeasureString(coloredText.Text).X / 2);

                        GUI.DrawString(spriteBatch, textPos, coloredText.Text, coloredText.Color, Color.Black * 0.7f, 2, GUI.SmallFont);

                        textPos.Y += 25;
                    }
                }  
            }

            if (Screen.Selected == GameMain.EditMapScreen) return;

            if (character.IsUnconscious || (character.Oxygen < 80.0f && !character.IsDead))
            {
                Vector2 offset = Rand.Vector(noiseOverlay.size.X);
                offset.X = Math.Abs(offset.X);
                offset.Y = Math.Abs(offset.Y);

                float alpha = character.IsUnconscious ? 1.0f : Math.Min((80.0f - character.Oxygen)/50.0f, 0.8f);

                noiseOverlay.DrawTiled(spriteBatch, Vector2.Zero - offset, new Vector2(GameMain.GraphicsWidth, GameMain.GraphicsHeight) + offset,
                    Vector2.Zero,
                    Color.White * alpha);

            }
            else
            {
                if (suicideButton != null) suicideButton.Visible = false;
            }

            if (damageOverlayTimer>0.0f)
            {
                damageOverlay.Draw(spriteBatch, Vector2.Zero, Color.White * damageOverlayTimer, Vector2.Zero, 0.0f,
                    new Vector2(GameMain.GraphicsWidth / damageOverlay.size.X, GameMain.GraphicsHeight / damageOverlay.size.Y));
            }

            if (character.IsUnconscious && GameMain.NetworkMember!=null)
            {
                if (suicideButton == null)
                {
                    suicideButton = new GUIButton(
                        new Rectangle(new Point(GameMain.GraphicsWidth / 2 - 60, 20), new Point(120, 20)), "Give in", GUI.Style);

                    suicideButton.ToolTip = "Let go of your character and enter spectator mode (other players will now longer be able to revive you)";

                    suicideButton.OnClicked = (button, userData) =>
                    {
                        character.Kill(character.CauseOfDeath);
                        return true;
                    };
                }

                suicideButton.Visible = true;
                suicideButton.Draw(spriteBatch);                
            }
        }

        private static void DrawStatusIcons(SpriteBatch spriteBatch, Character character)
        {
            if (drowningBar == null)
            {
                int width = 100, height = 20;

                drowningBar = new GUIProgressBar(new Rectangle(30, GameMain.GraphicsHeight - 200, width, height), Color.Blue, GUI.Style, 1.0f, Alignment.TopLeft);
                new GUIImage(new Rectangle(-27, -7, 20, 20), new Rectangle(17, 0, 20, 24), statusIcons, Alignment.TopLeft, drowningBar);

                healthBar = new GUIProgressBar(new Rectangle(30, GameMain.GraphicsHeight - 230, width, height), Color.Red, GUI.Style, 1.0f, Alignment.TopLeft);
                new GUIImage(new Rectangle(-26, -7, 20, 20), new Rectangle(0, 0, 13, 24), statusIcons, Alignment.TopLeft, healthBar);
            }

            drowningBar.BarSize = character.Oxygen / 100.0f;
            if (drowningBar.BarSize < 0.99f)
            {
                drowningBar.Draw(spriteBatch);
            }

            healthBar.BarSize = character.Health / character.MaxHealth;
            if (healthBar.BarSize < 1.0f)
            {
                healthBar.Draw(spriteBatch);
            }

            int bloodDropCount = (int)Math.Floor(character.Bleeding);
            bloodDropCount = MathHelper.Clamp(bloodDropCount, 0, 5);
            for (int i = 1; i < bloodDropCount; i++)
            {
                spriteBatch.Draw(statusIcons.Texture, new Vector2(5.0f + 20 * i, healthBar.Rect.Y - 20.0f), new Rectangle(39, 3, 15, 19), Color.White * 0.8f);
            }

            float pressureFactor = (character.AnimController.CurrentHull == null) ?
                100.0f : Math.Min(character.AnimController.CurrentHull.LethalPressure,100.0f);
            if (character.PressureProtection > 0.0f) pressureFactor = 0.0f;

            if (pressureFactor>0.0f)
            {
                float indicatorAlpha = ((float)Math.Sin(character.PressureTimer * 0.1f) + 1.0f) * 0.5f;

                indicatorAlpha = MathHelper.Clamp(indicatorAlpha, 0.1f, pressureFactor/100.0f);

                spriteBatch.Draw(statusIcons.Texture, new Vector2(10.0f, healthBar.Rect.Y - 60.0f), new Rectangle(0, 24, 24, 25), Color.White * indicatorAlpha);
            
            }
        }
    }
}
