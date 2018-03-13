﻿using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class FixRequirement
    {
        public static float SkillIncreaseMultiplier = 1.0f;

        private string name;

        private readonly List<Skill> RequiredSkills;
        private readonly List<string> requiredItems;

        public bool Fixed;

        public List<string> RequiredItems
        {
            get { return requiredItems; }
        }

        public FixRequirement(XElement element)
        {
            name = element.GetAttributeString("name", "");

            RequiredSkills = new List<Skill>();
            requiredItems = new List<string>();

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "skill":
                        string skillName = subElement.GetAttributeString("name", "");
                        int level = subElement.GetAttributeInt("level", 1);

                        RequiredSkills.Add(new Skill(skillName, level));
                        break;
                    case "item":
                        string itemName = subElement.GetAttributeString("name", "");

                        requiredItems.Add(itemName);
                        break;
                }
            }
        }

        public bool HasRequiredSkills(Character character)
        {
            foreach (Skill skill in RequiredSkills)
            {
                if (character.GetSkillLevel(skill.Name) < skill.Level) return false;
            }
            return true;
        }

        public bool HasRequiredItems(Character character)
        {
            foreach (string itemName in requiredItems)
            {
                if (character.Inventory.FindItem(itemName) == null) return false;
            }
            return true;
        }

        public bool CanBeFixed(Character character)
        {
            return character != null && HasRequiredSkills(character) && HasRequiredItems(character);
        }
    }
}
