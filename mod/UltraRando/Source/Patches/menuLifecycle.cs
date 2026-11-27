using System;
using Monocle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.UltraRando {
    public partial class RandoModule : EverestModule {
        private void LoadMenuLifecycle() {
            // Ancien bouton « Movement Rando » de rhelmot retiré : notre entrée est « Se connecter ».
            // Everest.Events.MainMenu.OnCreateButtons += CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons += ModifyLevelMenu;
            On.Celeste.OverworldLoader.ctor += EnterToRandoMenu;
            On.Celeste.LevelExit.ctor += EndlessCantRestart;
            On.Celeste.Level.GiveUp += EndlessCantRestartSnarky;
            On.Celeste.Overworld.ctor += HideMaddy;
            On.Celeste.MapData.Load += DontLoadRandoMaps;
            On.Celeste.AreaData.Load += InitRandoData;
        }

        private void UnloadMenuLifecycle() {
            // Everest.Events.MainMenu.OnCreateButtons -= CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons -= ModifyLevelMenu;
            On.Celeste.OverworldLoader.ctor -= EnterToRandoMenu;
            On.Celeste.LevelExit.ctor -= EndlessCantRestart;
            On.Celeste.Level.GiveUp -= EndlessCantRestartSnarky;
            On.Celeste.Overworld.ctor -= HideMaddy;
            On.Celeste.MapData.Load -= DontLoadRandoMaps;
            On.Celeste.AreaData.Load -= InitRandoData;
        }

        private void CreateMainMenuButton(OuiMainMenu menu, System.Collections.Generic.List<MenuButton> buttons) {
            MainMenuSmallButton btn = new MainMenuSmallButton("MODOPTIONS_CMRANDO_TOPMENU", "menu/randomizer", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play(SFX.ui_main_button_select);
                Audio.Play(SFX.ui_main_whoosh_large_in);
                if (this.SavedData.FastMenu ^ (Input.MenuJournal.Check || MInput.Keyboard.Check(Keys.LeftShift))) {
                    menu.Overworld.Goto<OuiRandoSettings>();
                } else {
                    menu.Overworld.Goto<OuiRandoMode>();
                }
            });
            buttons.Insert(1, btn);
        }

        private void ModifyLevelMenu(Level level, TextMenu pausemenu, bool minimal) {
            var settings = this.InRandomizerSettings;
            // CMR: the game only shows "Return to Map" once the Prologue is completed in the save; a randomizer save can
            // start anywhere, so add it on our maps (same action as the game's button: GiveUp to the map)
            if (settings == null && !minimal && this.InGeneratedLayout && !level.InCutscene &&
                !pausemenu.Items.Exists(i => i is TextMenu.Button b && b.Label == Dialog.Clean("MENU_PAUSE_RETURN"))) {
                int at = pausemenu.Items.FindIndex(i => i is TextMenu.Button b && b.Label == Dialog.Clean("MENU_PAUSE_RESTARTAREA"));
                var ret = new TextMenu.Button(Dialog.Clean("MENU_PAUSE_RETURN"));
                ret.Pressed(() => {
                    // as the game's own button: close the pause menu first (two open menus overlapped, and the one left
                    // behind could render with an invalid selection: TextMenu.get_Current crash), then the give-up prompt
                    int index = pausemenu.IndexOf(ret);
                    level.PauseMainMenuOpen = false;
                    pausemenu.RemoveSelf();
                    MonoMod.Utils.DynamicData.For(level).Invoke("GiveUp", index, false, minimal, false); // private in Level
                });
                ret.ConfirmSfx = "event:/ui/main/message_confirm";
                if (at >= 0) pausemenu.Insert(at + 1, ret); else pausemenu.Add(ret);
            }
            if (settings != null) {
                foreach (var item in new System.Collections.Generic.List<TextMenu.Item>(pausemenu.Items)) {
                    if (item.GetType() == typeof(TextMenu.Button)) {
                        var btn = (TextMenu.Button)item;
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_SAVEQUIT") || btn.Label == Dialog.Clean("MENU_PAUSE_RETURN")) {
                            pausemenu.Remove(item);
                        }
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_RESTARTAREA")) {
                            if (settings.Algorithm == LogicType.Endless) {
                                pausemenu.Remove(item);
                            } else {
                                btn.Label = Dialog.Clean("MENU_PAUSE_RESTARTRANDO");
                            }
                        }
                    }
                }

                int returnIdx = pausemenu.Items.Count;
                pausemenu.Add(new TextMenu.Button(Dialog.Clean("MENU_PAUSE_QUITRANDO")).Pressed(() => {
                    level.PauseMainMenuOpen = false;
                    pausemenu.RemoveSelf();

                    TextMenu menu = new TextMenu();
                    menu.AutoScroll = false;
                    menu.Position = new Vector2((float)Engine.Width / 2f, (float)((double)Engine.Height / 2.0 - 100.0));
                    menu.Add(new TextMenu.Header(Dialog.Clean("MENU_QUITRANDO_TITLE")));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CONFIRM")).Pressed((Action)(() => {
                        Engine.TimeRate = 1f;
                        menu.Focused = false;
                        level.Session.InArea = false;
                        Audio.SetMusic((string)null, true, true);
                        Audio.BusStopAll("bus:/gameplay_sfx", true);
                        level.DoScreenWipe(false, (Action)(() => Engine.Scene = (Scene)new LevelExit(LevelExit.Mode.SaveAndQuit, level.Session, level.HiresSnow)), true);
                        foreach (LevelEndingHook component in level.Tracker.GetComponents<LevelEndingHook>()) {
                            if (component.OnEnd != null)
                                component.OnEnd();
                        }
                    })));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CANCEL")).Pressed((Action)(() => menu.OnCancel())));
                    menu.OnPause = menu.OnESC = (Action)(() => {
                        menu.RemoveSelf();
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                        Audio.Play("event:/ui/game/unpause");
                    });
                    menu.OnCancel = (Action)(() => {
                        Audio.Play("event:/ui/main/button_back");
                        menu.RemoveSelf();
                        level.Pause(returnIdx, minimal, false);
                    });
                    level.Add((Entity)menu);
                }));
            }
        }

        private void EnterToRandoMenu(On.Celeste.OverworldLoader.orig_ctor orig, OverworldLoader self, Overworld.StartMode startMode, HiresSnow snow) {
            if ((startMode == Overworld.StartMode.MainMenu || startMode == Overworld.StartMode.AreaComplete) && this.InRandomizer) {
                startMode = RandoModule.STARTMODE_RANDOMIZER;
            }
            orig(self, startMode, snow);
        }

        private void EndlessCantRestart(On.Celeste.LevelExit.orig_ctor orig, LevelExit self, LevelExit.Mode mode, Session session, HiresSnow snow) {
            var settings = this.InRandomizerSettings;
            if (settings != null && settings.Algorithm == LogicType.Endless && (mode == LevelExit.Mode.Restart || mode == LevelExit.Mode.GoldenBerryRestart)) {
                mode = LevelExit.Mode.SaveAndQuit;
            }

            orig(self, mode, session, snow);
        }

        private void EndlessCantRestartSnarky(On.Celeste.Level.orig_GiveUp orig, Level self, int returnIndex, bool restartArea, bool minimal, bool showHint) {
            var settings = this.InRandomizerSettings;
            if (settings == null || settings.Algorithm != LogicType.Endless || !restartArea) {
                orig(self, returnIndex, restartArea, minimal, showHint);
                return;
            }

            self.Paused = true;
            var menu = new TextMenu() {
                new TextMenu.Header(Dialog.Clean("MENU_CANTRESTART_HEADER")),
            };
            menu.AutoScroll = false;
            menu.OnPause = menu.OnESC = () => {
                menu.RemoveSelf();
                self.Paused = false;
                Engine.FreezeTimer = 0.15f;
                Audio.Play("event:/ui/game/unpause");
            };
            menu.OnCancel = () => {
                Audio.Play("event:/ui/main/button_back");
                menu.RemoveSelf();
                self.Pause(returnIndex, minimal);
            };
            self.Add(menu);
        }

        // This is a bit of a hack. is there a better way to control this?
        private void HideMaddy(On.Celeste.Overworld.orig_ctor orig, Overworld self, OverworldLoader loader) {
            orig(self, loader);
            if (this.InRandomizer) {
                self.Maddy.Hide();
            }
        }

        private void DontLoadRandoMaps(On.Celeste.MapData.orig_Load orig, MapData self) {
            if (self.Data?.SID?.StartsWith("cmrando/") ?? false) {
                return;
            }
            orig(self);
        }

        private void InitRandoData(On.Celeste.AreaData.orig_Load orig) {
            orig();
            this.MetaConfig = RandoMetadataFile.LoadAll();
            RandoLogic.ProcessAreas();
            this.Settings.PruneMaps();
            if (SavedData.SavedSettings == null) {
                Settings.SetNormalMaps();
            }
            RoomsExport.ExportIfFlagged(); // CMR
            AP.SelfTest.RunIfFlagged(); // CMR (phase B)
        }
    }
}
