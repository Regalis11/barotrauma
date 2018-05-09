using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;

namespace Barotrauma
{
    public partial class Sprite
    {
        protected Texture2D texture;

        public Texture2D Texture
        {
            get { return texture; }
        }

        public Sprite(Texture2D texture, Rectangle? sourceRectangle, Vector2? newOffset, float newRotation = 0.0f)
        {
            this.texture = texture;

            sourceRect = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);

            offset = newOffset ?? Vector2.Zero;

            size = new Vector2(sourceRect.Width, sourceRect.Height);

            origin = Vector2.Zero;

            effects = SpriteEffects.None;

            rotation = newRotation;

            list.Add(this);
        }

        partial void LoadTexture(ref Vector4 sourceVector,ref bool shouldReturn)
        {
            texture = LoadTexture(this.file);

            if (texture == null)
            {
                shouldReturn = true;
                return;
            }

            if (sourceVector.Z == 0.0f) sourceVector.Z = texture.Width;
            if (sourceVector.W == 0.0f) sourceVector.W = texture.Height;
        }

        partial void CalculateSourceRect()
        {
            sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
        }


        public static Texture2D LoadTexture(string file)
        {
            foreach (Sprite s in list)
            {
                if (s.file == file) return s.texture;
            }

            if (File.Exists(file))
            {
                return TextureLoader.FromFile(file);
            }
            else
            {
                DebugConsole.ThrowError("Sprite \"" + file + "\" not found!");
            }

            return null;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 pos, float rotate = 0.0f, float scale = 1.0f, SpriteEffects spriteEffect = SpriteEffects.None)
        {
            this.Draw(spriteBatch, pos, Color.White, rotate, scale, spriteEffect);
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 pos, Color color, float rotate = 0.0f, float scale = 1.0f, SpriteEffects spriteEffect = SpriteEffects.None, float? depth = null)
        {
            this.Draw(spriteBatch, pos, color, this.origin, rotate, new Vector2(scale, scale), spriteEffect, depth);
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 pos, Color color, Vector2 origin, float rotate = 0.0f, float scale = 1.0f, SpriteEffects spriteEffect = SpriteEffects.None, float? depth = null)
        {
            this.Draw(spriteBatch, pos, color, origin, rotate, new Vector2(scale, scale), spriteEffect, depth);
        }

        public virtual void Draw(SpriteBatch spriteBatch, Vector2 pos, Color color, Vector2 origin, float rotate, Vector2 scale, SpriteEffects spriteEffect = SpriteEffects.None, float? depth = null)
        {
            if (texture == null) return;
            //DrawSilhouette(spriteBatch, pos, origin, rotate, scale, spriteEffect, depth);
            spriteBatch.Draw(texture, pos + offset, sourceRect, color, rotation + rotate, origin, scale, spriteEffect, depth ?? this.depth);
        }

