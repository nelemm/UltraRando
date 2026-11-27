// CMR (phase B): automatic self-test driven by the flag file <Celeste>/CMR_OFFLINE_TEST.
//   line 1: path of a slot_data JSON file (empty = <Celeste>/CMR_offline_slot_data.json)
//   line 2 (optional): "play"    = also load the first active chapter and visit every room of its built map
//                      "playall" = same for every active chapter that has a layout
// The play phase runs on the DEBUG save file (real saves are untouched). Every room load is checked; exceptions
// are recorded per room and the tour goes on. At the end the result is written to CMR_OFFLINE_TEST.result and,
// when a play phase ran, Celeste quits by itself.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static partial class SelfTest {
        public static string TestFlag => Path.Combine(Everest.PathGame, "CMR_OFFLINE_TEST");
        public static string TestResult => Path.Combine(Everest.PathGame, "CMR_OFFLINE_TEST.result");

        const float RoomTimeout = 8f;
        const float Settle = 0.2f;

        static StringBuilder report;
        static bool ok;
        static SlotData slot;
        static string slotPath;
        static int phase;               // 0 idle, 1 wait for overworld, 2 touring
        static float timer, roomTimer;
        static List<(string area, AreaKey key, string room)> queue;
        static int index;
        static bool requested;
        static readonly Dictionary<string, string> roomErrors = new Dictionary<string, string>();
        static readonly Dictionary<string, string> opts = new Dictionary<string, string>();
        static int roomsOk, roomsFailed;
        static MonoMod.RuntimeDetour.Hook loadHook;
        static HashSet<string> areaFilter;
        static long lastProgress;      // Environment.TickCount64 of the last tour progress (watchdog)
        static string currentRoom = "";
        static volatile bool finished;
        public static bool Running => phase != 0;

        public static void RunIfFlagged() {
            if (!File.Exists(TestFlag)) return;
            report = new StringBuilder();
            ok = true;
            string mode = "";
            try {
                try {
                    report.AppendLine("assemblies: " + ApClient.AssemblyCheck());
                } catch (Exception e) {
                    ok = false;
                    report.AppendLine("assemblies: FAILED " + e.GetType().Name + ": " + e.Message);
                }
                var lines = File.ReadAllLines(TestFlag).Select(l => l.Trim()).ToArray();
                slotPath = lines.Length > 0 && lines[0].Length > 0 ? lines[0] : ApMenu.DefaultOfflinePath;
                mode = lines.Length > 1 ? lines[1] : "";
                areaFilter = null;
                for (int li = 2; li < lines.Length; li++) {
                    var line = lines[li];
                    if (line.Length == 0) continue;
                    int eq = line.IndexOf('=');
                    string k = eq < 0 ? (li == 2 ? "areas" : "") : line.Substring(0, eq).Trim();
                    string v = eq < 0 ? line : line.Substring(eq + 1).Trim();
                    if (k.Length > 0) opts[k] = v;
                    if (k == "areas") areaFilter = new HashSet<string>(v.Split(',').Select(s => s.Trim()));
                    else if (k == "vanilla") walkVanilla = v == "1" || v == "true";
                    else if (k == "shots") shotsDir = v;
                    else if (k == "maxshots") int.TryParse(v, out maxShots);
                    else if (k == "from") int.TryParse(v, out walkFrom);
                    else if (k == "count") int.TryParse(v, out walkCount);
                }
                if (mode == "saves") {
                    // menu-driven test (SelfTest.Saves.cs): no slot_data file, the seed comes from the server
                    SetupSaves();
                    phase = 2;
                    timer = 0;
                    lastProgress = Environment.TickCount64;
                    StartWatchdog();
                    return;
                }
                slot = SlotData.LoadFile(slotPath);
                report.AppendLine($"slot_data: {slotPath}: {slot.Chapters.Count} chapters, {slot.LocationIds.Count} locations, mode '{mode}'");
                foreach (var kv in slot.Chapters) {
                    try {
                        if (!LayoutBuilder.TryParseAreaKey(kv.Key, out var key)) throw new Exception("bad area key");
                        var vanilla = AreaData.Get(key).Mode[(int)key.Mode].MapData;
                        var res = LayoutBuilder.Build(kv.Key, kv.Value, vanilla);
                        // every level must be found by name, and must be OUR level (Everest caches levels by name)
                        int wrong = res.Map.Levels.Count(l => !ReferenceEquals(res.Map.Get(l.Name), l));
                        int missing = kv.Value.Rooms.Count(r => res.Map.Get(r.Room) == null);
                        var start = res.Map.StartLevel();
                        bool startOurs = start != null && res.Map.Levels.Contains(start);
                        report.AppendLine($"{kv.Key}: build {res.Rooms} rooms, {res.Map.Levels.Count} levels, {res.Links} links ({res.LooseLinks} loose, {res.OneWayClosed} closed one-way), " +
                                          $"start={start?.Name}{(startOurs ? "" : " (NOT FROM LAYOUT)")}, missing={missing}, name-cache mismatches={wrong}, warnings={res.Warnings.Count}");
                        foreach (var w in res.Warnings.Take(10)) report.AppendLine("  warning: " + w);
                        if (missing > 0 || wrong > 0 || !startOurs) ok = false;
                        // mislabel detector: every built level must look like the vanilla level of the same name
                        int mislabeled = 0;
                        foreach (var l in res.Map.Levels) {
                            var (best, bestSim, sameSim) = BestVanillaMatch(vanilla, l);
                            if (best != l.Name) {
                                mislabeled++;
                                if (mislabeled <= 10) report.AppendLine($"  MISLABEL? {l.Name}: same-name sim {sameSim:P0}, best vanilla match {best} ({bestSim:P0})");
                            }
                        }
                        report.AppendLine($"  solids check: {res.Map.Levels.Count - mislabeled}/{res.Map.Levels.Count} levels best-match their vanilla namesake");
                        // census: every collectible location of this chapter has its entity in the built room
                        var kindNames = new Dictionary<string, string[]> {
                            { "strawberry", new[] { "strawberry", "memorialTextController" } }, { "golden", new[] { "goldenBerry" } },
                            { "key", new[] { "key" } }, { "gem", new[] { "summitgem" } }, { "heart", new[] { "blackGem", "birdForsakenCityGem" } },
                            { "cassette", new[] { "cassette" } },
                        };
                        int census = 0, missingEnt = 0;
                        foreach (var lk in slot.LocationIds.Keys.Where(k => k.StartsWith(kv.Key + "|"))) {
                            var parts = lk.Split('|');
                            if (parts.Length != 4 || !kindNames.TryGetValue(parts[2], out var names) || !int.TryParse(parts[3], out int eid)) continue;
                            var lvl = res.Map.Get(parts[1]);
                            if (lvl == null) continue; // room not placed: no location is created for it by the apworld either
                            census++;
                            if (!lvl.Entities.Any(e => e.ID == eid && names.Contains(e.Name))) {
                                missingEnt++;
                                report.AppendLine($"  MISSING COLLECTIBLE {lk}: no {string.Join("/", names)} #{eid} in the built room ({string.Join(",", lvl.Entities.Where(e => kindNames.Values.Any(n => n.Contains(e.Name))).Select(e => e.Name + "#" + e.ID))})");
                            }
                        }
                        report.AppendLine($"  collectibles census: {census - missingEnt}/{census} collectible locations have their entity in the built room");
                        if (missingEnt > 0) ok = false;
                        if (mislabeled > 0) ok = false;
                    } catch (Exception e) {
                        ok = false;
                        report.AppendLine($"{kv.Key}: build FAILED {e}");
                    }
                }
            } catch (Exception e) {
                ok = false;
                report.AppendLine("FAILED " + e);
            }
            if (mode == "mech" && slot != null) {
                SetupMech();
                phase = 1;
                timer = 0;
                lastProgress = Environment.TickCount64;
                StartWatchdog();
            } else if (mode == "walk" && slot != null) {
                SetupWalk();
                phase = 1;
                timer = 0;
                lastProgress = Environment.TickCount64;
                StartWatchdog();
            } else if ((mode == "play" || mode == "playall") && slot != null) {
                var areas = slot.ActiveAreas.Where(a => slot.Chapters.ContainsKey(a) && (areaFilter == null || areaFilter.Contains(a))).ToList();
                if (mode == "play") areas = areas.Take(1).ToList();
                queue = new List<(string, AreaKey, string)>();
                foreach (var a in areas) {
                    if (!LayoutBuilder.TryParseAreaKey(a, out var key)) continue;
                    var statics = LayoutBuilder.RoomKeys(a);
                    foreach (var r in slot.Chapters[a].Rooms) {
                        var vkey = string.IsNullOrEmpty(r.Variant) ? r.Room : r.Variant;
                        var name = statics.TryGetValue(vkey, out var st) ? st.Level.Name : r.Room;
                        if (!queue.Any(q => q.Item1 == a && q.Item3 == name)) queue.Add((a, key, name));
                    }
                }
                report.AppendLine($"play: {areas.Count} chapter(s) [{string.Join(",", areas)}], {queue.Count} rooms to visit");
                phase = 1;
                timer = 0;
                lastProgress = Environment.TickCount64;
                StartWatchdog();
                if (loadHook == null) {
                    // Everest wraps Level.orig_LoadLevel in its own try/catch; hook the inner method to see the exception
                    var m = typeof(Level).GetMethod("orig_LoadLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (m != null) loadHook = new MonoMod.RuntimeDetour.Hook(m, new Action<Action<Level, Player.IntroTypes, bool>, Level, Player.IntroTypes, bool>(RecordLoadException));
                    else report.AppendLine("play: Level.orig_LoadLevel not found, exceptions only seen as timeouts");
                }
            } else {
                Finish(false);
            }
        }

        static void RecordLoadException(Action<Level, Player.IntroTypes, bool> orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
            try {
                orig(self, playerIntro, isFromLoader);
            } catch (Exception e) {
                var key = RoomsExport.AreaKeyString(self.Session.Area) + "|" + self.Session.Level;
                if (!roomErrors.ContainsKey(key)) roomErrors[key] = e.GetType().Name + ": " + e.Message + " @ " + FirstFrames(e);
                try {
                    var md = self.Session.MapData;
                    var ld = self.Session.LevelData;
                    var sb = new StringBuilder($"diag {key}: map.Bounds={md.Bounds} TileBounds={md.TileBounds} solids={self.SolidsData?.Columns}x{self.SolidsData?.Rows} " +
                        $"level.Bounds={ld.Bounds} inMap={md.Levels.Contains(ld)}");
                    foreach (var en in ld.Entities) {
                        if (en.Name == "fakeWall" || en.Name == "coverupWall" || en.Name == "exitBlock" || en.Name == "dashBlock" || en.Name == "fakeBlock")
                            sb.Append($" | {en.Name}#{en.ID} pos={en.Position} size={en.Width}x{en.Height}");
                    }
                    report.AppendLine(sb.ToString());
                } catch { }
                throw;
            }
        }

        static string FirstFrames(Exception e) {
            var st = (e.StackTrace ?? "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).Take(3);
            return string.Join(" | ", st);
        }

        /// <summary>Main-thread hang protection: if the tour makes no progress for 40 s, write the result from this
        /// thread and terminate the process (a hung game cannot be closed any other way).</summary>
        static void StartWatchdog() {
            var th = new System.Threading.Thread(() => {
                while (!finished) {
                    System.Threading.Thread.Sleep(1000);
                    if (finished) return;
                    if (Environment.TickCount64 - lastProgress > 40000) {
                        lock (report) {
                            report.AppendLine($"play: HANG (no progress for 40 s) at {currentRoom}");
                            ok = false;
                            report.Insert(0, "CMR OFFLINE TEST: FAILURE\n");
                            try { File.WriteAllText(TestResult, report.ToString()); } catch { }
                        }
                        Logger.Log(LogLevel.Error, "UltraRando/AP", "self-test watchdog: game hung at " + currentRoom + ", terminating");
                        Environment.Exit(3);
                    }
                }
            }) { IsBackground = true, Name = "CMR self-test watchdog" };
            th.Start();
        }

        static void Finish(bool quit) {
            if (finished) return;
            finished = true;
            phase = 0;
            loadHook?.Dispose();
            loadHook = null;
            if (queue != null) {
                report.AppendLine($"play: {roomsOk} rooms OK, {roomsFailed} rooms FAILED");
                if (roomsFailed > 0) ok = false;
            }
            report.Insert(0, ok ? "CMR OFFLINE TEST: SUCCESS\n" : "CMR OFFLINE TEST: FAILURE\n");
            var text = report.ToString();
            Logger.Log(ok ? LogLevel.Info : LogLevel.Error, "UltraRando/AP", text);
            try { File.WriteAllText(TestResult, text); } catch { }
            if (quit) {
                Logger.Log(LogLevel.Info, "UltraRando/AP", "self-test finished, quitting Celeste");
                Engine.Instance.Exit();
            }
        }

        /// <summary>Main thread, every frame.</summary>
        public static void Update() {
            if (phase == 0) return;
            try {
                if (global::Celeste.Mod.UI.CriticalErrorHandler.CurrentHandler != null || (Engine.Scene != null && Engine.Scene.GetType().Name.Contains("CriticalError"))) {
                    report.AppendLine($"play: Everest critical error screen while at {currentRoom}; tour stopped");
                    ok = false;
                    Finish(true);
                    return;
                }
                timer += Engine.RawDeltaTime;
                if (savesSteps != null) {
                    SavesUpdate();
                    return;
                }
                if (phase == 1) {
                    if (!(Engine.Scene is Overworld) || timer < 2f) return;
                    if (!(walkSteps != null && walkVanilla)) ApState.StartOffline(slot, slotPath);
                    if (mechScript != null) ApState.ClearOwnedForTest();
                    SaveData.InitializeDebugMode();
                    phase = 2;
                    index = 0;
                    requested = false;
                    return;
                }
                if (walkSteps != null) {
                    WalkUpdate();
                    return;
                }
                if (mechScript != null) {
                    MechUpdate();
                    return;
                }
                if (index >= queue.Count) {
                    Finish(true);
                    return;
                }
                var (area, key, room) = queue[index];
                var level = Engine.Scene as Level;
                if (!requested) {
                    Request(level, area, key, room);
                    return;
                }
                roomTimer += Engine.RawDeltaTime;
                string errKey = area + "|" + room;
                bool there = level != null && level.Session.Area == key && level.Session.Level == room && !level.Transitioning &&
                             level.Tracker.GetEntity<Player>() != null;
                if (roomErrors.TryGetValue(errKey, out var err)) {
                    Record(area, room, false, err);
                } else if (there && roomTimer >= Settle) {
                    Record(area, room, true, null);
                } else if (roomTimer > RoomTimeout) {
                    Record(area, room, false, $"not reached within {RoomTimeout}s (scene {Engine.Scene?.GetType().Name}, room {level?.Session?.Level})");
                }
            } catch (Exception e) {
                report.AppendLine("play: test driver exception " + e);
                ok = false;
                Finish(true);
            }
        }

        static void Record(string area, string room, bool success, string error) {
            if (success) {
                roomsOk++;
            } else {
                roomsFailed++;
                report.AppendLine($"play: {area}|{room} FAILED {error}");
            }
            Logger.Log(LogLevel.Info, "UltraRando/AP", $"self-test {area}|{room}: {(success ? "ok" : "FAILED " + error)}");
            lastProgress = Environment.TickCount64;
            index++;
            requested = false;
        }

        static void Request(Level level, string area, AreaKey key, string room) {
            requested = true;
            currentRoom = area + "|" + room;
            Logger.Log(LogLevel.Info, "UltraRando/AP", "self-test going to " + currentRoom);
            roomTimer = 0;
            bool canTeleport = level != null && level.Session.Area == key && !level.Completed && !level.Transitioning &&
                               LayoutBuilder.IsApLayout(level.Session.MapData) && level.Tracker.GetEntity<Player>() is Player p && !p.Dead &&
                               level.Session.MapData.Get(room) != null;
            if (canTeleport) {
                level.OnEndOfFrame += () => {
                    try {
                        var pl = level.Tracker.GetEntity<Player>();
                        if (pl != null) level.TeleportTo(pl, room, Player.IntroTypes.Respawn);
                    } catch (Exception e) {
                        var k = area + "|" + room;
                        if (!roomErrors.ContainsKey(k)) roomErrors[k] = "teleport: " + e.GetType().Name + ": " + e.Message + " @ " + FirstFrames(e);
                    }
                };
                return;
            }
            // fresh load (first room of a chapter, or recovery after a failure / chapter end)
            LevelEnter.ErrorMessage = null;
            var session = new Session(key) { FirstLevel = false, StartedFromBeginning = false };
            session.Level = room;
            session.RespawnPoint = null;
            SaveData.Instance.StartSession(session);
            Engine.Scene = new LevelLoader(session);
        }
    }
}
