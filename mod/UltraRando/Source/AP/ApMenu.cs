// CMR (phase B): mod options entries, console commands and the startup offline self-test.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.UI;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static class ApMenu {
        static RandoModuleSettings S => RandoModule.Instance.SavedData;

        public static void Build(TextMenu menu, bool inGame) {
            menu.Add(new TextMenu.SubHeader("Archipelago (UltraRando)"));
            var status = new TextMenu.SubHeader("Statut : " + Text.Sanitize(ApState.Status, 80), false);
            menu.Add(status);

            if (!inGame) {
                // the connection itself is made from the title screen ("Climb" -> OuiApConnect): back on the main
                // menu the session is closed, so a "Connect" button here would be useless
                menu.Add(StringButton("Serveur", () => S.ApServer, v => S.ApServer = v, 64, false));
                menu.Add(StringButton("Slot", () => S.ApSlot, v => S.ApSlot = v, 16, false));
                menu.Add(StringButton("Mot de passe", () => S.ApPassword, v => S.ApPassword = v, 32, true));
                menu.Add(new TextMenu.SubHeader(Dialog.Clean("CMR_AP_MODOPTIONS_HINT"), false));
                menu.Add(new StatusUpdater(status));
                return;
            }
            if (!RandoSaves.CurrentIsRando && ApState.Mode != ApMode.Offline) {
                // in a vanilla save: nothing to connect
                menu.Add(new StatusUpdater(status));
                return;
            }
            menu.Add(new TextMenu.Button(ApClient.Connected ? "Reconnecter" : "Connecter").Pressed(() => {
                var sv = ApState.Save;
                ApClient.Connect(S.ApServer, sv != null && sv.IsBound ? sv.SlotName : S.ApSlot, S.ApPassword);
                status.Title = "Statut : " + Text.Sanitize(ApState.Status, 80);
            }));
            menu.Add(new TextMenu.Button("Déconnecter / arrêter la session").Pressed(() => {
                ApClient.Disconnect();
                status.Title = "Statut : " + Text.Sanitize(ApState.Status, 80);
            }));
            // live status refresh
            menu.Add(new StatusUpdater(status));
        }

        class StatusUpdater : TextMenu.Item {
            readonly TextMenu.SubHeader target;
            public StatusUpdater(TextMenu.SubHeader target) { this.target = target; Selectable = false; Visible = true; }
            public override float Height() => 0f;
            public override void Update() {
                base.Update();
                target.Title = "Statut : " + Text.Sanitize(ApState.Status, 80);
            }
        }

        static TextMenu.Item StringButton(string label, Func<string> get, Action<string> set, int max, bool secret) {
            string Shown() {
                var v = get() ?? "";
                return label + " : " + (secret ? (v.Length == 0 ? "(aucun)" : new string('*', Math.Min(v.Length, 12))) : Text.Sanitize(v, 64));
            }
            var btn = new TextMenu.Button(Shown());
            btn.Pressed(() => {
                Audio.Play(SFX.ui_main_savefile_rename_start);
                if (!(Engine.Scene is Overworld ow)) return;
                ow.Goto<OuiModOptionString>().Init<OuiModOptions>(get() ?? "", v => {
                    set(Text.Sanitize(v, max));
                    RandoModule.Instance.SaveSettings();
                }, max, 0);
            });
            return btn;
        }

        // ------------------------------------------------------------------ console commands

        [Command("cmr_connect", "UltraRando: connect to Archipelago [server] [slot] [password] (defaults: mod options)")]
        public static void CmdConnect(string server = null, string slot = null, string password = null) {
            if (!string.IsNullOrEmpty(server)) S.ApServer = server;
            if (!string.IsNullOrEmpty(slot)) S.ApSlot = slot;
            if (password != null) S.ApPassword = password;
            RandoModule.Instance.SaveSettings();
            ApClient.Connect(S.ApServer, S.ApSlot, S.ApPassword);
            Engine.Commands?.Log(ApState.Status);
        }

        [Command("cmr_disconnect", "UltraRando: disconnect and stop the Archipelago/offline session")]
        public static void CmdDisconnect() {
            ApClient.Disconnect();
            ApState.Stop();
            Engine.Commands?.Log("session stopped");
        }

        [Command("cmr_status", "UltraRando: show the Archipelago session status")]
        public static void CmdStatus() {
            Engine.Commands?.Log($"mode {ApState.Mode}, {ApState.Status}");
            if (ApState.Slot != null) {
                Engine.Commands?.Log($"active areas: {string.Join(",", ApState.Slot.ActiveAreas)}");
                Engine.Commands?.Log($"items: {(ApState.OwnsEverything ? "all" : string.Join(", ", ApState.OwnedItems.Take(40)))}");
            }
            var save = ApState.Save;
            if (save != null && SaveData.Instance != null) {
                Engine.Commands?.Log($"save: seed '{Text.Sanitize(save.Seed, 40)}' slot '{Text.Sanitize(save.SlotName, 20)}' items {save.ReceivedIndex} checks {save.CheckedLocations.Count}");
            }
        }

        public static string DefaultOfflinePath => Path.Combine(Everest.PathGame, "CMR_offline_slot_data.json");

        [Command("cmr_offline", "UltraRando: load a slot_data JSON file without server (path without spaces; default <Celeste>/CMR_offline_slot_data.json)")]
        public static void CmdOffline(string path = null) {
            path = string.IsNullOrEmpty(path) ? DefaultOfflinePath : path;
            try {
                ApClient.Disconnect(false);
                var slot = SlotData.LoadFile(path);
                ApState.StartOffline(slot, path);
                Engine.Commands?.Log($"offline session: {slot.Chapters.Count} chapters, active [{string.Join(",", slot.ActiveAreas)}]. Enter one of these chapters to play its layout.");
            } catch (Exception e) {
                ApState.Log("cmr_offline failed: " + e);
                Engine.Commands?.Log("cmr_offline failed: " + e.Message);
            }
        }
    }
}