        /// <summary>
        /// Creates a silhouette for the sprite (or outline if the sprite is rendered on top of it)
        /// </summary>
        public void DrawSilhouette(SpriteBatch spriteBatch, Vector2 pos, Vector2 origin, float rotate, Vector2 scale, SpriteEffects spriteEffect = SpriteEffects.None, float? depth = null)
        {
            if (texture == null) return;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    spriteBatch.Draw(texture, pos + offset + new Vector2(x, y), sourceRect, Color.Black, rotation + rotate, origin, scale, spriteEffect, (depth ?? this.depth) + 0.01f);
                }
            }
        }

        public void DrawTiled(SpriteBatch spriteBatch, Vector2 position, Vector2 targetSize,
            Rectangle? rect = null, Color? color = null, Point? startOffset = null, Vector2? textureScale = null, float? depth = null)
        {
            //Init optional values
            Vector2 drawOffset = startOffset.HasValue ? new Vector2(startOffset.Value.X, startOffset.Value.Y) : Vector2.Zero;
            Vector2 scale = textureScale ?? Vector2.One;
            Color drawColor = color ?? Color.White;

            //wrap the drawOffset inside the sourceRect
            drawOffset.X = (drawOffset.X / scale.X) % sourceRect.Width;
            drawOffset.Y = (drawOffset.Y / scale.Y) % sourceRect.Height;
            if (effects.HasFlag(SpriteEffects.FlipHorizontally))
            {
                float diff =  targetSize.X % (sourceRect.Width * scale.X);
                drawOffset.X += (sourceRect.Width * scale.X - diff) / scale.X;
            }
            if (effects.HasFlag(SpriteEffects.FlipVertically))
            {
                float diff = targetSize.Y % (sourceRect.Height * scale.Y);
                drawOffset.Y += (sourceRect.Height * scale.Y - diff) / scale.Y;
            }
            
            //how many times the texture needs to be drawn on the x-axis
            int xTiles = (int)Math.Ceiling((targetSize.X + drawOffset.X * scale.X) / (sourceRect.Width * scale.X));
            //how many times the texture needs to be drawn on the y-axis
            int yTiles = (int)Math.Ceiling((targetSize.Y + drawOffset.Y * scale.Y) / (sourceRect.Height * scale.Y));

            //where the current tile is being drawn;
            Vector2 currDrawPosition = position - drawOffset;
            //which part of the texture we are currently drawing
            Rectangle texPerspective = sourceRect;
            
            for (int x = 0; x < xTiles; x++)
            {
                texPerspective.X = sourceRect.X;
                texPerspective.Width = sourceRect.Width;
                texPerspective.Height = sourceRect.Height;

                //offset to the left, draw a partial slice
                if (currDrawPosition.X < position.X)
                {
                    float diff = (position.X - currDrawPosition.X);
                    currDrawPosition.X += diff;
                    texPerspective.Width -= (int)diff;
                    if (!effects.HasFlag(SpriteEffects.FlipHorizontally))
                    {
                        texPerspective.X += (int)diff;
                    }
                }
                //drawing an offset flipped sprite, need to draw an extra slice to the left side
                if (currDrawPosition.X > position.X && x == 0)
                {
                    if (effects.HasFlag(SpriteEffects.FlipHorizontally))
                    {
                        int sliceWidth = (int)((currDrawPosition.X - position.X) * scale.X);

                        Vector2 slicePos = currDrawPosition;
                        slicePos.X = position.X;
                        Rectangle sliceRect = texPerspective;
                        sliceRect.X = SourceRect.X;
                        sliceRect.Width = (int)(sliceWidth / scale.X);
                        
                        if (effects.HasFlag(SpriteEffects.FlipVertically))
                        {
                            slicePos.Y += size.Y;
                        }
                        
                        spriteBatch.Draw(texture, slicePos, sliceRect, drawColor, rotation, Vector2.Zero, scale, effects, depth ?? this.depth);                        
                        currDrawPosition.X = slicePos.X + sliceWidth;
                    }
                }
                //make sure the rightmost tiles don't go over the right side
                if (x == xTiles - 1)
                {
                    int diff = (int)(((currDrawPosition.X + texPerspective.Width * scale.X) - (position.X + targetSize.X)) / scale.X);
                    texPerspective.Width -= diff;
                    if (effects.HasFlag(SpriteEffects.FlipHorizontally))
                    {
                        texPerspective.X += diff;
                    }
                }
                
                currDrawPosition.Y = position.Y - drawOffset.Y;

                for (int y = 0; y < yTiles; y++)
                {
                    texPerspective.Y = sourceRect.Y;
                    texPerspective.Height = sourceRect.Height;

                    //offset above the top, draw a partial slice
                    if (currDrawPosition.Y < position.Y)
                    {
                        float diff = (position.Y - currDrawPosition.Y);
                        currDrawPosition.Y += diff;
                        texPerspective.Height -= (int)diff;
                        if (!effects.HasFlag(SpriteEffects.FlipVertically))
                        {
                            texPerspective.Y += (int)diff;
                        }
                    }

                    //drawing an offset flipped sprite, need to draw an extra slice to the top
                    if (currDrawPosition.Y > position.Y && y == 0)
                    {
                        if (effects.HasFlag(SpriteEffects.FlipVertically))
                        {
                            int sliceHeight = (int)((currDrawPosition.Y - position.Y) * scale.Y);

                            Vector2 slicePos = currDrawPosition;
                            slicePos.Y = position.Y;
                            Rectangle sliceRect = texPerspective;
                            sliceRect.Y = SourceRect.Y;
                            sliceRect.Height = (int)(sliceHeight / scale.Y);

                            spriteBatch.Draw(texture, slicePos, sliceRect, drawColor, rotation, Vector2.Zero, scale, effects, depth ?? this.depth);

                            currDrawPosition.Y = slicePos.Y + sliceHeight;
                        }
                    }

                    //make sure the bottommost tiles don't go over the bottom
                    if (y == yTiles - 1)
                    {
                        int diff = (int)(((currDrawPosition.Y + texPerspective.Height * scale.Y) - (position.Y + targetSize.Y)) / scale.Y);
                        texPerspective.Height -= diff;
                        if (effects.HasFlag(SpriteEffects.FlipVertically))
                        {
                            texPerspective.Y += diff;
                        }
                    }

                    spriteBatch.Draw(texture, currDrawPosition,
                        texPerspective, drawColor, rotation, Vector2.Zero, scale, effects, depth ?? this.depth);

                    currDrawPosition.Y += texPerspective.Height * scale.Y;
                }

                currDrawPosition.X += texPerspective.Width * scale.X;
            }
        }

        partial void DisposeTexture()
        {
            //check if another sprite is using the same texture
            foreach (Sprite s in list)
            {
                if (s.file == file) return;
            }

            //if not, free the texture
            if (texture != null)
            {
                texture.Dispose();
                texture = null;
            }
        }
    }

}

