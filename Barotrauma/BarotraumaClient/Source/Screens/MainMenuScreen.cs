﻿using Barotrauma.Networking;
using Barotrauma.Tutorials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;

namespace Barotrauma
{
    class MainMenuScreen : Screen
    {
        public enum Tab { NewGame = 1, LoadGame = 2, HostServer = 3, Settings = 4, Tutorials = 5 }

        private GUIFrame buttonsParent;

        private GUIFrame[] menuTabs;

        private CampaignSetupUI campaignSetupUI;

        private GUITextBox serverNameBox, portBox, passwordBox, maxPlayersBox;
        private GUITickBox isPublicBox, useUpnpBox;

        private GameMain game;

        private Tab selectedTab;

        public MainMenuScreen(GameMain game)
        {
            buttonsParent = new GUIFrame(new RectTransform(new Vector2(0.15f, 1), parent: Frame.RectTransform, anchor: Anchor.BottomLeft)
            {
                RelativeOffset = new Vector2(0, 0.1f),
                AbsoluteOffset = new Point(50, 0)
            }, color: Color.Transparent);
            var minButtonSize = new Point(120, 20);
            var maxButtonSize = new Point(240, 40);
            // 
            var buttons = GUI.CreateButtons(8, new Vector2(1, 0.04f), buttonsParent.RectTransform, anchor: Anchor.BottomLeft,
                minSize: minButtonSize, maxSize: maxButtonSize, relativeSpacing: 0.005f, extraSpacing: i => i % 2 == 0 ? 0 : 20);
            buttons.ForEach(b => b.Color *= 0.8f);
            SetupButtons(buttons);
            buttons.ForEach(b => b.TextBlock.SetTextPos());

            var relativeSize = new Vector2(0.5f, 0.5f);
            var minSize = new Point(600, 400);
            var maxSize = new Point(900, 600);
            var anchor = Anchor.Center;
            var pivot = Pivot.Center;
            menuTabs = new GUIFrame[Enum.GetValues(typeof(Tab)).Length + 1];

            menuTabs[(int)Tab.NewGame] = new GUIFrame(new RectTransform(relativeSize, Frame.RectTransform, anchor, pivot, minSize, maxSize));
            var paddedNewGame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), menuTabs[(int)Tab.NewGame].RectTransform, Anchor.Center), style: null);
            menuTabs[(int)Tab.LoadGame] = new GUIFrame(new RectTransform(relativeSize, Frame.RectTransform, anchor, pivot, minSize, maxSize));
            var paddedLoadGame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), menuTabs[(int)Tab.LoadGame].RectTransform, Anchor.Center), style: null);
            
            campaignSetupUI = new CampaignSetupUI(false, paddedNewGame, paddedLoadGame)
            {
                LoadGame = LoadGame,
                StartNewGame = StartGame
            };

            var hostServerScale = new Vector2(0.7f, 1.0f);
            menuTabs[(int)Tab.HostServer] = new GUIFrame(new RectTransform(
                Vector2.Multiply(relativeSize, hostServerScale), Frame.RectTransform, anchor, pivot, minSize.Multiply(hostServerScale), maxSize.Multiply(hostServerScale)));

            CreateHostServerFields();

            //----------------------------------------------------------------------

            menuTabs[(int)Tab.Tutorials] = new GUIFrame(new RectTransform(relativeSize, Frame.RectTransform, anchor, pivot, minSize, maxSize));

            //PLACEHOLDER
            var tutorialList = new GUIListBox(
                new RectTransform(new Vector2(0.95f, 0.85f), menuTabs[(int)Tab.Tutorials].RectTransform, Anchor.TopCenter) { RelativeOffset = new Vector2(0.0f, 0.1f) }, 
                false, null, "");
            foreach (Tutorial tutorial in Tutorial.Tutorials)
            {
                var tutorialText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), tutorialList.Content.RectTransform), tutorial.Name, textAlignment: Alignment.Center, font: GUI.LargeFont)
                {
                    UserData = tutorial
                };
            }
            tutorialList.OnSelected += (component, obj) =>
            {
                TutorialMode.StartTutorial(obj as Tutorial);
                return true;
            };

            UpdateTutorialList();

            this.game = game;
        }

        public override void Select()
        {
            base.Select();

            if (GameMain.NetworkMember != null)
            {
                GameMain.NetworkMember.Disconnect();
                GameMain.NetworkMember = null;
            }

            Submarine.Unload();

            UpdateTutorialList();

            campaignSetupUI.UpdateSubList();

            SelectTab(null, 0);
        }

        public bool SelectTab(GUIButton button, object obj)
        {
            if (obj is Tab)
            {
                SelectTab((Tab)obj);
            }
            else
            { 
                selectedTab = 0;
            }

            if (button != null) button.Selected = true;

            foreach (GUIComponent child in buttonsParent.Children)
            {
                GUIButton otherButton = child as GUIButton;
                if (otherButton == null || otherButton == button) continue;

                otherButton.Selected = false;
            }

            if (Selected != this) Select();

            return true;
        }

        public void SelectTab(Tab tab)
        {
            if (GameMain.Config.UnsavedSettings)
            {
                var applyBox = new GUIMessageBox(
                    TextManager.Get("ApplySettingsLabel"),
                    TextManager.Get("ApplySettingsQuestion"),
                    new string[] { TextManager.Get("ApplySettingsYes"), TextManager.Get("ApplySettingsNo") });
                applyBox.Buttons[0].OnClicked += applyBox.Close;
                applyBox.Buttons[0].OnClicked += ApplySettings;
                applyBox.Buttons[0].UserData = tab;
                applyBox.Buttons[1].OnClicked += applyBox.Close;
                applyBox.Buttons[1].OnClicked += DiscardSettings;
                applyBox.Buttons[1].UserData = tab;

                return;
            }

            selectedTab = tab;

            switch (selectedTab)
            {
                case Tab.NewGame:
                    campaignSetupUI.CreateDefaultSaveName();
                    break;
                case Tab.LoadGame:
                    campaignSetupUI.UpdateLoadMenu();
                    break;
                case Tab.Settings:
                    GameMain.Config.ResetSettingsFrame();
                    menuTabs[(int)Tab.Settings] = GameMain.Config.SettingsFrame;
                    break;
            }
        }

        private void UpdateTutorialList()
        {
            var tutorialList = menuTabs[(int)Tab.Tutorials].GetChild<GUIListBox>();
            foreach (GUITextBlock tutorialText in tutorialList.Content.Children)
            {
                if (((Tutorial)tutorialText.UserData).Completed)
                {
                    tutorialText.TextColor = Color.LightGreen;
                }
            }
        }

        private bool ApplySettings(GUIButton button, object userData)
        {
            GameMain.Config.Save();

            if (userData is Tab) SelectTab((Tab)userData);

            if (GameMain.GraphicsWidth != GameMain.Config.GraphicsWidth || GameMain.GraphicsHeight != GameMain.Config.GraphicsHeight)
            {
                new GUIMessageBox(
                    TextManager.Get("RestartRequiredLabel"),
                    TextManager.Get("RestartRequiredText"));
            }

            return true;
        }

        private bool DiscardSettings(GUIButton button, object userData)
        {
            GameMain.Config.Load("config.xml");
            if (userData is Tab) SelectTab((Tab)userData);

            return true;
        }
        
        private bool JoinServerClicked(GUIButton button, object obj)
        {
            GameMain.ServerListScreen.Select();
            return true;
        }

        private bool ChangeMaxPlayers(GUIButton button, object obj)
        {
            int.TryParse(maxPlayersBox.Text, out int currMaxPlayers);
            currMaxPlayers = (int)MathHelper.Clamp(currMaxPlayers + (int)button.UserData, 1, NetConfig.MaxPlayers);

            maxPlayersBox.Text = currMaxPlayers.ToString();

            return true;
        }

        private bool HostServerClicked(GUIButton button, object obj)
        {
            string name = serverNameBox.Text;
            if (string.IsNullOrEmpty(name))
            {
                serverNameBox.Flash();
                return false;
            }

            if (!int.TryParse(portBox.Text, out int port) || port < 0 || port > 65535)
            {
                portBox.Text = NetConfig.DefaultPort.ToString();
                portBox.Flash();

                return false;
            }

            GameMain.NetLobbyScreen = new NetLobbyScreen();
            try
            {
                GameMain.NetworkMember = new GameServer(name, port, isPublicBox.Selected, passwordBox.Text, useUpnpBox.Selected, int.Parse(maxPlayersBox.Text));
            }

            catch (Exception e)
            {
                DebugConsole.ThrowError("Failed to start server", e);
            }

            GameMain.NetLobbyScreen.IsServer = true;
            return true;
        }

        private bool QuitClicked(GUIButton button, object obj)
        {
            game.Exit();
            return true;
        }

        public override void AddToGUIUpdateList()
        {
            Frame.AddToGUIUpdateList(ignoreChildren: true);
            buttonsParent.AddToGUIUpdateList();
            if (selectedTab > 0)
            {
                menuTabs[(int)selectedTab].AddToGUIUpdateList();
            }
        }

        public override void Update(double deltaTime)
        {
            GameMain.TitleScreen.TitlePosition =
                Vector2.Lerp(GameMain.TitleScreen.TitlePosition, new Vector2(
                    GameMain.TitleScreen.TitleSize.X / 2.0f * GameMain.TitleScreen.Scale + 30.0f,
                    GameMain.TitleScreen.TitleSize.Y / 2.0f * GameMain.TitleScreen.Scale + 30.0f),
                    0.1f);
        }

        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            graphics.Clear(Color.CornflowerBlue);

            GameMain.TitleScreen.DrawLoadingText = false;
            GameMain.TitleScreen.Draw(spriteBatch, graphics, (float)deltaTime);

            //Game1.GameScreen.DrawMap(graphics, spriteBatch);

            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, GameMain.ScissorTestEnable);

            GUI.Draw((float)deltaTime, spriteBatch);

            //GUI.DrawString(spriteBatch, new Vector2(500, 100), "selected tab " + selectedTab, Color.White);

