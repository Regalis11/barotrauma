﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Barotrauma.Lights;
using System.Diagnostics;

namespace Barotrauma
{
    class GameScreen : Screen
    {
        Camera cam;

        readonly RenderTarget2D renderTarget;
        readonly RenderTarget2D renderTargetWater;
        readonly RenderTarget2D renderTargetAir;

        public BackgroundSpriteManager BackgroundSpriteManager;

        public Camera Cam
        {
            get { return cam; }
        }

        public GameScreen(GraphicsDevice graphics)
        {
            cam = new Camera();
            cam.Translate(new Vector2(-10.0f, 50.0f));
            
            renderTarget = new RenderTarget2D(graphics, GameMain.GraphicsWidth, GameMain.GraphicsHeight);
            renderTargetWater = new RenderTarget2D(graphics, GameMain.GraphicsWidth, GameMain.GraphicsHeight);
            renderTargetAir = new RenderTarget2D(graphics, GameMain.GraphicsWidth, GameMain.GraphicsHeight);

            
            BackgroundSpriteManager = new BackgroundSpriteManager("Content/BackgroundSprites/BackgroundSpritePrefabs.xml");
        }

        public override void Select()
        {
            base.Select();

            foreach (MapEntity entity in MapEntity.mapEntityList)
                entity.IsHighlighted = false;
        }

        public override void Deselect()
        {
            base.Deselect();

            Sounds.SoundManager.LowPassHFGain = 1.0f;
        }

        int rendc;

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(double deltaTime)
        {
            //the accumulator code is based on this article:
            //http://gafferongames.com/game-physics/fix-your-timestep/
            Physics.accumulator += deltaTime;

#if DEBUG
            if (GameMain.GameSession != null && GameMain.GameSession.Level != null && GameMain.GameSession.Submarine != null)
            {
                Vector2 targetMovement = Vector2.Zero;
                if (PlayerInput.KeyDown(Keys.I)) targetMovement.Y += 1.0f;
                if (PlayerInput.KeyDown(Keys.K)) targetMovement.Y -= 1.0f;
                if (PlayerInput.KeyDown(Keys.J)) targetMovement.X -= 1.0f;
                if (PlayerInput.KeyDown(Keys.L)) targetMovement.X += 1.0f;

                GameMain.GameSession.Submarine.ApplyForce(targetMovement * 1000000.0f);
            }
#endif

            if (GameMain.GameSession!=null) GameMain.GameSession.Update((float)deltaTime);
            //EventManager.Update(gameTime);

            if (Level.Loaded != null) Level.Loaded.Update((float)deltaTime);

            Character.UpdateAll(cam, (float)deltaTime);

            BackgroundSpriteManager.Update(cam, (float)deltaTime);

            GameMain.ParticleManager.Update((float)deltaTime);

            StatusEffect.UpdateAll((float)deltaTime);

            Physics.accumulator = Math.Min(Physics.accumulator, Physics.step * 6);
            //Physics.accumulator = Physics.step;
            while (Physics.accumulator >= Physics.step)
            {
                cam.MoveCamera((float)Physics.step);
                if (Character.Controlled != null)
                {
                    cam.TargetPos = Character.Controlled.WorldPosition;
                    Lights.LightManager.ViewPos = Character.Controlled.WorldPosition; 
                }

                if (Submarine.Loaded != null) Submarine.Loaded.SetPrevTransform(Submarine.Loaded.Position);

                foreach (PhysicsBody pb in PhysicsBody.list)
                {
                    pb.SetPrevTransform(pb.SimPosition, pb.Rotation);
                }
                
                MapEntity.UpdateAll(cam, (float)Physics.step);

                Character.UpdateAnimAll((float)Physics.step);

                Ragdoll.UpdateAll(cam, (float)Physics.step);

                if (GameMain.GameSession != null && GameMain.GameSession.Level != null && GameMain.GameSession.Submarine!=null)
                {
                    GameMain.GameSession.Submarine.Update((float)Physics.step);
                }

                GameMain.World.Step((float)Physics.step);

                //Level.AfterWorldStep();

                Physics.accumulator -= Physics.step;
            }


            Physics.Alpha = Physics.accumulator / Physics.step;

        }

        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            //if (!Physics.updated) return;

            cam.UpdateTransform();

            DrawMap(graphics, spriteBatch);

            spriteBatch.Begin();

            if (Character.Controlled != null && Character.Controlled.SelectedConstruction != null)
            {
                if (Character.Controlled.SelectedConstruction == Character.Controlled.ClosestItem)
                {
                    Character.Controlled.SelectedConstruction.DrawHUD(spriteBatch, Character.Controlled);
                }
                else
                {
                    Character.Controlled.SelectedConstruction = null;
                }
            }

            if (GameMain.GameSession != null) GameMain.GameSession.Draw(spriteBatch);

