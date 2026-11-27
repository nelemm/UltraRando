// CMR: Archipelago connection screen, opened by "Climb" on the title screen (RandoModuleSettings.ClimbOpensArchipelago).
// Own code; Open World's OuiConnection was only read as a behaviour reference (it is "All rights reserved").
using System;
using Celeste.Mod.UI;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public class OuiApConnect : GenericOui {
        static RandoModuleSettings S => RandoModule.Instance.SavedData;

        TextMenu.SubHeader status;
        bool waiting;

        protected override bool IsDeeperThan(Oui other) => other is OuiMainMenu || other == null;

        protected override Entity ReloadMenu() {
            // full-screen layout of the reference (docs/ref_openworld_ui/02.webp): big headers, value under each
            var menu = new TextMenu();
            if (ApClient.Connected) {
                menu.Add(new TextMenu.Button(RandoSaves.Fmt("CMR_AP_CONTINUE", Text.Sanitize(ApState.SlotName, 32))).Pressed(() => {
                    Audio.Play(SFX.ui_main_button_select);
                    OuiRandoSaves.Offline = false;
                    Overworld.Goto<OuiRandoSaves>();
                }));
            }
            menu.Add(new TextMenu.Header(Dialog.Clean("CMR_AP_SERVER")));
            menu.Add(Field(() => S.ApServer, v => S.ApServer = v, 64, false));
            menu.Add(new TextMenu.Header(Dialog.Clean("CMR_AP_SLOT")));
            menu.Add(Field(() => S.ApSlot, v => S.ApSlot = v, 16, false));
            menu.Add(new TextMenu.Header(Dialog.Clean("CMR_AP_PASSWORD")));
            menu.Add(Field(() => S.ApPassword, v => S.ApPassword = v, 32, true));
            menu.Add(new TextMenu.SubHeader("", false));
            menu.Add(new TextMenu.Button(Dialog.Clean("CMR_AP_CONNECT")).Pressed(Connect));
            status = new TextMenu.SubHeader("", false);
            menu.Add(status);
            var offline = new TextMenu.Button(Dialog.Clean("CMR_AP_OFFLINE")).Pressed(() => {
                Audio.Play(SFX.ui_main_button_select);
                OuiRandoSaves.Offline = true;
                Overworld.Goto<OuiRandoSaves>();
            });
            menu.Add(offline);
            offline.AddDescription(menu, Dialog.Clean("CMR_AP_OFFLINE_DESC"));
            var yaml = new TextMenu.Button(Dialog.Clean("CMR_YAML_OPEN")).Pressed(() => {
                Audio.Play(SFX.ui_main_button_select);
                Overworld.Goto<OuiYamlGenerator>();
            });
            menu.Add(yaml);
            yaml.AddDescription(menu, Dialog.Clean("CMR_YAML_OPEN_DESC"));
            var vanilla = new TextMenu.Button(Dialog.Clean("CMR_AP_VANILLA")).Pressed(PlayVanilla);
            menu.Add(vanilla);
            vanilla.AddDescription(menu, Dialog.Clean("CMR_AP_VANILLA_DESC"));
            menu.OnCancel = () => {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            };
            UpdateStatus();
            return menu;
        }

        /// <summary>Connect with the fields' values (also used by the self-test).</summary>
        public void Connect() {
            if (ApClient.Connecting) return;
            Audio.Play(SFX.ui_main_button_select);
            RandoModule.Instance.SaveSettings();
            waiting = true;
            ApClient.Connect(S.ApServer, S.ApSlot, S.ApPassword);
            UpdateStatus();
        }

        public void PlayVanilla() {
            Audio.Play(SFX.ui_main_button_select);
            ApClient.Disconnect();
            Overworld.Goto<OuiFileSelect>();
        }

        void UpdateStatus() {
            if (status == null) return;
            status.Title = Dialog.Clean("CMR_AP_STATUS") + " " + Text.Sanitize(ApState.Status, 80);
        }

        public override void Update() {
            base.Update();
            if (!Visible) return;
            UpdateStatus();
            if (waiting && !ApClient.Connecting) {
                waiting = false;
                if (ApClient.Connected && Overworld.Current == this) {
                    Audio.Play("event:/ui/main/button_climb");
                    OuiRandoSaves.Offline = false;
                    Overworld.Goto<OuiRandoSaves>();
                }
            }
        }

        TextMenu.Item Field(Func<string> get, Action<string> set, int max, bool secret) {
            string Shown() {
                var v = get() ?? "";
                if (v.Length == 0) return Dialog.Clean("CMR_AP_NONE");
                return secret ? new string('*', Math.Min(v.Length, 12)) : Text.Sanitize(v, 64);
            }
            return new TextMenu.Button(Shown()).Pressed(() => {
                Audio.Play(SFX.ui_main_savefile_rename_start);
                Overworld.Goto<OuiModOptionString>().Init<OuiApConnect>(get() ?? "", v => {
                    set(Text.Sanitize(v, max));
                    RandoModule.Instance.SaveSettings();
                }, max, 0);
            });
        }
    }
}
