﻿using Barotrauma.Networking;
using Barotrauma.Extensions;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    public enum Gender { None, Male, Female };
    
    partial class CharacterInfo
    {
        private static Dictionary<string, XDocument> cachedConfigs = new Dictionary<string, XDocument>();

        private static ushort idCounter;

        public string Name;
        public string DisplayName
        {
            get
            {
                string disguiseName = "?";
                if (Character == null || !Character.HideFace || (GameMain.Server != null && !GameMain.Server.AllowDisguises))
                {
                    return Name;
                }
#if CLIENT
                if (GameMain.Client != null && !GameMain.Client.AllowDisguises)
                {
                    return Name;
                }
#endif
                if (Character.Inventory != null)
                {
                    int cardSlotIndex = Character.Inventory.FindLimbSlot(InvSlotType.Card);
                    if (cardSlotIndex < 0) return disguiseName;

                    var idCard = Character.Inventory.Items[cardSlotIndex];
                    if (idCard == null) return disguiseName;

                    //Disguise as the ID card name if it's equipped                    
                    string[] readTags = idCard.Tags.Split(',');
                    foreach (string tag in readTags)
                    {
                        string[] s = tag.Split(':');
                        if (s[0] == "name")
                        {
                            return s[1];
                        }
                    }
                }
                return disguiseName;
            }
        }

        /// <summary>
        /// Note: Can be null.
        /// </summary>
        public Character Character;

        public readonly string File;
        
        public Job Job;

        private List<ushort> pickedItems;

        public ushort? HullID = null;

        private Gender gender;

        public int Salary;

        private Vector2[] headSpriteRange;

        private int headSpriteId;
        private Sprite headSprite;
        public Sprite Portrait { get; private set; }
        public Sprite PortraitBackground { get; private set; }


        public XElement SourceElement { get; private set; }

        public XElement HairElement { get; private set; }
        public XElement BeardElement { get; private set; }
        public XElement MoustacheElement { get; private set; }
        public XElement FaceAttachment { get; private set; }

        public int HairIndex { get; set; } = -1;
        public int BeardIndex { get; set; } = -1;
        public int MoustacheIndex { get; set; } = -1;
        public int FaceAttachmentIndex { get; set; } = -1;

        public bool StartItemsGiven;

        public CauseOfDeath CauseOfDeath;

        public byte TeamID;

        private NPCPersonalityTrait personalityTrait;

        //unique ID given to character infos in MP
        //used by clients to identify which infos are the same to prevent duplicate characters in round summary
        public ushort ID;

        public List<ushort> PickedItemIDs
        {
            get { return pickedItems; }
        }
        
        public Sprite HeadSprite
        {
            get
            {
                if (headSprite == null) LoadHeadSprite();
                return headSprite;
            }
        }

        public List<string> SpriteTags
        {
            get;
            private set;
        }

        public NPCPersonalityTrait PersonalityTrait
        {
            get { return personalityTrait; }
        }

        public int HeadSpriteId
        {
            get { return headSpriteId; }
            set
            {
                int oldId = headSpriteId;

                headSpriteId = value;
                Vector2 spriteRange = headSpriteRange[gender == Gender.Male || gender == Gender.None ? 0 : 1];
                
                if (headSpriteId < (int)spriteRange.X) headSpriteId = (int)(spriteRange.Y);
                if (headSpriteId > (int)spriteRange.Y) headSpriteId = (int)(spriteRange.X);

                if (headSpriteId != oldId) headSprite = null;
            }
        }

        public Gender Gender
        {
            get { return gender; }
            set
            {
                if (gender == value) return;
                gender = value;

                int genderIndex = (this.gender == Gender.Female) ? 1 : 0;
                if (headSpriteRange[genderIndex] != Vector2.Zero)
                {
                    HeadSpriteId = Rand.Range((int)headSpriteRange[genderIndex].X, (int)headSpriteRange[genderIndex].Y + 1);
                }
                else
                {
                    HeadSpriteId = 0;
                }

                LoadHeadSprite();
            }
        }

        private HumanRagdollParams ragdoll;
        public HumanRagdollParams Ragdoll
        {
            get
            {
                if (ragdoll == null)
                {
                    ragdoll = HumanRagdollParams.GetDefaultRagdollParams(Character?.SpeciesName ?? "human");
                }
                return ragdoll;
            }
            set { ragdoll = value; }
        }

        public CharacterInfo(string file, string name = "", Gender gender = Gender.None, JobPrefab jobPrefab = null, HumanRagdollParams ragdoll = null)
        {
            ID = idCounter;
            idCounter++;

            this.File = file;

            headSpriteRange = new Vector2[2];
            pickedItems = new List<ushort>();
            SpriteTags = new List<string>();

            XDocument doc = null;
            if (cachedConfigs.ContainsKey(file))
            {
                doc = cachedConfigs[file];
            }
            else
            {
                doc = XMLExtensions.TryLoadXml(file);
                if (doc == null) return;

                cachedConfigs.Add(file, doc);
            }

            SourceElement = doc.Root;

            if (doc.Root.GetAttributeBool("genders", false))
            {
                if (gender == Gender.None)
                {
                    float femaleRatio = doc.Root.GetAttributeFloat("femaleratio", 0.5f);
                    this.gender = (Rand.Range(0.0f, 1.0f, Rand.RandSync.Server) < femaleRatio) ? Gender.Female : Gender.Male;
                }
                else
                {
                    this.gender = gender;
                }
            }
                       
            headSpriteRange[0] = doc.Root.GetAttributeVector2("headid", Vector2.Zero);
            headSpriteRange[1] = headSpriteRange[0];
            if (headSpriteRange[0] == Vector2.Zero)
            {
                headSpriteRange[0] = doc.Root.GetAttributeVector2("maleheadid", Vector2.Zero);
                headSpriteRange[1] = doc.Root.GetAttributeVector2("femaleheadid", Vector2.Zero);
            }

            int genderIndex = (this.gender == Gender.Female) ? 1 : 0;
            if (headSpriteRange[genderIndex] != Vector2.Zero)
            {
                HeadSpriteId = Rand.Range((int)headSpriteRange[genderIndex].X, (int)headSpriteRange[genderIndex].Y + 1, Rand.RandSync.Server);
            }

            this.Job = (jobPrefab == null) ? Job.Random(Rand.RandSync.Server) : new Job(jobPrefab);

            if (!string.IsNullOrEmpty(name))
            {
                this.Name = name;
                return;
            }

            name = "";

            if (doc.Root.Element("name") != null)
            {
                string firstNamePath = doc.Root.Element("name").GetAttributeString("firstname", "");
                if (firstNamePath != "")
                {
                    firstNamePath = firstNamePath.Replace("[GENDER]", (this.gender == Gender.Female) ? "f" : "");
                    this.Name = ToolBox.GetRandomLine(firstNamePath);
                }

                string lastNamePath = doc.Root.Element("name").GetAttributeString("lastname", "");
                if (lastNamePath != "")
                {
                    lastNamePath = lastNamePath.Replace("[GENDER]", (this.gender == Gender.Female) ? "f" : "");
                    if (this.Name != "") this.Name += " ";
                    this.Name += ToolBox.GetRandomLine(lastNamePath);
                }
            }

            personalityTrait = NPCPersonalityTrait.GetRandom(name + HeadSpriteId);
            
            Salary = CalculateSalary();
            if (ragdoll != null)
            {
                this.ragdoll = ragdoll;
            }

            var portraitBackgroundElement = doc.Root.Element("portraitbackground");
            if (portraitBackgroundElement != null)
            {
                PortraitBackground = new Sprite(portraitBackgroundElement.Element("sprite"));
            }

            LoadHeadAttachments();
        }

        public CharacterInfo(XElement element)
        {
            ID = idCounter;
            idCounter++;

            SourceElement = element;

            Name = element.GetAttributeString("name", "unnamed");

            string genderStr = element.GetAttributeString("gender", "male").ToLowerInvariant();
            gender = (genderStr == "m") ? Gender.Male : Gender.Female;

            File = element.GetAttributeString("file", "");
            Salary = element.GetAttributeInt("salary", 1000);
            headSpriteId = element.GetAttributeInt("headspriteid", 1);
            StartItemsGiven = element.GetAttributeBool("startitemsgiven", false);

            string personalityName = element.GetAttributeString("personality", "");
            if (!string.IsNullOrEmpty(personalityName))
            {
                personalityTrait = NPCPersonalityTrait.List.Find(p => p.Name == personalityName);
            }

            int hullId = element.GetAttributeInt("hull", -1);
            if (hullId > 0 && hullId <= ushort.MaxValue) this.HullID = (ushort)hullId;

            pickedItems = new List<ushort>();

            string pickedItemString = element.GetAttributeString("items", "");
            if (!string.IsNullOrEmpty(pickedItemString))
            {
                string[] itemIds = pickedItemString.Split(',');
                foreach (string s in itemIds)
                {
                    pickedItems.Add((ushort)int.Parse(s));
                }
            }

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString().ToLowerInvariant() != "job") continue;

                Job = new Job(subElement);
                break;
            }

            string ragdollFile = element.GetAttributeString("ragdoll", string.Empty);
            ragdoll = HumanRagdollParams.GetRagdollParams("human", ragdollFile);

            var portraitBackgroundElement = element.Element("portraitbackground");
            if (portraitBackgroundElement != null)
            {
                PortraitBackground = new Sprite(portraitBackgroundElement.Element("sprite"));
            }

            LoadHeadAttachments();
        }

        public void LoadHeadSprite()
        {
            foreach (XElement limbElement in XMLExtensions.TryLoadXml(Ragdoll.FullPath).Root.Elements())
            {
                if (limbElement.GetAttributeString("type", "").ToLowerInvariant() != "head") continue;

                XElement spriteElement = limbElement.Element("sprite");

                string spritePath = spriteElement.Attribute("texture").Value;

                spritePath = spritePath.Replace("[GENDER]", (this.gender == Gender.Female) ? "f" : "");
                spritePath = spritePath.Replace("[HEADID]", HeadSpriteId.ToString());
                
                string fileName = Path.GetFileNameWithoutExtension(spritePath);

                //go through the files in the directory to find a matching sprite
                var files = Directory.GetFiles(Path.GetDirectoryName(spritePath)).ToList();
                foreach (string file in files)
                {
                    string fileWithoutTags = Path.GetFileNameWithoutExtension(file);
                    fileWithoutTags = fileWithoutTags.Split('[', ']').First();
                    if (fileWithoutTags != fileName) continue;

                    headSprite = new Sprite(spriteElement, "", file);
                    Portrait = new Sprite(spriteElement, "", file) { RelativeOrigin = Vector2.Zero };

                    //extract the tags out of the filename
                    SpriteTags = file.Split('[', ']').Skip(1).ToList();
                    if (SpriteTags.Any())
                    {
                        SpriteTags.RemoveAt(SpriteTags.Count-1);
                    }

                    break;                    
                }

                break;
            }
        }

        private List<XElement> hairs;
        private List<XElement> beards;
        private List<XElement> moustaches;
        private List<XElement> faceAttachments;
        public void LoadHeadAttachments()
        {
            var attachments = SourceElement.Element("HeadAttachments");
            if (attachments != null)
            {
                var wearables = attachments.Elements("Wearable");
                if (hairs == null)
                {
                    hairs = AddEmpty(FilterByType(wearables, WearableType.Hair));
                }
                if (beards == null)
                {
                    beards = AddEmpty(FilterByType(wearables, WearableType.Beard));
                }
                if (moustaches == null)
                {
                    moustaches = AddEmpty(FilterByType(wearables, WearableType.Moustache));
                }
                if (faceAttachments == null)
                {
                    faceAttachments = AddEmpty(FilterByType(wearables, WearableType.FaceAttachment));
                }

                if (IsValidIndex(HairIndex, hairs))
                {
                    HairElement = hairs[HairIndex];
                }
                else
                {
                    HairElement = GetRandomElement(hairs);
                    HairIndex = hairs.IndexOf(HairElement);
                }
                if (IsValidIndex(BeardIndex, beards))
                {
                    BeardElement = beards[BeardIndex];
                }
                else
                {
                    BeardElement = GetRandomElement(beards);
                    BeardIndex = beards.IndexOf(BeardElement);
                }
                if (IsValidIndex(MoustacheIndex, moustaches))
                {
                    MoustacheElement = moustaches[MoustacheIndex];
                }
                else
                {
                    MoustacheElement = GetRandomElement(moustaches);
                    MoustacheIndex = moustaches.IndexOf(MoustacheElement);
                }
                if (IsValidIndex(FaceAttachmentIndex, faceAttachments))
                {
                    FaceAttachment = faceAttachments[FaceAttachmentIndex];
                }
                else
                {
                    FaceAttachment = GetRandomElement(faceAttachments);
                    FaceAttachmentIndex = faceAttachments.IndexOf(FaceAttachment);
                }

                bool IsValidIndex(int index, List<XElement> list) => index >= 0 && index < list.Count - 1;

                IEnumerable<float> GetWeights(IEnumerable<XElement> elements) => elements.Select(h => h.GetAttributeFloat("commonness", 1f));

                List<XElement> AddEmpty(IEnumerable<XElement> elements)
                {
                    // Let's add an empty element so that there's a chance that we don't get any actual element -> allows bald and beardless guys, for example.
                    var emptyElement = new XElement("Empty");
                    var list = new List<XElement>() { emptyElement };
                    list.AddRange(elements);
                    return list;
                }

                XElement GetRandomElement(IEnumerable<XElement> elements)
                {
                    var filtered = elements.Where(e => IsWearableAllowed(e));
                    if (filtered.Count() == 0) { return null; }
                    var element = ToolBox.SelectWeightedRandom(filtered.ToList(), GetWeights(filtered).ToList(), Rand.RandSync.Server);
                    return element == null || element.Name == "Empty" ? null : element;
                }

                IEnumerable<XElement> FilterByType(IEnumerable<XElement> elements, WearableType targetType) 
                    => elements.Where(e => Enum.TryParse(e.GetAttributeString("type", ""), true, out WearableType type) && type == targetType);

                bool IsWearableAllowed(XElement element)
                {
                    var spriteElement = element.Element("sprite");
                    var p = spriteElement.GetAttributeString("texture", string.Empty);
                    if (!System.IO.File.Exists(p) || Path.GetFullPath(p) != Path.GetFullPath(HeadSprite.FilePath)) { return false; }
                    string spriteName = spriteElement.GetAttributeString("name", string.Empty);
                    return IsAllowed(HairElement, spriteName) && IsAllowed(BeardElement, spriteName) && IsAllowed(MoustacheElement, spriteName) && IsAllowed(FaceAttachment, spriteName);
                }

                bool IsAllowed(XElement element, string spriteName)
                {
                    if (element != null)
                    {
                        var disallowed = element.GetAttributeStringArray("disallow", new string[0]);
                        if (disallowed.Any(s => spriteName.Contains(s)))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
        }

        public void UpdateCharacterItems()
        {
            pickedItems.Clear();
            foreach (Item item in Character.Inventory.Items)
            {
                pickedItems.Add(item == null ? (ushort)0 : item.ID);
            }
        }
        
        private int CalculateSalary()
        {
            if (Name == null || Job == null) return 0;

            int salary = Math.Abs(Name.GetHashCode()) % 100;

            foreach (Skill skill in Job.Skills)
            {
                salary += (int)skill.Level * 10;
            }

            return salary;
        }

        public void IncreaseSkillLevel(string skillIdentifier, float increase, Vector2 worldPos)
        {
            if (Job == null || GameMain.Client != null) return;            

            float prevLevel = Job.GetSkillLevel(skillIdentifier);
            Job.IncreaseSkillLevel(skillIdentifier, increase);

            float newLevel = Job.GetSkillLevel(skillIdentifier);

            OnSkillChanged(skillIdentifier, prevLevel, newLevel, worldPos);

            if (GameMain.Server != null && (int)newLevel != (int)prevLevel)
            {
                GameMain.Server.CreateEntityEvent(Character, new object[] { NetEntityEvent.Type.UpdateSkills });                
            }
        }

        public void SetSkillLevel(string skillIdentifier, float level, Vector2 worldPos)
        {
            if (Job == null) return;

            var skill = Job.Skills.Find(s => s.Identifier == skillIdentifier);
            if (skill == null)
            {
                Job.Skills.Add(new Skill(skillIdentifier, level));
                OnSkillChanged(skillIdentifier, 0.0f, skill.Level, worldPos);
            }
            else
            {
                float prevLevel = skill.Level;
                skill.Level = level;
                OnSkillChanged(skillIdentifier, prevLevel, skill.Level, worldPos);
            }
        }

        partial void OnSkillChanged(string skillIdentifier, float prevLevel, float newLevel, Vector2 textPopupPos);

        public virtual XElement Save(XElement parentElement)
        {
            XElement charElement = new XElement("Character");

            charElement.Add(
                new XAttribute("name", Name),
                new XAttribute("file", File),
                new XAttribute("gender", gender == Gender.Male ? "m" : "f"),
                new XAttribute("salary", Salary),
                new XAttribute("headspriteid", HeadSpriteId),
                new XAttribute("hairindex", HairIndex),
                new XAttribute("beardindex", BeardIndex),
                new XAttribute("moustacheindex", MoustacheIndex),
                new XAttribute("faceattachmentindex", FaceAttachmentIndex),
                new XAttribute("startitemsgiven", StartItemsGiven),
                new XAttribute("ragdoll", Ragdoll.FileName),
                new XAttribute("personality", personalityTrait == null ? "" : personalityTrait.Name));
            
            // TODO: animations?

            if (Character != null)
            {
                if (Character.Inventory != null)
                {
                    UpdateCharacterItems();
                }

                if (Character.AnimController.CurrentHull != null)
                {
                    HullID = Character.AnimController.CurrentHull.ID;
                    charElement.Add(new XAttribute("hull", Character.AnimController.CurrentHull.ID));
                }
            }
            
            if (pickedItems.Count > 0)
            {
                charElement.Add(new XAttribute("items", string.Join(",", pickedItems)));
            }

            Job.Save(charElement);

            parentElement.Add(charElement);
            return charElement;
        }

        public void ServerWrite(NetBuffer msg)
        {
            msg.Write(ID);
            msg.Write(Name);
            msg.Write(Gender == Gender.Female);
            msg.Write((byte)HeadSpriteId);
            msg.Write((byte)HairIndex);
            msg.Write((byte)BeardIndex);
            msg.Write((byte)MoustacheIndex);
            msg.Write((byte)FaceAttachmentIndex);
            msg.Write(Ragdoll.FileName);
            if (Job != null)
            {
                msg.Write(Job.Prefab.Identifier);
                msg.Write((byte)Job.Skills.Count);
                foreach (Skill skill in Job.Skills)
                {
                    msg.Write(skill.Identifier);
                    msg.Write(skill.Level);
                }
            }
            else
            {
                msg.Write("");
            }
            // TODO: animations
        }

        public static CharacterInfo ClientRead(string configPath, NetBuffer inc)
        {
            ushort infoID               = inc.ReadUInt16();
            string newName              = inc.ReadString();
            bool isFemale               = inc.ReadBoolean();
            int headSpriteID            = inc.ReadByte();
            int hairIndex               = inc.ReadByte();
            int beardIndex              = inc.ReadByte();
            int moustacheIndex          = inc.ReadByte();
            int faceAttachmentIndex     = inc.ReadByte();
            string ragdoll              = inc.ReadString();

            string jobIdentifier        = inc.ReadString();
            JobPrefab jobPrefab = null;
            Dictionary<string, float> skillLevels = new Dictionary<string, float>();
            if (!string.IsNullOrEmpty(jobIdentifier))
            {
                jobPrefab = JobPrefab.List.Find(jp => jp.Identifier == jobIdentifier);
                int skillCount = inc.ReadByte();
                for (int i = 0; i < skillCount; i++)
                {
                    string skillIdentifier = inc.ReadString();
                    float skillLevel = inc.ReadSingle();
                    skillLevels.Add(skillIdentifier, skillLevel);
                }
            }

            // TODO: animations

            CharacterInfo ch = new CharacterInfo(configPath, newName, isFemale ? Gender.Female : Gender.Male, jobPrefab, HumanRagdollParams.GetRagdollParams("human", ragdoll))
            {
                ID = infoID,
                HeadSpriteId = headSpriteID,
                HairIndex = hairIndex,
                BeardIndex = beardIndex,
                MoustacheIndex = moustacheIndex,
                FaceAttachmentIndex = faceAttachmentIndex
            };
            // Need to reload the attachments, because the indices were changed.
            ch.LoadHeadAttachments();

            System.Diagnostics.Debug.Assert(skillLevels.Count == ch.Job.Skills.Count);
            if (ch.Job != null)
            {
                foreach (KeyValuePair<string, float> skill in skillLevels)
                {
                    Skill matchingSkill = ch.Job.Skills.Find(s => s.Identifier == skill.Key);
                    if (matchingSkill == null)
                    {
                        DebugConsole.ThrowError("Skill \"" + skill.Key + "\" not found in character \"" + newName + "\"");
                        continue;
                    }
                    matchingSkill.Level = skill.Value;
                }
            }
            return ch;
        }

        public void Remove()
        {
            Character = null;
            if (headSprite != null)
            {
                headSprite.Remove();
                headSprite = null;
            }
            if (Portrait != null)
            {
                Portrait.Remove();
                Portrait = null;
            }
            if (PortraitBackground != null)
            {
                PortraitBackground.Remove();
                PortraitBackground = null;
            }
        }
    }
}