            GUI.Draw((float)deltaTime, spriteBatch, cam);
                        
            if (PlayerInput.GetMouseState.LeftButton != ButtonState.Pressed) Inventory.draggingItem = null;

            spriteBatch.End();
        }

        public void DrawMap(GraphicsDevice graphics, SpriteBatch spriteBatch)
        {

            if (Submarine.Loaded != null) Submarine.Loaded.UpdateTransform();

            GameMain.LightManager.ObstructVision = Character.Controlled != null && Character.Controlled.ObstructVision;

            GameMain.LightManager.UpdateLightMap(graphics, spriteBatch, cam);
            GameMain.LightManager.UpdateObstructVision(graphics, spriteBatch, cam, 
                Character.Controlled==null ? LightManager.ViewPos : Character.Controlled.CursorWorldPosition);


            //----------------------------------------------------------------------------------------
            //1. draw the background, characters and the parts of the submarine that are behind them
            //----------------------------------------------------------------------------------------

            graphics.SetRenderTarget(renderTarget);
            graphics.Clear(new Color(11, 18, 26, 255));

            if (Level.Loaded != null) Level.Loaded.DrawBack(spriteBatch, cam, BackgroundSpriteManager);


            spriteBatch.Begin(SpriteSortMode.BackToFront,
                BlendState.AlphaBlend,
                null, null, null, null,
                cam.Transform);

            Submarine.DrawBack(spriteBatch);

            foreach (Character c in Character.CharacterList) c.Draw(spriteBatch);

            spriteBatch.End();

            GameMain.LightManager.DrawLightMap(spriteBatch, cam);

            //----------------------------------------------------------------------------------------
            //draw the rendertarget and particles that are only supposed to be drawn in water into renderTargetWater
            graphics.SetRenderTarget(renderTargetWater);

            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.Opaque);
            spriteBatch.Draw(renderTarget, new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight), new Color(0.75f, 0.8f, 0.9f, 1.0f));
            spriteBatch.End();

#if LINUX
            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                null, DepthStencilState.DepthRead, null, null,
                cam.Transform);
#else
            spriteBatch.Begin(SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            null, DepthStencilState.DepthRead, null, null,
            cam.Transform);
#endif
            GameMain.ParticleManager.Draw(spriteBatch, true, Particles.ParticleBlendState.AlphaBlend);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.Additive,
                null, DepthStencilState.Default, null, null,
                cam.Transform);
            GameMain.ParticleManager.Draw(spriteBatch, true, Particles.ParticleBlendState.Additive);
            spriteBatch.End();

            //----------------------------------------------------------------------------------------
            //draw the rendertarget and particles that are only supposed to be drawn in air into renderTargetAir

            graphics.SetRenderTarget(renderTargetAir);
            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.Opaque);
            spriteBatch.Draw(renderTarget, new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight), Color.White);
            spriteBatch.End();
#if LINUX
            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                null, DepthStencilState.DepthRead, null, null,
                cam.Transform);
#else
            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                null, DepthStencilState.DepthRead, null, null,
                cam.Transform);
#endif

            GameMain.ParticleManager.Draw(spriteBatch, false, Particles.ParticleBlendState.AlphaBlend);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.Additive,
                null, DepthStencilState.DepthRead, null, null,
                cam.Transform);
            GameMain.ParticleManager.Draw(spriteBatch, false, Particles.ParticleBlendState.Additive);
            spriteBatch.End();


            graphics.SetRenderTarget(null);

            //----------------------------------------------------------------------------------------
            //2. pass the renderTarget to the water shader to do the water effect
            //----------------------------------------------------------------------------------------

            Hull.renderer.RenderBack(spriteBatch, renderTargetWater);

            Array.Clear(Hull.renderer.vertices, 0, Hull.renderer.vertices.Length);
            Hull.renderer.PositionInBuffer = 0;
            foreach (Hull hull in Hull.hullList)
            {
                hull.Render(graphics, cam);
            }

            Hull.renderer.Render(graphics, cam, renderTargetAir, Cam.ShaderTransform);
            
            //----------------------------------------------------------------------------------------
            //3. draw the sections of the map that are on top of the water
            //----------------------------------------------------------------------------------------

            spriteBatch.Begin(SpriteSortMode.BackToFront,
                BlendState.AlphaBlend, SamplerState.LinearWrap,
                null, null, null,
                cam.Transform);

            foreach (Character c in Character.CharacterList) c.DrawFront(spriteBatch);

            Submarine.DrawFront(spriteBatch);
            
            if (Level.Loaded!=null) Level.Loaded.DrawFront(spriteBatch);            

            spriteBatch.End();

            GameMain.LightManager.DrawLOS(graphics, spriteBatch, cam, LightManager.ViewPos);
        }
    }
}
