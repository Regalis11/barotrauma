﻿using Microsoft.Xna.Framework;
using Barotrauma.Particles;
using System;
using System.Xml.Linq;


namespace Barotrauma
{
    enum CauseOfDeath
    {
        Damage, Bloodloss, Pressure, Suffocation, Drowning
    }

    public enum DamageType { None, Blunt, Slash }

    public enum AttackType
    {
        None, PinchCW, PinchCCW
    }

    struct AttackResult
    {
        public readonly float Damage;
        public readonly float Bleeding;

        public readonly bool HitArmor;

        public AttackResult(float damage, float bleeding, bool hitArmor=false)
        {
            this.Damage = damage;
            this.Bleeding = bleeding;

            this.HitArmor = hitArmor;
        }
    }

    class Attack
    {

        public readonly AttackType Type;
        public readonly float Range;
        public readonly float Duration;

        public readonly DamageType DamageType;

        private readonly float structureDamage;
        private readonly float damage;
        private readonly float bleedingDamage;

        private Sound sound;

        private ParticleEmitterPrefab particleEmitterPrefab;

        public readonly float Stun;

        private float priority;

        public float GetDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? damage : damage * deltaTime;
        }

        public float GetBleedingDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? bleedingDamage : bleedingDamage * deltaTime;
        }

        public float GetStructureDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? structureDamage : structureDamage * deltaTime;
        }


        //public Attack(AttackType type, float range,)
        //{

        //}

        public Attack(XElement element)
        {
            try
            {
                Type = (AttackType)Enum.Parse(typeof(AttackType), element.Attribute("type").Value, true);
            }
            catch
            {
                Type = AttackType.None;
            }

            try
            {
                DamageType = (DamageType)Enum.Parse(typeof(DamageType), ToolBox.GetAttributeString(element, "damagetype", "None"), true);
            }
            catch
            {
                DamageType = DamageType.None;
            }


            damage = ToolBox.GetAttributeFloat(element, "damage", 0.0f);
            structureDamage = ToolBox.GetAttributeFloat(element, "structuredamage", 0.0f);
            bleedingDamage = ToolBox.GetAttributeFloat(element, "bleedingdamage", 0.0f);

            Stun = ToolBox.GetAttributeFloat(element, "stun", 0.0f);

            string soundPath = ToolBox.GetAttributeString(element, "sound", "");
            if (!string.IsNullOrWhiteSpace(soundPath))
            {
                sound = Sound.Load(soundPath);
            }
                      
            Range = FarseerPhysics.ConvertUnits.ToSimUnits(ToolBox.GetAttributeFloat(element, "range", 0.0f));

            Duration = ToolBox.GetAttributeFloat(element, "duration", 0.0f); 

            priority = ToolBox.GetAttributeFloat(element, "priority", 1.0f);

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString().ToLower() == "particleemitter") particleEmitterPrefab = new ParticleEmitterPrefab(subElement);
            }
        }


        public AttackResult DoDamage(IDamageable attacker, IDamageable target, Vector2 position, float deltaTime, bool playSound = true)
        {
            float damageAmount = 0.0f;
            //DamageSoundType damageSoundType = DamageSoundType.None;

            if (target as Character == null)
            {
                damageAmount = structureDamage;

            }
            else
            {
                damageAmount = damage;
            }

            if (particleEmitterPrefab != null)
            {
                particleEmitterPrefab.Emit(position);
            }

            if (sound != null)
            {
                sound.Play(1.0f, 500.0f, position);
            }

            return target.AddDamage(attacker, position, this, deltaTime, playSound);

        }
    }
}
