﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;
using FarseerPhysics;

namespace Barotrauma
{
    class AnimationEditorScreen : Screen
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

        public override void Select()
        {
            base.Select();
            Submarine.RefreshSavedSubs();
            Submarine.MainSub = Submarine.SavedSubmarines.First(s => s.Name.Contains("AnimEditor"));
            Submarine.MainSub.Load(true);
            Submarine.MainSub.GodMode = true;
            CalculateMovementLimits();
            character = SpawnCharacter(Character.HumanConfigFile);
            AnimParams.ForEach(p => p.AddToEditor());
            CreateButtons();
        }

        #region Inifinite runner
        private int min;
        private int max;
        private void CalculateMovementLimits()
        {
            min = CurrentWalls.Select(w => w.Rect.Left).OrderBy(p => p).First();
            max = CurrentWalls.Select(w => w.Rect.Right).OrderBy(p => p).Last();
        }

        private List<Structure> _originalWalls;
        private List<Structure> OriginalWalls
        {
            get
            {
                if (_originalWalls == null)
                {
                    _originalWalls = Structure.WallList;
                }
                return _originalWalls;
            }
        }

        private List<Structure> clones = new List<Structure>();
        private List<Structure> previousWalls;

        private List<Structure> _currentWalls;
        private List<Structure> CurrentWalls
        {
            get
            {
                if (_currentWalls == null)
                {
                    _currentWalls = OriginalWalls;
                }
                return _currentWalls;
            }
            set
            {
                _currentWalls = value;
            }
        }

        private void CloneWalls(bool right)
        {
            previousWalls = CurrentWalls;
            if (previousWalls == null)
            {
                previousWalls = OriginalWalls;
            }
            if (clones.None())
            {
                OriginalWalls.ForEachMod(w => clones.Add(w.Clone() as Structure));
                CurrentWalls = clones;
            }
            else
            {
                // Select by position
                var lastWall = right ?
                    previousWalls.OrderBy(w => w.Rect.Right).Last() :
                    previousWalls.OrderBy(w => w.Rect.Left).First();

                CurrentWalls = clones.Contains(lastWall) ? clones : OriginalWalls;
            }
            if (CurrentWalls != OriginalWalls)
            {
                // Move the clones
                for (int i = 0; i < CurrentWalls.Count; i++)
                {
                    int amount = right ? previousWalls[i].Rect.Width : -previousWalls[i].Rect.Width;
                    CurrentWalls[i].Move(new Vector2(amount, 0));
                }
            }
            GameMain.World.ProcessChanges();
            CalculateMovementLimits();
        }
        #endregion

