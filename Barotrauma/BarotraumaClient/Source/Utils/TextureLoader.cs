using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Threading;
using Color = Microsoft.Xna.Framework.Color;

namespace Barotrauma
{
    /// <summary>
    /// Based on http://jakepoz.com/jake_poznanski__background_load_xna.html 
    /// </summary>
    public static class TextureLoader
    {
        static TextureLoader()
        {

            BlendColorBlendState = new BlendState
            {
                ColorDestinationBlend = Blend.Zero,
                ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue,
                AlphaDestinationBlend = Blend.Zero,
                AlphaSourceBlend = Blend.SourceAlpha,
                ColorSourceBlend = Blend.SourceAlpha
            };

            BlendAlphaBlendState = new BlendState
            {
                ColorWriteChannels = ColorWriteChannels.Alpha,
                AlphaDestinationBlend = Blend.Zero,
                ColorDestinationBlend = Blend.Zero,
                AlphaSourceBlend = Blend.One,
                ColorSourceBlend = Blend.One
            };
        }

        public static void Init(GraphicsDevice graphicsDevice, bool needsBmp = false)
        {
            _graphicsDevice = graphicsDevice;
            _needsBmp = needsBmp;
            _spriteBatch = new SpriteBatch(_graphicsDevice);
        }

        public static Texture2D FromFile(string path, bool preMultiplyAlpha = true)
        {
            try
            {
                using (Stream fileStream = File.OpenRead(path))
                {
                    var texture = Texture2D.FromStream(_graphicsDevice, fileStream);
                    if (preMultiplyAlpha)
                    {
                        PreMultiplyAlpha(texture);
                    }
                    return texture;
                }

            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Loading texture \"" + path + "\" failed!", e);
                return null;
            }
        }

        public static Texture2D FromStream(Stream fileStream, bool preMultiplyAlpha = true)
        {
            try
            {
                var texture = Texture2D.FromStream(_graphicsDevice, fileStream);
                PreMultiplyAlpha(texture);
                return texture;
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Loading texture from stream failed!", e);
                return null;
            }
        }

        private static void PreMultiplyAlpha(Texture2D texture)
        {
            UInt32[] data = new UInt32[texture.Width * texture.Height];
            texture.GetData(data);
            for (int y = 0; y < texture.Height; y++)
            {
                for (int x = 0; x < texture.Width; x++)
                {
                    uint a = (data[x + (y * texture.Width)] & 0xff000000) >> 24;
                    uint r = (data[x + (y * texture.Width)] & 0x00ff0000) >> 16;
                    uint g = (data[x + (y * texture.Width)] & 0x0000ff00) >> 8;
                    uint b = (data[x + (y * texture.Width)] & 0x000000ff);
                    b *= a; b /= 255;
                    g *= a; g /= 255;
                    r *= a; r /= 255;
                    data[x + (y * texture.Width)] = (a<<24) | (r<<16) | (g<<8) | b;
                }
            }
            texture.SetData(data);
        }
       
        
        private static readonly BlendState BlendColorBlendState;
        private static readonly BlendState BlendAlphaBlendState;

        private static GraphicsDevice _graphicsDevice;
        private static SpriteBatch _spriteBatch;
        private static bool _needsBmp;
    }
}