#if DEBUG
            GUI.Font.DrawString(spriteBatch, "Barotrauma v" + GameMain.Version + " (debug build)", new Vector2(10, GameMain.GraphicsHeight - 20), Color.White);
#else
            GUI.Font.DrawString(spriteBatch, "Barotrauma v" + GameMain.Version, new Vector2(10, GameMain.GraphicsHeight - 20), Color.White);
#endif

            spriteBatch.End();
        }

        private void StartGame(Submarine selectedSub, string saveName, string mapSeed)
        {
            if (string.IsNullOrEmpty(saveName)) return;

            string[] existingSaveFiles = SaveUtil.GetSaveFiles(SaveUtil.SaveType.Singleplayer);

            if (Array.Find(existingSaveFiles, s => s == saveName) != null)
            {
                new GUIMessageBox("Save name already in use", "Please choose another name for the save file");
                return;
            }

            if (selectedSub == null)
            {
                new GUIMessageBox(TextManager.Get("SubNotSelected"), TextManager.Get("SelectSubRequest"));
                return;
            }

            if (!Directory.Exists(SaveUtil.TempPath))
            {
                Directory.CreateDirectory(SaveUtil.TempPath);
            }

            File.Copy(selectedSub.FilePath, Path.Combine(SaveUtil.TempPath, selectedSub.Name + ".sub"), true);

            selectedSub = new Submarine(Path.Combine(SaveUtil.TempPath, selectedSub.Name + ".sub"), "");

            GameMain.GameSession = new GameSession(selectedSub, saveName, GameModePreset.list.Find(gm => gm.Name == "Single Player"));
            (GameMain.GameSession.GameMode as CampaignMode).GenerateMap(mapSeed);

            GameMain.LobbyScreen.Select();
        }

        private void LoadGame(string saveFile)
        {
            if (string.IsNullOrWhiteSpace(saveFile)) return;

            try
            {
                SaveUtil.LoadGame(saveFile);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Loading save \"" + saveFile + "\" failed", e);
                return;
            }


            GameMain.LobbyScreen.Select();
        }

        #region UI Methods
        private void SetupButtons(List<GUIButton> buttons)
        {
            for (int i = 0; i < 8; i++)
            {
                var button = buttons[i];
                switch (i)
                {
                    case 7:
                        button.Text = TextManager.Get("TutorialButton");
                        button.UserData = Tab.Tutorials;
                        button.OnClicked = SelectTab;
                        break;
                    case 6:
                        button.Text = TextManager.Get("NewGameButton");
                        button.UserData = Tab.NewGame;
                        button.OnClicked = SelectTab;
                        break;
                    case 5:
                        button.Text = TextManager.Get("LoadGameButton");
                        button.UserData = Tab.LoadGame;
                        button.OnClicked = SelectTab;
                        break;
                    case 4:
                        button.Text = TextManager.Get("JoinServerButton");
                        //button.UserData = (int)Tabs.JoinServer;
                        button.OnClicked = JoinServerClicked;
                        break;
                    case 3:
                        button.Text = TextManager.Get("HostServerButton");
                        button.UserData = Tab.HostServer;
                        button.OnClicked = SelectTab;
                        break;
                    case 2:
                        button.Text = TextManager.Get("SubEditorButton");
                        button.OnClicked = (btn, userdata) => { GameMain.SubEditorScreen.Select(); return true; };
                        break;
                    case 1:
                        button.Text = TextManager.Get("SettingsButton");
                        button.UserData = Tab.Settings;
                        button.OnClicked = SelectTab;
                        break;
                    case 0:
                        button.Text = TextManager.Get("QuitButton");
                        button.OnClicked = QuitClicked;
                        break;
                    default:
                        throw new Exception();
                }
            }
        }

        private void CreateHostServerFields()
        {
            Vector2 textLabelSize = new Vector2(1.0f, 0.1f);
            Alignment textAlignment = Alignment.CenterLeft;
            Vector2 textFieldSize = new Vector2(0.5f, 1.0f);
            Vector2 tickBoxSize = new Vector2(0.4f, 0.07f);
            var paddedFrame = new GUILayoutGroup(new RectTransform(new Vector2(0.85f, 0.75f), menuTabs[(int)Tab.HostServer].RectTransform, Anchor.TopCenter) { RelativeOffset = new Vector2(0.0f, 0.05f) })
            {
                RelativeSpacing = 0.02f,
                Stretch = true
            }; 
            GUIComponent parent = paddedFrame;
            
            new GUITextBlock(new RectTransform(textLabelSize, parent.RectTransform), TextManager.Get("HostServerButton"), textAlignment: Alignment.Center, font: GUI.LargeFont);

            var label = new GUITextBlock(new RectTransform(textLabelSize, parent.RectTransform), TextManager.Get("ServerName"), textAlignment: textAlignment);
            serverNameBox = new GUITextBox(new RectTransform(textFieldSize, label.RectTransform, Anchor.CenterRight), textAlignment: textAlignment);

            label = new GUITextBlock(new RectTransform(textLabelSize, parent.RectTransform), TextManager.Get("ServerPort"), textAlignment: textAlignment);
            portBox = new GUITextBox(new RectTransform(textFieldSize, label.RectTransform, Anchor.CenterRight), textAlignment: textAlignment)
            {
                Text = NetConfig.DefaultPort.ToString(),
                ToolTip = TextManager.Get("ServerPort")
            };

            var maxPlayersLabel = new GUITextBlock(new RectTransform(textLabelSize, parent.RectTransform), TextManager.Get("MaxPlayers"), textAlignment: textAlignment);
            var buttonContainer = new GUILayoutGroup(new RectTransform(textFieldSize, maxPlayersLabel.RectTransform, Anchor.CenterRight), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.1f
            };
            new GUIButton(new RectTransform(new Vector2(0.2f, 1.0f), buttonContainer.RectTransform), "-", textAlignment: Alignment.Center)
            {
                UserData = -1,
                OnClicked = ChangeMaxPlayers
            };

            maxPlayersBox = new GUITextBox(new RectTransform(new Vector2(0.6f, 1.0f), buttonContainer.RectTransform), textAlignment: Alignment.Center)
            {
                Text = "8",
                Enabled = false
            };
            new GUIButton(new RectTransform(new Vector2(0.2f, 1.0f), buttonContainer.RectTransform), "+", textAlignment: Alignment.Center)
            {
                UserData = 1,
                OnClicked = ChangeMaxPlayers
            };


            label = new GUITextBlock(new RectTransform(textLabelSize, parent.RectTransform), TextManager.Get("Password"), textAlignment: textAlignment);
            passwordBox = new GUITextBox(new RectTransform(textFieldSize, label.RectTransform, Anchor.CenterRight), textAlignment: textAlignment);
            
            isPublicBox = new GUITickBox(new RectTransform(tickBoxSize, parent.RectTransform), TextManager.Get("PublicServer"))
            {
                ToolTip = TextManager.Get("PublicServerToolTip")
            };
            
            useUpnpBox = new GUITickBox(new RectTransform(tickBoxSize, parent.RectTransform), TextManager.Get("AttemptUPnP"))
            {
                ToolTip = TextManager.Get("AttemptUPnPToolTip")
            };

            new GUIButton(new RectTransform(new Vector2(0.4f, 0.1f), menuTabs[(int)Tab.HostServer].RectTransform, Anchor.BottomRight)
            {
                RelativeOffset = new Vector2(0.05f, 0.05f)
            }, TextManager.Get("StartServerButton"), style: "GUIButtonLarge")
            {
                IgnoreLayoutGroups = true,
                OnClicked = HostServerClicked
            };
        }
        #endregion

    }
}
