// CMR (phase B): central state of the Archipelago / offline session. Everything here runs on the main thread
// except the few fields written by ApClient through its queues.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public enum ApMode {
        None,      // no slot_data: vanilla game
        Offline,   // cmr_offline test file (items from "owned_items" or everything)
        Cached,    // save bound to a seed, slot_data from the local cache, not connected
        Online,    // connected to an Archipelago server
    }

    public static class ApState {
        public const string GameName = "UltraRando";
        const string Tag = "UltraRando/AP";

        public static ApMode Mode { get; private set; } = ApMode.None;
        public static SlotData Slot { get; private set; }
        public static string Seed { get; private set; } = "";
        public static string SlotName { get; private set; } = "";
        public static string Status { get; set; } = "Déconnecté";

        /// <summary>Items received from the server in AP order (online), or replayed from the save (cached).</summary>
        static readonly List<string> received = new List<string>();
        static readonly HashSet<string> owned = new HashSet<string>();
        static bool ownAll;
        /// <summary>Checks sent (or queued) during this run, by id.</summary>
        static readonly HashSet<long> checkedThisRun = new HashSet<long>();
        static readonly HashSet<string> unknownLogged = new HashSet<string>();

        public static bool Active => Slot != null && Mode != ApMode.None;

        public static bool Has(string item) => ownAll || owned.Contains(item);

        /// <summary>Self-test only (SelfTest.Mech.cs): drop every owned item; items are then given back through
        /// ItemReceived, the same path as the server's items.</summary>
        internal static bool CheckedThisRunForTest(long id) => checkedThisRun.Contains(id);
        internal static void ClearOwnedForTest() { ownAll = false; owned.Clear(); received.Clear(); checkedThisRun.Clear(); }

        public static IEnumerable<string> OwnedItems => ownAll ? ItemNames.Movement.Concat(ItemNames.Interactables) : owned;
        public static bool OwnsEverything => ownAll;

        public static ApSaveData Save => RandoModule.Instance?._SaveData as ApSaveData;

        static bool SaveLoaded => SaveData.Instance != null && Save != null;

        // ---------------------------------------------------------------- session start / stop

        public static void StartOffline(SlotData slot, string sourcePath) {
            Stop(false);
            Slot = slot;
            Mode = ApMode.Offline;
            Seed = "offline";
            SlotName = Path.GetFileName(sourcePath ?? "");
            ownAll = slot.OwnedItems == null;
            if (!ownAll) {
                foreach (var it in slot.OwnedItems) AddOwned(it);
            }
            Status = $"Hors ligne : {Text.Sanitize(SlotName, 48)} ({(ownAll ? "tous les items" : owned.Count + " items")})";
            Log($"offline session started from {sourcePath}: {slot.Chapters.Count} chapters, active areas [{string.Join(",", slot.ActiveAreas)}], " +
                $"{slot.LocationIds.Count} locations, {(ownAll ? "all items owned" : owned.Count + " owned items")}");
            ApHud.Message("Mode hors ligne actif");
        }

        /// <summary>Called by ApClient (main thread) after a successful login.</summary>
        public static void StartOnline(SlotData slot, string seed, string slotName) {
            Stop(false);
            Slot = slot;
            Mode = ApMode.Online;
            Seed = Text.Sanitize(seed, 64);
            SlotName = Text.Sanitize(slotName, 32);
            ownAll = false;
            Status = $"Connecté : {SlotName}";
            WriteCache(slot, Seed, SlotName);
            Log($"online session: seed {Seed}, slot {SlotName}, {slot.Chapters.Count} chapters, {slot.LocationIds.Count} locations");
            ApHud.Message("Connecté à Archipelago (" + SlotName + ")");
            OnSaveLoaded();
        }

        /// <summary>Connection lost: keep the layout and items (cached mode) so the player can continue offline.</summary>
        public static void ConnectionLost(string why) {
            if (Mode != ApMode.Online) return;
            Mode = ApMode.Cached;
            Status = "Déconnecté (" + Text.Sanitize(why, 60) + ") - checks mis en file";
            ApHud.Message("Connexion Archipelago perdue");
        }

        public static void Stop(bool message = true) {
            LayoutInstaller.RestoreAll();
            Mode = ApMode.None;
            Slot = null;
            Seed = SlotName = "";
            received.Clear();
            owned.Clear();
            ownAll = false;
            checkedThisRun.Clear();
            goalSent = false;
            Status = "Déconnecté";
            if (message) ApHud.Message("Session Archipelago arrêtée");
        }

        // ---------------------------------------------------------------- items

        static void AddOwned(string name) {
            if (ItemNames.IsKnown(name) || (Slot != null && (Slot.Checkpoints.ContainsKey(name ?? "") || Slot.Locks.Values.Any(l => l.Contains(name))))) {
                owned.Add(name);
            } else if (unknownLogged.Add(name ?? "")) {
                Log($"unknown item name ignored: '{Text.Sanitize(name, 64)}'");
            }
        }

        /// <summary>Online: item number <paramref name="index"/> (AP order) was received.</summary>
        public static void ItemReceived(int index, string name, string fromPlayer) {
            name = Text.Sanitize(name, 64);
            while (received.Count < index) received.Add("");
            if (index < received.Count) received[index] = name; else received.Add(name);
            AddOwned(name);
            SyncSaveItems(Text.Sanitize(fromPlayer, 32));
            if (name == ItemNames.Strawberry) TrySendGoal();
            if (Slot != null && Slot.Checkpoints.ContainsKey(name)) ApplyCheckpoints();
        }

        /// <summary>
        /// checkpointsanity: in the randomizer save, the chapter select offers exactly the checkpoints whose item was
        /// received (the game also unlocks a checkpoint when it is reached: undone here). Called when the save is
        /// loaded, when a checkpoint item arrives and after each room load. Never touches a vanilla save.
        /// </summary>
        /// <summary>True when the game must not unlock this checkpoint by itself (checkpointsanity, item not received).</summary>
        // ---------------------------------------------------------------- session state kept for good

        static bool KeptFlag(string flag) => flag != null && (flag.StartsWith("oshiro_clutter_cleared_") || flag == "oshiro_clutter_door_open" || flag == "disable_lightning");

        /// <summary>Records a piece of session state ("flag" or "gem:N") of the current active area in the save.</summary>
        public static void KeepState(string what) {
            try {
                if (!Active || !(Engine.Scene is Level lv) || !AreaActive(lv.Session.Area, out var ak)) return;
                if (Mode == ApMode.Offline) { offlineKept.Add(ak + "|" + what); return; }
                if (!SaveLoaded || !Save.BoundTo(Seed, SlotName)) return;
                var key = ak + "|" + what;
                if (!Save.KeptState.Contains(key)) { Save.KeptState.Add(key); Log("kept for good: " + key); }
            } catch (Exception e) { Log("keep state failed: " + e.Message); }
        }
        static readonly HashSet<string> offlineKept = new HashSet<string>();

        /// <summary>Is this state kept for good in the area of this level ("door:&lt;room&gt;:&lt;id&gt;"...)?</summary>
        public static bool IsKept(AreaKey area, string what) {
            if (!Active || !AreaActive(area, out var ak)) return false;
            var key = ak + "|" + what;
            if (Mode == ApMode.Offline) return offlineKept.Contains(key);
            return SaveLoaded && Save.BoundTo(Seed, SlotName) && Save.KeptState.Contains(key);
        }

        public static void OnSessionFlag(string flag, bool set) { if (set && KeptFlag(flag)) KeepState(flag); }

        /// <summary>A new session of an active area begins (LevelLoader): put the kept state back.</summary>
        public static void RestoreState(Session session) {
            try {
                if (!Active || session == null || !AreaActive(session.Area, out var ak)) return;
                IEnumerable<string> kept = Mode == ApMode.Offline ? offlineKept : (SaveLoaded && Save.BoundTo(Seed, SlotName) ? Save.KeptState : null);
                if (kept == null) return;
                foreach (var k in kept.Where(k => k.StartsWith(ak + "|")).ToList()) {
                    var what = k.Substring(ak.Length + 1);
                    if (what.StartsWith("gem:")) {
                        if (int.TryParse(what.Substring(4), out int g) && g >= 0 && g < session.SummitGems.Length) session.SummitGems[g] = true;
                    } else if (!session.GetFlag(what)) {
                        session.SetFlag(what, true);
                    }
                }
            } catch (Exception e) { Log("restore state failed: " + e.Message); }
        }

        public static bool CheckpointLocked(AreaKey area, string level) {
            // no "checkpoints" list = seed made before checkpoint items: checkpoints unlock as in the base game
            if (!Active || Slot == null || Slot.Option("checkpointsanity", 0) == 0 || Slot.Checkpoints.Count == 0 || !AreaActive(area, out var ak)) return false;
            if (Mode != ApMode.Offline && !(SaveLoaded && RandoSaves.CurrentIsRando && Save.BoundTo(Seed, SlotName))) return false;
            string where = ak + "|" + level;
            return !Slot.Checkpoints.Any(kv => kv.Value == where && Has(kv.Key));
        }

        public static void ApplyCheckpoints() {
            try {
                if (!Active || Slot == null || Slot.Option("checkpointsanity", 0) == 0 || Slot.Checkpoints.Count == 0 || SaveData.Instance == null) return;
                if (Mode != ApMode.Offline && !(SaveLoaded && RandoSaves.CurrentIsRando && Save.BoundTo(Seed, SlotName))) return;
                var owned = new HashSet<string>(Slot.Checkpoints.Where(kv => Has(kv.Key)).Select(kv => kv.Value));
                foreach (var ak in Slot.ActiveAreas) {
                    if (!LayoutBuilder.TryParseAreaKey(ak, out var key) || key.ID >= SaveData.Instance.Areas_Safe.Count) continue;
                    var modes = SaveData.Instance.Areas_Safe[key.ID].Modes;
                    if ((int)key.Mode >= modes.Length) continue;
                    var cps = modes[(int)key.Mode].Checkpoints;
                    cps.RemoveWhere(level => !owned.Contains(ak + "|" + level));
                    foreach (var w in owned) if (w.StartsWith(ak + "|")) cps.Add(w.Substring(ak.Length + 1));
                }
            } catch (Exception e) {
                Log("checkpoint sync failed: " + e.Message);
            }
        }

        /// <summary>Copies newly received items into the bound save, and notifies the player once per item.</summary>
        static void SyncSaveItems(string fromPlayer = null) {
            if (Mode != ApMode.Online || !SaveLoaded) return;
            var save = Save;
            if (!save.BoundTo(Seed, SlotName)) return;
            if (save.ReceivedIndex > received.Count) {
                // server says fewer items than the save remembers: trust the server
                save.ReceivedIndex = 0;
                save.ReceivedItems.Clear();
            }
            while (save.ReceivedIndex < received.Count) {
                var n = received[save.ReceivedIndex];
                save.ReceivedItems.Add(n);
                save.ReceivedIndex++;
                if (!string.IsNullOrEmpty(n)) Log("received " + n + (string.IsNullOrEmpty(fromPlayer) ? "" : " from " + fromPlayer)); // the big notification comes from the server's ItemSend message
            }
        }

        // ---------------------------------------------------------------- save binding

        /// <summary>True if this save may use the current session (binds an unbound save in online mode).</summary>
        public static bool SaveMatches(bool bindIfFree) {
            if (Mode == ApMode.Offline) return true;
            if (!SaveLoaded) return false;
            // vanilla saves are never bound nor touched: only randomizer saves (RandoSaves) play a seed
            if (!RandoSaves.CurrentIsRando) return false;
            var save = Save;
            if (!save.IsBound) {
                if (!bindIfFree || Mode != ApMode.Online) return false;
                save.Seed = Seed;
                save.SlotName = SlotName;
                save.ReceivedIndex = 0;
                save.ReceivedItems.Clear();
                Log($"save file {SaveData.Instance.FileSlot} bound to seed {Seed} / slot {SlotName}");
                ApHud.Message("Sauvegarde liée à " + SlotName);
                SyncSaveItems();
                FlushChecks();
                return true;
            }
            return save.BoundTo(Seed, SlotName);
        }

        /// <summary>Called when a Celeste save file is loaded (and after a login).</summary>
        public static void OnSaveLoaded() {
            try {
                if (!SaveLoaded) return;
                var save = Save;
                if (!RandoSaves.CurrentIsRando) {
                    // a vanilla save: never plays a seed (even one bound by an older version of the mod)
                    if (Mode == ApMode.Cached) Stop(false);
                    return;
                }
                if (Mode == ApMode.Online) {
                    if (save.BoundTo(Seed, SlotName)) {
                        SyncSaveItems();
                        FlushChecks();
                        ApplyCheckpoints();
                    }
                    return;
                }
                if (Mode == ApMode.Offline) { ApplyCheckpoints(); return; }
                // not connected: resume the save's seed from the local cache
                if (save.IsBound) {
                    var slot = ReadCache(save.Seed, save.SlotName);
                    if (slot == null) {
                        if (Mode == ApMode.Cached) Stop(false);
                        Status = "Sauvegarde liée à " + Text.Sanitize(save.SlotName, 32) + " : connecte-toi au serveur";
                        return;
                    }
                    Stop(false);
                    Slot = slot;
                    Mode = ApMode.Cached;
                    Seed = save.Seed;
                    SlotName = save.SlotName;
                    foreach (var it in save.ReceivedItems) AddOwned(it);
                    Status = "Hors connexion (seed en cache) : " + Text.Sanitize(SlotName, 32);
                    Log($"cached session resumed for seed {Seed} / slot {SlotName}, {owned.Count} items");
                } else if (Mode == ApMode.Cached) {
                    Stop(false);
                }
                ApplyCheckpoints();
            } catch (Exception e) {
                Log("OnSaveLoaded failed: " + e);
            }
        }

        // ---------------------------------------------------------------- save change detection + auto-reconnect

        static SaveData lastSave;
        static string autoConnectTried;

        /// <summary>Main thread, every frame. Everest no longer calls EverestModule.LoadSaveData(int) for us, so a
        /// newly loaded save is detected here: it resumes the bound seed from the cache, then tries to reconnect
        /// with the saved server and the save's slot name.</summary>
        public static void PollSave() {
            var inst = SaveData.Instance;
            if (ReferenceEquals(inst, lastSave)) return;
            lastSave = inst;
            if (inst == null || SelfTest.Running) return;
            OnSaveLoaded();
            var save = Save;
            if (save == null || !save.IsBound || !RandoSaves.CurrentIsRando || Mode == ApMode.Online || ApClient.Connecting) return;
            string tag = save.Seed + "/" + save.SlotName + "/" + inst.FileSlot;
            if (autoConnectTried == tag) return;
            autoConnectTried = tag;
            var s = RandoModule.Instance.SavedData;
            Log($"save {inst.FileSlot} is bound to {save.SlotName}: auto-reconnecting to {Text.Sanitize(s.ApServer, 64)}");
            ApClient.Connect(s.ApServer, save.SlotName, s.ApPassword);
        }

        /// <summary>True when the bound save cannot use its seed (no server, no cache): the caller warns the player.</summary>
        public static bool BoundButInactive {
            get {
                if (!SaveLoaded || Active || !RandoSaves.CurrentIsRando) return false;
                return Save.IsBound;
            }
        }

        // ---------------------------------------------------------------- checks

        static Dictionary<long, Archipelago.MultiClient.Net.Enums.ItemFlags> scouts = new Dictionary<long, Archipelago.MultiClient.Net.Enums.ItemFlags>();
        public static void SetScouts(Dictionary<long, Archipelago.MultiClient.Net.Enums.ItemFlags> s) { scouts = s; Log($"scouted {s.Count} locations"); }
        public static int ScoutCountForTest => scouts.Count;

        /// <summary>Does this location hold only a filler item (Raspberry...)? Unknown (not scouted, offline): false.</summary>
        public static bool HoldsFiller(string key) =>
            Slot != null && key != null && Slot.LocationIds.TryGetValue(key, out long id) && scouts.TryGetValue(id, out var f)
            && (f & (Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement | Archipelago.MultiClient.Net.Enums.ItemFlags.NeverExclude | Archipelago.MultiClient.Net.Enums.ItemFlags.Trap)) == 0;

        /// <summary>Is this slot_data key a location of the seed that is not checked yet?</summary>
        public static bool IsOpenLocation(string key) {
            if (!Active || key == null || !Slot.LocationIds.TryGetValue(key, out long id)) return false;
            if (checkedThisRun.Contains(id)) return false;
            return !(SaveLoaded && Save != null && Save.CheckedLocations.Contains(id));
        }

        public static void Check(string key) {
            if (!Active || key == null) return;
            if (!Slot.LocationIds.TryGetValue(key, out long id)) {
                Logger.Log(LogLevel.Verbose, Tag, "no location for key " + key);
                return;
            }
            if (Mode != ApMode.Offline && !SaveMatches(true)) return;
            if (!checkedThisRun.Add(id)) return;
            if (Mode == ApMode.Offline) {
                Log($"[offline] check {key} -> {id}");
                ApHud.CheckEvent(key, null);
                return;
            }
            var save = Save;
            if (save != null && !save.CheckedLocations.Contains(id)) {
                save.CheckedLocations.Add(id);
                if (Mode != ApMode.Online) ApHud.CheckEvent(key, null); // online: the server's ItemSend message
            }
            if (Mode == ApMode.Online) ApClient.SendChecks(new[] { id });
        }

        /// <summary>Resends every check of the bound save (sync after reconnect).</summary>
        public static void FlushChecks() {
            if (Mode != ApMode.Online || !SaveLoaded) return;
            var save = Save;
            if (!save.BoundTo(Seed, SlotName)) return;
            if (save.CheckedLocations.Count > 0) ApClient.SendChecks(save.CheckedLocations.ToArray());
            TrySendGoal(); // e.g. the goal area was cleared while disconnected
        }

        /// <summary>Strawberry items received so far (online: from the server, cached: from the save).</summary>
        public static int StrawberriesReceived {
            get {
                if (Mode == ApMode.Online) return received.Count(n => n == ItemNames.Strawberry);
                if (Save != null && SaveLoaded) return Save.ReceivedItems.Count(n => n == ItemNames.Strawberry);
                return 0;
            }
        }

        public static void Complete(string areaKey) {
            Check(areaKey + "|clear");
            if (Active && Slot.StringOptions.TryGetValue("goal_area", out var goal) && goal == areaKey) TrySendGoal(true);
        }

        static bool goalSent;
        internal static bool GoalSentForTest => goalSent;

        /// <summary>
        /// Goal (ARCHITECTURE.md) = clear of goal_area + N strawberries received. Checked when the goal area is
        /// cleared, when a strawberry arrives and after each (re)connection, so that a goal reached while disconnected,
        /// or whose last strawberry arrives after the clear, is still sent. The clear is read from the bound save
        /// (it survives a restart of the game). Sent once per session (the server ignores repeats anyway).
        /// </summary>
        public static void TrySendGoal(bool fromClear = false) {
            if (!Active || goalSent || Slot == null) return;
            if (!Slot.StringOptions.TryGetValue("goal_area", out var goal) || !Slot.LocationIds.TryGetValue(goal + "|clear", out long clearId)) return;
            bool cleared = checkedThisRun.Contains(clearId) || (SaveLoaded && Save.BoundTo(Seed, SlotName) && Save.CheckedLocations.Contains(clearId));
            if (!cleared) return;
            int need = Slot.Option("strawberries_required", 0);
            int have = Mode == ApMode.Offline ? int.MaxValue : StrawberriesReceived;
            if (have < need) {
                if (fromClear) ApHud.Message($"Objectif : {have}/{need} fraises");
                return;
            }
            if (Mode != ApMode.Online) {
                if (fromClear) ApHud.Message(Mode == ApMode.Offline ? "Objectif atteint !" : "Objectif atteint : envoyé à la reconnexion");
                return;
            }
            goalSent = true;
            Log("goal reached: sent to the server");
            ApHud.Message("Objectif atteint !");
            ApClient.SendGoal();
        }

        // ---------------------------------------------------------------- area helpers

        public static bool AreaActive(AreaKey area, out string key) {
            key = null;
            if (!Active) return false;
            var data = AreaData.Get(area);
            if (data?.SID == null || !data.SID.StartsWith("Celeste/")) return false;
            key = RoomsExport.AreaKeyString(area);
            return Slot.ActiveAreas.Contains(key);
        }

        // ---------------------------------------------------------------- slot_data cache (one file per seed+slot)

        static string CacheDir => Path.Combine(Everest.PathSettings, "UltraRando");

        static string CachePath(string seed, string slot) =>
            Path.Combine(CacheDir, Text.FileToken(seed) + "__" + Text.FileToken(slot) + ".slot_data.json");

        public static string PendingCacheJson;

        static void WriteCache(SlotData slot, string seed, string slotName) {
            try {
                if (PendingCacheJson == null) return;
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(CachePath(seed, slotName), PendingCacheJson);
                PendingCacheJson = null;
            } catch (Exception e) {
                Log("slot_data cache write failed: " + e.Message);
            }
        }

        public static bool HasCache(string seed, string slotName) {
            try { return File.Exists(CachePath(seed, slotName)); } catch { return false; }
        }

        static SlotData ReadCache(string seed, string slotName) {
            try {
                var p = CachePath(seed, slotName);
                return File.Exists(p) ? SlotData.LoadFile(p) : null;
            } catch (Exception e) {
                Log("slot_data cache read failed: " + e.Message);
                return null;
            }
        }

        public static void Log(string msg) => Logger.Log(LogLevel.Info, Tag, msg);
    }
}
