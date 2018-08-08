﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;
using FarseerPhysics;

namespace Barotrauma
{
    class CharacterEditorScreen : Screen
    {
        private Camera cam;
        public override Camera Cam
        {
            get
            {
                if (cam == null)
                {
                    cam = new Camera();
                }
                return cam;
            }
        }

        private Character character;
        private Vector2 spawnPosition;
        private bool showAnimControls;
        private bool editSpriteDimensions;
        private bool editRagdoll;
        private bool editJointPositions;
        private bool editJointLimits;
        private bool showParamsEditor;
        private bool showSpritesheet;
        private bool freeze;
        private bool autoFreeze = true;

        public override void Select()
        {
            base.Select();
            Submarine.RefreshSavedSubs();
            Submarine.MainSub = Submarine.SavedSubmarines.First(s => s.Name.Contains("AnimEditor"));
            Submarine.MainSub.Load(true);
            Submarine.MainSub.GodMode = true;
            originalWall = new WallGroup(new List<Structure>(Structure.WallList));
            CloneWalls();
            CalculateMovementLimits();
            currentCharacterConfig = Character.HumanConfigFile;
            SpawnCharacter(currentCharacterConfig);
            GameMain.Instance.OnResolutionChanged += OnResolutionChanged;
        }

        public override void Deselect()
        {
            base.Deselect();
            GameMain.Instance.OnResolutionChanged -= OnResolutionChanged;
        }

        private void OnResolutionChanged()
        {
            CreateGUI();
        }

        #region Inifinite runner
        private int min;
        private int max;
        private void CalculateMovementLimits()
        {
            min = CurrentWall.walls.Select(w => w.Rect.Left).OrderBy(p => p).First();
            max = CurrentWall.walls.Select(w => w.Rect.Right).OrderBy(p => p).Last();
        }

        private WallGroup originalWall;
        private WallGroup[] clones = new WallGroup[3];
        private IEnumerable<Structure> AllWalls => originalWall.walls.Concat(clones.SelectMany(c => c.walls));

        private WallGroup _currentWall;
        private WallGroup CurrentWall
        {
            get
            {
                if (_currentWall == null)
                {
                    _currentWall = originalWall;
                }
                return _currentWall;
            }
            set
            {
                _currentWall = value;
            }
        }

        private class WallGroup
        {
            public readonly List<Structure> walls;
            
            public WallGroup(List<Structure> walls)
            {
                this.walls = walls;
            }

            public WallGroup Clone()
            {
                var clones = new List<Structure>();
                walls.ForEachMod(w => clones.Add(w.Clone() as Structure));
                return new WallGroup(clones);
            }     
        }

        private void CloneWalls()
        {
            for (int i = 0; i < 3; i++)
            {
                clones[i] = originalWall.Clone();
                for (int j = 0; j < originalWall.walls.Count; j++)
                {
                    if (i == 1)
                    {
                        clones[i].walls[j].Move(new Vector2(originalWall.walls[j].Rect.Width, 0));
                    }
                    else if (i == 2)
                    {
                        clones[i].walls[j].Move(new Vector2(-originalWall.walls[j].Rect.Width, 0));
                    }      
                }
            }
        }

        private WallGroup SelectClosestWallGroup(Vector2 pos)
        {
            var closestWall = clones.SelectMany(c => c.walls).OrderBy(w => Vector2.Distance(pos, w.Position)).First();
            return clones.Where(c => c.walls.Contains(closestWall)).FirstOrDefault();
        }

        private WallGroup SelectLastClone(bool right)
        {
            var lastWall = right 
                ? clones.SelectMany(c => c.walls).OrderBy(w => w.Rect.Right).Last() 
                : clones.SelectMany(c => c.walls).OrderBy(w => w.Rect.Left).First();
            return clones.Where(c => c.walls.Contains(lastWall)).FirstOrDefault();
        }

        private void UpdateWalls(bool right)
        {
            CurrentWall = SelectClosestWallGroup(character.Position);
            CalculateMovementLimits();
            var lastClone = SelectLastClone(!right);
            for (int i = 0; i < lastClone.walls.Count; i++)
            {
                var amount = right ? lastClone.walls[i].Rect.Width : -lastClone.walls[i].Rect.Width;
                var distance = CurrentWall.walls[i].Position.X - lastClone.walls[i].Position.X;
                lastClone.walls[i].Move(new Vector2(amount + distance, 0));
            }
            GameMain.World.ProcessChanges();
        }

        private void SetWallCollisions(bool enabled)
        {
            var collisionCategory = enabled ? FarseerPhysics.Dynamics.Category.Cat1 : FarseerPhysics.Dynamics.Category.None;
            AllWalls.ForEach(w => w.SetCollisionCategory(collisionCategory));
            GameMain.World.ProcessChanges();
        }
        #endregion

        #region Character spawning
        private int characterIndex = -1;
        private string currentCharacterConfig;
        private List<string> allFiles;
        private List<string> AllFiles
        {
            get
            {
                if (allFiles == null)
                {
                    allFiles = GameMain.Instance.GetFilesOfType(ContentType.Character).Where(f => !f.Contains("husk")).ToList();
                    allFiles.ForEach(f => DebugConsole.NewMessage(f, Color.White));
                }
                return allFiles;
            }
        }

        private string GetNextConfigFile()
        {
            CheckAndGetIndex();
            IncreaseIndex();
            currentCharacterConfig = AllFiles[characterIndex];
            return currentCharacterConfig;
        }

        private string GetPreviousConfigFile()
        {
            CheckAndGetIndex();
            ReduceIndex();
            currentCharacterConfig = AllFiles[characterIndex];
            return currentCharacterConfig;
        }

        // Check if the index is not set, in which case we'll get the index from the current species name.
        private void CheckAndGetIndex()
        {
            if (characterIndex == -1)
            {
                characterIndex = AllFiles.IndexOf(GetConfigFile(character.SpeciesName));
            }
        }

        private void IncreaseIndex()
        {
            characterIndex++;
            if (characterIndex > AllFiles.Count - 1)
            {
                characterIndex = 0;
            }
        }

        private void ReduceIndex()
        {
            characterIndex--;
            if (characterIndex < 0)
            {
                characterIndex = AllFiles.Count - 1;
            }
        }

        private string GetConfigFile(string speciesName)
        {
            return AllFiles.Find(c => c.EndsWith(speciesName + ".xml"));
        }

        private Character SpawnCharacter(string configFile, RagdollParams ragdoll = null)
        {
            DebugConsole.NewMessage($"Trying to spawn {configFile}", Color.HotPink);
            spawnPosition = WayPoint.GetRandom(sub: Submarine.MainSub).WorldPosition;
            var character = Character.Create(configFile, spawnPosition, ToolBox.RandomSeed(8), hasAi: false, ragdoll: ragdoll);
            character.Submarine = Submarine.MainSub;
            character.AnimController.forceStanding = character.IsHumanoid;
            character.dontFollowCursor = true;
            Character.Controlled = character;
            float size = ConvertUnits.ToDisplayUnits(character.AnimController.Collider.radius * 2);
            float margin = 100;
            float distance = Vector2.Distance(spawnPosition, new Vector2(spawnPosition.X, originalWall.walls.First().WorldPosition.Y)) - margin;
            if (size > distance)
            {
                character.AnimController.Teleport(ConvertUnits.ToSimUnits(new Vector2(0, size * 1.5f)), Vector2.Zero);
            }
            SetWallCollisions(character.AnimController.forceStanding);
            this.character = character;
            CreateTextures();
            CreateGUI();
            widgets.Clear();
            ResetParamsEditor();
            return character;
        }

        private void TeleportTo(Vector2 position)
        {
            character.AnimController.SetPosition(ConvertUnits.ToSimUnits(position), false);
        }
        #endregion

        #region GUI
        private GUIFrame rightPanel;
        private GUIFrame centerPanel;
        private GUIFrame ragdollControls;
        private GUIFrame animationControls;
        private GUITickBox freezeToggle;
        private GUIScrollBar jointScaleBar;
        private GUIScrollBar limbScaleBar;

        private void CreateGUI()
        {
            CreateRightPanel();
            CreateCenterPanel();
        }

        private void CreateCenterPanel()
        {
            // Release the old panel
            if (centerPanel != null)
            {
                centerPanel.RectTransform.Parent = null;
            }
            Point sliderSize = new Point(120, 20);
            int textAreaHeight = 20;
            centerPanel = new GUIFrame(new RectTransform(new Vector2(0.45f, 0.95f), parent: Frame.RectTransform, anchor: Anchor.Center), style: null) { CanBeFocused = false };
            // Ragdoll
            ragdollControls = new GUIFrame(new RectTransform(Vector2.One, centerPanel.RectTransform), style: null) { CanBeFocused = false };
            var layoutGroupRagdoll = new GUILayoutGroup(new RectTransform(Vector2.One, ragdollControls.RectTransform)) { CanBeFocused = false };
            var jointScaleElement = new GUIFrame(new RectTransform(sliderSize + new Point(0, textAreaHeight), layoutGroupRagdoll.RectTransform), style: null);
            var jointScaleText = new GUITextBlock(new RectTransform(new Point(sliderSize.X, textAreaHeight), jointScaleElement.RectTransform), $"Joint Scale: {RagdollParams.JointScale.FormatAsDoubleDecimal()}", Color.Black, textAlignment: Alignment.Center);
            jointScaleBar = new GUIScrollBar(new RectTransform(sliderSize, jointScaleElement.RectTransform, Anchor.BottomLeft), barSize: 0.2f)
            {
                BarScroll = RagdollParams.JointScale / 2,
                MinValue = 0.25f,
                MaxValue = 1f,
                Step = 0.01f,
                OnMoved = (scrollBar, value) =>
                {
                    TryUpdateRagdollParam("jointscale", value * 2);
                    jointScaleText.Text = $"Joint Scale: {RagdollParams.JointScale.FormatAsDoubleDecimal()}";
                    character.AnimController.ResetJoints();
                    return true;
                }
            };
            var limbScaleElement = new GUIFrame(new RectTransform(sliderSize + new Point(0, textAreaHeight), layoutGroupRagdoll.RectTransform), style: null);
            var limbScaleText = new GUITextBlock(new RectTransform(new Point(sliderSize.X, textAreaHeight), limbScaleElement.RectTransform), $"Limb Scale: {RagdollParams.LimbScale.FormatAsDoubleDecimal()}", Color.Black, textAlignment: Alignment.Center);
            limbScaleBar = new GUIScrollBar(new RectTransform(sliderSize, limbScaleElement.RectTransform, Anchor.BottomLeft), barSize: 0.2f)
            {
                BarScroll = RagdollParams.LimbScale / 2,
                MinValue = 0.25f,
                MaxValue = 1f,
                Step = 0.01f,
                OnMoved = (scrollBar, value) =>
                {
                    TryUpdateRagdollParam("limbscale", value * 2);
                    limbScaleText.Text = $"Limb Scale: {RagdollParams.LimbScale.FormatAsDoubleDecimal()}";
                    return true;
                }
            };
            // Animation
            animationControls = new GUIFrame(new RectTransform(Vector2.One, centerPanel.RectTransform), style: null) { CanBeFocused = false };
            var layoutGroupAnimation = new GUILayoutGroup(new RectTransform(Vector2.One, animationControls.RectTransform)) { CanBeFocused = false };
        }