        #region Character spawning
        private int characterIndex = -1;
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
            return AllFiles[characterIndex];
        }

        private string GetPreviousConfigFile()
        {
            CheckAndGetIndex();
            ReduceIndex();
            return AllFiles[characterIndex];
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

        private Character SpawnCharacter(string configFile)
        {
            DebugConsole.NewMessage($"Trying to spawn {configFile}", Color.HotPink);
            spawnPosition = WayPoint.GetRandom(sub: Submarine.MainSub).WorldPosition;
            var character = Character.Create(configFile, spawnPosition, ToolBox.RandomSeed(8), hasAi: false);
            character.Submarine = Submarine.MainSub;
            character.AnimController.forceStanding = character.IsHumanoid;
            character.dontFollowCursor = true;
            Character.Controlled = character;
            float size = ConvertUnits.ToDisplayUnits(character.AnimController.Collider.radius * 2);
            float margin = 100;
            float distance = Vector2.Distance(spawnPosition, new Vector2(spawnPosition.X, OriginalWalls.First().WorldPosition.Y)) - margin;
            if (size > distance)
            {
                character.AnimController.Teleport(ConvertUnits.ToSimUnits(new Vector2(0, size * 1.5f)), Vector2.Zero);
            }
            GameMain.World.ProcessChanges();
            return character;
        }
        #endregion

        #region GUI
        private GUIFrame panel;
        private void CreateButtons()
        {
            if (panel != null)
            {
                panel.RectTransform.Parent = null;
            }
            panel = new GUIFrame(new RectTransform(new Vector2(0.1f, 0.9f), parent: Frame.RectTransform, anchor: Anchor.CenterRight) { RelativeOffset = new Vector2(0.01f, 0) });
            var layoutGroup = new GUILayoutGroup(new RectTransform(Vector2.One, panel.RectTransform));
            var charButtons = new GUIFrame(new RectTransform(new Vector2(1, 0.1f), parent: layoutGroup.RectTransform), style: null);
            var prevCharacterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1), charButtons.RectTransform, Anchor.TopLeft), "Previous \nCharacter");
            prevCharacterButton.OnClicked += (b, obj) =>
            {
                character = SpawnCharacter(GetPreviousConfigFile());
                ResetEditor();
                CreateButtons();
                return true;
            };
            var nextCharacterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1), charButtons.RectTransform, Anchor.TopRight), "Next \nCharacter");
            nextCharacterButton.OnClicked += (b, obj) =>
            {
                character = SpawnCharacter(GetNextConfigFile());
                ResetEditor();
                CreateButtons();
                return true;
            };
            // TODO: use tick boxes?
            var swimButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.AnimController.forceStanding ? "Swim" : "Grounded");
            swimButton.OnClicked += (b, obj) =>
            {
                character.AnimController.forceStanding = !character.AnimController.forceStanding;
                swimButton.Text = character.AnimController.forceStanding ? "Swim" : "Grounded";
                return true;
            };
            swimButton.Enabled = character.AnimController.CanWalk;
            var moveButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.OverrideMovement.HasValue ? "Stop" : "Move");
            moveButton.OnClicked += (b, obj) =>
            {
                character.OverrideMovement = character.OverrideMovement.HasValue ? null : new Vector2(-1, 0) as Vector2?;
                moveButton.Text = character.OverrideMovement.HasValue ? "Stop" : "Move";
                return true;
            };
            var speedButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.ForceRun ? "Slow" : "Fast");
            speedButton.OnClicked += (b, obj) =>
            {
                character.ForceRun = !character.ForceRun;
                speedButton.Text = character.ForceRun ? "Slow" : "Fast";
                return true;
            };
            var turnButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.dontFollowCursor ? "Enable Turning" : "Disable Turning");
            turnButton.OnClicked += (b, obj) =>
            {
                character.dontFollowCursor = !character.dontFollowCursor;
                turnButton.Text = character.dontFollowCursor ? "Enable Turning" : "Disable Turning";
                return true;
            };
            var saveButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), "Save");
            saveButton.OnClicked += (b, obj) =>
            {
                AnimParams.ForEach(p => p.Save());
                return true;
            };
            var resetButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), "Reset");
            resetButton.OnClicked += (b, obj) =>
            {
                AnimParams.ForEach(p => p.Reset());
                ResetEditor();
                return true;
            };
        }
        #endregion

        #region AnimParams
        private List<AnimationParams> AnimParams => character.AnimController.AllAnimParams;

        private void ResetEditor()
        {
            AnimationParams.CreateEditor();
            AnimParams.ForEach(p => p.AddToEditor());
        }
        #endregion

        public override void AddToGUIUpdateList()
        {
            base.AddToGUIUpdateList();
            AnimationParams.Editor.AddToGUIUpdateList();
        }

        public override void Update(double deltaTime)
        {
            base.Update(deltaTime);

            Submarine.MainSub.SetPrevTransform(Submarine.MainSub.Position);
            Submarine.MainSub.Update((float)deltaTime);

            PhysicsBody.List.ForEach(pb => pb.SetPrevTransform(pb.SimPosition, pb.Rotation));

            character.ControlLocalPlayer((float)deltaTime, Cam, false);
            character.Control((float)deltaTime, Cam);
            character.AnimController.UpdateAnim((float)deltaTime);
            character.AnimController.Update((float)deltaTime, Cam);

            if (character.Position.X < min)
            {
                CloneWalls(false);
            }
            else if (character.Position.X > max)
            {
                CloneWalls(true);
            }

            //Cam.TargetPos = Vector2.Zero;
            Cam.MoveCamera((float)deltaTime, allowMove: false, allowZoom: GUI.MouseOn == null);
            Cam.Position = character.Position;
 
            GameMain.World.Step((float)deltaTime);
        }

        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            base.Draw(deltaTime, graphics, spriteBatch);
            graphics.Clear(Color.CornflowerBlue);
            Cam.UpdateTransform(true);

            // Submarine
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, transformMatrix: Cam.Transform);
            Submarine.Draw(spriteBatch, true);
            spriteBatch.End();

            // Character
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, transformMatrix: Cam.Transform);
            character.Draw(spriteBatch);
            spriteBatch.End();

            // GUI
            spriteBatch.Begin(SpriteSortMode.Immediate, rasterizerState: GameMain.ScissorTestEnable);
            Structure wall = clones.FirstOrDefault();
            Vector2 indicatorPos = wall == null ? OriginalWalls.First().DrawPosition : wall.DrawPosition;
            GUI.DrawIndicator(spriteBatch, indicatorPos, Cam, 700, GUI.SubmarineIcon, Color.White);
            GUI.Draw((float)deltaTime, spriteBatch);

            DrawWidgetEditor(spriteBatch);
            //DrawJointEditor(spriteBatch);

            // Debug
            if (GameMain.DebugDraw)
            {
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

                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 280), $"Movement Limits: MIN: {min} MAX: {max}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 300), $"Clones: {clones.Count}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 320), $"Total amount of walls: {Structure.WallList.Count}", Color.White, font: GUI.SmallFont);

                // Collider
                var collider = character.AnimController.Collider;
                var colliderDrawPos = SimToScreenPoint(collider.SimPosition);
                Vector2 forward = Vector2.Transform(Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation));
                var endPos = SimToScreenPoint(collider.SimPosition + Vector2.Normalize(forward) * collider.radius);
                GUI.DrawLine(spriteBatch, colliderDrawPos, endPos, Color.LightGreen);
                ShapeExtensions.DrawCircle(spriteBatch, colliderDrawPos, (endPos - colliderDrawPos).Length(), 40, Color.LightGreen);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth - 300, 0), $"Collider rotation: {MathHelper.ToDegrees(collider.Rotation)}", Color.White, font: GUI.SmallFont);
            }
            spriteBatch.End();
        }

        #region Widgets
        private void DrawWidgetEditor(SpriteBatch spriteBatch)
        {
            var collider = character.AnimController.Collider;
            var charDrawPos = SimToScreenPoint(collider.SimPosition);
            var animParams = character.AnimController.CurrentAnimationParams;
            var groundedParams = animParams as GroundedMovementParams;
            var humanGroundedParams = animParams as HumanGroundedParams;
            var fishGroundedParams = animParams as FishGroundedParams;
            var fishSwimParams = animParams as FishSwimParams;
            var head = character.AnimController.GetLimb(LimbType.Head);
            var torso = character.AnimController.GetLimb(LimbType.Torso);
            var tail = character.AnimController.GetLimb(LimbType.Tail);
            var rightFoot = character.AnimController.GetLimb(LimbType.RightFoot);
            var leftFoot = character.AnimController.GetLimb(LimbType.LeftFoot);
            var legs = character.AnimController.GetLimb(LimbType.Legs);
            var thigh = character.AnimController.GetLimb(LimbType.RightThigh) ?? character.AnimController.GetLimb(LimbType.RightThigh);
            Point widgetSize = new Point(10, 10);
            Vector2 colliderBottom = character.AnimController.GetColliderBottom();
            Vector2 centerOfMass = character.AnimController.GetCenterOfMass();
            foreach (Limb limb in character.AnimController.Limbs)
            {
                Vector2 limbDrawPos = Cam.WorldToScreen(limb.WorldPosition);
                // Limb positions
                GUI.DrawLine(spriteBatch, limbDrawPos + Vector2.UnitY * 5.0f, limbDrawPos - Vector2.UnitY * 5.0f, Color.White);
                GUI.DrawLine(spriteBatch, limbDrawPos + Vector2.UnitX * 5.0f, limbDrawPos - Vector2.UnitX * 5.0f, Color.White);
            }
            if (head != null)
            {
                // Head angle
                DrawCircularWidget(spriteBatch, SimToScreenPoint(head.SimPosition), animParams.HeadAngle, "Head Angle", Color.White, angle => TryUpdateValue("headangle", angle), circleRadius: 25, rotationOffset: head.Rotation);
                // Head position and leaning
                if (animParams.IsGroundedAnimation)
                {
                    if (humanGroundedParams != null)
                    {
                        var widgetDrawPos = SimToScreenPoint(head.SimPosition.X - humanGroundedParams.HeadLeanAmount, head.pullJoint.WorldAnchorB.Y);
                        DrawWidget(widgetDrawPos.ToPoint(), spriteBatch, widgetSize, Color.Red, "Head", () =>
                        {
                            TryUpdateValue("headleanamount", humanGroundedParams.HeadLeanAmount + 0.01f * -PlayerInput.MouseSpeed.X);
                            TryUpdateValue("headposition", humanGroundedParams.HeadPosition + 0.015f * -PlayerInput.MouseSpeed.Y);
                        });
                        var origin = widgetDrawPos - new Vector2(widgetSize.X / 2, 0);
                        GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Red);
                    }
                    else
                    {
                        // TODO: implement head leaning on fishes?
                        Vector2 drawPoint = SimToScreenPoint(head.SimPosition.X, head.pullJoint.WorldAnchorB.Y);
                        DrawWidget(drawPoint.ToPoint(), spriteBatch, widgetSize, Color.Red, "Head Position",
                            () => TryUpdateValue("headposition", groundedParams.HeadPosition + 0.015f * -PlayerInput.MouseSpeed.Y));
                    }
                }
            }
            if (torso != null)
            {
                // Torso angle
                DrawCircularWidget(spriteBatch, SimToScreenPoint(torso.SimPosition), animParams.TorsoAngle, "Torso Angle", Color.White, angle => TryUpdateValue("torsoangle", angle), rotationOffset: torso.Rotation);
                if (animParams.IsGroundedAnimation)
                {
                    // Torso position and leaning
                    if (humanGroundedParams != null)
                    {
                        Vector2 drawPoint = SimToScreenPoint(torso.SimPosition.X - humanGroundedParams.TorsoLeanAmount, torso.SimPosition.Y);
                        DrawWidget(drawPoint.ToPoint(), spriteBatch, widgetSize, Color.Red, "Torso", () =>
                        {
                            TryUpdateValue("torsoleanamount", humanGroundedParams.TorsoLeanAmount + 0.01f * -PlayerInput.MouseSpeed.X);
                            TryUpdateValue("torsoposition", humanGroundedParams.TorsoPosition + 0.015f * -PlayerInput.MouseSpeed.Y);
                        });
                        var origin = drawPoint - new Vector2(widgetSize.X / 2, 0);
                        GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Red);
                    }
                    else
                    {
                        // TODO: implement torso leaning on fishes?
                        Vector2 drawPoint = SimToScreenPoint(torso.SimPosition.X, torso.pullJoint.WorldAnchorB.Y);
                        DrawWidget(drawPoint.ToPoint(), spriteBatch, widgetSize, Color.Red, "Torso Position",
                            () => TryUpdateValue("torsoposition", groundedParams.TorsoPosition + 0.015f * -PlayerInput.MouseSpeed.Y));
                    }
                }
            }
            if (tail != null && fishSwimParams != null)
            {
                float amplitudeMultiplier = 0.01f;
                float lengthMultiplier = 0.1f;

                // In screen space
                float x = charDrawPos.X + fishSwimParams.WaveLength / lengthMultiplier;
                float y = charDrawPos.Y + fishSwimParams.WaveAmplitude / amplitudeMultiplier;
                var widgetDrawPos = new Vector2(x, y);

                // In sim space (test)
                //Vector2 widgetSimPos = ScreenToSimPoint(widgetDrawPos);
                //var dir = Vector2.Transform(-Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation));
                //float length = (character.SimPosition - widgetSimPos).Length();
                //var transformedPos = widgetSimPos + dir * length;
                //widgetDrawPos = SimToScreenPoint(transformedPos);

                DrawWidget(widgetDrawPos.ToPoint(), spriteBatch, widgetSize, Color.Red, "Tail", () =>
                {
                    TryUpdateValue("waveamplitude", fishSwimParams.WaveAmplitude + PlayerInput.MouseSpeed.Y * amplitudeMultiplier);
                    TryUpdateValue("wavelength", fishSwimParams.WaveLength + PlayerInput.MouseSpeed.X * lengthMultiplier);
                    GUI.DrawLine(spriteBatch, charDrawPos, widgetDrawPos, Color.Red);
                });

                // In sim space, rotation works, but input is worse
                //Vector2 referencePoint = character.SimPosition;
                //float x = referencePoint.X + fishSwimParams.WaveLength * 0.1f;
                //float y = referencePoint.Y;//+ fishSwimParams.WaveAmplitude * amplitudeMultiplier;
                //Vector2 widgetSimPos = new Vector2(x, y);

                //var dir = referencePoint - widgetSimPos;
                //var transformedDir = Vector2.Transform(-Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation));
                //var transformedPos = widgetSimPos + transformedDir * dir.Length();

                //var widgetDrawPos = SimToScreenPoint(transformedPos);
                //DrawWidget(widgetDrawPos.ToPoint(), spriteBatch, widgetSize, Color.Red, "Tail", () =>
                //{
                //    TryUpdateValue("waveamplitude", fishSwimParams.WaveAmplitude);
                //    TryUpdateValue("wavelength", fishSwimParams.WaveLength + PlayerInput.MouseSpeed.X * 0.05f);
                //});

                //GUI.DrawLine(spriteBatch, charDrawPos, widgetDrawPos, Color.Red);
            }
            var foot = rightFoot ?? leftFoot;
            if (foot != null)
            {
                if (fishGroundedParams != null)
                {
                    DrawCircularWidget(spriteBatch, SimToScreenPoint(colliderBottom), fishGroundedParams.FootRotation, "Foot Rotation", Color.White, angle =>
                        TryUpdateValue("footrotation", angle), circleRadius: 20, rotationOffset: collider.Rotation);
                }
                if (groundedParams != null)
                {
                    float multiplier = 0.005f;
                    Vector2 referencePoint = SimToScreenPoint(colliderBottom);
                    Vector2 drawPoint = referencePoint - groundedParams.StepSize / multiplier;
                    var origin = drawPoint - new Vector2(widgetSize.X / 2, 0);
                    DrawWidget(drawPoint.ToPoint(), spriteBatch, widgetSize, Color.Red, "Step Size", () =>
                    {
                        TryUpdateValue("stepsize", groundedParams.StepSize -PlayerInput.MouseSpeed * multiplier);
                        GUI.DrawLine(spriteBatch, origin, referencePoint, Color.Red);
                    });
                    GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Red);
                }
            }
            if (legs != null || foot != null)
            {
                if (humanGroundedParams != null)
                {
                    // TODO: does not seem to have any effect
                    DrawCircularWidget(spriteBatch, SimToScreenPoint(colliderBottom), humanGroundedParams.LegCorrectionTorque, "Leg Correction Torque", Color.White, angle =>
                        TryUpdateValue("legcorrectiontorque", angle), circleRadius: 20, rotationOffset: collider.Rotation);
                }
            }
            if (thigh != null)
            {
                if (humanGroundedParams != null)
                {
                    DrawCircularWidget(spriteBatch, SimToScreenPoint(collider.SimPosition), humanGroundedParams.ThighCorrectionTorque, "Thigh Correction Torque", Color.White, angle =>
                        TryUpdateValue("thighcorrectiontorque", angle), circleRadius: 20, rotationOffset: collider.Rotation);
                }
            }
        }

        private void TryUpdateValue(string name, object value)
        {
            var animParams = character.AnimController.CurrentAnimationParams;
            if (animParams.SerializableProperties.TryGetValue(name, out SerializableProperty p))
            {
                UpdateValue(p, animParams, value);
            }
        }

        /// <summary>
        /// Note: currently only handles floats and vector2s.
        /// </summary>
        private void UpdateValue(SerializableProperty property, AnimationParams animationParams, object newValue)
        {
            if (!animationParams.SerializableEntityEditor.Fields.TryGetValue(property, out GUIComponent[] fields))
            {
                return;
            }
            if (newValue is float f)
            {
                foreach (var field in fields)
                {
                    if (field is GUINumberInput numInput)
                    {
                        if (numInput.InputType == GUINumberInput.NumberType.Float)
                        {
                            numInput.FloatValue = f;
                        }
                    }
                }
            }
            else if (newValue is Vector2 v)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field is GUINumberInput numInput)
                    {
                        if (numInput .InputType == GUINumberInput.NumberType.Float)
                        {
                            numInput.FloatValue = i == 0 ? v.X : v.Y;
                        }
                    }
                }
            }
        }

        private Vector2 ScreenToSimPoint(float x, float y) => ScreenToSimPoint(new Vector2(x, y));
        private Vector2 ScreenToSimPoint(Vector2 p) => ConvertUnits.ToSimUnits(Cam.ScreenToWorld(p));
        private Vector2 SimToScreenPoint(float x, float y) => SimToScreenPoint(new Vector2(x, y));
        private Vector2 SimToScreenPoint(Vector2 p) => Cam.WorldToScreen(ConvertUnits.ToDisplayUnits(p));

        private void DrawCircularWidget(SpriteBatch spriteBatch, Vector2 drawPos, float value, string toolTip, Color color, Action<float> onClick, float circleRadius = 30, int widgetSize = 10, float rotationOffset = 0)
        {
            var angle = value;
            if (!MathUtils.IsValid(angle))
            {
                angle = 0;
            }
            var forward = VectorExtensions.Forward(rotationOffset, circleRadius);
            var widgetDrawPos = drawPos - forward;
            widgetDrawPos = MathUtils.RotatePointAroundTarget(widgetDrawPos, drawPos, angle, clockWise: true);
            GUI.DrawLine(spriteBatch, drawPos, widgetDrawPos, color);
            DrawWidget(widgetDrawPos.ToPoint(), spriteBatch, new Point(widgetSize), color, toolTip, () =>
            {
                GUI.DrawLine(spriteBatch, drawPos, drawPos - forward, Color.Red);
                ShapeExtensions.DrawCircle(spriteBatch, drawPos, circleRadius, 40, color, thickness: 1);
                float x = PlayerInput.MouseSpeed.X * 1.5f;
                float y = PlayerInput.MouseSpeed.Y * 1.5f;
                var widgetRot = MathHelper.ToDegrees(-(float)Math.Atan2(forward.X, forward.Y));
                //DebugConsole.NewMessage(widgetRot.ToString(), Color.White);
                var transformedRot = angle + widgetRot;
                if ((transformedRot > 90 && transformedRot < 270) || (transformedRot < -90 && transformedRot > -270))
                {
                    x = -x;
                }
                if (transformedRot > 180 || (transformedRot < 0 && transformedRot > -180))
                {
                    y = -y;
                }
                angle += x + y;
                if (angle > 360 || angle < -360)
                {
                    angle = 0;
                }
                onClick(angle);
            });
        }

        private string selectedWidget;
        private void DrawWidget(Point drawPoint, SpriteBatch spriteBatch, Point size, Color color, string name, Action onPressed)
        {
            var drawRect = new Rectangle(new Point(drawPoint.X - size.X / 2, drawPoint.Y - size.Y / 2), size);
            var inputRect = drawRect;
            inputRect.Inflate(size.X, size.Y);
            bool isMouseOn = inputRect.Contains(PlayerInput.MousePosition);
            // Unselect
            if (!isMouseOn && selectedWidget == name)
            {
                selectedWidget = null;
            }
            bool isSelected = isMouseOn && (selectedWidget == null || selectedWidget == name);
            if (isSelected)
            {
                selectedWidget = name;
                // Label/tooltip
                GUI.DrawString(spriteBatch, new Vector2(drawRect.Right + 5, drawRect.Y - drawRect.Height / 2), name, Color.White, Color.Black * 0.5f);
                GUI.DrawRectangle(spriteBatch, drawRect, color, false, thickness: 3);
                if (PlayerInput.LeftButtonHeld())
                {
                    onPressed();
                }
            }
            else
            {
                GUI.DrawRectangle(spriteBatch, drawRect, color);
                //ShapeExtensions.DrawCircle(spriteBatch, drawPoint.ToVector2(), 10, 10, Color.White, thickness: 1);
            }
            // Bezier test
            //if (PlayerInput.LeftButtonHeld())
            //{
            //    Vector2 start = drawPoint.ToVector2();
            //    Vector2 end = start + new Vector2(50, 0);
            //    Vector2 dir = end - start;
            //    Vector2 control = start + dir / 2 + new Vector2(0, -20);
            //    var points = new Vector2[10];
            //    for (int i = 0; i < points.Length; i++)
            //    {
            //        float t = (float)i / (points.Length - 1);
            //        Vector2 pos = MathUtils.Bezier(start, control, end, t);
            //        points[i] = pos;
            //        //DebugConsole.NewMessage(i.ToString(), Color.White);
            //        //DebugConsole.NewMessage(t.ToString(), Color.Blue);
            //        //DebugConsole.NewMessage(pos.ToString(), Color.Red);
            //        ShapeExtensions.DrawPoint(spriteBatch, pos, Color.White, size: 2);
            //    }
            //}
        }
        #endregion

        #region Joint edit (test)
        private void DrawJointEditor(SpriteBatch spriteBatch)
        {
            foreach (Limb limb in character.AnimController.Limbs)
            {
                Vector2 limbBodyPos = Cam.WorldToScreen(limb.WorldPosition);
                GUI.DrawRectangle(spriteBatch, new Rectangle(limbBodyPos.ToPoint(), new Point(5, 5)), Color.Red);

                DrawJoints(spriteBatch, limb, limbBodyPos);

                GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitY * 5.0f, limbBodyPos - Vector2.UnitY * 5.0f, Color.White);
                GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitX * 5.0f, limbBodyPos - Vector2.UnitX * 5.0f, Color.White);

                if (Vector2.Distance(PlayerInput.MousePosition, limbBodyPos) < 5.0f && PlayerInput.LeftButtonHeld())
                {
                    limb.sprite.Origin += PlayerInput.MouseSpeed;
                }
            }
        }

        private void DrawJoints(SpriteBatch spriteBatch, Limb limb, Vector2 limbBodyPos)
        {
            foreach (var joint in character.AnimController.LimbJoints)
            {
                Vector2 jointPos = Vector2.Zero;

                if (joint.BodyA == limb.body.FarseerBody)
                {
                    jointPos = ConvertUnits.ToDisplayUnits(joint.LocalAnchorA);

                }
                else if (joint.BodyB == limb.body.FarseerBody)
                {
                    jointPos = ConvertUnits.ToDisplayUnits(joint.LocalAnchorB);
                }
                else
                {
                    continue;
                }

                Vector2 tformedJointPos = jointPos /= limb.Scale;
                tformedJointPos.Y = -tformedJointPos.Y;
                tformedJointPos += limbBodyPos;

                if (joint.BodyA == limb.body.FarseerBody)
                {
                    float a1 = joint.UpperLimit - MathHelper.PiOver2;
                    float a2 = joint.LowerLimit - MathHelper.PiOver2;
                    float a3 = (a1 + a2) / 2.0f;
                    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a1), -(float)Math.Sin(a1)) * 30.0f, Color.Green);
                    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a2), -(float)Math.Sin(a2)) * 30.0f, Color.DarkGreen);

                    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a3), -(float)Math.Sin(a3)) * 30.0f, Color.LightGray);
                }

                GUI.DrawRectangle(spriteBatch, tformedJointPos, new Vector2(5.0f, 5.0f), Color.Red, true);
                if (Vector2.Distance(PlayerInput.MousePosition, tformedJointPos) < 10.0f)
                {
                    GUI.DrawString(spriteBatch, tformedJointPos + Vector2.One * 10.0f, jointPos.ToString(), Color.White, Color.Black * 0.5f);
                    GUI.DrawRectangle(spriteBatch, tformedJointPos - new Vector2(3.0f, 3.0f), new Vector2(11.0f, 11.0f), Color.Red, false);
                    if (PlayerInput.LeftButtonHeld())
                    {
                        Vector2 speed = ConvertUnits.ToSimUnits(PlayerInput.MouseSpeed);
                        speed.Y = -speed.Y;
                        if (joint.BodyA == limb.body.FarseerBody)
                        {
                            joint.LocalAnchorA += speed;
                        }
                        else
                        {
                            joint.LocalAnchorB += speed;
                        }
                    }
                }
            }
        }
        #endregion
    }
}
