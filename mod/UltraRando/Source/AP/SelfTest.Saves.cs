// CMR: "saves" self-test, driven by code (no input): title -> vanilla file select (recorded) -> main menu ->
// "Climb" (hooked) -> connection screen -> Connect (real server) -> randomizer save list -> new or existing save ->
// chapter select -> journal page -> optional chapter load. Screenshots are taken from the back buffer.
// Flag file: line 1 empty, line 2 = "saves", then key=value lines:
//   shots=<dir>  vanillafs=1  server=localhost:38281  slot=Nelem  newsave=1  journal=1  journalshot=<png>  level=1a
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static partial class SelfTest {
        static List<string> savesSteps;
        static int savesIndex;
        static float savesTimer;
        static bool savesStarted;
        static string pendingShot;
        static string savedServer, savedSlot, savedPassword;
        static bool settingsOverridden;
        static bool acted;
        static List<int> journalShots;
        static int journalShot;
        static float journalTimer;
        static bool offlinePicked;
        static float offlinePickTime;
        static bool Once() { if (acted) return false; acted = true; return true; }

        static string Opt(string k, string def = "") => opts.TryGetValue(k, out var v) ? v : def;

        static void SetupSaves() {
            savesSteps = new List<string> { "start" };
            if (Opt("vanillafs") == "1") savesSteps.Add("vanilla_fs");
            if (Opt("offline").Length > 0) savesSteps.Add("offline"); // journal preview of a slot_data file on the DEBUG save
            else if (Opt("offlinelist") == "1") savesSteps.AddRange(new[] { "to_menu", "climb", "offline_list" }); // no server
            else if (Opt("manage") == "1") savesSteps.AddRange(new[] { "to_menu", "climb", "connect", "manage" });
            else savesSteps.AddRange(new[] { "to_menu", "climb", "connect", "pick" });
            if (Opt("journal") == "1") savesSteps.Add("journal");
            if (Opt("level").Length > 0) savesSteps.Add("level");
            if (Opt("help") == "1") savesSteps.Add("help"); // needs level=: F6 panel in that level
            if (Opt("chain").Length > 0) savesSteps.Add("chain"); // SelfTest.Chain.cs, after "pick"
            savesSteps.Add("finish");
            shotsDir = Opt("shots");
            if (shotsDir.Length > 0) {
                try { Directory.CreateDirectory(shotsDir); } catch (Exception e) { report.AppendLine("shots dir: " + e.Message); shotsDir = null; }
            }
            On.Monocle.Engine.Draw += CaptureAfterDraw;
            report.AppendLine("saves test: steps " + string.Join(" > ", savesSteps));
        }

        static void CaptureAfterDraw(On.Monocle.Engine.orig_Draw orig, Engine self, GameTime gameTime) {
            orig(self, gameTime);
            var path = pendingShot;
            if (path == null) return;
            pendingShot = null;
            try {
                var gd = Engine.Graphics.GraphicsDevice;
                int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
                var data = new Color[w * h];
                gd.GetBackBufferData(data);
                using var tex = new Texture2D(gd, w, h);
                tex.SetData(data);
                using var fs = File.Create(path);
                tex.SaveAsPng(fs, w, h);
                report.AppendLine("  screenshot " + path);
            } catch (Exception e) {
                report.AppendLine("  screenshot FAILED " + path + ": " + e.Message);
            }
        }

        static void Shot(string name) {
            if (string.IsNullOrEmpty(shotsDir) && !Path.IsPathRooted(name)) return;
            var path = Path.IsPathRooted(name) ? name : Path.Combine(shotsDir, name);
            if (!shotsTaken.Add(path)) return; // one capture per file (a 2560x1440 PNG costs ~0.5 s)
            pendingShot = path;
        }

        static readonly HashSet<string> shotsTaken = new HashSet<string>();

        static void NextSave(string note = null) {
            if (note != null) report.AppendLine("  " + note);
            savesIndex++;
            savesTimer = 0;
            savesStarted = false;
            acted = false;
            lastProgress = Environment.TickCount64;
        }

        static void FailSaves(string why) {
            report.AppendLine($"saves: step '{savesSteps[savesIndex]}' FAILED: {why}");
            ok = false;
            savesIndex = savesSteps.Count - 1; // finish
            savesTimer = 0;
            savesStarted = false;
            acted = false;
        }

        static void SavesUpdate() {
            if (savesIndex >= savesSteps.Count) return;
            savesTimer += Engine.RawDeltaTime;
            var step = savesSteps[savesIndex];
            var ow = Engine.Scene as Overworld;
            var cur = ow?.Current;
            if (!savesStarted) {
                savesStarted = true;
                report.AppendLine($"saves: {step} (scene {Engine.Scene?.GetType().Name}, ui {cur?.GetType().Name})");
                Logger.Log(LogLevel.Info, "UltraRando/AP", "self-test saves step " + step);
            }
            // chain: bounded by the 115 s launch cap instead
            if (savesTimer > 30f && step != "finish" && step != "chain") { FailSaves("timeout in scene " + Engine.Scene?.GetType().Name + " / " + cur?.GetType().Name); return; }
            switch (step) {
                case "start":
                    if (ow != null && cur != null && savesTimer > 4f) NextSave();
                    break;

                case "vanilla_fs":
                    if (savesTimer < 0.1f) return;
                    if (!(cur is OuiFileSelect)) {
                        if (Once()) { OuiFileSelect.Loaded = false; ow.Goto<OuiFileSelect>(); }
                        return;
                    }
                    if (!OuiFileSelect.Loaded || savesTimer < 4f) return;
                    if (savesTimer < 4.5f) { Shot("01_vanilla_fileselect.png"); return; }
                    if (pendingShot != null) return;
                    {
                        var fs = (OuiFileSelect)cur;
                        var slots = fs.Slots;
                        var exists = slots.Where(s => s != null && s.Exists).Select(s => $"{s.FileSlot}:{Text.Sanitize(s.Name, 16)}").ToList();
                        bool leak = slots.Any(s => s != null && RandoSaves.IsRandoSlot(s.FileSlot));
                        report.AppendLine($"  VANILLA FILE SELECT: {slots.Length} slots, existing [{string.Join(", ", exists)}], rando slot visible: {(leak ? "YES (BUG)" : "no")}");
                        if (leak) ok = false;
                        NextSave();
                    }
                    break;

                case "offline":
                    if (Once()) {
                        ApState.StartOffline(SlotData.LoadFile(Opt("offline")), Opt("offline"));
                        SaveData.InitializeDebugMode(); // the debug save is never written by this test
                        ow.Goto<OuiChapterSelect>();
                        return;
                    }
                    if (cur is OuiChapterSelect && savesTimer > 3f) NextSave($"offline slot_data on the debug save, {ApState.OwnedItems.Count()} items owned");
                    break;

                case "to_menu":
                    if (Once()) { if (!(cur is OuiMainMenu)) ow.Goto<OuiMainMenu>(); return; }
                    if (!(cur is OuiMainMenu) || savesTimer < 2.5f) return;
                    if (savesTimer < 2.7f) { Shot("00_title_menu.png"); return; }
                    if (pendingShot == null) NextSave();
                    break;

                case "climb":
                    if (Once()) {
                        var menu = ow.GetUI<OuiMainMenu>();
                        typeof(OuiMainMenu).GetMethod("OnBegin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                            .Invoke(menu, null);
                        return;
                    }
                    if (cur is OuiFileSelect) { FailSaves("Climb opened the vanilla file select"); return; }
                    if (!(cur is OuiApConnect) || savesTimer < 2.5f) return;
                    if (savesTimer < 3f) { Shot("02_connect.png"); return; }
                    if (pendingShot == null) NextSave("Climb -> OuiApConnect OK");
                    break;

                case "connect":
                    if (Once()) {
                        var s = RandoModule.Instance.SavedData;
                        string server = Opt("server"), slotName = Opt("slot"), pw = Opt("password");
                        if ((server.Length > 0 && server != s.ApServer) || (slotName.Length > 0 && slotName != s.ApSlot) || pw.Length > 0) {
                            savedServer = s.ApServer; savedSlot = s.ApSlot; savedPassword = s.ApPassword; settingsOverridden = true;
                            if (server.Length > 0) s.ApServer = server;
                            if (slotName.Length > 0) s.ApSlot = slotName;
                            if (pw.Length > 0) s.ApPassword = pw;
                        }
                        ow.GetUI<OuiApConnect>().Connect();
                        return;
                    }
                    if (!(cur is OuiRandoSaves) || savesTimer < 3f) return;
                    if (savesTimer < 3.5f) { Shot("03_rando_saves.png"); return; }
                    if (pendingShot != null) return;
                    {
                        var list = ((OuiRandoSaves)cur).Entries;
                        report.AppendLine($"  connected: mode {ApState.Mode}, seed {ApState.Seed}, slot {ApState.SlotName}; rando saves listed: {list.Count}");
                        foreach (var e in list)
                            report.AppendLine($"    rando save {e.Slot} (#{RandoSaves.NumberOf(e.Slot)}): bound {e.Ap?.SlotName}/{e.Ap?.Seed}, items {e.Ap?.ReceivedIndex}, checks {e.Ap?.CheckedLocations?.Count}");
                        bool leak = list.Any(e => !RandoSaves.IsRandoSlot(e.Slot));
                        if (leak) { ok = false; report.AppendLine("  BUG: a non-rando slot is in the rando list"); }
                        NextSave();
                    }
                    break;

                case "pick":
                    if (Once()) {
                        var ui = (OuiRandoSaves)cur;
                        if (Opt("newsave") == "1") {
                            report.AppendLine("  creating a new rando save");
                            ui.NewSave();
                        } else {
                            // the newest save of this seed (randomizer slots go -1000, -1001...: the most negative is the newest)
                            var e = ui.Entries.Where(x => x.Matches(ApState.Seed, ApState.SlotName)).OrderBy(x => x.Slot).FirstOrDefault();
                            if (e == null) { FailSaves("no rando save bound to this seed"); return; }
                            report.AppendLine($"  loading rando save {e.Slot}");
                            ui.LoadEntry(e.Slot);
                        }
                        return;
                    }
                    // a save quit inside a chapter resumes it (vanilla "continue"): a Level is fine too
                    if (!(cur is OuiChapterSelect || (Engine.Scene is Level rl && rl.Tracker.GetEntity<Player>() != null)) || savesTimer < 3.5f) return;
                    if (savesTimer < 4f) { Shot("04_chapter_select.png"); return; }
                    if (pendingShot != null) return;
                    {
                        var sd = SaveData.Instance;
                        var ap = ApState.Save;
                        report.AppendLine($"  CURRENT SAVE: FileSlot {sd?.FileSlot} (rando: {RandoSaves.CurrentIsRando}), name {Text.Sanitize(sd?.Name, 16)}, " +
                                          $"bound {ap?.SlotName}/{ap?.Seed}, unlocked areas {sd?.UnlockedAreas_Safe}, ApState {ApState.Mode}, matches {ApState.SaveMatches(false)}");
                        if (!RandoSaves.CurrentIsRando || !ApState.SaveMatches(false)) ok = false;
                        NextSave();
                    }
                    break;

                case "manage": {
                    // import of an old vanilla save bound to this seed, then deletion of the copy with the delete mode
                    var ui = cur as OuiRandoSaves;
                    if (ui == null) { if (savesTimer > 5f) FailSaves("not on the rando save list"); return; }
                    if (savesTimer < 1f) return;
                    var legacy = RandoSaves.LegacyBound(ApState.Seed, ApState.SlotName);
                    report.AppendLine($"  old vanilla saves bound to this seed: [{string.Join(", ", legacy)}]");
                    var dir = Path.GetDirectoryName(UserIO.GetSaveFilePath("dummy"));
                    string Hash(int slot) {
                        var name = SaveData.GetFilename(slot);
                        var files = Directory.GetFiles(dir, name + ".celeste").Concat(Directory.GetFiles(dir, name + "-mod*.celeste")).OrderBy(f => f).ToList();
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        var sb = new System.Text.StringBuilder();
                        foreach (var f in files) sb.Append(Path.GetFileName(f)).Append(':').Append(Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(f))).Substring(0, 12)).Append(' ');
                        return sb.ToString();
                    }
                    var before = ui.Entries.Select(e => e.Slot).ToList();
                    int target = 0;
                    if (legacy.Count > 0) {
                        int from = legacy[0];
                        string h0 = Hash(from);
                        ui.ImportForTest(from);
                        var after = RandoSaves.List().Select(e => e.Slot).ToList();
                        var added = after.Except(before).ToList();
                        string h1 = Hash(from);
                        report.AppendLine($"  IMPORT vanilla {from}: new rando slots [{string.Join(", ", added)}], original unchanged: {(h0 == h1 ? "yes" : "NO (BUG)")}");
                        report.AppendLine($"    files {Hash(from)}");
                        if (added.Count != 1 || h0 != h1) { ok = false; }
                        else {
                            target = added[0];
                            report.AppendLine($"    copy {Hash(target)}");
                            var ent = ui.Entries.FirstOrDefault(e => e.Slot == target);
                            report.AppendLine($"    in the list: {(ent != null ? ui.LabelOf(ent) : "NO (BUG)")}, usable {(ent != null && ui.IsUsable(ent))}");
                            if (ent == null || !ui.IsUsable(ent)) ok = false;
                        }
                    } else {
                        report.AppendLine("  (no old vanilla save to import: import not tested)");
                    }
                    // delete mode: the first press asks for confirmation, the second deletes
                    if (target != 0) {
                        ui.SetDeleteModeForTest(true);
                        var ent = ui.Entries.First(e => e.Slot == target);
                        ui.PickForTest(ent);
                        bool stillThere = File.Exists(UserIO.GetSaveFilePath(SaveData.GetFilename(target)));
                        report.AppendLine($"  DELETE {target}: after 1st press file still there (confirmation): {stillThere}");
                        ent = ui.Entries.First(e => e.Slot == target);
                        ui.PickForTest(ent);
                        string left = string.Join(" ", Directory.GetFiles(dir, SaveData.GetFilename(target) + "*.celeste").Select(Path.GetFileName));
                        bool listed = ui.Entries.Any(e => e.Slot == target);
                        report.AppendLine($"  after 2nd press: files left [{left}], still listed {listed}, current scene ui {ow.Current?.GetType().Name}");
                        if (!stillThere || left.Length > 0 || listed) ok = false;
                        ui.SetDeleteModeForTest(false);
                    }
                    // a vanilla slot can never be deleted through the rando code path
                    if (legacy.Count > 0) {
                        string h0 = Hash(legacy[0]);
                        RandoSaves.Delete(legacy[0]);
                        bool same = Hash(legacy[0]) == h0;
                        report.AppendLine($"  RandoSaves.Delete(vanilla {legacy[0]}) refused: {(same ? "yes" : "NO (BUG)")}");
                        if (!same) ok = false;
                    }
                    report.AppendLine($"  rando saves now: [{string.Join(", ", ui.Entries.Select(e => e.Slot))}]");
                    Shot("08_manage_list.png");
                    NextSave();
                    break;
                }

                case "offline_list": {
                    // "Play offline" from the connection screen, no server running: a save whose seed is cached
                    if (Once()) {
                        report.AppendLine($"  before: ApState {ApState.Mode}, client connected {ApClient.Connected}");
                        OuiRandoSaves.Offline = true;
                        ow.Goto<OuiRandoSaves>();
                        return;
                    }
                    if (offlinePicked) {
                        bool inChapterSelect = cur is OuiChapterSelect;
                        bool resumed = Engine.Scene is Level || Engine.Scene is LevelLoader;
                        if (!inChapterSelect && !resumed) return;
                        if (savesTimer < offlinePickTime + 3f) return;
                        var ap = ApState.Save;
                        report.AppendLine($"  OFFLINE: {(resumed ? "resumed in a chapter" : "chapter select")}, FileSlot {SaveData.Instance?.FileSlot}, ApState {ApState.Mode}, " +
                                          $"bound {ap?.SlotName}/{ap?.Seed}, matches {ApState.SaveMatches(false)}, client connected {ApClient.Connected}");
                        if (!RandoSaves.CurrentIsRando || ApState.Mode == ApMode.None || !ApState.SaveMatches(false)) ok = false;
                        NextSave();
                        return;
                    }
                    var ui = cur as OuiRandoSaves;
                    if (ui == null || savesTimer < 2.5f) return;
                    if (savesTimer < 3f) { Shot("09_offline_list.png"); return; }
                    if (pendingShot != null) return;
                    foreach (var e in ui.Entries) report.AppendLine($"    {ui.LabelOf(e)}  usable {ui.IsUsable(e)}");
                    var pick = ui.Entries.FirstOrDefault(e => ui.IsUsable(e));
                    if (pick == null) { FailSaves("no usable save offline"); return; }
                    report.AppendLine($"  loading rando save {pick.Slot} offline");
                    offlinePicked = true;
                    offlinePickTime = savesTimer;
                    ui.LoadEntry(pick.Slot);
                    break;
                }

                case "journal":
                    if (Once()) { ow.Goto<OuiJournal>(); return; }
                    if (!(cur is OuiJournal journal) || savesTimer < 3f) return;
                    if (journalShots == null) {
                        report.AppendLine($"  journal pages: {string.Join(", ", journal.Pages.Select(p => p.GetType().Name))}");
                        journalShots = new List<int> { 0 };
                        for (int i = 0; i < journal.Pages.Count; i++) if (journal.Pages[i] is ApJournalPageBase) journalShots.Add(i);
                        // pages depend on the seed (keys / movement / items only when it has some): at least one locations page
                        if (!journal.Pages.Any(pg => pg is ApLocationsPage)) { journalShots = null; FailSaves("Archipelago pages missing from the journal"); return; }
                        journalShot = 0;
                        journalTimer = -1f;
                        return;
                    }
                    if (pendingShot != null) return;
                    if (journalShot >= journalShots.Count) {
                        journalShots = null;
                        ow.Goto<OuiChapterSelect>();
                        NextSave("journal pages shown");
                        return;
                    }
                    if (journalTimer < 0f) {
                        journal.PageIndex = journalShots[journalShot];
                        journal.Page.Redraw(journal.CurrentPageBuffer);
                        journalTimer = 0f;
                        return;
                    }
                    journalTimer += Engine.RawDeltaTime;
                    if (journalTimer < 0.8f) return;
                    {
                        var page = journal.Pages[journalShots[journalShot]];
                        string dir = Opt("journaldir", shotsDir ?? "");
                        Shot(Path.Combine(dir, $"journal_{journalShot}_{page.GetType().Name.Replace("Ap", "").Replace("Page", "").Replace("OuiJournal", "")}.png"));
                        journalShot++;
                        journalTimer = -1f;
                    }
                    break;

                case "level":
                    if (savesTimer < 1.5f) return; // let the journal close
                    if (!(Engine.Scene is Level) && !(Engine.Scene is LevelLoader) && Once()) {
                        if (!LayoutBuilder.TryParseAreaKey(Opt("level"), out var key)) { FailSaves("bad level option"); return; }
                        var session = new Session(key);
                        SaveData.Instance.StartSession(session);
                        LevelEnter.Go(session, true); // fromSaveData: no chapter postcard waiting for a key press
                        return;
                    }
                    if (!(Engine.Scene is Level lv) || lv.Tracker.GetEntity<Player>() == null || savesTimer < 9f) return;
                    if (savesTimer < 9.3f) { Shot("05_level_" + Opt("level") + ".png"); return; }
                    if (pendingShot != null) return;
                    {
                        bool ap = LayoutBuilder.IsApLayout(lv.Session.MapData);
                        report.AppendLine($"  LEVEL {Opt("level")}: AP layout {(ap ? "YES" : "NO")}, room {lv.Session.Level}, {lv.Session.MapData.Levels.Count} rooms, ApState {ApState.Mode}");
                        if (!ap) ok = false;
                        NextSave();
                    }
                    break;

                case "help": {
                    var overlay = HelpOverlay.Current;
                    if (!(Engine.Scene is Level) || overlay == null) { if (savesTimer > 5f) FailSaves("no level or no help overlay"); return; }
                    if (pendingShot != null) return;
                    if (savesTimer < 0.5f) { overlay.SetPageForTest(1); return; }
                    if (savesTimer < 1.0f) return;
                    if (savesTimer < 1.2f) { Shot("06_help_room.png"); return; }
                    if (savesTimer < 1.5f) { overlay.SetPageForTest(2); return; }
                    if (savesTimer < 2.0f) return;
                    if (savesTimer < 2.2f) { Shot("07_help_techniques.png"); return; }
                    report.AppendLine("  help room page:");
                    report.Append(overlay.DumpRoomPage());
                    foreach (var r in Opt("helprooms").Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                        report.AppendLine("  help page of room " + r + ":");
                        report.Append(overlay.DumpRoomPage(r));
                    }
                    overlay.SetPageForTest(0);
                    report.AppendLine("  help keys: " + string.Join("/", RandoModule.Instance.SavedData.HelpKey?.Keys ?? new List<Microsoft.Xna.Framework.Input.Keys>())
                        + " next " + string.Join("/", RandoModule.Instance.SavedData.HelpNextKey?.Keys ?? new List<Microsoft.Xna.Framework.Input.Keys>())
                        + " previous " + string.Join("/", RandoModule.Instance.SavedData.HelpPreviousKey?.Keys ?? new List<Microsoft.Xna.Framework.Input.Keys>()));
                    foreach (bool inGame in new[] { false, true }) {
                        try {
                            var menu = new TextMenu();
                            RandoModule.Instance.CreateModMenuSection(menu, inGame, null);
                            var labels = new List<string>();
                            foreach (var it in menu.Items) {
                                var f = it.GetType().GetField("Label");
                                labels.Add(f?.GetValue(it) as string ?? it.GetType().Name);
                            }
                            report.AppendLine($"  mod menu (inGame={inGame}): " + string.Join(" | ", labels));
                        } catch (Exception e) {
                            report.AppendLine($"  mod menu (inGame={inGame}) FAILED: " + e.Message);
                        }
                    }
                    NextSave($"help shown ({HelpData.Logic?.Levels.Count ?? 0} logic levels, {HelpData.Techniques.Count} techniques)");
                    break;
                }

                case "chain":
                    if (mechScript == null && mechId == 0) { SetupMech(ChainScript(Opt("chain"))); return; }
                    MechUpdate();
                    break;

                case "finish":
                    if (settingsOverridden) {
                        var s = RandoModule.Instance.SavedData;
                        s.ApServer = savedServer; s.ApSlot = savedSlot; s.ApPassword = savedPassword ?? "";
                        RandoModule.Instance.SaveSettings();
                        settingsOverridden = false;
                    }
                    On.Monocle.Engine.Draw -= CaptureAfterDraw;
                    savesIndex = savesSteps.Count;
                    Finish(true);
                    break;
            }
        }
    }
}