        private void CreateRightPanel()
        {
            // Release the old panel
            if (rightPanel != null)
            {
                rightPanel.RectTransform.Parent = null;
            }
            Vector2 buttonSize = new Vector2(1, 0.05f);
            Vector2 toggleSize = new Vector2(0.03f, 0.03f);
            Point margin = new Point(40, 60);
            rightPanel = new GUIFrame(new RectTransform(new Vector2(0.15f, 0.95f), parent: Frame.RectTransform, anchor: Anchor.CenterRight) { RelativeOffset = new Vector2(0.01f, 0) });
            var layoutGroup = new GUILayoutGroup(new RectTransform(new Point(rightPanel.Rect.Width - margin.X, rightPanel.Rect.Height - margin.Y), rightPanel.RectTransform, Anchor.Center));
            var charButtons = new GUIFrame(new RectTransform(buttonSize, parent: layoutGroup.RectTransform), style: null);
            var prevCharacterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1), charButtons.RectTransform, Anchor.TopLeft), "Previous \nCharacter");
            prevCharacterButton.OnClicked += (b, obj) =>
            {
                SpawnCharacter(GetPreviousConfigFile());
                return true;
            };
            var nextCharacterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1), charButtons.RectTransform, Anchor.TopRight), "Next \nCharacter");
            nextCharacterButton.OnClicked += (b, obj) =>
            {
                SpawnCharacter(GetNextConfigFile());
                return true;
            };
            var animControlsToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Show Animation Controls") { Selected = showAnimControls };
            var paramsToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Show Parameters") { Selected = showParamsEditor };
            var spriteDimensionsToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Edit Sprite Dimensions") { Selected = editSpriteDimensions };
            var ragdollToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Edit Ragdoll") { Selected = editRagdoll };
            var jointPositionsToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Edit Joint Positions") { Selected = editJointPositions };
            var jointLimitsToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Edit Joints Limits") { Selected = editJointLimits };
            var spritesheetToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Show Spritesheet") { Selected = showSpritesheet };
            freezeToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Freeze") { Selected = freeze };
            var autoFreezeToggle = new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Auto Freeze") { Selected = autoFreeze };
            animControlsToggle.OnSelected = box =>
            {
                showAnimControls = box.Selected;
                if (showAnimControls)
                {
                    spritesheetToggle.Selected = false;
                    spriteDimensionsToggle.Selected = false;
                    ragdollToggle.Selected = false;
                    ResetParamsEditor();
                }
                return true;
            };
            paramsToggle.OnSelected = box =>
            {
                showParamsEditor = box.Selected;
                if (showParamsEditor)
                {
                    spritesheetToggle.Selected = false;
                }
                return true;
            };
            spriteDimensionsToggle.OnSelected = box =>
            {
                editSpriteDimensions = box.Selected;
                if (editSpriteDimensions)
                {
                    ragdollToggle.Selected = false;
                    animControlsToggle.Selected = false;
                    spritesheetToggle.Selected = true;
                    ResetParamsEditor();
                }
                return true;
            };
            ragdollToggle.OnSelected = box =>
            {
                editRagdoll = box.Selected;
                if (editRagdoll)
                {
                    spriteDimensionsToggle.Selected = false;
                    animControlsToggle.Selected = false;
                    ResetParamsEditor();
                }
                else
                {
                    jointPositionsToggle.Selected = false;
                    jointLimitsToggle.Selected = false;
                }
                return true;
            };
            jointPositionsToggle.OnSelected = box =>
            {
                editJointPositions = box.Selected;
                if (editJointPositions)
                {
                    ragdollToggle.Selected = true;
                    spritesheetToggle.Selected = !paramsToggle.Selected;
                    jointLimitsToggle.Selected = false;
                }
                return true;
            };
            jointLimitsToggle.OnSelected = box =>
            {
                editJointLimits = box.Selected;
                if (editJointLimits)
                {
                    ragdollToggle.Selected = true;
                    spritesheetToggle.Selected = !paramsToggle.Selected;
                    jointPositionsToggle.Selected = false;
                }
                return true;
            };
            spritesheetToggle.OnSelected = box =>
            {
                showSpritesheet = box.Selected;
                if (showSpritesheet)
                {
                    animControlsToggle.Selected = false;
                    paramsToggle.Selected = false;
                }
                return true;
            };
            freezeToggle.OnSelected = box =>
            {
                freeze = box.Selected;
                return true;
            };
            autoFreezeToggle.OnSelected = box =>
            {
                autoFreeze = box.Selected;
                return true;
            };
            new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Auto Move")
            {
                OnSelected = (GUITickBox box) =>
                {
                    character.OverrideMovement = box.Selected ? new Vector2(-1, 0) as Vector2? : null;
                    return true;
                }
            };
            new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Force Fast Speed")
            {
                OnSelected = (GUITickBox box) =>
                {
                    character.ForceRun = box.Selected;
                    return true;
                }
            };
            new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Follow Cursor")
            {
                OnSelected = (GUITickBox box) =>
                {
                    character.dontFollowCursor = !box.Selected;
                    return true;
                }
            };
            new GUITickBox(new RectTransform(toggleSize, layoutGroup.RectTransform), "Swim")
            {
                Enabled = character.AnimController.CanWalk,
                Selected = character.AnimController is FishAnimController,
                OnSelected = (GUITickBox box) =>
                {
                    character.AnimController.forceStanding = !box.Selected;
                    SetWallCollisions(character.AnimController.forceStanding);
                    if (character.AnimController.forceStanding)
                    {
                        TeleportTo(spawnPosition);
                    }
                    return true;
                }
            };
            var quickSaveAnimButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Quick Save Animation");
            quickSaveAnimButton.OnClicked += (button, userData) =>
            {
                AnimParams.ForEach(p => p.Save());
                return true;
            };
            var quickSaveRagdollButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Quick Save Ragdoll");
            quickSaveRagdollButton.OnClicked += (button, userData) =>
            {
                character.AnimController.SaveRagdoll();
                return true;
            };
            var resetAnimButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Reset Animation");
            resetAnimButton.OnClicked += (button, userData) =>
            {
                AnimParams.ForEach(p => p.Reset());
                ResetParamsEditor();
                return true;
            };
            var resetRagdollButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Reset Ragdoll");
            resetRagdollButton.OnClicked += (button, userData) =>
            {
                character.AnimController.ResetRagdoll();
                CreateCenterPanel();
                ResetParamsEditor();
                widgets.Values.ForEach(w => w.refresh?.Invoke());
                return true;
            };
            int messageBoxWidth = GameMain.GraphicsWidth / 2;
            int messageBoxHeight = GameMain.GraphicsHeight / 2;
            var saveRagdollButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Save Ragdoll");
            saveRagdollButton.OnClicked += (button, userData) =>
            {
                var box = new GUIMessageBox("Save Ragdoll", "Please provide a name for the file:", new string[] { "Cancel", "Save" }, messageBoxWidth, messageBoxHeight);
                var inputField = new GUITextBox(new RectTransform(new Point(box.Content.Rect.Width, 30), box.Content.RectTransform, Anchor.Center), RagdollParams.Name);
                box.Buttons[0].OnClicked += (b, d) =>
                {
                    box.Close();
                    return true;
                };
                box.Buttons[1].OnClicked += (b, d) =>
                {
                    character.AnimController.SaveRagdoll(inputField.Text);
                    ResetParamsEditor();
                    box.Close();
                    return true;
                };
                return true;
            };
            var loadRagdollButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Load Ragdoll");
            loadRagdollButton.OnClicked += (button, userData) =>
            {
                var loadBox = new GUIMessageBox("Load Ragdoll", "", new string[] { "Cancel", "Load", "Delete" }, messageBoxWidth, messageBoxHeight);
                loadBox.Buttons[0].OnClicked += loadBox.Close;
                var listBox = new GUIListBox(new RectTransform(new Vector2(0.9f, 0.6f), loadBox.Content.RectTransform, Anchor.TopCenter));
                var deleteButton = loadBox.Buttons[2];
                deleteButton.Enabled = false;
                void PopulateListBox()
                {
                    try
                    {
                        var filePaths = Directory.GetFiles(RagdollParams.Folder);
                        foreach (var path in filePaths)
                        {
                            GUITextBlock textBlock = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), listBox.Content.RectTransform) { MinSize = new Point(0, 30) },
                                ToolBox.LimitString(Path.GetFileNameWithoutExtension(path), GUI.Font, listBox.Rect.Width - 80))
                            {
                                UserData = path,
                                ToolTip = path
                            };
                        }
                    }
                    catch (Exception e)
                    {
                        DebugConsole.ThrowError("Couldn't open directory \"" + RagdollParams.Folder + "\"!", e);
                    }
                }
                PopulateListBox();
                // Handle file selection
                string selectedFile = null;
                listBox.OnSelected += (component, data) =>
                {
                    selectedFile = data as string;
                    // Don't allow to delete the ragdoll that is currently in use, nor the default file.
                    var fileName = Path.GetFileNameWithoutExtension(selectedFile);
                    deleteButton.Enabled = fileName != RagdollParams.Name && fileName != RagdollParams.GetDefaultFileName(character.SpeciesName);
                    return true;
                };
                deleteButton.OnClicked += (btn, data) =>
                {
                    if (selectedFile == null)
                    {
                        loadBox.Close();
                        return false;
                    }
                    var msgBox = new GUIMessageBox(
                        TextManager.Get("DeleteDialogLabel"),
                        TextManager.Get("DeleteDialogQuestion").Replace("[file]", selectedFile),
                        new string[] { TextManager.Get("Yes"), TextManager.Get("Cancel") }, messageBoxWidth - 100, messageBoxHeight - 100);
                    msgBox.Buttons[0].OnClicked += (b, d) =>
                    {
                        try
                        {
                            File.Delete(selectedFile);
                        }
                        catch (Exception e)
                        {
                            DebugConsole.ThrowError(TextManager.Get("DeleteFileError").Replace("[file]", selectedFile), e);
                        }
                        msgBox.Close();
                        listBox.ClearChildren();
                        PopulateListBox();
                        selectedFile = null;
                        return true;
                    };
                    msgBox.Buttons[1].OnClicked += (b, d) =>
                    {
                        msgBox.Close();
                        return true;
                    };
                    return true;
                };
                loadBox.Buttons[1].OnClicked += (btn, data) =>
                {
                    string fileName = Path.GetFileNameWithoutExtension(selectedFile);
                    var ragdoll = character.IsHumanoid ? HumanRagdollParams.GetRagdollParams(fileName) as RagdollParams : RagdollParams.GetRagdollParams<FishRagdollParams>(character.SpeciesName, fileName);
                    character.AnimController.Recreate(ragdoll);
                    TeleportTo(spawnPosition);
                    ResetParamsEditor();
                    loadBox.Close();
                    return true;
                };
                return true;
            };
            var saveAnimationButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Save Animation");
            saveAnimationButton.OnClicked += (button, userData) =>
            {
                var box = new GUIMessageBox("Save Animation", string.Empty, new string[] { "Cancel", "Save" }, messageBoxWidth, messageBoxHeight);
                var textArea = new GUIFrame(new RectTransform(new Vector2(1, 0.1f), box.Content.RectTransform) { MinSize = new Point(350, 30) }, style: null);
                var inputLabel = new GUITextBlock(new RectTransform(new Vector2(0.3f, 1), textArea.RectTransform) { MinSize = new Point(250, 30) }, "Please provide a name for the file:");
                var inputField = new GUITextBox(new RectTransform(new Vector2(0.5f, 1), textArea.RectTransform, Anchor.TopRight) { MinSize = new Point(100, 30) }, CurrentAnimation.Name);
                // Type filtering
                var typeSelectionArea = new GUIFrame(new RectTransform(new Vector2(1f, 0.1f), box.Content.RectTransform) { MinSize = new Point(0, 30) }, style: null);
                var typeLabel = new GUITextBlock(new RectTransform(new Vector2(0.4f, 1), typeSelectionArea.RectTransform, Anchor.TopCenter, Pivot.TopRight), "Select Animation Type:");
                var typeDropdown = new GUIDropDown(new RectTransform(new Vector2(0.4f, 1), typeSelectionArea.RectTransform, Anchor.TopCenter, Pivot.TopLeft), elementCount: 4);
                foreach (object enumValue in Enum.GetValues(typeof(AnimationType)))
                {
                    typeDropdown.AddItem(enumValue.ToString(), enumValue);
                }
                typeDropdown.SelectItem(AnimationType.Walk);
                AnimationType selectedType = AnimationType.Walk;
                typeDropdown.OnSelected = (component, data) =>
                {
                    selectedType = (AnimationType)data;
                    inputField.Text = character.AnimController.GetAnimationParamsFromType(selectedType).Name;
                    return true;
                };
                box.Buttons[0].OnClicked += (b, d) =>
                {
                    box.Close();
                    return true;
                };
                box.Buttons[1].OnClicked += (b, d) =>
                {
                    character.AnimController.GetAnimationParamsFromType(selectedType).Save(inputField.Text);
                    ResetParamsEditor();
                    box.Close();
                    return true;
                };
                return true;
            };
            var loadAnimationButton = new GUIButton(new RectTransform(buttonSize, layoutGroup.RectTransform), "Load Animation");
            loadAnimationButton.OnClicked += (button, userData) =>
            {
                var loadBox = new GUIMessageBox("Load Animation", "", new string[] { "Cancel", "Load", "Delete" }, messageBoxWidth, messageBoxHeight);
                loadBox.Buttons[0].OnClicked += loadBox.Close;
                var listBox = new GUIListBox(new RectTransform(new Vector2(0.9f, 0.6f), loadBox.Content.RectTransform));
                var deleteButton = loadBox.Buttons[2];
                deleteButton.Enabled = false;
                // Type filtering
                var typeSelectionArea = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.1f), loadBox.Content.RectTransform) { MinSize = new Point(0, 30) }, style: null);
                var typeLabel = new GUITextBlock(new RectTransform(new Vector2(0.4f, 1), typeSelectionArea.RectTransform, Anchor.TopCenter, Pivot.TopRight), "Select Animation Type:");
                var typeDropdown = new GUIDropDown(new RectTransform(new Vector2(0.4f, 1), typeSelectionArea.RectTransform, Anchor.TopCenter, Pivot.TopLeft), elementCount: 4);
                foreach (object enumValue in Enum.GetValues(typeof(AnimationType)))
                {
                    typeDropdown.AddItem(enumValue.ToString(), enumValue);
                }
                typeDropdown.SelectItem(AnimationType.Walk);
                AnimationType selectedType = AnimationType.Walk;
                typeDropdown.OnSelected = (component, data) =>
                {
                    selectedType = (AnimationType)data;
                    PopulateListBox();
                    return true;
                };
                void PopulateListBox()
                {
                    try
                    {
                        listBox.ClearChildren();
                        var filePaths = Directory.GetFiles(CurrentAnimation.Folder);
                        foreach (var path in AnimationParams.FilterFilesByType(filePaths, selectedType))
                        {
                            GUITextBlock textBlock = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), listBox.Content.RectTransform) { MinSize = new Point(0, 30) }, ToolBox.LimitString(Path.GetFileNameWithoutExtension(path), GUI.Font, listBox.Rect.Width - 80))
                            {
                                UserData = path,
                                ToolTip = path
                            };
                        }
                    }
                    catch (Exception e)
                    {
                        DebugConsole.ThrowError("Couldn't open directory \"" + CurrentAnimation.Folder + "\"!", e);
                    }
                }
                PopulateListBox();
                // Handle file selection
                string selectedFile = null;
                listBox.OnSelected += (component, data) =>
                {
                    selectedFile = data as string;
                    // Don't allow to delete the animation that is currently in use, nor the default file.
                    var fileName = Path.GetFileNameWithoutExtension(selectedFile);
                    deleteButton.Enabled = fileName != CurrentAnimation.Name && fileName != AnimationParams.GetDefaultFileName(character.SpeciesName, CurrentAnimation.AnimationType);
                    return true;
                };
                deleteButton.OnClicked += (btn, data) =>
                {
                    if (selectedFile == null)
                    {
                        loadBox.Close();
                        return false;
                    }
                    var msgBox = new GUIMessageBox(
                        TextManager.Get("DeleteDialogLabel"),
                        TextManager.Get("DeleteDialogQuestion").Replace("[file]", selectedFile),
                        new string[] { TextManager.Get("Yes"), TextManager.Get("Cancel") }, messageBoxWidth - 100, messageBoxHeight - 100);
                    msgBox.Buttons[0].OnClicked += (b, d) =>
                    {
                        try
                        {
                            File.Delete(selectedFile);
                        }
                        catch (Exception e)
                        {
                            DebugConsole.ThrowError(TextManager.Get("DeleteFileError").Replace("[file]", selectedFile), e);
                        }
                        msgBox.Close();
                        PopulateListBox();
                        selectedFile = null;
                        return true;
                    };
                    msgBox.Buttons[1].OnClicked += (b, d) =>
                    {
                        msgBox.Close();
                        return true;
                    };
                    return true;
                };
                loadBox.Buttons[1].OnClicked += (btn, data) =>
                {
                    string fileName = Path.GetFileNameWithoutExtension(selectedFile);
                    if (character.IsHumanoid)
                    {
                        switch (selectedType)
                        {
                            case AnimationType.Walk:
                                character.AnimController.WalkParams = HumanWalkParams.GetAnimParams(fileName);
                            break;
                            case AnimationType.Run:
                                character.AnimController.RunParams = HumanRunParams.GetAnimParams(fileName);
                                break;
                            case AnimationType.SwimSlow:
                                character.AnimController.SwimSlowParams = HumanSwimSlowParams.GetAnimParams(fileName);
                                break;
                            case AnimationType.SwimFast:
                                character.AnimController.SwimFastParams = HumanSwimFastParams.GetAnimParams(fileName);
                                break;
                            default:
                                DebugConsole.ThrowError($"Animation type {selectedType.ToString()} not implemented!");
                                break;
                        }
                    }
                    else
                    {
                        switch (selectedType)
                        {
                            case AnimationType.Walk:
                                character.AnimController.WalkParams = FishWalkParams.GetAnimParams(character, fileName);
                                break;
                            case AnimationType.Run:
                                character.AnimController.RunParams = FishRunParams.GetAnimParams(character, fileName);
                                break;
                            case AnimationType.SwimSlow:
                                character.AnimController.SwimSlowParams = FishSwimSlowParams.GetAnimParams(character, fileName);
                                break;
                            case AnimationType.SwimFast:
                                character.AnimController.SwimFastParams = FishSwimFastParams.GetAnimParams(character, fileName);
                                break;
                            default:
                                DebugConsole.ThrowError($"Animation type {selectedType.ToString()} not implemented!");
                                break;
                        }
                    }
                    ResetParamsEditor();
                    loadBox.Close();
                    return true;
                };
                return true;
            };
        }
        #endregion

        #region Params
        private List<AnimationParams> AnimParams => character.AnimController.AllAnimParams;
        private AnimationParams CurrentAnimation => character.AnimController.CurrentAnimationParams;
        private RagdollParams RagdollParams => character.AnimController.RagdollParams;

        private void ResetParamsEditor()
        {
            ParamsEditor.Instance.Clear();
            if (editRagdoll || editSpriteDimensions)
            {
                RagdollParams.AddToEditor(ParamsEditor.Instance);
            }
            else
            {
                AnimParams.ForEach(p => p.AddToEditor(ParamsEditor.Instance));
            }
        }

        private void TryUpdateAnimParam(string name, object value) => TryUpdateParam(character.AnimController.CurrentAnimationParams, name, value);
        private void TryUpdateRagdollParam(string name, object value) => TryUpdateParam(RagdollParams, name, value);

        private void TryUpdateParam(EditableParams editableParams, string name, object value)
        {
            if (editableParams.SerializableProperties.TryGetValue(name, out SerializableProperty p))
            {
                editableParams.SerializableEntityEditor.UpdateValue(p, value);
            }
        }

        private void TryUpdateJointParam(LimbJoint joint, string name, object value) => TryUpdateSubParam(joint.jointParams, name, value);
        private void TryUpdateLimbParam(Limb limb, string name, object value) => TryUpdateSubParam(limb.limbParams, name, value);

        private void TryUpdateSubParam(RagdollSubParams ragdollParams, string name, object value)
        {
            if (ragdollParams.SerializableProperties.TryGetValue(name, out SerializableProperty p))
            {
                ragdollParams.SerializableEntityEditor.UpdateValue(p, value);
            }
            else
            {
                var subParams = ragdollParams.SubParams.Where(sp => sp.SerializableProperties.ContainsKey(name)).FirstOrDefault();
                if (subParams != null)
                {
                    if (subParams.SerializableProperties.TryGetValue(name, out p))
                    {
                        subParams.SerializableEntityEditor.UpdateValue(p, value);
                    }
                }
                else
                {
                    DebugConsole.ThrowError($"No field for {name} found!");
                    //ragdollParams.SubParams.ForEach(sp => sp.SerializableProperties.ForEach(prop => DebugConsole.ThrowError($"{sp.Name}: sub param field: {prop.Key}")));
                }
            }
        }
        #endregion

        public override void AddToGUIUpdateList()
        {
            //base.AddToGUIUpdateList();
            rightPanel.AddToGUIUpdateList();
            if (showAnimControls)
            {
                animationControls.AddToGUIUpdateList();
            }
            if (editRagdoll)
            {
                ragdollControls.AddToGUIUpdateList();
            }
            if (showParamsEditor)
            {
                ParamsEditor.Instance.EditorBox.AddToGUIUpdateList();
            }
        }

        public override void Update(double deltaTime)
        {
            base.Update(deltaTime);
            if (!freeze)
            {
                Submarine.MainSub.SetPrevTransform(Submarine.MainSub.Position);
                Submarine.MainSub.Update((float)deltaTime);
                PhysicsBody.List.ForEach(pb => pb.SetPrevTransform(pb.SimPosition, pb.Rotation));
                character.ControlLocalPlayer((float)deltaTime, Cam, false);
                character.Control((float)deltaTime, Cam);
                character.AnimController.UpdateAnim((float)deltaTime);
                character.AnimController.Update((float)deltaTime, Cam);
                if (character.Position.X < min)
                {
                    UpdateWalls(false);
                }
                else if (character.Position.X > max)
                {
                    UpdateWalls(true);
                }
                GameMain.World.Step((float)deltaTime);
            }
            //Cam.TargetPos = Vector2.Zero;
            Cam.MoveCamera((float)deltaTime, allowMove: false, allowZoom: GUI.MouseOn == null);
            Cam.Position = character.Position;
            widgets.Values.ForEach(w => w.Update((float)deltaTime));
        }

        /// <summary>
        /// Fps independent mouse input. The draw method is called multiple times per frame.
        /// </summary>
        private Vector2 scaledMouseSpeed;
        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            base.Draw(deltaTime, graphics, spriteBatch);
            scaledMouseSpeed = PlayerInput.MouseSpeedPerSecond * (float)deltaTime;

            graphics.Clear(Color.CornflowerBlue);
            Cam.UpdateTransform(true);

            // Submarine
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, transformMatrix: Cam.Transform);
            Submarine.Draw(spriteBatch, true);
            spriteBatch.End();

            // Character
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, transformMatrix: Cam.Transform);
            character.Draw(spriteBatch);
            if (GameMain.DebugDraw)
            {
                character.AnimController.Collider.DebugDraw(spriteBatch, Color.LightGreen);
            }
            spriteBatch.End();

            // GUI
            spriteBatch.Begin(SpriteSortMode.Immediate, rasterizerState: GameMain.ScissorTestEnable);
            if (showAnimControls)
            {
                DrawAnimationControls(spriteBatch);
            }
            if (editSpriteDimensions)
            {
                DrawSpriteOriginEditor(spriteBatch);
            }
            if (editRagdoll)
            {
                DrawRagdollEditor(spriteBatch, (float)deltaTime);
            }
            if (showSpritesheet)
            {
                DrawSpritesheetEditor(spriteBatch, (float)deltaTime);
            }
            //widgets.Values.ForEach(w => w.Draw(spriteBatch, (float)deltaTime));
            Structure wall = CurrentWall.walls.FirstOrDefault();
            Vector2 indicatorPos = wall == null ? originalWall.walls.First().DrawPosition : wall.DrawPosition;
            GUI.DrawIndicator(spriteBatch, indicatorPos, Cam, 700, GUI.SubmarineIcon, Color.White);
            GUI.Draw(Cam, spriteBatch);

            // Debug
            if (GameMain.DebugDraw)
            {
                // Limb positions
                foreach (Limb limb in character.AnimController.Limbs)
                {
                    Vector2 limbDrawPos = Cam.WorldToScreen(limb.WorldPosition);
                    GUI.DrawLine(spriteBatch, limbDrawPos + Vector2.UnitY * 5.0f, limbDrawPos - Vector2.UnitY * 5.0f, Color.White);
                    GUI.DrawLine(spriteBatch, limbDrawPos + Vector2.UnitX * 5.0f, limbDrawPos - Vector2.UnitX * 5.0f, Color.White);
                }

                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 0), $"Cursor World Pos: {character.CursorWorldPosition}", Color.White, font: GUI.SmallFont);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 20), $"Cursor Pos: {character.CursorPosition}", Color.White, font: GUI.SmallFont);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 40), $"Cursor Screen Pos: {PlayerInput.MousePosition}", Color.White, font: GUI.SmallFont);


                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 80), $"Character World Pos: {character.WorldPosition}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 100), $"Character Pos: {character.Position}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 120), $"Character Sim Pos: {character.SimPosition}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 140), $"Character Draw Pos: {character.DrawPosition}", Color.White, font: GUI.SmallFont);

                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 180), $"Submarine World Pos: {Submarine.MainSub.WorldPosition}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 200), $"Submarine Pos: {Submarine.MainSub.Position}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 220), $"Submarine Sim Pos: {Submarine.MainSub.Position}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 240), $"Submarine Draw Pos: {Submarine.MainSub.DrawPosition}", Color.White, font: GUI.SmallFont);

                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 280), $"Movement Limits: MIN: {min} MAX: {max}", Color.White, font: GUI.SmallFont);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 300), $"Clones: {clones.Length}", Color.White, font: GUI.SmallFont);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 320), $"Total amount of walls: {Structure.WallList.Count}", Color.White, font: GUI.SmallFont);

                // Collider
                var collider = character.AnimController.Collider;
                var colliderDrawPos = SimToScreen(collider.SimPosition);
                Vector2 forward = Vector2.Transform(Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation));
                var endPos = SimToScreen(collider.SimPosition + forward * collider.radius);
                GUI.DrawLine(spriteBatch, colliderDrawPos, endPos, Color.LightGreen);
                GUI.DrawLine(spriteBatch, colliderDrawPos, SimToScreen(collider.SimPosition + forward * 0.25f), Color.Blue);
                //Vector2 left = Vector2.Transform(-Vector2.UnitX, Matrix.CreateRotationZ(collider.Rotation));
                //Vector2 left = -Vector2.UnitX.TransformVector(forward);
                Vector2 left = -forward.Right();
                GUI.DrawLine(spriteBatch, colliderDrawPos, SimToScreen(collider.SimPosition + left * 0.25f), Color.Red);
                ShapeExtensions.DrawCircle(spriteBatch, colliderDrawPos, (endPos - colliderDrawPos).Length(), 40, Color.LightGreen);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth - 300, 0), $"Collider rotation: {MathHelper.ToDegrees(MathUtils.WrapAngleTwoPi(collider.Rotation))}", Color.White, font: GUI.SmallFont);
            }
            spriteBatch.End();
        }

        #region Helpers
        private Vector2 ScreenToSim(float x, float y) => ScreenToSim(new Vector2(x, y));
        private Vector2 ScreenToSim(Vector2 p) => ConvertUnits.ToSimUnits(Cam.ScreenToWorld(p));
        private Vector2 SimToScreen(float x, float y) => SimToScreen(new Vector2(x, y));
        private Vector2 SimToScreen(Vector2 p) => Cam.WorldToScreen(ConvertUnits.ToDisplayUnits(p));
        #endregion

        #region Animation Controls
        private void DrawAnimationControls(SpriteBatch spriteBatch)
        {
            var collider = character.AnimController.Collider;
            var colliderDrawPos = SimToScreen(collider.SimPosition);
            var animParams = character.AnimController.CurrentAnimationParams;
            var groundedParams = animParams as GroundedMovementParams;
            var humanGroundedParams = animParams as HumanGroundedParams;
            var fishGroundedParams = animParams as FishGroundedParams;
            var fishSwimParams = animParams as FishSwimParams;
            var humanSwimParams = animParams as HumanSwimParams;
            var head = character.AnimController.GetLimb(LimbType.Head);
            var torso = character.AnimController.GetLimb(LimbType.Torso);
            var tail = character.AnimController.GetLimb(LimbType.Tail);
            var legs = character.AnimController.GetLimb(LimbType.Legs);
            var thigh = character.AnimController.GetLimb(LimbType.RightThigh) ?? character.AnimController.GetLimb(LimbType.LeftThigh);
            var foot = character.AnimController.GetLimb(LimbType.RightFoot) ?? character.AnimController.GetLimb(LimbType.LeftFoot);
            var hand = character.AnimController.GetLimb(LimbType.RightHand) ?? character.AnimController.GetLimb(LimbType.LeftHand);
            var arm = character.AnimController.GetLimb(LimbType.RightArm) ?? character.AnimController.GetLimb(LimbType.LeftArm);
            int widgetDefaultSize = 10;
            // collider does not rotate when the sprite is flipped -> rotates only when swimming
            float dir = character.AnimController.Dir;
            Vector2 colliderBottom = character.AnimController.GetColliderBottom();
            Vector2 centerOfMass = character.AnimController.GetCenterOfMass();
            Vector2 simSpaceForward = Vector2.Transform(Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation));
            Vector2 simSpaceLeft = Vector2.Transform(-Vector2.UnitX, Matrix.CreateRotationZ(collider.Rotation));
            Vector2 screenSpaceForward = -VectorExtensions.Forward(collider.Rotation, 1);
            Vector2 screenSpaceLeft = screenSpaceForward.Right();
            // The forward vector is left or right in screen space when the unit is not swimming. Cannot rely on the collider here, because the rotation may vary on ground.
            Vector2 forward = animParams.IsSwimAnimation ? screenSpaceForward : Vector2.UnitX * dir;

            if (GameMain.DebugDraw)
            {
                //GUI.DrawLine(spriteBatch, charDrawPos, charDrawPos + screenSpaceForward * 40, Color.Blue);
                //GUI.DrawLine(spriteBatch, charDrawPos, charDrawPos + screenSpaceLeft * 40, Color.Red);
            }

            // Widgets for all anims -->
            // Speed
            Vector2 referencePoint = SimToScreen(head != null ? head.SimPosition : collider.SimPosition);
            Vector2 drawPos = referencePoint;
            drawPos += forward * ConvertUnits.ToDisplayUnits(animParams.Speed) * Cam.Zoom;
            DrawWidget(spriteBatch, drawPos, WidgetType.Circle, 20, Color.Turquoise, "Movement Speed", () =>
            {
                float speed = animParams.Speed + ConvertUnits.ToSimUnits(Vector2.Multiply(scaledMouseSpeed, forward).Combine()) / Cam.Zoom;
                TryUpdateAnimParam("speed", MathHelper.Clamp(speed, 0.1f, Ragdoll.MAX_SPEED));
                GUI.DrawLine(spriteBatch, drawPos, referencePoint, Color.Turquoise);
            });
            GUI.DrawLine(spriteBatch, drawPos + forward * 10, drawPos + forward * 15, Color.Turquoise);
            if (head != null)
            {
                // Head angle
                DrawCircularWidget(spriteBatch, SimToScreen(head.SimPosition), animParams.HeadAngle, "Head Angle", Color.White,
                    angle => TryUpdateAnimParam("headangle", angle), circleRadius: 25, rotationOffset: collider.Rotation, clockWise: dir < 0);
                // Head position and leaning
                if (animParams.IsGroundedAnimation)
                {
                    if (humanGroundedParams != null)
                    {
                        drawPos = SimToScreen(head.SimPosition.X + humanGroundedParams.HeadLeanAmount * dir, head.pullJoint.WorldAnchorB.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Head", () =>
                        {
                            var scaledInput = ConvertUnits.ToSimUnits(scaledMouseSpeed) / Cam.Zoom;
                            TryUpdateAnimParam("headleanamount", humanGroundedParams.HeadLeanAmount + scaledInput.X * dir);
                            TryUpdateAnimParam("headposition", humanGroundedParams.HeadPosition - scaledInput.Y * 1.5f);
                            GUI.DrawLine(spriteBatch, drawPos, SimToScreen(head.SimPosition), Color.Red);
                        });
                        var origin = drawPos + new Vector2(widgetDefaultSize / 2, 0) * dir;
                        GUI.DrawLine(spriteBatch, origin, origin + Vector2.UnitX * 5 * dir, Color.Red);
                    }
                    else
                    {
                        drawPos = SimToScreen(head.SimPosition.X, head.pullJoint.WorldAnchorB.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Head Position", () =>
                        {
                            float v = groundedParams.HeadPosition - ConvertUnits.ToSimUnits(scaledMouseSpeed.Y) / Cam.Zoom;
                            TryUpdateAnimParam("headposition", v);
                            GUI.DrawLine(spriteBatch, new Vector2(drawPos.X, 0), new Vector2(drawPos.X, GameMain.GraphicsHeight), Color.Red);
                        });
                    }
                }
            }
            if (torso != null)
            {
                referencePoint = torso.SimPosition;
                if (animParams is HumanGroundedParams || animParams is HumanSwimParams)
                {
                    referencePoint -= simSpaceForward * 0.25f;
                }
                // Torso angle
                DrawCircularWidget(spriteBatch, SimToScreen(referencePoint), animParams.TorsoAngle, "Torso Angle", Color.White,
                    angle => TryUpdateAnimParam("torsoangle", angle), rotationOffset: collider.Rotation, clockWise: dir < 0);

                if (animParams.IsGroundedAnimation)
                {
                    // Torso position and leaning
                    if (humanGroundedParams != null)
                    {
                        drawPos = SimToScreen(torso.SimPosition.X + humanGroundedParams.TorsoLeanAmount * dir, torso.SimPosition.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Torso", () =>
                        {
                            var scaledInput = ConvertUnits.ToSimUnits(scaledMouseSpeed) / Cam.Zoom;
                            TryUpdateAnimParam("torsoleanamount", humanGroundedParams.TorsoLeanAmount + scaledInput.X * dir);
                            TryUpdateAnimParam("torsoposition", humanGroundedParams.TorsoPosition - scaledInput.Y * 1.5f);
                            GUI.DrawLine(spriteBatch, drawPos, SimToScreen(torso.SimPosition), Color.Red);
                        });
                        var origin = drawPos + new Vector2(widgetDefaultSize / 2, 0) * dir;
                        GUI.DrawLine(spriteBatch, origin, origin + Vector2.UnitX * 5 * dir, Color.Red);
                    }
                    else
                    {
                        drawPos = SimToScreen(torso.SimPosition.X, torso.pullJoint.WorldAnchorB.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Torso Position", () =>
                        {
                            float v = groundedParams.TorsoPosition - ConvertUnits.ToSimUnits(scaledMouseSpeed.Y) / Cam.Zoom;
                            TryUpdateAnimParam("torsoposition", v);
                            GUI.DrawLine(spriteBatch, new Vector2(drawPos.X, 0), new Vector2(drawPos.X, GameMain.GraphicsHeight), Color.Red);
                        });
                    }
                }
            }
            if (foot != null)
            {
                // Fish grounded only
                if (fishGroundedParams != null)
                {
                    DrawCircularWidget(spriteBatch, SimToScreen(colliderBottom), fishGroundedParams.FootRotation, "Foot Rotation", Color.White,
                        angle => TryUpdateAnimParam("footrotation", angle), circleRadius: 25, rotationOffset: collider.Rotation, clockWise: dir < 0);
                }
                // Both
                if (groundedParams != null)
                {
                    referencePoint = SimToScreen(colliderBottom);
                    var v = ConvertUnits.ToDisplayUnits(groundedParams.StepSize);
                    drawPos = referencePoint + new Vector2(v.X * dir, -v.Y) * Cam.Zoom;
                    var origin = drawPos - new Vector2(widgetDefaultSize / 2, 0) * -dir;
                    DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Blue, "Step Size", () =>
                    {
                        var transformedInput = ConvertUnits.ToSimUnits(scaledMouseSpeed) * dir / Cam.Zoom;
                        if (dir > 0)
                        {
                            transformedInput.Y = -transformedInput.Y;
                        }
                        TryUpdateAnimParam("stepsize", groundedParams.StepSize + transformedInput);
                        GUI.DrawLine(spriteBatch, origin, referencePoint, Color.Blue);
                    });
                    GUI.DrawLine(spriteBatch, origin, origin + Vector2.UnitX * 5 * dir, Color.Blue);
                }
            }
            // Human grounded only -->
            if (humanGroundedParams != null)
            {
                if (legs != null || foot != null)
                {
                    drawPos = SimToScreen(colliderBottom + simSpaceForward * 0.3f);
                    float multiplier = 10;
                    DrawCircularWidget(spriteBatch, drawPos, humanGroundedParams.LegCorrectionTorque * multiplier, "Leg Angle", Color.Chartreuse, angle =>
                    {
                        TryUpdateAnimParam("legcorrectiontorque", angle / multiplier);
                        GUI.DrawString(spriteBatch, drawPos, humanGroundedParams.LegCorrectionTorque.FormatAsSingleDecimal(), Color.Black, Color.Chartreuse, font: GUI.SmallFont);
                    }, circleRadius: 25, rotationOffset: collider.Rotation, clockWise: dir < 0, displayAngle: false);
                }
                if (hand != null || arm != null)
                {
                    referencePoint = SimToScreen(collider.SimPosition + simSpaceForward * 0.2f);
                    var v = ConvertUnits.ToDisplayUnits(humanGroundedParams.HandMoveAmount);
                    drawPos = referencePoint + new Vector2(v.X * dir, v.Y) * Cam.Zoom;
                    var origin = drawPos - new Vector2(widgetDefaultSize / 2, 0) * -dir;
                    DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Blue, "Hand Move Amount", () =>
                    {
                        var transformedInput = ConvertUnits.ToSimUnits(new Vector2(scaledMouseSpeed.X * dir, scaledMouseSpeed.Y)) / Cam.Zoom;
                        TryUpdateAnimParam("handmoveamount", humanGroundedParams.HandMoveAmount + transformedInput);
                        GUI.DrawLine(spriteBatch, origin, referencePoint, Color.Blue);
                    });
                    GUI.DrawLine(spriteBatch, origin, origin + Vector2.UnitX * 5 * dir, Color.Blue);
                }
            }
            // Fish swim only -->
            else if (tail != null && fishSwimParams != null)
            {
                float amplitudeMultiplier = 0.5f;
                float lengthMultiplier = 20;
                float amplitude = ConvertUnits.ToDisplayUnits(fishSwimParams.WaveAmplitude) * Cam.Zoom / amplitudeMultiplier;
                float length = ConvertUnits.ToDisplayUnits(fishSwimParams.WaveLength) * Cam.Zoom / lengthMultiplier;
                referencePoint = colliderDrawPos - screenSpaceForward * ConvertUnits.ToDisplayUnits(collider.radius) * 3 * Cam.Zoom;
                drawPos = referencePoint;
                drawPos -= screenSpaceForward * length;
                Vector2 toRefPoint = referencePoint - drawPos;
                var start = drawPos + toRefPoint / 2;
                var control = start + (screenSpaceLeft * dir * amplitude);
                int points = 1000;
                // Length
                DrawWidget(spriteBatch, drawPos, WidgetType.Circle, 15, Color.Purple, "Wave Length", () =>
                {
                    var input = Vector2.Multiply(ConvertUnits.ToSimUnits(scaledMouseSpeed), screenSpaceForward).Combine() / Cam.Zoom * lengthMultiplier;
                    TryUpdateAnimParam("wavelength", MathHelper.Clamp(fishSwimParams.WaveLength - input, 0, 150));
                    //GUI.DrawLine(spriteBatch, drawPos, referencePoint, Color.Purple);
                    //GUI.DrawBezierWithDots(spriteBatch, referencePoint, drawPos, control, points, Color.Purple);
                    GUI.DrawSineWithDots(spriteBatch, referencePoint, -toRefPoint, amplitude, length, 5000, points, Color.Purple);

                });
                // Amplitude
                DrawWidget(spriteBatch, control, WidgetType.Circle, 15, Color.Purple, "Wave Amplitude", () =>
                {
                    var input = Vector2.Multiply(ConvertUnits.ToSimUnits(scaledMouseSpeed), screenSpaceLeft).Combine() * dir / Cam.Zoom * amplitudeMultiplier;
                    TryUpdateAnimParam("waveamplitude", MathHelper.Clamp(fishSwimParams.WaveAmplitude + input, -4, 4));
                    //GUI.DrawLine(spriteBatch, start, control, Color.Purple);
                    //GUI.DrawBezierWithDots(spriteBatch, referencePoint, drawPos, control, points, Color.Purple);
                    GUI.DrawSineWithDots(spriteBatch, referencePoint, -toRefPoint, amplitude, length, 5000, points, Color.Purple);

                });
            }
            // Human swim only -->
            else if (humanSwimParams != null)
            {
                // Legs
                float amplitudeMultiplier = 5;
                float lengthMultiplier = 5;
                float legMoveAmount = ConvertUnits.ToDisplayUnits(humanSwimParams.LegMoveAmount) * Cam.Zoom / amplitudeMultiplier;
                float legCycleLength = ConvertUnits.ToDisplayUnits(humanSwimParams.LegCycleLength) * Cam.Zoom / lengthMultiplier;
                referencePoint = SimToScreen(character.SimPosition - simSpaceForward / 2);
                drawPos = referencePoint;
                drawPos -= screenSpaceForward * legCycleLength;
                Vector2 toRefPoint = referencePoint - drawPos;
                Vector2 start = drawPos + toRefPoint / 2;
                Vector2 control = start + (screenSpaceLeft * dir * legMoveAmount);
                int points = 1000;
                // Cycle length
                DrawWidget(spriteBatch, drawPos, WidgetType.Circle, 15, Color.Purple, "Leg Movement Speed", () =>
                {
                    float input = Vector2.Multiply(ConvertUnits.ToSimUnits(scaledMouseSpeed), screenSpaceForward).Combine() / Cam.Zoom * amplitudeMultiplier;
                    TryUpdateAnimParam("legcyclelength", MathHelper.Clamp(humanSwimParams.LegCycleLength - input, 0, 20));
                    //GUI.DrawLine(spriteBatch, drawPos, referencePoint, Color.Purple);
                    //GUI.DrawBezierWithDots(spriteBatch, referencePoint, drawPos, control, points, Color.Purple);
                    GUI.DrawSineWithDots(spriteBatch, referencePoint, -toRefPoint, legMoveAmount, legCycleLength, 5000, points, Color.Purple);
                });
                // Movement amount
                DrawWidget(spriteBatch, control, WidgetType.Circle, 15, Color.Purple, "Leg Movement Amount", () =>
                {
                    float input = Vector2.Multiply(ConvertUnits.ToSimUnits(scaledMouseSpeed), screenSpaceLeft).Combine() * dir / Cam.Zoom * lengthMultiplier;
                    TryUpdateAnimParam("legmoveamount", MathHelper.Clamp(humanSwimParams.LegMoveAmount + input, -2, 2));
                    //GUI.DrawLine(spriteBatch, start, control, Color.Purple);
                    //GUI.DrawBezierWithDots(spriteBatch, referencePoint, drawPos, control, points, Color.Purple);
                    GUI.DrawSineWithDots(spriteBatch, referencePoint, -toRefPoint, legMoveAmount, legCycleLength, 5000, points, Color.Purple);
                });
                // Arms
                referencePoint = colliderDrawPos + screenSpaceForward * 10;
                Vector2 handMoveAmount = ConvertUnits.ToDisplayUnits(humanSwimParams.HandMoveAmount) * Cam.Zoom;
                drawPos = referencePoint + new Vector2(handMoveAmount.X * dir, handMoveAmount.Y);
                Vector2 origin = drawPos - new Vector2(widgetDefaultSize / 2, 0) * -dir;
                DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Blue, "Hand Move Amount", () =>
                {
                    Vector2 transformedInput = ConvertUnits.ToSimUnits(new Vector2(scaledMouseSpeed.X * dir, scaledMouseSpeed.Y)) / Cam.Zoom;
                    Vector2 handMovement = humanSwimParams.HandMoveAmount + transformedInput;
                    TryUpdateAnimParam("handmoveamount", handMovement);
                    TryUpdateAnimParam("handcyclespeed", handMovement.X * 4);
                    GUI.DrawLine(spriteBatch, origin, referencePoint, Color.Blue);
                });
                GUI.DrawLine(spriteBatch, origin, origin + Vector2.UnitX * 5 * dir, Color.Blue);
            }
        }
        #endregion

        #region Ragdoll
        private Vector2[] corners = new Vector2[4];
        private void DrawSpriteOriginEditor(SpriteBatch spriteBatch)
        {
            float inputMultiplier = 0.5f;
            Limb selectedLimb = null;
            foreach (Limb limb in character.AnimController.Limbs)
            {
                if (limb == null || limb.sprite == null) { continue; }
                Vector2 limbScreenPos = SimToScreen(limb.SimPosition);
                //GUI.DrawRectangle(spriteBatch, new Rectangle(limbBodyPos.ToPoint(), new Point(5, 5)), Color.Red);
                Vector2 size = limb.sprite.SourceRect.Size.ToVector2() * Cam.Zoom * limb.Scale;
                Vector2 up = -VectorExtensions.Forward(limb.Rotation);
                corners = MathUtils.GetImaginaryRect(corners, up, limbScreenPos, size);
                //var rect = new Rectangle(limbBodyPos.ToPoint() - size.Divide(2), size);
                //GUI.DrawRectangle(spriteBatch, rect, Color.Blue);
                GUI.DrawRectangle(spriteBatch, corners, Color.Red);
                GUI.DrawLine(spriteBatch, limbScreenPos, limbScreenPos + up * 20, Color.White, width: 3);
                GUI.DrawLine(spriteBatch, limbScreenPos, limbScreenPos + up * 20, Color.Red);
                // Limb positions
                GUI.DrawLine(spriteBatch, limbScreenPos + Vector2.UnitY * 5.0f, limbScreenPos - Vector2.UnitY * 5.0f, Color.White, width: 3);
                GUI.DrawLine(spriteBatch, limbScreenPos + Vector2.UnitX * 5.0f, limbScreenPos - Vector2.UnitX * 5.0f, Color.White, width: 3);
                GUI.DrawLine(spriteBatch, limbScreenPos + Vector2.UnitY * 5.0f, limbScreenPos - Vector2.UnitY * 5.0f, Color.Red);
                GUI.DrawLine(spriteBatch, limbScreenPos + Vector2.UnitX * 5.0f, limbScreenPos - Vector2.UnitX * 5.0f, Color.Red);
                if (PlayerInput.LeftButtonHeld() && selectedWidget == null && MathUtils.RectangleContainsPoint(corners, PlayerInput.MousePosition))
                {
                    if (selectedLimb == null)
                    {
                        selectedLimb = limb;
                    }
                }
                else if (selectedLimb == limb)
                {
                    selectedLimb = null;
                }
            }
            if (selectedLimb != null)
            {
                float multiplier = 0.5f;
                Vector2 up = Vector2.Transform(Vector2.UnitY, Matrix.CreateRotationZ(selectedLimb.Rotation));
                var input = -scaledMouseSpeed * inputMultiplier * Cam.Zoom / selectedLimb.Scale * multiplier;
                var sprite = selectedLimb.sprite;
                var origin = sprite.Origin;
                origin += input.TransformVector(up);
                var sourceRect = sprite.SourceRect;
                var max = new Vector2(sourceRect.Width, sourceRect.Height);
                sprite.Origin = origin.Clamp(Vector2.Zero, max);
                if (selectedLimb.damagedSprite != null)
                {
                    selectedLimb.damagedSprite.Origin = sprite.Origin;
                }
                if (character.AnimController.IsFlipped)
                {
                    origin.X = Math.Abs(origin.X - sourceRect.Width);
                }
                var relativeOrigin = new Vector2(sprite.Origin.X / sourceRect.Width, sprite.Origin.Y / sourceRect.Height);
                TryUpdateLimbParam(selectedLimb, "origin", relativeOrigin);
            }
        }

        private void DrawRagdollEditor(SpriteBatch spriteBatch, float deltaTime)
        {
            bool ctrlDown = PlayerInput.GetKeyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt);
            if (!ctrlDown && editJointPositions)
            {
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2 - 150, 20), "PRESS Left Alt TO MANIPULATE THE OTHER END OF THE JOINT", Color.White, Color.Black * 0.5f, 10, GUI.Font);
            }
            foreach (Limb limb in character.AnimController.Limbs)
            {
                Vector2 limbScreenPos = SimToScreen(limb.SimPosition);
                foreach (var joint in character.AnimController.LimbJoints)
                {
                    Vector2 jointPos = Vector2.Zero;
                    Vector2 otherPos = Vector2.Zero;
                    Vector2 jointDir = Vector2.Zero;
                    Vector2 anchorPosA = ConvertUnits.ToDisplayUnits(joint.LocalAnchorA);
                    Vector2 anchorPosB = ConvertUnits.ToDisplayUnits(joint.LocalAnchorB);
                    Vector2 up = -VectorExtensions.Forward(limb.Rotation);
                    if (joint.BodyA == limb.body.FarseerBody)
                    {
                        jointPos = anchorPosA;
                        otherPos = anchorPosB;
                    }
                    else if (joint.BodyB == limb.body.FarseerBody)
                    {
                        jointPos = anchorPosB;
                        otherPos = anchorPosA;
                    }
                    else
                    {
                        continue;
                    }
                    var f = Vector2.Transform(jointPos, Matrix.CreateRotationZ(limb.Rotation));
                    f.Y = -f.Y;
                    Vector2 tformedJointPos = limbScreenPos + f * Cam.Zoom;
                    ShapeExtensions.DrawPoint(spriteBatch, limbScreenPos, Color.Black, size: 5);
                    ShapeExtensions.DrawPoint(spriteBatch, limbScreenPos, Color.White, size: 1);
                    GUI.DrawLine(spriteBatch, limbScreenPos, tformedJointPos, Color.Black, width: 3);
                    GUI.DrawLine(spriteBatch, limbScreenPos, tformedJointPos, Color.White, width: 1);
                    if (editJointLimits)
                    {
                        GetToggleWidget($"{joint.jointParams.Name} limits toggle ragdoll", $"{joint.jointParams.Name} limits toggle spritesheet", joint)
                            .Draw(spriteBatch, deltaTime, tformedJointPos);
                        if (joint.LimitEnabled)
                        {
                            DrawJointLimitWidgets(spriteBatch, joint, tformedJointPos, autoFreeze: true, rotationOffset: character.AnimController.Collider.Rotation);
                        }
                    }
                    if (editJointPositions)
                    {
                        if (ctrlDown && joint.BodyA == limb.body.FarseerBody)
                        {
                            continue;
                        }
                        if (!ctrlDown && joint.BodyB == limb.body.FarseerBody)
                        {
                            continue;
                        }
                        Color color = joint.BodyA == limb.body.FarseerBody ? Color.Red : Color.Blue;
                        var widgetSize = new Vector2(5, 5);
                        var rect = new Rectangle((tformedJointPos - widgetSize / 2).ToPoint(), widgetSize.ToPoint());
                        GUI.DrawRectangle(spriteBatch, tformedJointPos - widgetSize / 2, widgetSize, color, true);
                        var inputRect = rect;
                        inputRect.Inflate(widgetSize.X, widgetSize.Y);
                        //GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + up * 20, Color.White);
                        if (inputRect.Contains(PlayerInput.MousePosition))
                        {
                            //GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + up * 20, Color.White, width: 3);
                            GUI.DrawLine(spriteBatch, limbScreenPos, tformedJointPos, Color.Yellow, width: 3);
                            GUI.DrawRectangle(spriteBatch, inputRect, Color.Red);
                            GUI.DrawString(spriteBatch, tformedJointPos + new Vector2(widgetSize.X, -widgetSize.Y) * 2, jointPos.FormatAsZeroDecimal(), Color.White, Color.Black * 0.5f);
                            if (PlayerInput.LeftButtonHeld())
                            {
                                if (autoFreeze)
                                {
                                    freeze = true;
                                }
                                Vector2 input = ConvertUnits.ToSimUnits(scaledMouseSpeed) / Cam.Zoom;
                                input.Y = -input.Y;
                                input = input.TransformVector(-up);
                                if (joint.BodyA == limb.body.FarseerBody)
                                {
                                    joint.LocalAnchorA += input;
                                    TryUpdateJointParam(joint, "limb1anchor", ConvertUnits.ToDisplayUnits(joint.LocalAnchorA));
                                }
                                else
                                {
                                    joint.LocalAnchorB += input;
                                    TryUpdateJointParam(joint, "limb2anchor", ConvertUnits.ToDisplayUnits(joint.LocalAnchorB));
                                }
                            }
                            else
                            {
                                freeze = freezeToggle.Selected;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Spritesheet
        private List<Texture2D> textures;
        private List<Texture2D> Textures
        {
            get
            {
                if (textures == null)
                {
                    CreateTextures();
                }
                return textures;
            }
        }
        private List<string> texturePaths;
        private void CreateTextures()
        {
            textures = new List<Texture2D>();
            texturePaths = new List<string>();
            foreach (Limb limb in character.AnimController.Limbs)
            {
                if (limb.sprite == null || texturePaths.Contains(limb.sprite.FilePath)) { continue; }
                textures.Add(limb.sprite.Texture);
                texturePaths.Add(limb.sprite.FilePath);
            }
        }

        private void DrawSpritesheetEditor(SpriteBatch spriteBatch, float deltaTime)
        {
            //TODO: allow to zoom the sprite sheet
            //TODO: separate or combine the controls for the limbs that share a texture?
            int y = 30;
            int x = 30;
            for (int i = 0; i < Textures.Count; i++)
            {
                spriteBatch.Draw(Textures[i], new Vector2(x, y), Color.White);
                foreach (Limb limb in character.AnimController.Limbs)
                {
                    if (limb.sprite == null || limb.sprite.FilePath != texturePaths[i]) continue;
                    Rectangle rect = limb.sprite.SourceRect;
                    rect.X += x;
                    rect.Y += y;
                    GUI.DrawRectangle(spriteBatch, rect, Color.Red);
                    Vector2 origin = limb.sprite.Origin;
                    Vector2 limbBodyPos = new Vector2(rect.X + origin.X, rect.Y + origin.Y);
                    // The origin is manipulated when the character is flipped. We have to undo it here.
                    if (character.AnimController.Dir < 0)
                    {
                        limbBodyPos.X = rect.X + rect.Width - origin.X;
                    }
                    if (editRagdoll)
                    {
                        DrawSpritesheetRagdollEditor(spriteBatch, deltaTime, limb, limbBodyPos);
                    }
                    if (editSpriteDimensions)
                    {
                        // Draw the sprite origins
                        GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitY * 5.0f, limbBodyPos - Vector2.UnitY * 5.0f, Color.White, width: 3);
                        GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitX * 5.0f, limbBodyPos - Vector2.UnitX * 5.0f, Color.White, width: 3);
                        GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitY * 5.0f, limbBodyPos - Vector2.UnitY * 5.0f, Color.Red);
                        GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitX * 5.0f, limbBodyPos - Vector2.UnitX * 5.0f, Color.Red);
                        // Draw the  source rect widgets
                        int widgetSize = 5;
                        Vector2 stringOffset = new Vector2(5, 14);
                        var topLeft = rect.Location.ToVector2();
                        var topRight = new Vector2(topLeft.X + rect.Width, topLeft.Y);
                        var bottomRight = new Vector2(topRight.X, topRight.Y + rect.Height);
                        //var bottomLeft = new Vector2(topLeft.X, bottomRight.Y);
                        DrawWidget(spriteBatch, topLeft, WidgetType.Rectangle, widgetSize, Color.Red, "Position", () =>
                        {
                            // Adjust the source rect location
                            var newRect = limb.sprite.SourceRect;
                            var newLocation = new Vector2(PlayerInput.MousePosition.X - x, PlayerInput.MousePosition.Y - y);
                            newRect.Location = newLocation.ToPoint();
                            limb.sprite.SourceRect = newRect;
                            if (limb.damagedSprite != null)
                            {
                                limb.damagedSprite.SourceRect = limb.sprite.SourceRect;
                            }
                            TryUpdateLimbParam(limb, "sourcerect", newRect);
                            GUI.DrawString(spriteBatch, topLeft + new Vector2(stringOffset.X, -stringOffset.Y * 1.5f), limb.sprite.SourceRect.Location.ToString(), Color.White, Color.Black * 0.5f);
                        });
                        DrawWidget(spriteBatch, bottomRight, WidgetType.Rectangle, widgetSize, Color.White, "Size", () =>
                        {
                            // Adjust the source rect width and height, and the sprite size.
                            var newRect = limb.sprite.SourceRect;
                            int width = (int)PlayerInput.MousePosition.X - rect.X;
                            int height = (int)PlayerInput.MousePosition.Y - rect.Y;
                            int dx = newRect.Width - width;
                            newRect.Width = width;
                            newRect.Height = height;
                            limb.sprite.SourceRect = newRect;
                            limb.sprite.size = new Vector2(width, height);
                            // Also the origin should be adjusted to the new width, so that it will remain at the same position relative to the source rect location.
                            limb.sprite.Origin = new Vector2(origin.X - dx, origin.Y);
                            if (limb.damagedSprite != null)
                            {
                                limb.damagedSprite.SourceRect = limb.sprite.SourceRect;
                                limb.damagedSprite.Origin = limb.sprite.Origin;
                            }
                            if (character.AnimController.IsFlipped)
                            {
                                origin.X = Math.Abs(origin.X - newRect.Width);
                            }
                            var relativeOrigin = new Vector2(origin.X / newRect.Width, origin.Y / newRect.Height);
                            TryUpdateLimbParam(limb, "origin", relativeOrigin);
                            TryUpdateLimbParam(limb, "sourcerect", newRect);
                            GUI.DrawString(spriteBatch, bottomRight + stringOffset, limb.sprite.size.FormatAsZeroDecimal(), Color.White, Color.Black * 0.5f);
                        });
                        if (PlayerInput.LeftButtonHeld() && selectedWidget == null)
                        {
                            if (rect.Contains(PlayerInput.MousePosition))
                            {
                                var input = scaledMouseSpeed;
                                input.X *= character.AnimController.Dir;
                                // Adjust the sprite origin
                                origin += input;
                                var sprite = limb.sprite;
                                var sourceRect = sprite.SourceRect;
                                var max = new Vector2(sourceRect.Width, sourceRect.Height);
                                sprite.Origin = origin.Clamp(Vector2.Zero, max);
                                if (limb.damagedSprite != null)
                                {
                                    limb.damagedSprite.Origin = limb.sprite.Origin;
                                }
                                if (character.AnimController.IsFlipped)
                                {
                                    origin.X = Math.Abs(origin.X - sourceRect.Width);
                                }
                                var relativeOrigin = new Vector2(origin.X / sourceRect.Width, origin.Y / sourceRect.Height);
                                TryUpdateLimbParam(limb, "origin", relativeOrigin);
                                GUI.DrawString(spriteBatch, limbBodyPos + new Vector2(10, -10), relativeOrigin.FormatAsDoubleDecimal(), Color.White, Color.Black * 0.5f);
                            }
                        }
                    }
                }
                y += Textures[i].Height;
            }
        }

        private void DrawSpritesheetRagdollEditor(SpriteBatch spriteBatch, float deltaTime, Limb limb, Vector2 limbScreenPos, float spriteRotation = 0)
        {
            foreach (var joint in character.AnimController.LimbJoints)
            {
                Vector2 jointPos = Vector2.Zero;
                Vector2 anchorPosA = ConvertUnits.ToDisplayUnits(joint.LocalAnchorA);
                Vector2 anchorPosB = ConvertUnits.ToDisplayUnits(joint.LocalAnchorB);
                if (joint.BodyA == limb.body.FarseerBody)
                {
                    jointPos = anchorPosA;
                }
                else if (joint.BodyB == limb.body.FarseerBody)
                {
                    jointPos = anchorPosB;
                }
                else
                {
                    continue;
                }
                Vector2 tformedJointPos = jointPos /= RagdollParams.JointScale;
                tformedJointPos.Y = -tformedJointPos.Y;
                tformedJointPos.X *= character.AnimController.Dir;
                tformedJointPos += limbScreenPos;
                if (editJointLimits)
                {
                    //if (joint.BodyA == limb.body.FarseerBody)
                    //{
                    //    float a1 = joint.UpperLimit - MathHelper.PiOver2;
                    //    float a2 = joint.LowerLimit - MathHelper.PiOver2;
                    //    float a3 = (a1 + a2) / 2.0f;
                    //    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a1), -(float)Math.Sin(a1)) * 30.0f, Color.Yellow);
                    //    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a2), -(float)Math.Sin(a2)) * 30.0f, Color.Cyan);
                    //    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a3), -(float)Math.Sin(a3)) * 30.0f, Color.Black);
                    //}
                    if (joint.BodyA == limb.body.FarseerBody)
                    {
                        GetToggleWidget($"{joint.jointParams.Name} limits toggle spritesheet", $"{joint.jointParams.Name} limits toggle ragdoll", joint)
                            .Draw(spriteBatch, deltaTime, tformedJointPos);
                        if (joint.LimitEnabled)
                        {
                            DrawJointLimitWidgets(spriteBatch, joint, tformedJointPos, autoFreeze: false);
                        }
                    }
                }
                if (editJointPositions)
                {
                    Color color = joint.BodyA == limb.body.FarseerBody ? Color.Red : Color.Blue;
                    Vector2 widgetSize = new Vector2(5.0f, 5.0f); ;
                    var rect = new Rectangle((tformedJointPos - widgetSize / 2).ToPoint(), widgetSize.ToPoint());
                    var inputRect = rect;
                    inputRect.Inflate(widgetSize.X * 0.75f, widgetSize.Y * 0.75f);
                    GUI.DrawRectangle(spriteBatch, rect, color, isFilled: true);
                    if (inputRect.Contains(PlayerInput.MousePosition))
                    {          
                        GUI.DrawString(spriteBatch, tformedJointPos + Vector2.One * 10.0f, $"{jointPos.FormatAsZeroDecimal()}", Color.White, Color.Black * 0.5f);
                        GUI.DrawRectangle(spriteBatch, inputRect, color);
                        if (PlayerInput.LeftButtonHeld())
                        {
                            Vector2 input = ConvertUnits.ToSimUnits(scaledMouseSpeed);
                            input.Y = -input.Y;
                            input.X *= character.AnimController.Dir;
                            input *= limb.Scale;
                            if (joint.BodyA == limb.body.FarseerBody)
                            {
                                joint.LocalAnchorA += input;
                                TryUpdateJointParam(joint, "limb1anchor", ConvertUnits.ToDisplayUnits(joint.LocalAnchorA));
                            }
                            else
                            {
                                joint.LocalAnchorB += input;
                                TryUpdateJointParam(joint, "limb2anchor", ConvertUnits.ToDisplayUnits(joint.LocalAnchorB));
                            }
                        }
                    }
                }
            }
        }

        private void DrawJointLimitWidgets(SpriteBatch spriteBatch, LimbJoint joint, Vector2 drawPos, bool autoFreeze, float rotationOffset = 0)
        {
            // The joint limits are flipped and inversed when the character is flipped, so we have to handle it here, because we don't want it to affect the user interface.
            if (character.AnimController.IsFlipped)
            {
                DrawCircularWidget(spriteBatch, drawPos, MathHelper.ToDegrees(-joint.UpperLimit), "Upper Limit", Color.Cyan, angle =>
                {
                    joint.UpperLimit = MathHelper.ToRadians(-angle);
                    TryUpdateJointParam(joint, "upperlimit", -angle);
                }, rotationOffset: rotationOffset, autoFreeze: autoFreeze);
                DrawCircularWidget(spriteBatch, drawPos, MathHelper.ToDegrees(-joint.LowerLimit), "Lower Limit", Color.Yellow, angle =>
                {
                    joint.LowerLimit = MathHelper.ToRadians(-angle);
                    TryUpdateJointParam(joint, "lowerlimit", -angle);
                }, rotationOffset: rotationOffset, autoFreeze: autoFreeze);
            }
            else
            {
                DrawCircularWidget(spriteBatch, drawPos, MathHelper.ToDegrees(joint.UpperLimit), "Upper Limit", Color.Yellow, angle =>
                {
                    joint.UpperLimit = MathHelper.ToRadians(angle);
                    TryUpdateJointParam(joint, "upperlimit", angle);
                }, rotationOffset: rotationOffset, autoFreeze: autoFreeze);
                DrawCircularWidget(spriteBatch, drawPos, MathHelper.ToDegrees(joint.LowerLimit), "Lower Limit", Color.Cyan, angle =>
                {
                    joint.LowerLimit = MathHelper.ToRadians(angle);
                    TryUpdateJointParam(joint, "lowerlimit", angle);
                }, rotationOffset: rotationOffset, autoFreeze: autoFreeze);
            }
        }
        #endregion

        #region Widgets as methods
        private void DrawCircularWidget(SpriteBatch spriteBatch, Vector2 drawPos, float value, string toolTip, Color color, Action<float> onClick,
            float circleRadius = 30, int widgetSize = 10, float rotationOffset = 0, bool clockWise = true, bool displayAngle = true, bool autoFreeze = false)
        {
            var angle = value;
            if (!MathUtils.IsValid(angle))
            {
                angle = 0;
            }
            var up = -VectorExtensions.Forward(rotationOffset, circleRadius);
            var widgetDrawPos = drawPos + up;
            widgetDrawPos = MathUtils.RotatePointAroundTarget(widgetDrawPos, drawPos, angle, clockWise);
            GUI.DrawLine(spriteBatch, drawPos, widgetDrawPos, color);
            DrawWidget(spriteBatch, widgetDrawPos, WidgetType.Rectangle, 10, color, toolTip, () =>
            {
                GUI.DrawLine(spriteBatch, drawPos, drawPos + up, Color.Red);
                ShapeExtensions.DrawCircle(spriteBatch, drawPos, circleRadius, 40, color, thickness: 1);
                var rotationOffsetInDegrees = MathHelper.ToDegrees(MathUtils.WrapAnglePi(rotationOffset));
                // Collider rotation is counter-clockwise, todo: this should be handled before passing the arguments
                var transformedRot = clockWise ? angle - rotationOffsetInDegrees : angle + rotationOffsetInDegrees;
                if (transformedRot > 360)
                {
                    transformedRot -= 360;
                }
                else if (transformedRot < -360)
                {
                    transformedRot += 360;
                }
                //GUI.DrawString(spriteBatch, drawPos + Vector2.UnitX * 30, rotationOffsetInDegrees.FormatAsInt(), Color.Red);
                //GUI.DrawString(spriteBatch, drawPos + Vector2.UnitX * 30, transformedRot.FormatAsInt(), Color.Red);
                var input = scaledMouseSpeed * 1.5f;
                float x = input.X;
                float y = input.Y;
                if (clockWise)
                {
                    if ((transformedRot > 90 && transformedRot < 270) || (transformedRot < -90 && transformedRot > -270))
                    {
                        x = -x;
                    }
                    if (transformedRot > 180 || (transformedRot < 0 && transformedRot > -180))
                    {
                        y = -y;
                    }
                }
                else
                {
                    if (transformedRot < 90 && transformedRot > -90)
                    {
                        x = -x;
                    }
                    if (transformedRot < 0 && transformedRot > -180)
                    {
                        y = -y;
                    }
                }
                angle += x + y;
                if (angle > 360 || angle < -360)
                {
                    angle = 0;
                }
                if (displayAngle)
                {
                    GUI.DrawString(spriteBatch, drawPos, angle.FormatAsInt(), Color.Black, backgroundColor: color, font: GUI.SmallFont);
                }
                onClick(angle);
            }, autoFreeze);
        }

        public enum WidgetType { Rectangle, Circle }
        private string selectedWidget;
        private void DrawWidget(SpriteBatch spriteBatch, Vector2 drawPos, WidgetType widgetType, int size, Color color, string name, Action onPressed, bool autoFreeze = false)
        {
            var drawRect = new Rectangle((int)drawPos.X - size / 2, (int)drawPos.Y - size / 2, size, size);
            var inputRect = drawRect;
            inputRect.Inflate(size / 2, size / 2);
            bool isMouseOn = inputRect.Contains(PlayerInput.MousePosition);
            // Unselect
            if (!isMouseOn && selectedWidget == name)
            {
                selectedWidget = null;
            }
            bool isSelected = isMouseOn && (selectedWidget == null || selectedWidget == name);
            switch (widgetType)
            {
                case WidgetType.Rectangle:
                    GUI.DrawRectangle(spriteBatch, drawRect, color, false, thickness: isSelected ? 3 : 1);
                    break;
                case WidgetType.Circle:
                    ShapeExtensions.DrawCircle(spriteBatch, drawPos, size / 2, 40, color, thickness: isSelected ? 3 : 1);
                    break;
                default: throw new NotImplementedException(widgetType.ToString());
            }
            if (isSelected)
            {
                selectedWidget = name;
                // Label/tooltip
                GUI.DrawString(spriteBatch, new Vector2(drawRect.Right + 5, drawRect.Y - drawRect.Height / 2), name, Color.White, Color.Black * 0.5f);
                if (PlayerInput.LeftButtonHeld())
                {
                    if (autoFreeze)
                    {
                        freeze = true;
                    }
                    onPressed();
                }
                else
                {
                    freeze = freezeToggle.Selected;
                }
            }
        }
        #endregion

        #region Widgets as classes (experimental)
        private Dictionary<string, Widget> widgets = new Dictionary<string, Widget>();

        private Widget GetToggleWidget(string id, string linkedId, LimbJoint joint)
        {
            // Joint creation method
            Widget CreateJointLimitToggle(string ID, LimbJoint j)
            {
                var widget = new Widget(ID, Widget.Shape.Circle) { size = 10 };
                widget.refresh = () =>
                {
                    if (j.LimitEnabled)
                    {
                        widget.tooltip = j.jointParams.Name + " Disable Joint Limits";
                        widget.color = Color.Green;
                    }
                    else
                    {
                        widget.tooltip = j.jointParams.Name + " Enable Joint Limits";
                        widget.color = Color.Red;
                    }
                };
                widget.refresh();
                widget.Clicked += () =>
                {
                    j.LimitEnabled = !j.LimitEnabled;
                    TryUpdateJointParam(j, "limitenabled", j.LimitEnabled);
                    if (j.LimitEnabled)
                    {
                        if (float.IsNaN(j.jointParams.UpperLimit))
                        {
                            joint.UpperLimit = 0;
                            TryUpdateJointParam(j, "upperlimit", 0);
                        }
                        if (float.IsNaN(j.jointParams.LowerLimit))
                        {
                            joint.LowerLimit = 0;
                            TryUpdateJointParam(j, "lowerlimit", 0);
                        }
                    }
                    widget.refresh();
                    if (widget.linkedWidget != null)
                    {
                        widget.linkedWidget.refresh();
                    }
                };
                widgets.Add(ID, widget);
                return widget;
            }
            // Handle joint linking and create the joints
            if (!widgets.TryGetValue(id, out Widget toggleWidget))
            {
                if (!widgets.TryGetValue(linkedId, out Widget linkedWidget))
                {
                    linkedWidget = CreateJointLimitToggle(linkedId, joint);
                }
                toggleWidget = CreateJointLimitToggle(id, joint);
                toggleWidget.linkedWidget = linkedWidget;
                linkedWidget.linkedWidget = toggleWidget;
            }
            return toggleWidget;
        }

        private class Widget
        {
            public enum Shape
            {
                Rectangle,
                Circle
            }

            public Shape shape;
            public string tooltip;
            public Rectangle DrawRect => new Rectangle((int)DrawPos.X - size / 2, (int)DrawPos.Y - size / 2, size, size);
            public Rectangle InputRect
            {
                get
                {
                    var inputRect = DrawRect;
                    inputRect.Inflate(inputAreaMargin.X, inputAreaMargin.Y);
                    return inputRect;
                }
            }

            public Vector2 DrawPos { get; private set; }
            public int size = 10;
            /// <summary>
            /// Used only for circles.
            /// </summary>
            public int sides = 40;
            /// <summary>
            /// Currently used only for rectangles.
            /// </summary>
            public bool isFilled;
            public Point inputAreaMargin;
            public Color color = Color.Red;
            public Color textColor = Color.White;
            public Color textBackgroundColor = Color.Black * 0.5f;
            public bool autoFreeze;
            public readonly string id;

            public event Action Hovered;
            public event Action Clicked;
            public event Action MouseDown;
            public event Action MouseUp;
            public event Action MouseHeld;
            public event Action<SpriteBatch, float> PreDraw;
            public event Action<SpriteBatch, float> PostDraw;

            public Action refresh;

            public bool IsSelected => selectedWidget == this;
            public bool IsControlled => IsSelected && PlayerInput.LeftButtonHeld();
            public bool IsMouseOver => GUI.MouseOn == null && InputRect.Contains(PlayerInput.MousePosition);
            public Vector2? tooltipOffset;
            
            public Widget linkedWidget;

            public static Widget selectedWidget;

            public Widget(string id, Shape shape)
            {
                this.id = id;
                this.shape = shape;
            }

            public virtual void Update(float deltaTime)
            {
                if (IsMouseOver)
                {
                    if (selectedWidget == null)
                    {
                        selectedWidget = this;
                    }
                    Hovered?.Invoke();
                }
                else if (selectedWidget == this)
                {
                    selectedWidget = null;
                }
                if (IsSelected)
                {
                    if (PlayerInput.LeftButtonHeld())
                    {
                        if (autoFreeze)
                        {
                            //freeze = true;
                        }
                        MouseHeld?.Invoke();
                    }
                    else
                    {
                        //freeze = freezeToggle.Selected;
                    }
                    if (PlayerInput.LeftButtonDown())
                    {
                        MouseDown?.Invoke();
                    }
                    if (PlayerInput.LeftButtonReleased())
                    {
                        MouseUp?.Invoke();
                    }
                    if (PlayerInput.LeftButtonClicked())
                    {
                        Clicked?.Invoke();
                    }
                }
            }

            public virtual void Draw(SpriteBatch spriteBatch, float deltaTime, Vector2 drawPos)
            {
                PreDraw?.Invoke(spriteBatch, deltaTime);
                DrawPos = drawPos;
                var drawRect = DrawRect;
                switch (shape)
                {
                    case Shape.Rectangle:
                        GUI.DrawRectangle(spriteBatch, drawRect, color, isFilled, thickness: IsSelected ? 3 : 1);
                        break;
                    case Shape.Circle:
                        ShapeExtensions.DrawCircle(spriteBatch, drawPos, size / 2, sides, color, thickness: IsSelected ? 3 : 1);
                        break;
                    default: throw new NotImplementedException(shape.ToString());
                }
                if (IsSelected)
                {
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        var offset = tooltipOffset ?? new Vector2(size, -size / 2);
                        GUI.DrawString(spriteBatch, drawPos + offset, tooltip, textColor, textBackgroundColor);
                    }
                }
                PostDraw?.Invoke(spriteBatch, deltaTime);
            }
        }

        //// TODO: test and fix
        //private class RadialWidget : Widget
        //{
        //    public float angle;
        //    public float circleRadius = 30;
        //    public float rotationOffset;
        //    public bool clockWise = true;
        //    public bool displayAngle = true;
        //    public Vector2 center;

        //    public RadialWidget(string id, Shape shape, Vector2 center) : base(id, center, shape)
        //    {
        //        this.center = center;
        //    }

        //    public override void Update(float deltaTime)
        //    {
        //        if (!MathUtils.IsValid(angle))
        //        {
        //            angle = 0;
        //        }
        //        var up = -VectorExtensions.Forward(rotationOffset, circleRadius);
        //        drawPos = center + up;
        //        drawPos = MathUtils.RotatePointAroundTarget(drawPos, center, angle, clockWise);
        //        base.Update(deltaTime);
        //        if (IsControlled)
        //        {
        //            var rotationOffsetInDegrees = MathHelper.ToDegrees(MathUtils.WrapAnglePi(rotationOffset));
        //            // Collider rotation is counter-clockwise, todo: this should be handled before passing the arguments
        //            var transformedRot = clockWise ? angle - rotationOffsetInDegrees : angle + rotationOffsetInDegrees;
        //            if (transformedRot > 360)
        //            {
        //                transformedRot -= 360;
        //            }
        //            else if (transformedRot < -360)
        //            {
        //                transformedRot += 360;
        //            }
        //            var input = PlayerInput.MouseSpeed * 1.5f;
        //            float x = input.X;
        //            float y = input.Y;
        //            if (clockWise)
        //            {
        //                if ((transformedRot > 90 && transformedRot < 270) || (transformedRot < -90 && transformedRot > -270))
        //                {
        //                    x = -x;
        //                }
        //                if (transformedRot > 180 || (transformedRot < 0 && transformedRot > -180))
        //                {
        //                    y = -y;
        //                }
        //            }
        //            else
        //            {
        //                if (transformedRot < 90 && transformedRot > -90)
        //                {
        //                    x = -x;
        //                }
        //                if (transformedRot < 0 && transformedRot > -180)
        //                {
        //                    y = -y;
        //                }
        //            }
        //            angle += x + y;
        //            if (angle > 360 || angle < -360)
        //            {
        //                angle = 0;
        //            }
        //        }
        //    }

        //    public override void Draw(SpriteBatch spriteBatch, float deltaTime)
        //    {
        //        base.Draw(spriteBatch, deltaTime);
        //        GUI.DrawLine(spriteBatch, drawPos, drawPos, color);
        //        // Draw controller widget
        //        if (IsSelected)
        //        {
        //            //var up = -VectorExtensions.Forward(rotationOffset, circleRadius);
        //            //GUI.DrawLine(spriteBatch, drawPos, drawPos + up, Color.Red);
        //            ShapeExtensions.DrawCircle(spriteBatch, drawPos, circleRadius, sides, color, thickness: 1);
        //            if (displayAngle)
        //            {
        //                GUI.DrawString(spriteBatch, drawPos, angle.FormatAsInt(), textColor, textBackgroundColor, font: GUI.SmallFont);
        //            }
        //        }
        //    }
        //}
        #endregion
    }
}
