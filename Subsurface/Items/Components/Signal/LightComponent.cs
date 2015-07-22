﻿using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Subsurface.Lights;
using System;
using System.Xml.Linq;

namespace Subsurface.Items.Components
{
    class LightComponent : Powered
    {
        private Color lightColor;

        //private Sprite sprite;

        LightSource light;

        float range;

        float lightBrightness;

        [HasDefaultValue(100.0f, true)]
        public float Range
        {
            get { return range; }
            set
            {
                range = MathHelper.Clamp(value, 0.0f, 2048.0f);
            }
        }

        [InGameEditable, HasDefaultValue("1.0,1.0,1.0,1.0", true)]
        public string LightColor
        {
            get { return ToolBox.Vector4ToString(lightColor.ToVector4()); }
            set
            {
                lightColor = new Color(ToolBox.ParseToVector4(value));
            }
        }

        public override void Move(Vector2 amount)
        {
            light.Position += amount;
        }

        public LightComponent(Item item, XElement element)
            : base (item, element)
        {
            //foreach (XElement subElement in element.Elements())
            //{
            //    if (subElement.Name.ToString().ToLower() != "sprite") continue;
            //    sprite = new Sprite(subElement);
            //    break;
            //}

            light = new LightSource(item.Position, 100.0f, Color.White);

            isActive = true;

            //lightColor = new Color(ToolBox.GetAttributeVector4(element, "color", Vector4.One));
        }

        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam);

            if (item.body != null)
            {
                light.Position = ConvertUnits.ToDisplayUnits(item.body.Position);
            }

            if (powerConsumption == 0.0f)
            {
                voltage = 1.0f;
            }
            else
            {
                currPowerConsumption = powerConsumption;                
            }

            if (voltage < Rand.Range(0.0f, minVoltage))
            {
                lightBrightness = 0.0f;
            }
            else
            {
                lightBrightness = MathHelper.Lerp(lightBrightness, Math.Min(voltage, 1.0f), 0.1f);
            }

            light.Color = lightColor * lightBrightness;

            light.Range = range * (float)Math.Sqrt(lightBrightness);

            voltage = 0.0f;
        }

        public override void Draw(SpriteBatch spriteBatch, bool editing)
        {
            if (!isActive)
            {
                light.Color = Color.Transparent;
            }   
        }

        public override void ReceiveSignal(string signal, Connection connection, Item sender, float power=0.0f)
        {
            base.ReceiveSignal(signal, connection, sender, power);

            switch (connection.Name)
            {
                case "toggle":
                    isActive = !isActive;
                    break;
                case "set_state":           
                    isActive = (signal != "0");                   
                    break;
            }
        }
    }
}
