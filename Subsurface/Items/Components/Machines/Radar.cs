﻿using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Subsurface.Items.Components
{
    class Radar : ItemComponent
    {
        float range;

        float angle;

        float pingState;

        //RenderTarget2D renderTarget;

        Sprite pingCircle, screenOverlay;

        [HasDefaultValue(0.0f, false)]
        public float Range
        {
            get { return ConvertUnits.ToDisplayUnits(range); }
            set { range = ConvertUnits.ToSimUnits(value); }
        }

        public Radar(Item item, XElement element)
            : base(item, element)
        {
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLower())
                {
                    case "pingcircle":
                        pingCircle = new Sprite(subElement);
                        break;
                    case "screenoverlay":
                        screenOverlay = new Sprite(subElement);
                        break;
                }
            }

            //renderTarget = new RenderTarget2D(Game1.CurrGraphicsDevice, GuiFrame.Rect.Width, GuiFrame.Rect.Height);
        }

        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam);

            pingState = (pingState + deltaTime * 0.5f);
            if (pingState>1.0f)
            {
                item.Use(deltaTime, null);
                pingState = 0.0f;
            }

            //angle = (angle + deltaTime) % MathHelper.TwoPi;
        }

        public override bool Use(float deltaTime, Character character = null)
        {
            return (pingState > 1.0f);
        }

        public override void DrawHUD(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Character character)
        {
            int width = GuiFrame.Rect.Width, height = GuiFrame.Rect.Height;
            int x = GuiFrame.Rect.X;
            int y = GuiFrame.Rect.Y;
            
            GuiFrame.Draw(spriteBatch);

            if (GUI.DrawButton(spriteBatch, new Rectangle(x+20, y+20, 200, 30), "Activate Radar")) isActive = !isActive;

            int radius = GuiFrame.Rect.Height / 2 - 10;
            DrawRadar(spriteBatch, new Rectangle((int)GuiFrame.Center.X - radius, (int)GuiFrame.Center.Y - radius, radius * 2, radius * 2));
        }

        private void DrawRadar(SpriteBatch spriteBatch, Rectangle rect)
        {

            Vector2 center = new Vector2(rect.Center.X, rect.Center.Y);
            //lineEnd += new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Math.Min(width, height) / 2.0f;
            //GUI.DrawLine(spriteBatch, GuiFrame.Center, lineEnd, Color.Green);

            if (!isActive) return;

            if (pingCircle!=null)
            {
                pingCircle.Draw(spriteBatch, center, Color.White * (1.0f-pingState), 0.0f, (rect.Width/pingCircle.size.X)*pingState);
            }


            float scale = 0.015f;
            float displayScale = ConvertUnits.ToDisplayUnits(scale);

            if (Level.Loaded != null)
            {
                List<Vector2[]> edges = Level.Loaded.GetCellEdges(-Level.Loaded.Position, 7);
                Vector2 offset = Vector2.Zero;

                for (int i = 0; i < edges.Count; i++)
                {
                    GUI.DrawLine(spriteBatch,
                        center + (edges[i][0] - offset) * scale,
                        center + (edges[i][1] - offset) * scale, Color.White);
                }

                for (int i = 0; i < Submarine.Loaded.HullVertices.Count; i++)
                {
                    Vector2 start = Submarine.Loaded.HullVertices[i] * displayScale;
                    start.Y = -start.Y;
                    Vector2 end = Submarine.Loaded.HullVertices[(i + 1) % Submarine.Loaded.HullVertices.Count] * displayScale;
                    end.Y = -end.Y;

                    GUI.DrawLine(spriteBatch, center + start, center + end, Color.White);
                }
            }

            foreach (Character c in Character.CharacterList)
            {
                if (c.AnimController.CurrentHull != null) continue;

                if (c.SimPosition != Vector2.Zero && c.SimPosition.Length() < 7 * Level.GridCellWidth)
                {
                    int width = (int)MathHelper.Clamp(c.Mass / 20, 1, 10);

                    Vector2 pos = c.Position * scale;
                    pos.Y = -pos.Y;
                    pos += center;

                    GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X - width / 2, (int)pos.Y - width / 2, width, width), Color.White, true);
                }
            }

            if (screenOverlay!=null)
            {
                screenOverlay.Draw(spriteBatch, center, 0.0f, rect.Width/screenOverlay.size.X);
            }

            //if (Level.Loaded != null)
            //{

            //    for (int i = 0; i < 2; i++)
            //    {
            //        Vector2 targetPos = (i == 0) ? Level.Loaded.StartPosition : Level.Loaded.EndPosition;
            //        targetPos += Level.Loaded.Position;

            //        float dist = targetPos.Length();

            //        targetPos.Y = -targetPos.Y;
            //        Vector2 markerPos = Vector2.Normalize(targetPos) * (rect.Width * 0.55f);
            //        markerPos += center;

            //        GUI.DrawRectangle(spriteBatch, new Rectangle((int)markerPos.X, (int)markerPos.Y, 5, 5), Color.LightGreen);

            //        string label;
            //        if (Game1.GameSession.Map!=null)
            //        {
            //            label = (i == 0) ? Game1.GameSession.Map.CurrentLocation.Name : Game1.GameSession.Map.SelectedLocation.Name;
            //        }
            //        else
            //        {
            //            label = (i == 0) ? "Start" : "End";
            //        }

            //        spriteBatch.DrawString(GUI.SmallFont, label, new Vector2(markerPos.X + 10, markerPos.Y), Color.LightGreen);
            //        spriteBatch.DrawString(GUI.SmallFont, (int)(dist / 80.0f) + " m", new Vector2(markerPos.X + 10, markerPos.Y + 15), Color.LightGreen);
            //    }

                DrawMarker(spriteBatch, 
                    (Game1.GameSession.Map == null) ? "Start" : Game1.GameSession.Map.CurrentLocation.Name,
                    Level.Loaded.StartPosition + Level.Loaded.Position, center, (rect.Width * 0.55f));

                DrawMarker(spriteBatch,
                    (Game1.GameSession.Map == null) ? "End" : Game1.GameSession.Map.SelectedLocation.Name,
                    Level.Loaded.EndPosition + Level.Loaded.Position, center, (rect.Width * 0.55f));

            if (Game1.GameSession.Map != null && Game1.GameSession.Map.SelectedConnection.Quest!=null)
            {
                var quest = Game1.GameSession.Map.SelectedConnection.Quest;

                if (!string.IsNullOrWhiteSpace(quest.RadarLabel))
                {
                    DrawMarker(spriteBatch,
                        quest.RadarLabel,
                        quest.RadarPosition, center, (rect.Width * 0.55f));
                }


            }
        }

        private void DrawMarker(SpriteBatch spriteBatch, string label, Vector2 position, Vector2 center, float radius)
        {
            //position += Level.Loaded.Position;

            float dist = position.Length();

            position.Y = -position.Y;
            Vector2 markerPos = center + Vector2.Normalize(position) * radius;

            GUI.DrawRectangle(spriteBatch, new Rectangle((int)markerPos.X, (int)markerPos.Y, 5, 5), Color.LightGreen);

            spriteBatch.DrawString(GUI.SmallFont, label, new Vector2(markerPos.X + 10, markerPos.Y), Color.LightGreen);
            spriteBatch.DrawString(GUI.SmallFont, (int)(dist / 80.0f) + " m", new Vector2(markerPos.X + 10, markerPos.Y + 15), Color.LightGreen);
                
        }

        private void UpdateRendertarget()
        {

        }
    }
}
