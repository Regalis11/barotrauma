﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma.Networking
{
    partial class GameServer : NetworkMember, ISerializableEntity
    {
        private GUIFrame settingsFrame;
        private GUIFrame[] settingsTabs;
        private int settingsTabIndex;

        enum SettingsTab
        {
            Rounds,
            Server,
            Banlist,
            Whitelist
        }
        
        private void CreateSettingsFrame()
        {
            settingsFrame = new GUIFrame(new RectTransform(Vector2.One, GUI.Canvas), style: null, color: Color.Black * 0.5f);
            new GUIButton(new RectTransform(Vector2.One, settingsFrame.RectTransform), "", style: null)
            {
                OnClicked = ToggleSettingsFrame
            };

            GUIFrame innerFrame = new GUIFrame(new RectTransform(new Vector2(0.2f, 0.4f), settingsFrame.RectTransform, Anchor.Center) { MinSize = new Point(400, 430) });
            GUIFrame paddedFrame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), innerFrame.RectTransform, Anchor.Center), style: null);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), paddedFrame.RectTransform), "Settings", font: GUI.LargeFont);

            var buttonArea = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.07f), paddedFrame.RectTransform) { RelativeOffset = new Vector2(0.0f, 0.1f) }, isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.01f
            };

            var tabValues = Enum.GetValues(typeof(SettingsTab)).Cast<SettingsTab>().ToArray();
            string[] tabNames = new string[tabValues.Count()];
            for (int i = 0; i<tabNames.Length; i++)
            {
                tabNames[i] = TextManager.Get("ServerSettings" + tabValues[i] + "Tab");
            }
            settingsTabs = new GUIFrame[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                settingsTabs[i] = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.79f), paddedFrame.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(0.0f, 0.05f) },
                    style: "InnerFrame");

                var tabButton = new GUIButton(new RectTransform(new Vector2(0.2f, 1.0f), buttonArea.RectTransform), tabNames[i])
                {
                    UserData = i,
                    OnClicked = SelectSettingsTab
                };
            }
            
            SelectSettingsTab(null, 0);

            var closeButton = new GUIButton(new RectTransform(new Vector2(0.25f, 0.05f), paddedFrame.RectTransform, Anchor.BottomRight), TextManager.Get("Close"))
            {
                OnClicked = ToggleSettingsFrame
            };

            //--------------------------------------------------------------------------------
            //                              game settings 
            //--------------------------------------------------------------------------------
            
            var roundsTab = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), settingsTabs[(int)SettingsTab.Rounds].RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.02f
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), TextManager.Get("ServerSettingsSubSelection"));
            var selectionFrame = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };
            for (int i = 0; i < 3; i++)
            {
                var selectionTick = new GUITickBox(new RectTransform(new Vector2(0.3f, 1.0f), selectionFrame.RectTransform), ((SelectionMode)i).ToString(), font: GUI.SmallFont)
                {
                    Selected = i == (int)subSelectionMode,
                    OnSelected = SwitchSubSelection,
                    UserData = (SelectionMode)i
                };
            }
            
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), TextManager.Get("ServerSettingsModeSelection"));
            selectionFrame = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };
            for (int i = 0; i < 3; i++)
            {
                var selectionTick = new GUITickBox(new RectTransform(new Vector2(0.3f, 1.0f), selectionFrame.RectTransform), ((SelectionMode)i).ToString(), font: GUI.SmallFont)
                {
                    Selected = i == (int)modeSelectionMode,
                    OnSelected = SwitchModeSelection,
                    UserData = (SelectionMode)i
                };
            }
            
            var endBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform),
                TextManager.Get("ServerSettingsEndRoundWhenDestReached"))
            {
                Selected = EndRoundAtLevelEnd,
                OnSelected = (GUITickBox) => { EndRoundAtLevelEnd = GUITickBox.Selected; return true; }
            };
            
            var endVoteBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform),
                TextManager.Get("ServerSettingsEndRoundVoting"))
            {
                Selected = Voting.AllowEndVoting,
                OnSelected = (GUITickBox) =>
                {
                    Voting.AllowEndVoting = !Voting.AllowEndVoting;
                    GameMain.Server.UpdateVoteStatus();
                    return true;
                }
            };

            GUIScrollBar slider;
            GUITextBlock sliderLabel;
            CreateLabeledSlider(roundsTab, "ServerSettingsEndRoundVotesRequired", out slider, out sliderLabel);

            string endRoundLabel = sliderLabel.Text;
            slider.Step = 0.2f;
            slider.BarScroll = (EndVoteRequiredRatio - 0.5f) * 2.0f;
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                EndVoteRequiredRatio = barScroll / 2.0f + 0.5f;
                ((GUITextBlock)scrollBar.UserData).Text = endRoundLabel + (int)MathUtils.Round(EndVoteRequiredRatio * 100.0f, 10.0f) + " %";
                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);
            
            var respawnBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform),
                TextManager.Get("ServerSettingsAllowRespawning"))
            {
                Selected = AllowRespawn,
                OnSelected = (GUITickBox) =>
                {
                    AllowRespawn = !AllowRespawn;
                    return true;
                }
            };

            CreateLabeledSlider(roundsTab, "ServerSettingsRespawnInterval", out slider, out sliderLabel);
            string intervalLabel = sliderLabel.Text;
            slider.Step = 0.05f;
            slider.BarScroll = RespawnInterval / 600.0f;
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                GUITextBlock text = scrollBar.UserData as GUITextBlock;
                RespawnInterval = Math.Max(barScroll * 600.0f, 10.0f);
                text.Text = intervalLabel + ToolBox.SecondsToReadableTime(RespawnInterval);
                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);
            
            var minRespawnText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), "")
            {
                ToolTip = TextManager.Get("ServerSettingsMinRespawnToolTip")
            };

            string minRespawnLabel = TextManager.Get("ServerSettingsMinRespawn");
            CreateLabeledSlider(roundsTab, "", out slider, out sliderLabel);
            slider.ToolTip = minRespawnText.ToolTip;
            slider.UserData = minRespawnText;
            slider.Step = 0.1f;
            slider.BarScroll = MinRespawnRatio;
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                MinRespawnRatio = barScroll;
                ((GUITextBlock)scrollBar.UserData).Text = minRespawnLabel + (int)MathUtils.Round(MinRespawnRatio * 100.0f, 10.0f) + " %";
                return true;
            };
            slider.OnMoved(slider, MinRespawnRatio);
            
            var respawnDurationText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), roundsTab.RectTransform), "")
            {
                ToolTip = TextManager.Get("ServerSettingsRespawnDurationToolTip")
            };

            string respawnDurationLabel = TextManager.Get("ServerSettingsRespawnDuration");
            CreateLabeledSlider(roundsTab, "", out slider, out sliderLabel);
            slider.ToolTip = minRespawnText.ToolTip;
            slider.UserData = respawnDurationText;
            slider.Step = 0.1f;
            slider.BarScroll = MaxTransportTime <= 0.0f ? 1.0f : (MaxTransportTime - 60.0f) / 600.0f;
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                if (barScroll == 1.0f)
                {
                    MaxTransportTime = 0;
                    ((GUITextBlock)scrollBar.UserData).Text = respawnDurationLabel + "unlimited";
                }
                else
                {
                    MaxTransportTime = barScroll * 600.0f + 60.0f;
                    ((GUITextBlock)scrollBar.UserData).Text = respawnDurationLabel + ToolBox.SecondsToReadableTime(MaxTransportTime);
                }

                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);
            
            var buttonHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.07f), roundsTab.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            var monsterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), buttonHolder.RectTransform),
                TextManager.Get("ServerSettingsMonsterSpawns"))
            {
                Enabled = !GameStarted
            };
            //TODO: reimplement
            var monsterFrame = new GUIListBox(new Rectangle(-290, 60, 280, 250), "", settingsTabs[0]);
            monsterFrame.Visible = false;
            monsterFrame.ClampMouseRectToParent = false;
            monsterButton.UserData = monsterFrame;
            monsterButton.OnClicked = (button, obj) =>
            {
                if (gameStarted)
                {
                    ((GUIComponent)obj).Visible = false;
                    button.Enabled = false;
                    return true;
                }
                ((GUIComponent)obj).Visible = !((GUIComponent)obj).Visible;
                return true;
            };
            List<string> monsterNames = monsterEnabled.Keys.ToList();
            foreach (string s in monsterNames)
            {
                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(0, 0, 260, 25),
                    s,
                    "",
                    Alignment.Left, Alignment.Left, monsterFrame);
                textBlock.Padding = new Vector4(35.0f, 3.0f, 0.0f, 0.0f);
                textBlock.UserData = monsterFrame;
                textBlock.CanBeFocused = false;

                var monsterEnabledBox = new GUITickBox(new Rectangle(-25, 0, 20, 20), "", Alignment.Left, textBlock);
                monsterEnabledBox.Selected = monsterEnabled[s];
                monsterEnabledBox.OnSelected = (GUITickBox) =>
                {
                    if (gameStarted)
                    {
                        monsterFrame.Visible = false;
                        monsterButton.Enabled = false;
                        return true;
                    }
                    monsterEnabled[s] = !monsterEnabled[s];
                    return true;
                };
            }

            var cargoButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), buttonHolder.RectTransform),
                TextManager.Get("ServerSettingsAdditionalCargo"))
            {
                Enabled = !GameStarted
            };
            //TODO: reimplement
            var cargoFrame = new GUIListBox(new Rectangle(300, 60, 280, 250), "", settingsTabs[0]);
            cargoFrame.Visible = false;
            cargoFrame.ClampMouseRectToParent = false;
            cargoButton.UserData = cargoFrame;
            cargoButton.OnClicked = (button, obj) =>
            {
                if (gameStarted)
                {
                    ((GUIComponent)obj).Visible = false;
                    button.Enabled = false;
                    return true;
                }
                ((GUIComponent)obj).Visible = !((GUIComponent)obj).Visible;
                return true;
            };
            
            foreach (MapEntityPrefab pf in MapEntityPrefab.List)
            {
                ItemPrefab ip = pf as ItemPrefab;

                if (ip == null || (!ip.CanBeBought && !ip.Tags.Contains("smallitem"))) continue;
                
                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(0, 0, 260, 25),
                    ip.Name, "",
                    Alignment.Left, Alignment.CenterLeft, cargoFrame, false, GUI.SmallFont);
                textBlock.Padding = new Vector4(40.0f, 3.0f, 0.0f, 0.0f);
                textBlock.UserData = cargoFrame;
                textBlock.CanBeFocused = false;

                if (ip.sprite != null)
                {
                    float scale = Math.Min(Math.Min(30.0f / ip.sprite.SourceRect.Width, 30.0f / ip.sprite.SourceRect.Height), 1.0f);
                    GUIImage img = new GUIImage(new Rectangle(-20 - (int)(ip.sprite.SourceRect.Width * scale * 0.5f), 12 - (int)(ip.sprite.SourceRect.Height * scale * 0.5f), 40, 40), ip.sprite, Alignment.Left, textBlock);
                    img.Color = ip.SpriteColor;
                    img.Scale = scale;
                }

                int cargoVal = 0;
                extraCargo.TryGetValue(ip, out cargoVal);
                var amountInput = new GUINumberInput(new Rectangle(160, 0, 50, 20), "", GUINumberInput.NumberType.Int, textBlock);
                amountInput.MinValueInt = 0;
                amountInput.MaxValueInt = 100;
                amountInput.IntValue = cargoVal;

                amountInput.OnValueChanged += (numberInput) =>
                {
                    if (extraCargo.ContainsKey(ip))
                    {
                        extraCargo[ip] = numberInput.IntValue;
                    }
                    else
                    {
                        extraCargo.Add(ip, numberInput.IntValue);
                    }
                };                
            }


            //--------------------------------------------------------------------------------
            //                              server settings 
            //--------------------------------------------------------------------------------
            
            var serverTab = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), settingsTabs[(int)SettingsTab.Server].RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.02f
            };

            string autoRestartDelayLabel = TextManager.Get("ServerSettingsAutoRestartDelay");
            var startIntervalText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), autoRestartDelayLabel);
            var startIntervalSlider = new GUIScrollBar(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), barSize: 0.1f)
            {
                UserData = startIntervalText,
                Step = 0.05f,
                BarScroll = AutoRestartInterval / 300.0f,
                OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
                {
                    GUITextBlock text = scrollBar.UserData as GUITextBlock;
                    AutoRestartInterval = Math.Max(barScroll * 300.0f, 10.0f);
                    text.Text = autoRestartDelayLabel + ToolBox.SecondsToReadableTime(AutoRestartInterval);
                    return true;
                }
            };
            startIntervalSlider.OnMoved(startIntervalSlider, startIntervalSlider.BarScroll);
            
            var allowSpecBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsAllowSpectating"))
            {
                Selected = AllowSpectating,
                OnSelected = (GUITickBox) =>
                {
                    AllowSpectating = GUITickBox.Selected;
                    GameMain.NetLobbyScreen.LastUpdateID++;
                    return true;
                }
            };
            
            var voteKickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsAllowVoteKick"))
            {
                Selected = Voting.AllowVoteKick,
                OnSelected = (GUITickBox) =>
                {
                    Voting.AllowVoteKick = !Voting.AllowVoteKick;
                    GameMain.Server.UpdateVoteStatus();
                    return true;
                }
            };

            CreateLabeledSlider(serverTab, "ServerSettingsKickVotesRequired", out slider, out sliderLabel);
            string votesRequiredLabel = sliderLabel.Text;
            slider.Step = 0.2f;
            slider.BarScroll = (KickVoteRequiredRatio - 0.5f) * 2.0f;
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                KickVoteRequiredRatio = barScroll / 2.0f + 0.5f;
                ((GUITextBlock)scrollBar.UserData).Text = votesRequiredLabel + (int)MathUtils.Round(KickVoteRequiredRatio * 100.0f, 10.0f) + " %";
                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);

            CreateLabeledSlider(serverTab, "ServerSettingsAutobanTime", out slider, out sliderLabel);
            string autobanLabel = sliderLabel.Text;
            slider.Step = 0.05f;
            slider.BarScroll = AutoBanTime / MaxAutoBanTime;
            slider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                AutoBanTime = Math.Max(barScroll * MaxAutoBanTime, 0);
                ((GUITextBlock)scrollBar.UserData).Text = autobanLabel + ToolBox.SecondsToReadableTime(AutoBanTime);
                return true;
            };
            slider.OnMoved(slider, slider.BarScroll);

            var shareSubsBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsShareSubFiles"))
            {
                Selected = AllowFileTransfers,
                OnSelected = (GUITickBox) =>
                {
                    AllowFileTransfers = GUITickBox.Selected;
                    return true;
                }
            };

            var randomizeLevelBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsRandomizeSeed"))
            {
                Selected = RandomizeSeed,
                OnSelected = (GUITickBox) =>
                {
                    RandomizeSeed = GUITickBox.Selected;
                    return true;
                }
            };

            var saveLogsBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsSaveLogs"))
            {
                Selected = SaveServerLogs,
                OnSelected = (GUITickBox) =>
                {
                    SaveServerLogs = GUITickBox.Selected;
                    showLogButton.Visible = SaveServerLogs;
                    return true;
                }
            };

            var ragdollButtonBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsAllowRagdollButton"))
            {
                Selected = AllowRagdollButton,
                OnSelected = (GUITickBox) =>
                {
                    AllowRagdollButton = GUITickBox.Selected;
                    return true;
                }
            };
            
            var traitorRatioBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsUseTraitorRatio"));

            CreateLabeledSlider(serverTab, "", out slider, out sliderLabel);
            /*var traitorRatioText = new GUITextBlock(new Rectangle(20, y + 20, 20, 20), "Traitor ratio: 20 %", "", settingsTabs[1], GUI.SmallFont);
            var traitorRatioSlider = new GUIScrollBar(new Rectangle(150, y + 22, 100, 15), "", 0.1f, settingsTabs[1]);*/
            var traitorRatioSlider = slider;
            traitorRatioBox.Selected = TraitorUseRatio;
            traitorRatioBox.OnSelected = (GUITickBox) =>
            {
                TraitorUseRatio = GUITickBox.Selected;
                traitorRatioSlider.Step = TraitorUseRatio ? 0.01f : 1f / (maxPlayers - 1);
                traitorRatioSlider.OnMoved(traitorRatioSlider, traitorRatioSlider.BarScroll);
                return true;
            };

            string traitorRatioLabel = TextManager.Get("ServerSettingsTraitorRatio");
            string traitorCountLabel = TextManager.Get("ServerSettingsTraitorCount");
            traitorRatioSlider.OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
            {
                GUITextBlock traitorText = scrollBar.UserData as GUITextBlock;
                if (TraitorUseRatio)
                {
                    TraitorRatio = barScroll * 0.9f + 0.1f;
                    traitorText.Text = traitorRatioLabel + (int)MathUtils.Round(TraitorRatio * 100.0f, 1.0f) + " %";
                }
                else
                {
                    TraitorRatio = MathUtils.Round(barScroll * (maxPlayers-1), 1f) + 1;
                    traitorText.Text = traitorCountLabel + TraitorRatio;
                }
                return true;
            };
            traitorRatioSlider.OnMoved(traitorRatioSlider, traitorRatioSlider.BarScroll);
            traitorRatioBox.OnSelected(traitorRatioBox);
            
            new GUITickBox(new RectTransform(new Vector2(1.0f, 0.05f), serverTab.RectTransform), TextManager.Get("ServerSettingsUseKarma"))
            {
                Selected = KarmaEnabled,
                OnSelected = (GUITickBox) =>
                {
                    KarmaEnabled = GUITickBox.Selected;
                    return true;
                }
            };

            //--------------------------------------------------------------------------------
            //                              banlist
            //--------------------------------------------------------------------------------
            
            banList.CreateBanFrame(settingsTabs[2]);

            //--------------------------------------------------------------------------------
            //                              whitelist
            //--------------------------------------------------------------------------------

            whitelist.CreateWhiteListFrame(settingsTabs[3]);

        }

        private void CreateLabeledSlider(GUIComponent parent, string labelTag, out GUIScrollBar slider, out GUITextBlock label)
        {
            var container = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), parent.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            slider = new GUIScrollBar(new RectTransform(new Vector2(0.5f, 0.8f), container.RectTransform), barSize: 0.1f);
            label = new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.8f), container.RectTransform), 
                string.IsNullOrEmpty(labelTag) ? "" : TextManager.Get(labelTag), font: GUI.SmallFont);

            //slider has a reference to the label to change the text when it's used
            slider.UserData = label;
        }

        private bool SwitchSubSelection(GUITickBox tickBox)
        {
            subSelectionMode = (SelectionMode)tickBox.UserData;

            foreach (GUIComponent otherTickBox in tickBox.Parent.Children)
            {
                if (otherTickBox == tickBox) continue;
                ((GUITickBox)otherTickBox).Selected = false;
            }

            Voting.AllowSubVoting = subSelectionMode == SelectionMode.Vote;

            if (subSelectionMode == SelectionMode.Random)
            {
                GameMain.NetLobbyScreen.SubList.Select(Rand.Range(0, GameMain.NetLobbyScreen.SubList.CountChildren));
            }

            return true;
        }

        private bool SelectSettingsTab(GUIButton button, object obj)
        {
            settingsTabIndex = (int)obj;

            for (int i = 0; i < settingsTabs.Length; i++)
            {
                settingsTabs[i].Visible = i == settingsTabIndex;
            }

            return true;
        }

        private bool SwitchModeSelection(GUITickBox tickBox)
        {
            modeSelectionMode = (SelectionMode)tickBox.UserData;

            foreach (GUIComponent otherTickBox in tickBox.Parent.Children)
            {
                if (otherTickBox == tickBox) continue;
                ((GUITickBox)otherTickBox).Selected = false;
            }

            Voting.AllowModeVoting = modeSelectionMode == SelectionMode.Vote;

            if (modeSelectionMode == SelectionMode.Random)
            {
                GameMain.NetLobbyScreen.ModeList.Select(Rand.Range(0, GameMain.NetLobbyScreen.ModeList.CountChildren));
            }

            return true;
        }


        public bool ToggleSettingsFrame(GUIButton button, object obj)
        {
            if (settingsFrame == null)
            {
                CreateSettingsFrame();
            }
            else
            {
                settingsFrame = null;
                SaveSettings();
            }

            return false;
        }

        public void ManagePlayersFrame(GUIFrame infoFrame)
        {
            GUIListBox cList = new GUIListBox(new Rectangle(0, 0, 0, 300), Color.White * 0.7f, "", infoFrame);
            cList.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);
            //crewList.OnSelected = SelectCrewCharacter;

            foreach (Client c in ConnectedClients)
            {
                GUIFrame frame = new GUIFrame(new Rectangle(0, 0, 0, 40), Color.Transparent, null, cList);
                frame.Padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);
                frame.Color = (c.InGame && c.Character != null && !c.Character.IsDead) ? Color.Gold * 0.2f : Color.Transparent;
                frame.HoverColor = Color.LightGray * 0.5f;
                frame.SelectedColor = Color.Gold * 0.5f;

                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(40, 0, 0, 25),
                    c.Name + " (" + c.Connection.RemoteEndPoint.Address.ToString() + ")",
                    Color.Transparent, Color.White,
                    Alignment.Left, Alignment.Left,
                    null, frame);

                var banButton = new GUIButton(new Rectangle(-110, 0, 100, 20), "Ban", Alignment.Right | Alignment.CenterY, "", frame);
                banButton.UserData = c.Name;
                banButton.OnClicked = GameMain.NetLobbyScreen.BanPlayer;

                var rangebanButton = new GUIButton(new Rectangle(-220, 0, 100, 20), "Ban range", Alignment.Right | Alignment.CenterY, "", frame);
                rangebanButton.UserData = c.Name;
                rangebanButton.OnClicked = GameMain.NetLobbyScreen.BanPlayerRange;

                var kickButton = new GUIButton(new Rectangle(0, 0, 100, 20), "Kick", Alignment.Right | Alignment.CenterY, "", frame);
                kickButton.UserData = c.Name;
                kickButton.OnClicked = GameMain.NetLobbyScreen.KickPlayer;

                textBlock.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);
            }
        }
    }
}
