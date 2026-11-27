// CMR: randomizer save files, kept apart from the vanilla Celeste saves.
//
// A randomizer save is an ordinary Celeste save file stored at a NEGATIVE slot index: rando save k uses
// FileSlot = FirstSlot - k (-1000, -1001, ...), i.e. files "-1000.celeste", "-1000-modsave-<mod>.celeste", ...
// in the normal Saves folder. Why negative: Everest's file select sizes its slot list from the highest number
// found in "<n>.celeste" / "<n>-mod*.celeste" names (max + 2), and the vanilla file select only shows slots
// 0..that size. Negative numbers never raise that maximum and "-1000-mod..." does not even parse, so these files
// never appear in (nor enlarge) the vanilla list, even with this mod disabled. Slot -1 is the debug save and is
// never used here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static class RandoSaves {
        public const int FirstSlot = -1000;
        public const int MaxSaves = 100;

        public static bool IsRandoSlot(int slot) => slot <= FirstSlot && slot > FirstSlot - MaxSaves;
        public static int SlotFor(int k) => FirstSlot - k;
        public static int NumberOf(int slot) => FirstSlot - slot + 1; // 1-based number shown to the player

        /// <summary>True when the currently loaded save is a randomizer save.</summary>
        public static bool CurrentIsRando => SaveData.Instance != null && IsRandoSlot(SaveData.Instance.FileSlot);

        public class Entry {
            public int Slot;
            public SaveData Data;      // null if unreadable
            public ApSaveData Ap;      // null if no mod data
            public bool Matches(string seed, string slot) => Ap != null && Ap.BoundTo(seed, slot);
        }

        /// <summary>Dialog text with @0, @1... placeholders ([..] and {..} are dialog commands, stripped by Dialog.Clean).</summary>
        public static string Fmt(string key, params object[] args) {
            var s = Dialog.Clean(key);
            for (int i = 0; i < args.Length; i++) s = s.Replace("@" + i, Convert.ToString(args[i], System.Globalization.CultureInfo.InvariantCulture));
            return s;
        }

        static string ModName => RandoModule.Instance?.Metadata?.Name ?? "UltraRando";

        static string ModSavePath(int slot) => UserIO.GetSaveFilePath(SaveData.GetFilename(slot) + "-modsave-" + ModName);

        /// <summary>Every randomizer save found on disk (vanilla saves are never read here).</summary>
        public static List<Entry> List() {
            var res = new List<Entry>();
            bool opened = false;
            try {
                opened = UserIO.Open(UserIO.Mode.Read);
                for (int k = 0; k < MaxSaves; k++) {
                    int slot = SlotFor(k);
                    if (!UserIO.Exists(SaveData.GetFilename(slot))) continue;
                    var e = new Entry { Slot = slot };
                    try {
                        e.Data = UserIO.Load<SaveData>(SaveData.GetFilename(slot));
                        e.Data?.AfterInitialize();
                    } catch (Exception ex) {
                        ApState.Log($"rando save {slot}: unreadable ({ex.Message})");
                        e.Data = null;
                    }
                    e.Ap = ReadAp(slot);
                    res.Add(e);
                }
            } catch (Exception ex) {
                ApState.Log("rando save listing failed: " + ex);
            } finally {
                if (opened) UserIO.Close();
            }
            return res;
        }

        public static ApSaveData ReadAp(int slot) {
            try {
                var p = ModSavePath(slot);
                if (!File.Exists(p)) return null;
                using var reader = new StreamReader(p);
                return YamlHelper.Deserializer.Deserialize<ApSaveData>(reader) ?? new ApSaveData();
            } catch (Exception e) {
                ApState.Log($"rando save {slot}: mod data unreadable ({e.Message})");
                return null;
            }
        }

        public static int FreeSlot() {
            for (int k = 0; k < MaxSaves; k++) {
                int slot = SlotFor(k);
                if (!File.Exists(UserIO.GetSaveFilePath(SaveData.GetFilename(slot)))) return slot;
            }
            return 0; // 0 = none (never a rando slot)
        }

        /// <summary>Creates a new randomizer save bound to the connected seed + slot and makes it current.
        /// Every active chapter is reachable from the chapter select (the AP logic connects all of them to the start).</summary>
        public static bool CreateAndStart(out string error) {
            error = null;
            int slot = FreeSlot();
            if (!IsRandoSlot(slot)) { error = "MAX"; return false; }
            // a stale mod file could exist at this index (deleted save): clean it, only at our own index
            SaveData.TryDeleteModSaveData(slot);
            var data = new SaveData {
                Name = Text.Sanitize(ApState.SlotName, 12),
                AssistMode = false,
                VariantMode = false,
            };
            SaveData.Start(data, slot);
            Unlock(SaveData.Instance, ApState.Slot);
            if (!ApState.SaveMatches(true)) { error = "BIND"; return false; }
            ApState.Log($"new rando save {slot} for seed {ApState.Seed} / slot {ApState.SlotName}");
            UserIO.SaveHandler(true, false);
            return true;
        }

        /// <summary>Opens the chapters (and B/C sides) that the seed uses.</summary>
        static void Unlock(SaveData save, SlotData slot) {
            if (save == null || slot == null) return;
            int maxId = 0;
            bool anyC = false;
            foreach (var a in slot.ActiveAreas) {
                if (!LayoutBuilder.TryParseAreaKey(a, out var key)) continue;
                maxId = Math.Max(maxId, key.ID);
                if (key.Mode == AreaMode.BSide && key.ID < save.Areas_Safe.Count) save.Areas_Safe[key.ID].Cassette = true;
                if (key.Mode == AreaMode.CSide) anyC = true;
            }
            save.UnlockedAreas_Safe = Math.Max(save.UnlockedAreas_Safe, Math.Min(maxId, save.MaxArea));
            if (maxId >= 9) save.RevealedChapter9 = true;
            if (anyC) {
                // vanilla rule: C-sides open once every B-side is completed
                for (int i = 0; i < save.Areas_Safe.Count; i++) {
                    var ad = AreaData.Get(i);
                    if (ad != null && ad.HasMode(AreaMode.BSide) && save.Areas_Safe[i].Modes.Length > 1) save.Areas_Safe[i].Modes[1].Completed = true;
                }
            }
        }

        /// <summary>Loads a randomizer save and makes it current (same steps as the vanilla "continue").</summary>
        public static bool LoadAndStart(int slot) {
            if (!IsRandoSlot(slot)) return false;
            SaveData data = null;
            bool opened = false;
            try {
                opened = UserIO.Open(UserIO.Mode.Read);
                data = UserIO.Load<SaveData>(SaveData.GetFilename(slot));
            } finally {
                if (opened) UserIO.Close();
            }
            if (data == null) return false;
            data.AfterInitialize();
            SaveData.Start(data, slot);
            SaveData.Instance.AssistModeChecks();
            if (ApState.Mode == ApMode.Online) ApState.SaveMatches(true);
            ApState.Log($"rando save {slot} loaded ({Text.Sanitize(ApState.Save?.SlotName, 32)})");
            return true;
        }

        public static void Delete(int slot) {
            if (!IsRandoSlot(slot)) return; // never touch a vanilla slot
            if (SaveData.Instance != null && SaveData.Instance.FileSlot == slot) SaveData.Instance = null;
            SaveData.TryDelete(slot);
            SaveData.TryDeleteModSaveData(slot);
            ApState.Log($"rando save {slot} deleted");
        }

        // ------------------------------------------------------------------ old saves (before the split)

        /// <summary>Vanilla slots that an older version of the mod bound to this seed + slot. They are only READ
        /// (copied into a new randomizer save); the vanilla files are never modified.</summary>
        public static List<int> LegacyBound(string seed, string slotName) {
            var res = new List<int>();
            try {
                var dir = Path.GetDirectoryName(UserIO.GetSaveFilePath("dummy"));
                foreach (var f in Directory.GetFiles(dir, "*-modsave-" + ModName + ".celeste")) {
                    var name = Path.GetFileName(f);
                    var num = name.Substring(0, name.IndexOf("-modsave-", StringComparison.Ordinal));
                    if (!int.TryParse(num, out int slot) || slot < 0) continue;
                    if (!File.Exists(UserIO.GetSaveFilePath(SaveData.GetFilename(slot)))) continue;
                    var ap = ReadAp(slot);
                    if (ap != null && ap.BoundTo(seed, slotName)) res.Add(slot);
                }
            } catch (Exception e) {
                ApState.Log("legacy save scan failed: " + e.Message);
            }
            res.Sort();
            return res;
        }

        /// <summary>Copies vanilla slot <paramref name="from"/> (all its files) into a new randomizer save.</summary>
        public static int ImportLegacy(int from) {
            if (from < 0 || IsRandoSlot(from)) return 0;
            int to = FreeSlot();
            if (!IsRandoSlot(to)) return 0;
            var dir = Path.GetDirectoryName(UserIO.GetSaveFilePath("dummy"));
            string src = SaveData.GetFilename(from), dst = SaveData.GetFilename(to);
            File.Copy(Path.Combine(dir, src + ".celeste"), Path.Combine(dir, dst + ".celeste"), false);
            foreach (var f in Directory.GetFiles(dir, src + "-mod*.celeste")) {
                var name = Path.GetFileName(f);
                File.Copy(f, Path.Combine(dir, dst + name.Substring(src.Length)), false);
            }
            ApState.Log($"vanilla save {from} copied into rando save {to} (original untouched)");
            return to;
        }
    }
}
