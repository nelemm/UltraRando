// CMR: online "chain" self-test (saves flow, option chain=1 or chain=2, seed v1/output/cmr_chain_77.zip, vanilla
// layout, items placed by plando/fill: see v1/output/cmr_chain_77_spoiler.txt). Every check is triggered in the game
// (real room transitions, Madeline moved onto the collectible, a real dash into the heart), then the test waits for
// the item sent back by the Archipelago server, then proves the ability with the injected-input mechanics tests.
// chain=1 and chain=2 are two launches on the same randomizer save: part 2 also proves that progression survives a
// restart of the game.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static partial class SelfTest {
        static string ChainArea => ApHooks.ActiveAreaKey();

        static IEnumerator LoadRoom(string area, string room) {
            Release();
            LayoutBuilder.TryParseAreaKey(area, out var key);
            var session = new Session(key) { FirstLevel = false, StartedFromBeginning = false, Level = room };
            SaveData.Instance.StartSession(session);
            LevelEnter.ErrorMessage = null;
            Engine.Scene = new LevelLoader(session);
            yield return (Func<bool>)(() => Pl != null && Lvl.Session.Level == room && !Lvl.Transitioning);
            yield return 0.6f;
            if (Lvl != null && Lvl.InCutscene) { Lvl.SkipCutscene(); yield return 1.5f; }
            SetHome();
            Result(Lvl?.Session.Level == room, $"load {area} room {room}", $"in room {Lvl?.Session.Level}, ApState {ApState.Mode}");
        }

        /// <summary>A spot near home where Madeline stands on a solid with 16 px of solid floor on each side.</summary>
        static Vector2 GroundSpot() {
            var p = Pl; var save = p.Position;
            Vector2 found = home;
            bool ok3 = false;
            for (int ox = 0; ox <= 200 && !ok3; ox += 8)
                foreach (int sx in ox == 0 ? new[] { 0 } : new[] { -ox, ox }) {
                    for (int dy = -48; dy <= 48 && !ok3; dy += 1) {
                        var c = home + new Vector2(sx, dy);
                        var rb = Lvl.Bounds;
                        if (c.X < rb.Left + 32 || c.X > rb.Right - 32 || c.Y > rb.Bottom - 16 || c.Y < rb.Top + 32) continue; // not at an exit
                        p.Position = c;
                        if (p.CollideCheck<Solid>()) continue;
                        if (!p.CollideCheck<Solid>(c + new Vector2(-16, 1)) || !p.CollideCheck<Solid>(c + new Vector2(0, 1)) || !p.CollideCheck<Solid>(c + new Vector2(16, 1))) continue;
                        if (p.CollideCheck<Solid>(c + new Vector2(-16, 0)) || p.CollideCheck<Solid>(c + new Vector2(16, 0))) continue;
                        found = c; ok3 = true;
                    }
                    if (ok3) break;
                }
            p.Position = save;
            return found;
        }

        static void SetHome() {
            if (Pl == null) return;
            testRoom = Lvl.Session.Level;
            home = Lvl.GetSpawnPoint(Pl.Position);
            Pl.StateMachine.State = Player.StNormal;
            Pl.Position = home;
            Pl.Speed = Vector2.Zero;
        }

        /// <summary>A real screen transition through the slot_data link between the current room and <paramref name="to"/>.</summary>
        static IEnumerator GoThrough(string to) {
            Release();
            string area = ChainArea, from = Lvl.Session.Level;
            var ch = ApState.Slot.Chapters[area];
            var l = ch.Links.FirstOrDefault(x => (x.A == from && x.B == to) || (x.B == from && x.A == to));
            if (l == null) { Result(false, $"transition {from} -> {to}", "no link in slot_data"); yield break; }
            var statics = LayoutBuilder.RoomKeys(area);
            StaticRoom St(string k) => statics.TryGetValue(k, out var s) ? s : statics.Values.First(x => x.Level.Name == k);
            bool aIsFrom = l.A == from;
            var step = new WalkStep {
                Area = area, Key = Lvl.Session.Area, From = from, Expected = to,
                Hole = aIsFrom ? St(l.A).Holes[l.AHole] : St(l.B).Holes[l.BHole],
                OtherHole = aIsFrom ? St(l.B).Holes[l.BHole] : St(l.A).Holes[l.AHole],
                HoleIndex = aIsFrom ? l.AHole : l.BHole,
            };
            if (clearExits) ClearObstacles(Lvl, step, false); // locks / breakable walls a player with the items opens
            for (float t = 0; t < 4f; t += Engine.RawDeltaTime) {
                if (Lvl.Session.Level == to && !Lvl.Transitioning) break;
                if (!Lvl.Transitioning && Pl != null && Lvl.Session.Level == from) Push(Lvl, Pl, step);
                yield return null;
            }
            yield return 0.3f;
            bool ok2 = Lvl.Session.Level == to;
            if (ok2) SetHome();
            Result(ok2, $"transition {from} -> {to} (real screen transition)", $"now in {Lvl.Session.Level}");
            yield return 0.2f;
        }

        static string EntityNames() => string.Join(",", Lvl.Entities.Select(e => e.GetType().Name).Distinct().OrderBy(n => n)) +
            " | level data: " + string.Join(",", Lvl.Session.LevelData.Entities.Select(e => e.Name).Distinct().OrderBy(n => n));

        static bool clearExits;

        /// <summary>Room path (slot_data links, in a direction the logic uses) from <paramref name="from"/> to
        /// <paramref name="to"/>, breadth first.</summary>
        static List<string> RoomPath(string area, string from, string to) {
            var ch = ApState.Slot.Chapters[area];
            var prev = new Dictionary<string, string> { { from, null } };
            var q = new Queue<string>(); q.Enqueue(from);
            while (q.Count > 0) {
                var r = q.Dequeue();
                if (r == to) break;
                foreach (var l in ch.Links) {
                    string d = l.Dir ?? "both";
                    if (l.A == r && (d == "ab" || d == "both") && !prev.ContainsKey(l.B)) { prev[l.B] = r; q.Enqueue(l.B); }
                    if (l.B == r && (d == "ba" || d == "both") && !prev.ContainsKey(l.A)) { prev[l.A] = r; q.Enqueue(l.A); }
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<string>();
            for (var r = to; r != null; r = prev[r]) path.Insert(0, r);
            return path;
        }

        /// <summary>The two ways back into the 3A detour after the shaftskip: from the Elevator Shaft checkpoint
        /// (chapter select) and from the start of the chapter. Real transitions all the way to 04-c, then its
        /// strawberry is collected.</summary>
        static IEnumerator DetourReturn() {
            LayoutBuilder.TryParseAreaKey("3a", out var key);
            clearExits = true;
            foreach (var start in new[] { "checkpoint 09-d", "chapter start" }) {
                Release();
                var session = start.StartsWith("checkpoint") ? new Session(key, "09-d") : new Session(key);
                session.FirstLevel = false;
                SaveData.Instance.StartSession(session);
                LevelEnter.ErrorMessage = null;
                Engine.Scene = new LevelLoader(session);
                yield return (Func<bool>)(() => Pl != null && !Lvl.Transitioning);
                yield return 0.8f;
                if (Lvl.InCutscene) { Lvl.SkipCutscene(); yield return 1.5f; }
                SetHome();
                string first = Lvl.Session.Level;
                var path = RoomPath("3a", first, "04-c");
                Result(path != null, $"3A {start}: started in {first}, route to the detour room 04-c", path == null ? "no route" : string.Join(" > ", path));
                if (path == null) continue;
                int failsBefore = mechFail;
                for (int i = 1; i < path.Count && mechFail == failsBefore; i++) yield return GoThrough(path[i]);
                if (Lvl.Session.Level == "04-c") {
                    var berry = Lvl.Entities.OfType<Strawberry>().FirstOrDefault();
                    if (berry != null) {
                        yield return TouchStrawberry(berry.ID.ID);
                        Result(berry.Scene == null, $"3A {start}: detour strawberry of 04-c collected", $"strawberry {berry.ID.ID} collected={berry.Scene == null}");
                    }
                }
            }
            clearExits = false;
            Release();
        }

        static long Loc(string key) => ApState.Slot.LocationIds.TryGetValue(key, out var id) ? id : -1;
        static bool Sent(string key) => ApState.Save != null && ApState.Save.CheckedLocations.Contains(Loc(key));

        /// <summary>Waits (8 s max) for a big notification whose text matches, checks its icon, takes a screenshot.</summary>
        static IEnumerator ToastIs(Func<string, bool> text, string iconPath, string what, string shot) {
            static string T(ApHud.Toast t) => t == null ? "" : string.Concat(t.Parts.Select(p => p.text));
            int start = ApHud.ToastsForTest;
            ApHud.Toast found = null;
            for (float t = 0; t < 8f && found == null; t += Engine.RawDeltaTime) {
                if (ApHud.LastToastForTest != null && text(T(ApHud.LastToastForTest))) found = ApHud.LastToastForTest;
                else yield return null;
            }
            var want = ApJournal.Tex(GFX.Game, iconPath);
            Result(found != null && found.Icon != null && found.Icon == want, what,
                $"text '{T(found ?? ApHud.LastToastForTest)}', icon {(found?.Icon == null ? "none" : found.Icon == want ? iconPath : found.Icon.AtlasPath)}");
            yield return 0.3f;
            Shot(shot);
        }

        static IEnumerator ExpectCheck(string key) {
            yield return (Func<bool>)(() => Sent(key));
            Result(Sent(key), $"check {key} sent", $"location id {Loc(key)}, in save: {Sent(key)}, ApState {ApState.Mode}");
        }

        static IEnumerator ExpectItem(string item, string fromCheck) {
            yield return (Func<bool>)(() => ApState.Has(item));
            Result(ApState.Has(item), $"item {item} received from the server after {fromCheck}", $"owned={ApState.Has(item)}");
        }

        static IEnumerator TouchStrawberry(int id) {
            yield return ResetPlayer();
            var berry = Lvl.Entities.OfType<Strawberry>().FirstOrDefault(b => b.ID.ID == id);
            if (berry == null) { Result(false, $"strawberry {id} in {Lvl.Session.Level}", "not in the room: " + string.Join(",", Lvl.Entities.OfType<Strawberry>().Select(b => b.ID.ID + (b.Winged ? "w" : "")))); yield break; }
            report.AppendLine($"  strawberry {id} at {berry.Position} winged={berry.Winged}, Madeline home {home}");
            // the strawberry is brought to Madeline standing on safe ground (teleporting her next to it can kill her on
            // nearby spikes); the touch, the follow and the collection on safe ground are Celeste's own code
            Pl.Position = GroundSpot(); Pl.Speed = Vector2.Zero; // a spawn point can be in the air or on a ledge
            yield return (Func<bool>)(() => Pl != null && Pl.OnSafeGround);
            home = Pl.Position;
            berry.Position = Pl.Center;
            yield return 0.3f;
            if (!berry.Follower.HasLeader && Pl != null) {
                report.AppendLine($"  strawberry {id}: no contact pickup (StrawberriesBlocked={Pl.StrawberriesBlocked}, state {Pl.StateMachine.State}): calling Strawberry.OnPlayer (the game's contact handler)");
                berry.OnPlayer(Pl);
                yield return 0.3f;
            }
            report.AppendLine($"  strawberry {id}: following={berry.Follower.HasLeader}, Madeline on ground={Pl?.OnGround()}, safe ground={Pl?.OnSafeGround}, dead={Pl?.Dead}, " +
                              $"state {Pl?.StateMachine.State}, at {Pl?.Position}, speed {Pl?.Speed}, level paused={Lvl?.Paused} frozen={Lvl?.Frozen} " +
                              $"timeRate={Engine.TimeRate} freeze={Engine.FreezeTimer:0.00} transitioning={Lvl?.Transitioning} inCutscene={Lvl?.InCutscene}, injected=[{string.Join(",", injected ?? new HashSet<Microsoft.Xna.Framework.Input.Keys>())}]");
            if (Pl != null && !Pl.OnSafeGround) Shot($"chain_berry{id}_not_on_ground.png");
            yield return (Func<bool>)(() => berry.Scene == null);
            yield return 0.5f;
        }

        static IEnumerator ChainScript(string part) {
            report.AppendLine($"chain part {part}: keys dash={KDash} demo={KDemo}, ApState {ApState.Mode}, seed {ApState.Seed}");
            if (part == "gem") {
                // a summit gem broken by a real dash stays collected in a new session (dash freed for this test only)
                int ds = ApState.Slot.Option("dash_shuffle", 0);
                SetOpt("dash_shuffle", 0);
                yield return LoadRoom("7a", "a-06");
                var gem = Lvl.Entities.OfType<SummitGem>().FirstOrDefault();
                if (gem == null) { Result(false, "summit gem in 7a a-06", "not found"); yield break; }
                int gid = gem.GemID;
                SaveData.Instance.Assists.Invincible = true;
                Pl.Position = gem.Center + new Vector2(-30, 6); Pl.Speed = Vector2.Zero; Pl.RefillDash();
                Hold(KRight); yield return null; Hold(KRight, KDash); yield return null; Hold(KRight);
                yield return 0.8f; Release();
                SaveData.Instance.Assists.Invincible = false;
                yield return ExpectCheck("7a|a-06|gem|110");
                yield return LoadRoom("7a", "b-00");
                Result(Lvl.Session.SummitGems[gid], $"summit gem {gid} stays collected in a new session", $"session gem={Lvl.Session.SummitGems[gid]}");
                SetOpt("dash_shuffle", ds);
            } else if (part == "keys") {
                // seed v1/output/cmr_keys_61.zip: Nelem's Front Door Key is on Ami's Room 2 (sent by tools/ap_test_client.py
                // ~55 s after it connects), Nelem's Hallway Key 1 on Nelem's own Room 2
                const string front = "Celestial Resort A - Front Door Key", hall = "Celestial Resort A - Hallway Key 1";
                int Doors() => Lvl.Entities.OfType<LockBlock>().Count();
                yield return LoadRoom("3a", "s3");
                Result(Doors() > 0 && !ApState.Has(front), "3A s3 without the Front Door Key: door closed", $"{Doors()} lock block(s)");
                for (float t = 0; t < 60f && !ApState.Has(front); t += Engine.RawDeltaTime) yield return null;
                Result(ApState.Has(front), "Front Door Key received from Ami's world", $"owned={ApState.Has(front)}");
                yield return LoadRoom("3a", "s3");
                Result(Doors() == 0, "3A s3 with the Front Door Key: door open", $"{Doors()} lock block(s)");
                yield return LoadRoom("3a", "02-a");
                int before = Doors();
                yield return LoadRoom("1a", "1");
                yield return GoThrough("2");
                yield return ExpectItem(hall, "Nelem's own Room 2");
                yield return LoadRoom("3a", "02-a"); // a new session (LevelLoader): the key is still owned
                Result(before > 0 && Doors() == 0, "3A 02-a: closed before Hallway Key 1, open after (new session)", $"lock blocks {before} -> {Doors()}");
                // clutter kept for good: the clutter switch's own SetFlag, then a new session
                yield return LoadRoom("3a", "10-x");
                Lvl.Session.SetFlag("oshiro_clutter_cleared_0", true);
                yield return LoadRoom("3a", "02-a");
                Result(Lvl.Session.GetFlag("oshiro_clutter_cleared_0"), "brown clutter cleared stays cleared in a new session", $"flag={Lvl.Session.GetFlag("oshiro_clutter_cleared_0")}");
                // a summit gem broken by a real dash stays collected in a new session
                yield return LoadRoom("7a", "a-06");
                var gem = Lvl.Entities.OfType<SummitGem>().FirstOrDefault();
                if (gem != null) {
                    int gid = gem.GemID;
                    SaveData.Instance.Assists.Invincible = true;
                    Pl.Position = gem.Center + new Vector2(-30, 6); Pl.Speed = Vector2.Zero; Pl.RefillDash();
                    Hold(KRight); yield return null; Hold(KRight, KDash); yield return null; Hold(KRight);
                    yield return 0.8f; Release();
                    SaveData.Instance.Assists.Invincible = false;
                    yield return LoadRoom("7a", "a-06");
                    Result(Lvl.Session.SummitGems[gid], $"summit gem {gid} stays collected in a new session", $"session gem={Lvl.Session.SummitGems[gid]}");
                } else Result(false, "summit gem in 7a a-06", "not found");
            } else if (part == "cp2") {
                // new launch on the same save: the checkpoint stays unlocked (its item comes back from the server)
                LayoutBuilder.TryParseAreaKey("1a", out var k1b);
                yield return 2f;
                var c2 = SaveData.Instance.Areas_Safe[k1b.ID].Modes[0].Checkpoints;
                Result(c2.Contains("6") && ApState.Has("Forsaken City A - Crossing"), "after restart: checkpoint 6 still unlocked", $"save checkpoints [{string.Join(",", c2)}]");
            } else if (part == "cp") {
                // seed v1/output/cmr_cp_51.zip (checkpointsanity): the item "Forsaken City A - Crossing" is on Room 2
                const string cpItem = "Forsaken City A - Crossing";
                LayoutBuilder.TryParseAreaKey("1a", out var k1a);
                HashSet<string> Cps() => SaveData.Instance.Areas_Safe[k1a.ID].Modes[0].Checkpoints;
                Result(!ApState.Slot.LocationIds.Keys.Any(x => x.EndsWith("|checkpoint")), "no checkpoint check in the seed",
                    $"{ApState.Slot.LocationIds.Count} locations, checkpoint items {ApState.Slot.Checkpoints.Count}");
                yield return LoadRoom("1a", "5");
                yield return GoThrough("6");
                yield return 0.5f;
                Result(!Cps().Contains("6") && !ApState.Has(cpItem), "checkpoint room 6 reached without its item: not unlocked in the chapter select",
                    $"save checkpoints [{string.Join(",", Cps())}]");
                yield return LoadRoom("1a", "1");
                yield return GoThrough("2");
                yield return ExpectItem(cpItem, "entering room 2");
                Result(Cps().Contains("6"), "checkpoint item received: checkpoint 6 unlocked in the chapter select", $"save checkpoints [{string.Join(",", Cps())}]");
                // start the chapter from it, as the chapter select does
                var cs = new Session(k1a, "6") { FirstLevel = false };
                SaveData.Instance.StartSession(cs);
                Engine.Scene = new LevelLoader(cs);
                yield return (Func<bool>)(() => Pl != null && !Lvl.Transitioning && Lvl.Session.Level == "6");
                yield return 1f;
                Result(Lvl.Session.Level == "6" && Cps().Contains("6"), "chapter started from checkpoint 6", $"room {Lvl.Session.Level}");
                Result(ApHud.RoomTextForTest == "Room: 6", "room name shown after the checkpoint start", $"text '{ApHud.RoomTextForTest}'");
                yield return ExpectCheck("1a|6|room");
            } else if (part == "sync") {
                // seed v1/output/cmr_sync_41.zip, two worlds: Nelem's Dash is on Ami's "Room 2", Ami's Dash on Nelem's.
                // Ami is tools/ap_test_client.py, which sends her Room 2 check about 55 s after it connects.
                yield return LoadRoom("1a", "1");
                yield return Dash(1, 0, false, "before Ami's check");
                for (float t = 0; t < 60f && !ApState.Has(ItemNames.Dash); t += Engine.RawDeltaTime) yield return null;
                Result(ApState.Has(ItemNames.Dash), "Dash received from Ami's world (Ami checked her Room 2)", $"owned={ApState.Has(ItemNames.Dash)}");
                yield return ToastIs(t => t.StartsWith("Reçu Dash de Ami"), "characters/player/dash00", "notification: Dash received from Ami, Celeste dash icon", "notify_received.png");
                yield return Dash(1, 0, true, "Dash found by the other player");
                yield return GoThrough("2"); // Nelem's Room 2 holds Ami's Dash
                float entered = ApHud.RoomShownAtForTest;
                Result(ApHud.RoomTextForTest == "Room: 2", "room name shown on entering room 2", $"text '{ApHud.RoomTextForTest}'");
                yield return ExpectCheck("1a|2|room");
                yield return ToastIs(t => t.StartsWith("Envoyé Dash à Ami"), "characters/player/dash00", "notification: Dash sent to Ami (Celeste item), dash icon", "notify_sent.png");
                while (ApHud.ClockForTest - entered < 3.8f) yield return null;
                Result(ApHud.RoomTextForTest == "Room: 2", "room name still shown at 3.8 s", $"t={ApHud.ClockForTest - entered:0.00} text '{ApHud.RoomTextForTest}'");
                Shot("notify_room_name.png");
                while (ApHud.ClockForTest - entered < 4.1f) yield return null;
                Result(ApHud.RoomTextForTest == null, "room name gone after 4 s", $"t={ApHud.ClockForTest - entered:0.00} text '{ApHud.RoomTextForTest}'");
                // a respawn in the same room does not show it again; a new room does, once
                Pl.Die(Vector2.Zero, true);
                yield return (Func<bool>)(() => Pl != null && Pl.StateMachine.State == Player.StNormal);
                yield return 0.5f;
                Result(ApHud.RoomTextForTest == null, "respawn in the same room: room name not shown again", $"text '{ApHud.RoomTextForTest}'");
                yield return 1f;
            } else if (part == "epilogue") {
                // on a randomizer save, entering the Epilogue (chapter select) is refused: error screen, then the map
                var area = AreaData.Get(ApHooks.EpilogueSid);
                Result(area != null, "Epilogue area exists", area?.SID ?? "none");
                if (area == null) yield break;
                Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
                yield return 3f;
                LevelEnter.Go(new Session(area.ToKey()), false);
                yield return 2f;
                Shot("epilogue_blocked.png");
                bool sawLevel = false;
                for (float t = 0; t < 12f && !(Engine.Scene is Overworld); t += Engine.RawDeltaTime) {
                    if (Engine.Scene is Level l && l.Session.Area.SID == ApHooks.EpilogueSid) sawLevel = true;
                    yield return null;
                }
                Result(ApHooks.EpilogueBlockedForTest == 1 && !sawLevel, "Epilogue refused on a randomizer save", $"blocked {ApHooks.EpilogueBlockedForTest}, entered={sawLevel}, scene {Engine.Scene?.GetType().Name}");
            } else if (part == "final") {
                // seed v1/output/cmr_final_7101.zip: the goal chapter (7A) opens with 3 strawberries (1A rooms 3, 4, 5);
                // it holds Springs (needed in 1A room 2), Feathers and Climb; its clear with 3 strawberries is the goal
                LayoutBuilder.TryParseAreaKey("7a", out var k7);
                IEnumerator TryEnter7a() {
                    LevelEnter.Go(new Session(k7), false);
                    for (float t = 0; t < 12f && !(Engine.Scene is Level l7 && l7.Session.Area.ID == k7.ID && l7.Tracker.GetEntity<Player>() != null); t += Engine.RawDeltaTime) {
                        if (Engine.Scene is LevelEnter && t > 3f) break;
                        yield return null;
                    }
                    yield return 1f;
                }
                // 1. new run, nothing received: the final chapter is refused
                yield return LoadRoom("1a", "1");
                int blocked0 = ApHooks.GoalBlockedForTest;
                yield return TryEnter7a();
                Shot("final_locked.png");
                Result(ApHooks.GoalBlockedForTest == blocked0 + 1 && !(Engine.Scene is Level), "1. final chapter (7A) refused with 0 strawberry",
                    $"blocked {ApHooks.GoalBlockedForTest - blocked0}, scene {Engine.Scene?.GetType().Name}, strawberries {ApState.StrawberriesReceived}");
                // 2. the strawberries: Dash in 1A room 2, strawberries on the room checks of 1A rooms 3, 4, 5
                yield return LoadRoom("1a", "1");
                yield return GoThrough("2");
                yield return ExpectItem(ItemNames.Dash, "1A room 2");
                int springs0 = Lvl.Entities.OfType<Spring>().Count();
                Result(springs0 == 0, "Springs not received: none in 1A room 2", $"{springs0} spring(s), in the room data {Lvl.Session.LevelData.Entities.Count(e => e.Name == "spring")}");
                foreach (var r in new[] { "3", "4", "5" }) yield return LoadRoom("1a", r);
                for (float t = 0; t < 8f && ApState.StrawberriesReceived < 3; t += Engine.RawDeltaTime) yield return null;
                Result(ApState.StrawberriesReceived == 3, "2. three strawberries received (1A rooms 3, 4, 5)", $"strawberries {ApState.StrawberriesReceived}");
                // 3-4. the final chapter opens and is entered from the chapter start
                int blocked1 = ApHooks.GoalBlockedForTest;
                yield return TryEnter7a();
                Result(ApHooks.GoalBlockedForTest == blocked1 && Engine.Scene is Level l7b && l7b.Session.Area.ID == k7.ID,
                    "3-4. final chapter unlocked and entered", $"scene {Engine.Scene?.GetType().Name}, room {(Engine.Scene as Level)?.Session.Level}");
                // 5-6. its checks: the start rooms hold Feathers and Springs (a progression item for 1A)
                yield return LoadRoom("7a", "a-00");
                yield return ExpectItem("Springs", "7A a-00");
                yield return LoadRoom("7a", "a-00-intro");
                yield return ExpectItem("Feathers", "7A a-00-intro");
                // 7-8. back in 1A: the Springs found in the final chapter are now in 1A room 2
                yield return LoadRoom("1a", "2");
                int springs1 = Lvl.Entities.OfType<Spring>().Count();
                Result(springs1 > 0, "7-8. back in 1A room 2: the Springs found in the final chapter are there", $"{springs1} spring(s)");
                // 9. the remaining item of the final chapter (Climb, on 7A a-01)
                yield return LoadRoom("7a", "a-01");
                yield return ExpectItem(ItemNames.Climb, "7A a-01");
                // 10-11. clear of the final chapter (its real end trigger in g-03): the goal is sent
                yield return LoadRoom("7a", "g-03");
                var trig = Lvl.Entities.OfType<EventTrigger>().FirstOrDefault();
                if (trig != null) { Pl.Position = trig.Center; Pl.Speed = Vector2.Zero; }
                for (float t = 0; t < 20f && !ApState.GoalSentForTest; t += Engine.RawDeltaTime) yield return null;
                yield return 2f;
                Shot("final_goal.png");
                Result(ApState.GoalSentForTest && Sent("7a|clear"), "10-11. final chapter cleared: Level Clear sent and the goal sent to the server",
                    $"clear sent={Sent("7a|clear")}, goal sent={ApState.GoalSentForTest}, end trigger={(trig != null)}");
            } else if (part == "panel") {
                // chapter select: the goal chapter's panel (checks counter, strawberries line), then its checkpoints
                LayoutBuilder.TryParseAreaKey("7a", out var kp);
                SaveData.Instance.LastArea_Safe = kp;
                // a checkpoint of 7A in the save, so the panel has a checkpoint list to open
                ApState.ItemReceived(9000, "The Summit A - 500 M", "selftest");
                ApState.ItemReceived(9001, "The Summit A - 1000 M", "selftest");
                ApState.ApplyCheckpoints();
                Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaQuit); // as when leaving a chapter (the title screen ends the session)
                yield return 3f;
                var ow = Engine.Scene as Overworld;
                if (!(ow?.Current is OuiChapterPanel)) { ow?.Goto<OuiChapterSelect>(); yield return 2f; ow?.Goto<OuiChapterPanel>(); }
                yield return 2.5f;
                Shot("panel_goal.png");
                var panel = ow?.GetUI<OuiChapterPanel>();
                Result(panel != null && ApHooks.PanelCounterForTest != null && ApHooks.PanelGoalTextForTest != null,
                    "goal chapter panel: checks counter and strawberries line", $"counter {ApHooks.PanelCounterForTest}, text '{ApHooks.PanelGoalTextForTest}'");
                if (panel == null) yield break;
                // open the checkpoint list as the confirm button does
                var dyn = MonoMod.Utils.DynamicData.For(panel);
                dyn.Invoke("Swap"); // private in OuiChapterPanel
                yield return 2f;
                Shot("panel_checkpoints.png");
                int opt = dyn.Get<int>("option");
                var cps = dyn.Get<System.Collections.IList>("checkpoints");
                Result(cps != null && cps.Count > 1 && opt == 0, "checkpoint list opens on the FIRST checkpoint", $"option {opt} of {cps?.Count}");
            } else if (part == "deathlink") {
                // seed v1/output/cmr_dl_43.zip (death_link on): Ami (tools/ap_test_client.py --deathlink-at) sends a death
                yield return LoadRoom("1a", "1");
                for (float t = 0; t < 60f && DeathLinkBridge.KillsForTest == 0; t += Engine.RawDeltaTime) yield return null;
                Result(DeathLinkBridge.KillsForTest == 1, "DeathLink received from Ami: Madeline killed once", $"kills {DeathLinkBridge.KillsForTest}");
                yield return (Func<bool>)(() => Pl != null && Pl.StateMachine.State == Player.StNormal);
                yield return 1.5f;
                // my own death goes to the other players (Ami prints DEATHLINK RECEIVED)
                Pl.Die(Vector2.Zero, true);
                yield return 3f;
                Result(DeathLinkBridge.KillsForTest == 1, "my own death is not sent back to me", $"kills {DeathLinkBridge.KillsForTest}");
            } else if (part == "goal") {
                // seed v1/output/cmr_goal_31.zip: goal = Summit A clear + 1 strawberry, the strawberry is on "Room 2"
                yield return LoadRoom("7a", "a-00");
                Lvl.RegisterAreaComplete(); // Everest OnComplete, as at the end of the chapter (end cutscene not played)
                yield return ExpectCheck("7a|clear");
                yield return 2f;
                Result(!ApState.GoalSentForTest, "Summit A cleared with 0/1 strawberry: goal not sent yet", $"goal sent={ApState.GoalSentForTest}, strawberries {ApState.StrawberriesReceived}");
                // the connection drops
                ApClient.Disconnect(false);
                ApState.ConnectionLost("test: connection dropped");
                yield return 1f;
                Result(ApState.Mode == ApMode.Cached, "connection lost: cached mode", $"mode {ApState.Mode}");
                // a check made while disconnected (the strawberry item is on it)
                yield return LoadRoom("1a", "1");
                yield return GoThrough("2");
                Result(Sent("1a|2|room") && ApState.StrawberriesReceived == 0, "Room 2 entered while disconnected: check kept in the save, no item yet",
                    $"in save={Sent("1a|2|room")}, strawberries {ApState.StrawberriesReceived}");
                // reconnection: the check is resent, the strawberry arrives, the goal is complete
                var st = RandoModule.Instance.SavedData;
                ApClient.Connect(st.ApServer, st.ApSlot, st.ApPassword);
                yield return (Func<bool>)(() => ApState.Mode == ApMode.Online);
                for (float t = 0; t < 8f && !ApState.GoalSentForTest; t += Engine.RawDeltaTime) yield return null;
                Result(ApState.Mode == ApMode.Online && ApState.StrawberriesReceived >= 1 && ApState.GoalSentForTest,
                    "after reconnection: check resent, strawberry received, goal sent", $"mode {ApState.Mode}, strawberries {ApState.StrawberriesReceived}, goal sent={ApState.GoalSentForTest}");
                yield return 2f;
            } else if (part == "1") {
                yield return LoadRoom("1a", "1");
                // nothing received yet: dash, climb and demo dash are locked (unified dash, single climb)
                yield return Dash(1, 0, false, "before any check");
                yield return Climb(1, false, "before any check");
                // room entry check "Room 2" holds Dash
                yield return GoThrough("2");
                yield return ExpectCheck("1a|2|room");
                yield return ExpectItem(ItemNames.Dash, "entering room 2");
                // springs (room 2 has some) are not received yet: none loaded
                int springs = Lvl.Entities.OfType<Spring>().Count();
                Result(springs == 0, "Springs locked: no spring loaded in room 2", $"{springs} spring(s)");
                // strawberry of room 2 holds Climb (touched before the dash tests: a dash test could knock it off)
                yield return TouchStrawberry(11);
                yield return ExpectCheck("1a|2|strawberry|11");
                yield return ExpectItem(ItemNames.Climb, "the room 2 strawberry");
                yield return Dash(1, 0, true, "Dash received in room 2");
                yield return Dash(-1, -1, true, "Dash received in room 2");
                yield return Climb(1, true, "Climb received");
                yield return Climb(-1, true, "Climb received");
                // progression kept through a transition
                yield return GoThrough("3");
                yield return ExpectCheck("1a|3|room");
                yield return Dash(1, 0, true, "after the transition to room 3");
                yield return Climb(1, true, "after the transition to room 3");
                yield return TouchStrawberry(9);
                yield return ExpectCheck("1a|3|strawberry|9");
                yield return ExpectItem("Feathers", "the room 3 strawberry");
                yield return GoThrough("4");
                yield return GoThrough("3b");
                yield return GoThrough("5");
                // strawberry of room 5 holds Demo Dash
                yield return DashVariant("demokey", 1, 0, 0, 0, (d, c) => !c && d.X > 0.5f, "demo key R before Demo Dash: standing");
                yield return TouchStrawberry(21);
                yield return ExpectCheck("1a|5|strawberry|21");
                yield return ExpectItem(ItemNames.DemoDash, "the room 5 strawberry");
                yield return DashVariant("demokey", 1, 0, 0, 0, (d, c) => c && d.X > 0.5f, "demo key R with Demo Dash: crouched");
                yield return DashVariant("downswitch", 1, 0, 0, 0, (d, c) => c && d.X > 0.5f, "down+dash then R with Demo Dash: crouched");
                yield return Crouch(false, "Crouch not received yet");
                // the game's own save (what "Save & Quit" does), so that part 2 can also check the local copy
                UserIO.SaveHandler(true, false);
                yield return (Func<bool>)(() => !UserIO.Saving);
                var sv = ApState.Save;
                Result(sv != null && sv.ReceivedItems.Contains(ItemNames.DemoDash) && sv.CheckedLocations.Contains(Loc("1a|5|strawberry|21")),
                    "game saved: received items and checks written in the randomizer save", $"{sv?.ReceivedItems.Count} items, {sv?.CheckedLocations.Count} checks");
            } else {
                // part 2: a new launch on the same save (restart of the game)
                yield return LoadRoom("1a", "5");
                foreach (var it in new[] { ItemNames.Dash, ItemNames.Climb, "Feathers", ItemNames.DemoDash })
                    Result(ApState.Has(it), $"after restart: {it} still owned", $"owned={ApState.Has(it)}");
                yield return Dash(1, 0, true, "after restart");
                yield return Climb(1, true, "after restart");
                yield return DashVariant("demokey", -1, 0, 0, 0, (d, c) => c && d.X < -0.5f, "demo key L after restart: crouched");
                int springs = Lvl.Entities.OfType<Spring>().Count();
                Result(springs == 0, "Springs locked: no spring loaded in room 5", $"{springs} spring(s)");
                yield return Crouch(false, "Crouch not received yet");
                // room 6 check holds Crouch (checkpoints are items now, not locations)
                yield return GoThrough("6");
                yield return ExpectCheck("1a|6|room");
                yield return ExpectItem(ItemNames.Crouch, "the room 6 check");
                yield return Crouch(true, "Crouch received");
                // crystal heart (room s1) holds Springs: a real dash into the heart
                yield return LoadRoom("1a", "s0");
                // the bird code entered on the satellite = session flag "unlocked_satellite" (set by the game's own
                // ForsakenCitySatellite.UnlockGem); with it, the satellite creates the heart when s1 is entered
                Lvl.Session.SetFlag("unlocked_satellite");
                yield return GoThrough("s1");
                var heart = Lvl.Entities.OfType<HeartGem>().FirstOrDefault();
                if (heart == null) Result(false, "crystal heart in s1", "not found; entities: " + EntityNames());
                else {
                    Pl.Position = heart.Position + new Vector2(-28, 6);
                    Pl.Speed = Vector2.Zero;
                    Hold(KRight); yield return null;
                    Hold(KRight, KDash); yield return null;
                    Hold(KRight);
                    yield return 0.6f;
                    Release();
                    yield return ExpectCheck("1a|s1|heart|14");
                    yield return ExpectItem("Springs", "the crystal heart");
                    // the springs exist again once a room is loaded, and they launch Madeline
                    yield return LoadRoom("1a", "5");
                    var spring = Lvl.Entities.OfType<Spring>().FirstOrDefault(s => s.Orientation == Spring.Orientations.Floor);
                    Result(spring != null, "Springs received: springs loaded in room 5", $"{Lvl.Entities.OfType<Spring>().Count()} spring(s)");
                    if (spring != null) {
                        Pl.Position = spring.Position + new Vector2(0, -2);
                        Pl.Speed = new Vector2(0, 40);
                        float best = 0;
                        for (float t = 0; t < 0.3f; t += Engine.RawDeltaTime) { if (Pl != null) best = Math.Min(best, Pl.Speed.Y); yield return null; }
                        Result(best < -150, "spring launches Madeline", $"best upward speed {best:0}");
                    }
                }
                // cassette (room 11z)
                yield return LoadRoom("1a", "11");
                yield return GoThrough("11z");
                var cassette = Lvl.Entities.OfType<Cassette>().FirstOrDefault();
                if (cassette == null) Result(false, "cassette in 11z", "not found; entities: " + EntityNames());
                else {
                    int before = ApState.OwnedItems.Count();
                    Pl.Position = home; Pl.Speed = Vector2.Zero;
                    cassette.Position = Pl.Center;
                    yield return ExpectCheck("1a|11z|cassette|12");
                    yield return 3f; // the cassette sequence
                }
                // level clear: Level.RegisterAreaComplete raises Everest's OnComplete (the end cutscene is not played)
                if (Lvl != null) {
                    Lvl.RegisterAreaComplete();
                    yield return ExpectCheck("1a|clear");
                }
                // key of 3A (room 02-b) holds Traffic Blocks
                yield return LoadRoom("3a", "02-b");
                var keyE = Lvl.Entities.OfType<Key>().FirstOrDefault();
                if (keyE == null) Result(false, "key in 3a 02-b", "not found");
                else {
                    Pl.Position = home; Pl.Speed = Vector2.Zero;
                    keyE.Position = Pl.Center;
                    yield return ExpectCheck("3a|02-b|key|32");
                    yield return ExpectItem("Traffic Blocks", "Hallway Key 1");
                    yield return LoadRoom("1a", "4");
                    int zips = Lvl.Entities.OfType<ZipMover>().Count();
                    Result(zips > 0, "Traffic Blocks received: traffic blocks loaded in 1a room 4", $"{zips} traffic block(s)");
                }
            }
            Release();
        }
    }
}
