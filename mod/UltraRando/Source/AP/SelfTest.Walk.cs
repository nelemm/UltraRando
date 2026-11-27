// CMR (phase B): "walk" self-test. Unlike the room tour (which teleports by name), this one leaves each room the
// way a player does: Madeline is put just inside a linked hole and pushed across the screen edge, so the game's own
// transition code (Level.EnforceBounds -> MapData.GetAt -> TransitionTo) picks the next room. For each step we record
// the room actually entered, compare it with the layout, compare its solids with the vanilla room of the same name,
// compare the LIVE solid-tile grid with the room's own tiles, and optionally save a screenshot.
// Flag file: line 2 = "walk"; then key=value lines: areas=1a,3a  vanilla=1 (walk the vanilla map along the same exits)
//            shots=<directory for PNG screenshots>  maxshots=8
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static partial class SelfTest {
        class WalkStep {
            public string Area;
            public AreaKey Key;
            public string From, Expected;
            public Hole Hole, OtherHole;
            public int HoleIndex;
            public string Why;
            public bool ClearExit; // the exit hole has an obstacle behind it (lock, breakable wall...) cleared from inside in real play
        }

        static bool KindExit(HoleKind k) => k == HoleKind.Out || k == HoleKind.InOut;
        static bool KindEnter(HoleKind k) => k == HoleKind.In || k == HoleKind.InOut || k == HoleKind.Unknown;
        static int walkSkipped, failShots;

        static List<WalkStep> walkSteps;
        static bool walkVanilla;
        static string shotsDir;
        static int maxShots = 8;
        static int walkIndex, walkStage, walkOk, walkBad;
        static float stageTimer, stepTimer;
        static bool forceReload;
        static int walkFrom, walkCount;

        static void SetupWalk() {
            walkSteps = new List<WalkStep>();
            var areas = slot.ActiveAreas.Where(a => slot.Chapters.ContainsKey(a) && (areaFilter == null || areaFilter.Contains(a))).ToList();
            foreach (var a in areas) {
                if (!LayoutBuilder.TryParseAreaKey(a, out var key)) continue;
                var statics = LayoutBuilder.RoomKeys(a);
                StaticRoom St(string k) => statics.TryGetValue(k, out var s) ? s : statics.Values.FirstOrDefault(x => x.Level.Name == k);
                var ch = slot.Chapters[a];
                // every link, in each direction that the apworld logic uses ("dir" in slot_data) or, when the
                // slot_data has no "dir", that the rhelmot hole kinds allow (exit: out/inout, enter: in/inout/unknown)
                string start = null;
                try { start = LayoutInstaller.Original(key).StartLevel()?.Name; } catch { }
                if (start == null) continue;
                int skipped = 0;
                for (int i = 0; i < ch.Links.Count; i++) {
                    var l = ch.Links[i];
                    var sa = St(l.A); var sb = St(l.B);
                    if (sa == null || sb == null || l.AHole < 0 || l.AHole >= sa.Holes.Count || l.BHole < 0 || l.BHole >= sb.Holes.Count) continue;
                    var ha = sa.Holes[l.AHole]; var hb = sb.Holes[l.BHole];
                    bool ab, ba;
                    string how;
                    if (!string.IsNullOrEmpty(l.Dir) && l.Dir != "none") {
                        ab = l.Dir == "ab" || l.Dir == "both";
                        ba = l.Dir == "ba" || l.Dir == "both";
                        how = "logic";
                    } else {
                        // no logical direction given (locked vanilla pair: the logic uses an Open World connection):
                        // hole kinds, and never into a hole whose entry is obstructed
                        string obs = l.Obstructed ?? "";
                        ab = KindExit(ha.Kind) && KindEnter(hb.Kind) && !obs.Contains('b');
                        ba = KindExit(hb.Kind) && KindEnter(ha.Kind) && !obs.Contains('a');
                        how = "kinds";
                    }
                    string ob = l.Obstructed ?? "";
                    if (ab) walkSteps.Add(new WalkStep { Area = a, Key = key, From = sa.Level.Name, Expected = sb.Level.Name, Hole = ha, HoleIndex = l.AHole, OtherHole = hb, Why = how, ClearExit = ob.Contains('a') });
                    else skipped++;
                    if (ba) walkSteps.Add(new WalkStep { Area = a, Key = key, From = sb.Level.Name, Expected = sa.Level.Name, Hole = hb, HoleIndex = l.BHole, OtherHole = ha, Why = how, ClearExit = ob.Contains('b') });
                    else skipped++;
                }
                walkSkipped += skipped;
                ApState.Log($"self-test walk {a}: {walkSteps.Count(s => s.Area == a)} directed steps ({ch.Links.Count} links, {skipped} one-way directions not walked)");
                report.AppendLine($"walk {a}: {walkSteps.Count(s => s.Area == a)} directed steps ({ch.Links.Count} links, {skipped} one-way directions not walked), map={(walkVanilla ? "VANILLA" : "AP layout")}");
            }
            if (walkFrom > 0) walkSteps = walkSteps.Skip(walkFrom).ToList();
            if (walkCount > 0) walkSteps = walkSteps.Take(walkCount).ToList();
            walkIndex = 0;
            walkStage = 0;
            if (!string.IsNullOrEmpty(shotsDir)) {
                try {
                    Directory.CreateDirectory(shotsDir);
                    if (walkFrom == 0) foreach (var f in Directory.GetFiles(shotsDir, "*.png")) File.Delete(f);
                } catch (Exception e) { report.AppendLine("shots dir: " + e.Message); shotsDir = null; }
            }
        }

        static void WalkUpdate() {
            if (walkIndex >= walkSteps.Count) {
                report.AppendLine($"walk: {walkOk} transitions as expected, {walkBad} not ({walkSkipped} one-way directions not walked)");
                if (walkBad > 0 && !walkVanilla) ok = false;
                Finish(true);
                return;
            }
            var step = walkSteps[walkIndex];
            var level = Engine.Scene as Level;
            var player = level?.Tracker.GetEntity<Player>();
            float dt = Engine.RawDeltaTime;
            stageTimer += dt;
            stepTimer += dt;
            currentRoom = $"{step.Area}|{step.From} -> {step.Expected}";
            if (stepTimer > 8f) {
                bool stuck = level != null && level.Transitioning;
                WalkRecord(step, level, $"TIMEOUT in stage {walkStage} (scene {Engine.Scene?.GetType().Name}, room {level?.Session?.Level}{(stuck ? ", transition never ended" : "")})", null);
                forceReload = true; // start the next step from a fresh LevelLoader
                return;
            }
            switch (walkStage) {
                case 0: // go to the source room
                    stageTimer = 0;
                    walkStage = 1;
                    if (level != null && level.Transitioning && !forceReload) { walkStage = 0; return; } // never teleport during a transition
                    // never teleport out of a running cutscene (its OnEnd then crashes on entities of the old room):
                    // start the next step from a fresh LevelLoader instead
                    if (level != null && level.InCutscene) forceReload = true;
                    if (level != null && level.Session.Area == step.Key && !level.Completed && player != null && !player.Dead && !forceReload &&
                        level.Session.MapData.Get(step.From) != null) {
                        if (level.Session.Level == step.From && !level.Transitioning) return;
                        level.OnEndOfFrame += () => {
                            var p = level.Tracker.GetEntity<Player>();
                            if (p != null) level.TeleportTo(p, step.From, Player.IntroTypes.None);
                        };
                    } else {
                        forceReload = false;
                        LevelEnter.ErrorMessage = null;
                        var session = new Session(step.Key) { FirstLevel = false, StartedFromBeginning = false };
                        session.Level = step.From;
                        session.RespawnPoint = null;
                        SaveData.Instance.StartSession(session);
                        Engine.Scene = new LevelLoader(session);
                    }
                    return;
                case 1: // settle in the source room
                    if (level != null && !level.Transitioning && level.Session.Level != step.From && stepTimer > 1.5f && stepTimer < 6f) {
                        walkStage = 0; // e.g. died and respawned elsewhere: go back
                        return;
                    }
                    if (level == null || player == null || level.Transitioning || level.Session.Level != step.From) { stageTimer = 0; pinned = null; return; }
                    // keep Madeline where the teleport put her: a spawn point can sit right above a linked bottom hole
                    // (4a b-06) and she would fall into the next room before the step starts
                    if (pinned == null) pinned = player.Position;
                    player.Position = pinned.Value;
                    player.Speed = Vector2.Zero;
                    if (level.InCutscene) { // a room cutscene (roof00 Oshiro, 3a 08-a...) would keep Madeline in StDummy
                        SkipCutsceneOnce(level);
                        stageTimer = 0;
                        return;
                    }
                    if (player.StateMachine.State != Player.StNormal && player.StateMachine.State != Player.StPickup) player.StateMachine.State = Player.StNormal;
                    // Theo rooms (5a e-*): a real player carries Theo to the exit (gates and the right edge need him)
                    var theo = CarryTheo(level, player);
                    if (stageTimer < (theo != null ? 0.6f : 0.15f)) return;
                    ClearObstacles(level, step, step.ClearExit);
                    stageTimer = 0;
                    walkStage = 2;
                    return;
                case 2: // push Madeline through the hole
                    if (level == null) return;
                    if (level.Transitioning || level.Session.Level != step.From) { stageTimer = 0; walkStage = 3; return; }
                    if (player == null || player.Dead) return;
                    if (level.InCutscene) SkipCutsceneOnce(level);
                    CarryTheo(level, player);
                    Push(level, player, step);
                    if (stageTimer > 2f) WalkRecord(step, level, "no transition (blocked or not linked in the map geometry)", null);
                    return;
                case 3: // arrived?
                    if (level == null || level.Transitioning || player == null) { stageTimer = 0; return; }
                    if (stageTimer < 0.2f) return;
                    WalkRecord(step, level, null, level.Session.Level);
                    return;
            }
        }

        static readonly System.Reflection.MethodInfo pickupMethod = typeof(Player).GetMethod("Pickup",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(Holdable) }, null);

        static Vector2? pinned;
        static float cutsceneTimer;
        static bool cutsceneSkipped;

        /// <summary>Skip a room cutscene like a player would from the pause menu: once, and not on its first frames
        /// (skipping CS03_OshiroRooftop at once, or twice, crashes in its OnEnd).</summary>
        static void SkipCutsceneOnce(Level level) {
            cutsceneTimer += Engine.RawDeltaTime;
            if (cutsceneSkipped || level.SkippingCutscene || cutsceneTimer < 1.5f) return;
            cutsceneSkipped = true;
            level.SkipCutscene();
        }

        static readonly System.Reflection.FieldInfo minHoldTimerField = typeof(Player).GetField("minHoldTimer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        /// <summary>Theo rooms (5a e-*): a real player carries Theo to the exit (Theo gates). Pick him up and keep
        /// him held (no grab input here, so the hold timer is kept up, otherwise Madeline throws him at once).</summary>
        static TheoCrystal CarryTheo(Level level, Player player) {
            var theo = level.Tracker.GetEntity<TheoCrystal>();
            if (theo == null) return null;
            try {
                if (player.Holding?.Entity != theo) {
                    theo.Position = player.Position;
                    pickupMethod?.Invoke(player, new object[] { theo.Hold });
                }
                if (player.Holding?.Entity == theo) minHoldTimerField?.SetValue(player, 1f);
            } catch { }
            return theo;
        }

        /// <summary>The exit is behind an obstacle that the player clears from inside (lock block opened with the key,
        /// breakable wall dashed, cracked temple block...): remove the solid entities that cover the hole.</summary>
        // CrumbleWallOnRumble: falls on a room event the walk skips (10a j-00 east exit, also its vanilla exit to j-00b)
        static readonly HashSet<string> Breakable = new HashSet<string> { "DashBlock", "LockBlock", "TempleCrackedBlock", "ClutterBlockBase", "ClutterBlock", "CrumbleWallOnRumble" };

        /// <param name="all">true: every solid entity (the hole is "obstructed"); false: only breakable/openable ones
        /// (dash blocks, locks, cracked blocks, clutter), that a player opens with the items the logic asks for.</param>
        static void ClearObstacles(Level level, WalkStep step, bool all) {
            var b = level.Bounds;
            var h = step.Hole;
            int lo = h.LowBound * 8, hi = (h.HighBound + 1) * 8, depth = 48;
            Rectangle r = h.Side switch {
                ScreenDirection.Up => new Rectangle(b.Left + lo, b.Top, hi - lo, depth),
                ScreenDirection.Down => new Rectangle(b.Left + lo, b.Bottom - depth, hi - lo, depth),
                ScreenDirection.Left => new Rectangle(b.Left, b.Top + lo, depth, hi - lo),
                _ => new Rectangle(b.Right - depth, b.Top + lo, depth, hi - lo),
            };
            var removed = new List<string>();
            foreach (var e in level.Tracker.GetEntities<Solid>().ToList()) {
                if (e is SolidTiles || !e.Collidable || !e.CollideRect(r)) continue;
                if (!all && !Breakable.Contains(e.GetType().Name)) continue;
                removed.Add(e.GetType().Name);
                e.RemoveSelf();
            }
            if (removed.Count > 0) {
                var msg = $"walk: cleared obstacle(s) [{string.Join(",", removed)}] before {step.From} hole#{step.HoleIndex}";
                report.AppendLine("  " + msg);
                ApState.Log("self-test " + msg);
            }
        }

        static void Push(Level level, Player player, WalkStep step) {
            var hole = step.Hole;
            var b = level.Session.LevelData.Bounds;
            float c = (hole.LowBound + hole.HighBound + 1) * 4f;
            // aim at the part of the hole that faces the linked room's hole (the rest is blocked by Bake's barriers)
            var nb = step.OtherHole != null ? level.Session.MapData.Get(step.Expected)?.Bounds : null;
            if (nb != null) {
                bool vertical = hole.Side == ScreenDirection.Up || hole.Side == ScreenDirection.Down;
                int aOrigin = vertical ? b.Left : b.Top, bOrigin = vertical ? nb.Value.Left : nb.Value.Top;
                int lo = Math.Max(aOrigin + hole.LowBound * 8, bOrigin + step.OtherHole.LowBound * 8);
                int hi = Math.Min(aOrigin + (hole.HighBound + 1) * 8, bOrigin + (step.OtherHole.HighBound + 1) * 8);
                if (hi > lo) c = (lo + hi) / 2f - aOrigin;
            }
            player.StateMachine.State = Player.StNormal;
            // a camera lock (7a Badeline boost rooms...) clamps Madeline to the camera; the camera would follow a
            // real player, not a teleported one
            if (level.CameraLockMode != Level.CameraLockModes.None) level.CameraLockMode = Level.CameraLockModes.None;
            switch (hole.Side) {
                case ScreenDirection.Up:
                    player.Position = new Vector2(b.Left + c, b.Top + 6);
                    player.Speed = new Vector2(0, -240);
                    break;
                case ScreenDirection.Down:
                    player.Position = new Vector2(b.Left + c, b.Bottom + 12);
                    player.Speed = new Vector2(0, 240);
                    break;
                case ScreenDirection.Left:
                    player.Position = new Vector2(b.Left + 1, b.Top + c + 8);
                    player.Speed = new Vector2(-240, 0);
                    break;
                default:
                    player.Position = new Vector2(b.Right - 1, b.Top + c + 8);
                    player.Speed = new Vector2(240, 0);
                    break;
            }
        }

        static void WalkRecord(WalkStep step, Level level, string error, string actual) {
            int i = walkIndex;
            string line;
            int gi = i + walkFrom;
            if (error != null) {
                walkBad++;
                string shot = "";
                if (shotsDir != null && failShots < 40) {
                    failShots++;
                    var file = Path.Combine(shotsDir, $"FAIL_{gi:000}_{San(step.Area)}_from-{San(step.From)}_to-{San(step.Expected)}.png");
                    shot = SaveShot(file) ? " shot=" + Path.GetFileName(file) : " shot=FAILED";
                }
                line = $"walk {gi:000} {step.Area} {step.From} hole#{step.HoleIndex}({step.Hole.Side},{step.Hole.Kind}->{step.OtherHole?.Kind},{step.Why}) expected={step.Expected}: {error} | {WalkDiag(level, step)}{shot}";
            } else {
                bool asExpected = actual == step.Expected;
                var ld = level.Session.LevelData;
                // core mode (9a/9b/9c): the mode set on arrival must be the one of the entry side (rhelmot config)
                string core = "";
                var cm = new MonoMod.Utils.DynData<LevelData>(ld).Get<RandoConfigCoreMode>("CMR_coreModes");
                if (cm != null && asExpected) {
                    var side = step.Hole.Side;
                    var want = side == ScreenDirection.Right ? cm.Left : side == ScreenDirection.Left ? cm.Right
                             : side == ScreenDirection.Up ? cm.Down : cm.Up;
                    core = $" core={level.CoreMode}/{want}";
                    if (level.CoreMode != want) { asExpected = false; core += " COREMODE-WRONG"; }
                }
                if (asExpected) walkOk++; else walkBad++;
                var vanilla = LayoutInstaller.Original(step.Key);
                var (best, bestSim, sameSim) = BestVanillaMatch(vanilla, ld);
                double grid = LiveGridMatch(level, ld);
                var ents = string.Join(",", ld.Entities.Select(e => e.Name).Where(n => n != "invisibleBarrier").Distinct().OrderBy(n => n).Take(12));
                string shot = "";
                if (!asExpected && shotsDir != null && failShots < 40) {
                    failShots++;
                    var file = Path.Combine(shotsDir, $"FAIL_{gi:000}_{San(step.Area)}_from-{San(step.From)}_to-{San(step.Expected)}_actual-{San(actual)}.png");
                    shot = SaveShot(file) ? " shot=" + Path.GetFileName(file) : " shot=FAILED";
                } else if (shotsDir != null && i < maxShots) {
                    var file = Path.Combine(shotsDir, $"{gi:000}_from-{San(step.From)}_expected-{San(step.Expected)}_actual-{San(actual)}.png");
                    shot = SaveShot(file) ? " shot=" + Path.GetFileName(file) : " shot=FAILED";
                }
                line = $"walk {gi:000} {step.Area} {step.From} hole#{step.HoleIndex}({step.Hole.Side},{step.Hole.Kind}->{step.OtherHole?.Kind},{step.Why}) expected={step.Expected} actual={actual} " +
                       $"{(asExpected ? "OK" : "MISMATCH")}{core} bounds={ld.Bounds.X},{ld.Bounds.Y},{ld.Bounds.Width}x{ld.Bounds.Height} " +
                       $"solids~vanilla[{actual}]={sameSim:P0} bestVanilla={best}({bestSim:P0}) liveGrid={grid:P0} hash={Fnv(ld.Solids):x8} ents=[{ents}]{shot}";
            }
            report.AppendLine(line);
            Logger.Log(LogLevel.Info, "UltraRando/AP", "self-test " + line);
            lastProgress = Environment.TickCount64;
            walkIndex++;
            walkStage = 0;
            cutsceneTimer = 0;
            cutsceneSkipped = false;
            pinned = null;
            stageTimer = stepTimer = 0;
        }

        /// <summary>Why a step failed: where Madeline is, what the game says about the transition target, and which
        /// solids / Theo / barriers are around the pushed position.</summary>
        static string WalkDiag(Level level, WalkStep step) {
            try {
                if (level == null) return "no level";
                var p = level.Tracker.GetEntity<Player>();
                var sb = new System.Text.StringBuilder();
                sb.Append($"scene room={level.Session.Level} transitioning={level.Transitioning}");
                if (p == null) return sb.Append(" no player").ToString();
                var b = level.Bounds;
                sb.Append($" player={p.Position.X:0},{p.Position.Y:0} state={p.StateMachine.State} dead={p.Dead} bounds={b.X},{b.Y},{b.Width}x{b.Height} camLock={level.CameraLockMode}");
                Vector2 dir = step.Hole.Side switch {
                    ScreenDirection.Up => -Vector2.UnitY, ScreenDirection.Down => Vector2.UnitY,
                    ScreenDirection.Left => -Vector2.UnitX, _ => Vector2.UnitX,
                };
                var target = p.Center + dir * 8;
                var t = level.Session.MapData.GetTransitionTarget(level, target);
                sb.Append($" target@{target.X:0},{target.Y:0}={t?.Name ?? "none"}");
                var theo = level.Tracker.GetEntity<TheoCrystal>();
                if (theo != null) sb.Append($" THEO(held={p.Holding?.Entity == theo})");
                var box = new Rectangle((int)p.Left - 8, (int)p.Top - 8, (int)p.Width + 16, (int)p.Height + 16);
                var near = level.Tracker.GetEntities<Solid>().Where(e => e.Collidable && e.CollideRect(box)).Select(e => e.GetType().Name).Distinct().Take(8);
                sb.Append($" solids~[{string.Join(",", near)}]");
                if (step.Hole.Side == ScreenDirection.Up || step.Hole.Side == ScreenDirection.Down) {
                    var holes = new MonoMod.Utils.DynData<LevelData>(level.Session.LevelData).Get<List<Hole>>("CMR_UsedVerticalHoles");
                    sb.Append(" usedVertical=[" + (holes == null ? "null" : string.Join(",", holes.Select(h => $"{h.Side}{h.LowBound}-{h.HighBound}"))) + "]");
                }
                return sb.ToString();
            } catch (Exception e) {
                return "diag failed: " + e.Message;
            }
        }

        static string San(string s) => Text.FileToken(s ?? "none", 24);

        static bool SaveShot(string file) {
            try {
                var rt = GameplayBuffers.Level?.Target;
                if (rt == null) return false;
                using var fs = File.Create(file);
                rt.SaveAsPng(fs, rt.Width, rt.Height);
                return true;
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "UltraRando/AP", "screenshot failed: " + e.Message);
                return false;
            }
        }

        static uint Fnv(string s) {
            uint h = 2166136261;
            foreach (char ch in (s ?? "").Replace("\r", "")) { h ^= ch; h *= 16777619; }
            return h;
        }

        static string[] Rows(string s) => (s ?? "").Replace("\r", "").Split('\n');

        /// <summary>Fraction of equal tiles among tiles that are solid in either room (same top-left origin).</summary>
        static double SolidsSim(string a, string b) {
            var ra = Rows(a); var rb = Rows(b);
            int rows = Math.Max(ra.Length, rb.Length), same = 0, total = 0;
            for (int y = 0; y < rows; y++) {
                var la = y < ra.Length ? ra[y] : ""; var lb = y < rb.Length ? rb[y] : "";
                int cols = Math.Max(la.Length, lb.Length);
                for (int x = 0; x < cols; x++) {
                    char ca = x < la.Length ? la[x] : '0', cb = x < lb.Length ? lb[x] : '0';
                    if (ca == '0' && cb == '0') continue;
                    total++;
                    if ((ca != '0') == (cb != '0')) same++;
                }
            }
            return total == 0 ? 1 : (double)same / total;
        }

        /// <summary>Vanilla level whose solids look most like <paramref name="ld"/> (same size only).</summary>
        static (string best, double bestSim, double sameNameSim) BestVanillaMatch(MapData vanilla, LevelData ld) {
            string best = null; double bestSim = -1, same = 0;
            if (vanilla == null) return ("?", 0, 0);
            foreach (var v in vanilla.Levels) {
                if (v.Bounds.Width != ld.Bounds.Width || v.Bounds.Height != ld.Bounds.Height) continue;
                double s = SolidsSim(v.Solids, ld.Solids);
                if (v.Name == ld.Name) same = s;
                if (s > bestSim || (s == bestSim && v.Name == ld.Name)) { bestSim = s; best = v.Name; }
            }
            return (best ?? "?", bestSim, same);
        }

        /// <summary>Compares the live solid-tile grid of the Level (what is drawn and collided with) with the room's
        /// own Solids string, at the room's position in the map.</summary>
        static double LiveGridMatch(Level level, LevelData ld) {
            var grid = level.SolidsData;
            var tb = level.Session.MapData.TileBounds;
            if (grid == null) return 0;
            var rows = Rows(ld.Solids);
            int ox = ld.TileBounds.X - tb.X, oy = ld.TileBounds.Y - tb.Y, same = 0, total = 0;
            for (int y = 0; y < ld.TileBounds.Height; y++) {
                var line = y < rows.Length ? rows[y] : "";
                for (int x = 0; x < ld.TileBounds.Width; x++) {
                    char want = x < line.Length ? line[x] : '0';
                    int gx = ox + x, gy = oy + y;
                    if (gx < 0 || gy < 0 || gx >= grid.Columns || gy >= grid.Rows) continue;
                    char got = grid[gx, gy];
                    if (want == '0' && got == '0') continue;
                    total++;
                    if ((want != '0') == (got != '0')) same++;
                }
            }
            return total == 0 ? 1 : (double)same / total;
        }
    }
}
