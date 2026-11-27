// CMR: list of the randomizer saves (RandoSaves), shown after a successful connection (or offline, for saves
// whose seed is cached). Vanilla saves never appear here, and randomizer saves never appear in the vanilla list.
using System;
using System.Collections.Generic;
using System.Linq;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public class OuiRandoSaves : GenericOui {
        /// <summary>True: opened without a connection (only saves whose seed is cached can be played).</summary>
        public static bool Offline;

        bool deleteMode;
        int confirmSlot;
        bool starting;
        public List<RandoSaves.Entry> Entries { get; private set; } = new List<RandoSaves.Entry>();

        protected override bool IsDeeperThan(Oui other) => other is OuiApConnect || other is OuiMainMenu || other == null;

        public override System.Collections.IEnumerator Enter(Oui from) {
            deleteMode = false;
            confirmSlot = 0;
            starting = false;
            return base.Enter(from);
        }

        bool Online => !Offline && ApClient.Connected;

        protected override Entity ReloadMenu() {
            Entries = RandoSaves.List();
            var menu = new TextMenu();
            menu.Add(new TextMenu.Header(Dialog.Clean("CMR_SAVES_TITLE")));
            if (Online) {
                menu.Add(new TextMenu.SubHeader(RandoSaves.Fmt("CMR_SAVES_FOR", Text.Sanitize(ApState.SlotName, 32), ShortSeed(ApState.Seed)), false));
                menu.Add(new TextMenu.Button(Dialog.Clean("CMR_SAVES_NEW")).Pressed(NewSave));
                foreach (var legacy in RandoSaves.LegacyBound(ApState.Seed, ApState.SlotName)) {
                    int from = legacy;
                    var b = new TextMenu.Button(RandoSaves.Fmt("CMR_SAVES_IMPORT", from));
                    b.Pressed(() => Import(from));
                    menu.Add(b);
                    b.AddDescription(menu, Dialog.Clean("CMR_SAVES_IMPORT_DESC"));
                }
            } else {
                menu.Add(new TextMenu.SubHeader(Dialog.Clean("CMR_SAVES_OFFLINE"), false));
            }

            menu.Add(new TextMenu.SubHeader(Dialog.Clean(deleteMode ? "CMR_SAVES_LIST_DELETE" : "CMR_SAVES_LIST")));
            if (Entries.Count == 0) menu.Add(new TextMenu.SubHeader(Dialog.Clean("CMR_SAVES_EMPTY"), false));
            foreach (var e in Entries) {
                var entry = e;
                bool usable = Usable(entry);
                string label = confirmSlot == entry.Slot ? RandoSaves.Fmt("CMR_SAVES_CONFIRM_DELETE", RandoSaves.NumberOf(entry.Slot)) : Label(entry);
                var b = new TextMenu.Button(label) { Disabled = !usable && !deleteMode };
                b.Pressed(() => Pick(entry));
                menu.Add(b);
            }
            if (Entries.Count > 0) {
                menu.Add(new TextMenu.OnOff(Dialog.Clean("CMR_SAVES_DELETE_MODE"), deleteMode).Change(v => {
                    deleteMode = v;
                    confirmSlot = 0;
                    Rebuild();
                }));
            }
            menu.OnCancel = () => {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiApConnect>();
            };
            return menu;
        }

        void Rebuild() {
            if (Menu == null) return;
            var old = Menu as TextMenu;
            int sel = old?.Selection ?? 0;
            float x = Menu.X;
            Menu.RemoveSelf();
            Menu = ReloadMenu();
            Menu.X = x;
            Scene.Add(Menu);
            if (Menu is TextMenu tm && sel >= 0 && sel < tm.Items.Count && tm.Items[sel].Selectable) tm.Selection = sel;
            Menu.Active = true;
        }

        bool Usable(RandoSaves.Entry e) {
            if (e.Data == null || e.Ap == null || !e.Ap.IsBound) return false;
            if (Online) return e.Matches(ApState.Seed, ApState.SlotName);
            return ApState.HasCache(e.Ap.Seed, e.Ap.SlotName);
        }

        static string ShortSeed(string seed) {
            seed = Text.Sanitize(seed, 64);
            return seed.Length <= 6 ? seed : "…" + seed.Substring(seed.Length - 6);
        }

        string Label(RandoSaves.Entry e) {
            string n = "#" + RandoSaves.NumberOf(e.Slot);
            if (e.Data == null) return n + "  " + Dialog.Clean("CMR_SAVES_CORRUPT");
            string who = e.Ap != null && e.Ap.IsBound ? Text.Sanitize(e.Ap.SlotName, 16) + " (seed " + ShortSeed(e.Ap.Seed) + ")" : Dialog.Clean("CMR_SAVES_UNBOUND");
            int berries = e.Ap?.ReceivedItems?.Count(i => i == ItemNames.Strawberry) ?? 0;
            int checks = e.Ap?.CheckedLocations?.Count ?? 0;
            string prog = RandoSaves.Fmt("CMR_SAVES_PROGRESS", berries, checks, Dialog.FileTime(e.Data.Time));
            string other = Online && !e.Matches(ApState.Seed, ApState.SlotName) ? "  " + Dialog.Clean("CMR_SAVES_OTHER_SEED") : "";
            return n + "  " + who + "  -  " + prog + other;
        }

        void Pick(RandoSaves.Entry e) {
            if (starting) return;
            if (deleteMode) {
                if (confirmSlot != e.Slot) {
                    Audio.Play(SFX.ui_main_button_select);
                    confirmSlot = e.Slot;
                    Rebuild();
                    return;
                }
                Audio.Play("event:/ui/main/savefile_delete");
                RandoSaves.Delete(e.Slot);
                confirmSlot = 0;
                Rebuild();
                return;
            }
            if (!Usable(e)) return;
            LoadEntry(e.Slot);
        }

        /// <summary>Loads a randomizer save and continues like the vanilla file select (also used by the self-test).</summary>
        public void LoadEntry(int slot) {
            if (starting) return;
            try {
                if (!RandoSaves.LoadAndStart(slot)) {
                    Audio.Play(SFX.ui_main_button_invalid);
                    return;
                }
            } catch (Exception ex) {
                ApState.Log("rando save load failed: " + ex);
                Audio.Play(SFX.ui_main_button_invalid);
                return;
            }
            Begin();
        }

        /// <summary>New randomizer save bound to the connected seed (also used by the self-test).</summary>
        public void NewSave() {
            if (starting || !Online) return;
            if (!RandoSaves.CreateAndStart(out var err)) {
                ApState.Log("new rando save failed: " + err);
                Audio.Play(SFX.ui_main_button_invalid);
                ApState.Status = Dialog.Clean(err == "MAX" ? "CMR_SAVES_FULL" : "CMR_SAVES_FAILED");
                return;
            }
            Begin();
        }

        void Import(int from) {
            if (starting) return;
            try {
                int to = RandoSaves.ImportLegacy(from);
                if (to == 0) { Audio.Play(SFX.ui_main_button_invalid); return; }
                Audio.Play(SFX.ui_main_button_select);
            } catch (Exception ex) {
                ApState.Log("import failed: " + ex.Message);
                Audio.Play(SFX.ui_main_button_invalid);
            }
            Rebuild();
        }

        // ------------------------------------------------------------------ self-test hooks (SelfTest.Saves.cs)
        public bool IsUsable(RandoSaves.Entry e) => Usable(e);
        public bool DeleteMode => deleteMode;
        /// <summary>Same path as the "delete mode" switch of the menu.</summary>
        public void SetDeleteModeForTest(bool v) { deleteMode = v; confirmSlot = 0; Rebuild(); }
        /// <summary>Same path as pressing the entry's button.</summary>
        public void PickForTest(RandoSaves.Entry e) => Pick(e);
        /// <summary>Same path as the "import" button of an old vanilla save.</summary>
        public void ImportForTest(int from) => Import(from);
        public string LabelOf(RandoSaves.Entry e) => Label(e);

        void Begin() {
            starting = true;
            Audio.Play("event:/ui/main/savefile_begin");
            var session = SaveData.Instance.CurrentSession_Safe;
            if (session != null && session.InArea) {
                // the save was quit inside a chapter: resume it, like the vanilla "continue"
                Audio.SetMusic(null);
                Audio.SetAmbience(null);
                new FadeWipe(Scene, false, () => LevelEnter.Go(session, true));
                return;
            }
            Overworld.Goto<OuiChapterSelect>();
        }
    }
}
