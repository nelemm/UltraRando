// CMR: mechanics self-test (flag file line 2 = "mech"). Proves in the running game that the movement locks work:
// keyboard keys are injected under MInput (the player's own bindings), so a test press goes through VirtualButton,
// Player.NormalUpdate/StartDash and our hooks exactly like a real key press. Items are given through
// ApState.ItemReceived (the server's path). A test passes on what Madeline actually does (state, speed, dash spent),
// never on a flag. Each result line: "mech <id> <PASS|FAIL> <name>: <measures>".
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static partial class SelfTest {
        static IEnumerator mechScript;
        static readonly Stack<IEnumerator> mechStack = new Stack<IEnumerator>();
        static object mechWait;          // float seconds, or Func<bool> (wait until true, 5 s max)
        static float mechWaitTimer;
        static int mechPass, mechFail, mechId;
        static HashSet<Keys> injected;   // null = real keyboard
        static MonoMod.RuntimeDetour.Hook kbUpdateHook, kbNullHook;
        static int itemIndex;

        static int dieCalls;
        static PlayerDeadBody CountDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 dir, bool evenIfInvincible, bool registerDeathInStats) {
            dieCalls++;
            return orig(self, dir, evenIfInvincible, registerDeathInStats);
        }

        static void SetupMech(IEnumerator script = null) {
            mechScript = script ?? MechScript();
            if (!string.IsNullOrEmpty(shotsDir) && savesSteps == null) {
                System.IO.Directory.CreateDirectory(shotsDir);
                On.Monocle.Engine.Draw += CaptureAfterDraw; // Shot() (SelfTest.Saves.cs); the process exits at the end
            }
            var kd = typeof(MInput.KeyboardData);
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            kbUpdateHook = new MonoMod.RuntimeDetour.Hook(kd.GetMethod("Update", bf), new Action<Action<MInput.KeyboardData>, MInput.KeyboardData>(KeyboardHook));
            kbNullHook = new MonoMod.RuntimeDetour.Hook(kd.GetMethod("UpdateNull", bf), new Action<Action<MInput.KeyboardData>, MInput.KeyboardData>(KeyboardHook));
            // a real controller (stick at rest that drifts, a thumb on it) must not add to the injected keys
            padHook = new MonoMod.RuntimeDetour.Hook(typeof(MInput.GamePadData).GetMethod("Update", bf),
                new Action<Action<MInput.GamePadData>, MInput.GamePadData>((orig, self) => self.UpdateNull()));
        }
        static MonoMod.RuntimeDetour.Hook padHook;

        static void KeyboardHook(Action<MInput.KeyboardData> orig, MInput.KeyboardData self) {
            orig(self);
            if (injected != null) self.CurrentState = new KeyboardState(injected.ToArray());
        }

        static void MechUpdate() {
            lastProgress = Environment.TickCount64;
            if (mechWait is float f) {
                mechWaitTimer += Engine.RawDeltaTime;
                if (mechWaitTimer < f) return;
            } else if (mechWait is Func<bool> until) {
                mechWaitTimer += Engine.RawDeltaTime;
                if (!until() && mechWaitTimer < 5f) return;
            }
            mechWait = null;
            mechWaitTimer = 0;
            bool more = true;
            try {
                // nested scripts (yield return Dash(...)) run on a stack, like Monocle coroutines
                while (true) {
                    var top = mechStack.Count > 0 ? mechStack.Peek() : mechScript;
                    if (!top.MoveNext()) {
                        if (mechStack.Count == 0) { more = false; break; }
                        mechStack.Pop();
                        continue;
                    }
                    if (top.Current is IEnumerator sub) { mechStack.Push(sub); continue; }
                    mechWait = top.Current;
                    break;
                }
            } catch (Exception e) {
                report.AppendLine("mech: script exception " + e);
                ok = false;
                more = false;
            }
            if (!more) {
                injected = null;
                kbUpdateHook?.Dispose(); kbNullHook?.Dispose();
                report.AppendLine($"mech: {mechPass} PASS, {mechFail} FAIL");
                if (mechFail > 0) ok = false;
                mechScript = null;
                if (savesSteps != null) { NextSave(); return; } // chain test: the saves flow ends with its own "finish"
                Finish(true);
                return;
            }
        }

        // ------------------------------------------------------------------ helpers

        static Keys K(Binding b) => b.Keyboard.Count > 0 ? b.Keyboard[0] : Keys.None;
        static Keys KDash => K(Settings.Instance.Dash);
        static Keys KJump => K(Settings.Instance.Jump);
        static Keys KGrab => K(Settings.Instance.Grab);
        static Keys KLeft => K(Settings.Instance.Left);
        static Keys KRight => K(Settings.Instance.Right);
        static Keys KUp => K(Settings.Instance.Up);
        static Keys KDown => K(Settings.Instance.Down);

        static IEnumerable<Keys> DirKeys(int dx, int dy) {
            if (dx < 0) yield return KLeft;
            if (dx > 0) yield return KRight;
            if (dy < 0) yield return KUp;
            if (dy > 0) yield return KDown;
        }

        static void Hold(params Keys[] keys) => injected = new HashSet<Keys>(keys.Where(k => k != Keys.None));
        static void Release() => injected = new HashSet<Keys>();

        static Level Lvl => Engine.Scene as Level;
        static Player Pl => Lvl?.Tracker.GetEntity<Player>();

        static void Result(bool pass, string name, string measures) {
            mechId++;
            if (pass) mechPass++; else mechFail++;
            var line = $"mech {mechId:000} {(pass ? "PASS" : "FAIL")} {name}: {measures}";
            report.AppendLine(line);
            Logger.Log(LogLevel.Info, "UltraRando/AP", "self-test " + line);
        }

        static readonly string[] DirName = { "UL", "U", "UR", "L", "", "R", "DL", "D", "DR" };
        static string Dn(int dx, int dy) => DirName[(dy + 1) * 3 + dx + 1];
        static readonly Dictionary<string, string> DirItem = new Dictionary<string, string> {
            { "U", ItemNames.UpDash }, { "UR", ItemNames.UpRightDash }, { "R", ItemNames.RightDash }, { "DR", ItemNames.DownRightDash },
            { "D", ItemNames.DownDash }, { "DL", ItemNames.DownLeftDash }, { "L", ItemNames.LeftDash }, { "UL", ItemNames.UpLeftDash },
        };
        static readonly (int, int)[] Dirs = { (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1) };

        static void Own(params string[] items) {
            ApState.ClearOwnedForTest();
            itemIndex = 0;
            foreach (var it in items) ApState.ItemReceived(itemIndex++, it, "selftest");
        }

        static void SetOpt(string name, int v) => ApState.Slot.Options[name] = v;

        static Vector2 home;
        static readonly List<Entity> testSolids = new List<Entity>();

        /// <summary>Madeline back on her spot, standing, full dash and stamina, no test wall, no key held.</summary>
        static string testRoom;

        static IEnumerator ResetPlayer(bool fast = false) {
            Release();
            foreach (var s in testSolids) s.RemoveSelf();
            testSolids.Clear();
            yield return (Func<bool>)(() => Pl != null && !Pl.Dead && !Lvl.Transitioning);
            if (testRoom != null && Lvl.Session.Level != testRoom) { // a test movement left the room: come back
                report.AppendLine($"  (left the test room {testRoom} for {Lvl.Session.Level}: teleported back)");
                var lv = Lvl;
                lv.OnEndOfFrame += () => { if (lv.Tracker.GetEntity<Player>() is Player pp) lv.TeleportTo(pp, testRoom, Player.IntroTypes.Respawn); };
                yield return (Func<bool>)(() => Pl != null && Lvl.Session.Level == testRoom && !Lvl.Transitioning);
                yield return 0.3f;
            }
            var p = Pl;
            p.Position = home;
            p.Speed = Vector2.Zero;
            p.StateMachine.State = Player.StNormal;
            if (p.Ducking && p.CanUnDuck) p.Ducking = false;
            p.RefillDash();
            p.RefillStamina();
            yield return fast ? 0.04f : 0.12f;
            p = Pl;
            if (p != null) { p.Position = home; p.Speed = Vector2.Zero; p.RefillDash(); }
            yield return fast ? 0.02f : 0.05f;
        }

        /// <summary>A point above home where standing Madeline touches no solid, with 24 px free on both sides
        /// (rooms of the chain test are real rooms: the spawn point can be under a low ceiling).</summary>
        static Vector2 AirSpot() {
            var p = Pl;
            var save = p.Position;
            bool ducking = p.Ducking;
            p.Ducking = false; // test with the standing hitbox
            Vector2 best = home - new Vector2(0, 40);
            var b = Lvl.Bounds;
            bool found = false;
            for (int ox = 0; ox <= 160 && !found; ox += 8) {
                foreach (int sx in ox == 0 ? new[] { 0 } : new[] { -ox, ox }) {
                    for (int dy = 24; dy <= 120 && !found; dy += 4) {
                        var c = home + new Vector2(sx, -dy);
                        if (c.X < b.Left + 96 || c.X > b.Right - 96 || c.Y < b.Top + 48) continue; // a dash must not leave the room
                        bool free = true;
                        foreach (var dx in new[] { -32, 0, 32 }) {
                            p.Position = c + new Vector2(dx, 0);
                            if (p.CollideCheck<Solid>() || p.CollideCheck<Solid>(p.Position - new Vector2(0, 12))) { free = false; break; }
                        }
                        if (free) { best = c; found = true; }
                    }
                    if (found) break;
                }
            }
            p.Position = save;
            p.Ducking = ducking;
            return best;
        }

        static Solid Wall(float x, float y, float w, float h) {
            var s = new Solid(new Vector2(x, y), w, h, true);
            Lvl.Add(s);
            testSolids.Add(s);
            return s;
        }

        // one dash attempt, in the air (on the ground Celeste turns a down-diagonal dash horizontal and refills the
        // dash at once): direction keys held, dash pressed for one frame, then watched. "spent" = the dash count
        // went down at some point (a landing may refill it afterwards).
        static IEnumerator Dash(int dx, int dy, bool expect, string why, bool fast = false) {
            yield return ResetPlayer(fast);
            var p = Pl;
            p.Position = AirSpot();
            p.Speed = Vector2.Zero;
            int before = p.Dashes, low = before;
            var dirKeys = DirKeys(dx, dy).ToArray();
            Hold(dirKeys);
            yield return null;
            Hold(dirKeys.Concat(new[] { KDash }).ToArray());
            yield return null;
            Hold(dirKeys);
            bool dashed = false;
            Vector2 dir = Vector2.Zero;
            for (float t = 0; t < 0.15f; t += Engine.RawDeltaTime) {
                p = Pl;
                if (p == null) break;
                low = Math.Min(low, p.Dashes);
                if (p.StateMachine.State == Player.StDash) dashed = true;
                if (dashed && p.DashDir != Vector2.Zero && dir == Vector2.Zero) dir = p.DashDir;
                yield return null;
            }
            bool dirOk = !dashed || (Math.Sign(dir.X) == dx && Math.Sign(dir.Y) == dy);
            bool spent = low < before;
            bool pass = expect ? (dashed && dirOk && spent) : (!dashed && !spent);
            Release();
            if (!fast || !pass)
                Result(pass, $"dash {Dn(dx, dy)} {(expect ? "allowed" : "refused")} ({why})",
                    $"state StDash seen={dashed} DashDir=({dir.X:0.00},{dir.Y:0.00}) dashes {before}->min {low}");
            else { mechPass++; mechId++; }
        }

        static Keys KDemo => K(Settings.Instance.DemoDash);

        /// <summary>A dash in the air, started either with the crouch-dash (demo) key aiming (dx, dy), or by holding
        /// down at the press then aiming (dx, dy) before the direction is fixed; or "switch": press aiming (sx, sy)
        /// then aim (dx, dy). Reports the final DashDir and whether Madeline was crouched once it was fixed.</summary>
        static IEnumerator DashVariant(string how, int dx, int dy, int sx, int sy, Func<Vector2, bool, bool> check, string name, bool ground = false) {
            yield return ResetPlayer();
            var p = Pl;
            if (!ground) { p.Position = AirSpot(); p.Speed = Vector2.Zero; }
            bool ducking0 = p.Ducking; p.Ducking = false;
            bool canStand = !p.CollideCheck<Solid>();
            p.Ducking = ducking0;
            var target = DirKeys(dx, dy).ToArray();
            Keys[] first = how == "demokey" ? target.Concat(new[] { KDemo }).ToArray()
                         : how == "downswitch" ? new[] { KDown, KDash }
                         : DirKeys(sx, sy).Concat(new[] { KDash }).ToArray();
            if (how == "switch") { Hold(DirKeys(sx, sy).ToArray()); yield return null; }
            else if (how == "ground") { Hold(target); yield return null; first = target.Concat(new[] { KDash }).ToArray(); }
            Hold(first);
            yield return null;
            Hold(target);
            bool dashed = false, ducked = false;
            Vector2 dir = Vector2.Zero;
            int after = 0;
            for (float t = 0; t < 0.2f; t += Engine.RawDeltaTime) {
                p = Pl;
                if (p == null) break;
                if (p.StateMachine.State == Player.StDash) dashed = true;
                if (dashed && p.DashDir != Vector2.Zero) {
                    if (dir == Vector2.Zero) dir = p.DashDir;
                    if (after++ < 3 && p.Ducking) ducked = true; // crouched once the direction is fixed
                }
                yield return null;
            }
            if (!canStand) { Release(); Result(false, name, $"INVALID TEST: no room to stand at {p.Position} (Celeste keeps Madeline crouched)"); yield break; }
            Release();
            Result(dashed && check(dir, ducked), name, $"StDash={dashed} DashDir=({dir.X:0.00},{dir.Y:0.00}) crouched after the direction was fixed={ducked} " +
                $"(Demo Dash owned={ApState.Has(ItemNames.DemoDash)}, demo_dash_shuffle={ApState.Slot?.Option("demo_dash_shuffle", 0)}, room to stand={canStand}, at {p.Position})");
        }

        static IEnumerator SwimDash(int dx, int dy, bool expectMove, string why) {
            yield return ResetPlayer();
            var p = Pl;
            var w = new Water(home - new Vector2(40, 120), true, true, 80, 100);
            Lvl.Add(w); testSolids.Add(w);
            yield return null;
            p = Pl;
            p.Position = home - new Vector2(0, 60);
            p.Speed = Vector2.Zero;
            yield return (Func<bool>)(() => Pl != null && Pl.StateMachine.State == Player.StSwim);
            bool swimming = Pl?.StateMachine.State == Player.StSwim;
            var keys = DirKeys(dx, dy).ToArray();
            Hold(keys); yield return null;
            Hold(keys.Concat(new[] { KDash }).ToArray()); yield return null;
            Hold(keys);
            bool dashed = false; Vector2 dir = Vector2.Zero; float maxSpeed = 0;
            for (float t = 0; t < 0.2f; t += Engine.RawDeltaTime) {
                p = Pl;
                if (p == null) break;
                if (p.StateMachine.State == Player.StDash) { dashed = true; maxSpeed = Math.Max(maxSpeed, p.Speed.Length()); if (dir == Vector2.Zero) dir = p.DashDir; }
                yield return null;
            }
            bool moved = dashed && maxSpeed > 100;
            Release();
            Result(swimming && moved == expectMove, $"water dash {Dn(dx, dy)} {(expectMove ? "moves" : "does not move")} ({why})",
                $"swimming={swimming} StDash={dashed} DashDir=({dir.X:0.00},{dir.Y:0.00}) max dash speed {maxSpeed:0}");
        }

        static IEnumerator Climb(int side, bool expect, string why) {
            yield return ResetPlayer();
            var p = Pl;
            if (side > 0) Wall(p.Right, p.Bottom - 48, 16, 48); else Wall(p.Left - 16, p.Bottom - 48, 16, 48);
            yield return null;
            Hold(side > 0 ? KRight : KLeft, KGrab);
            bool climbed = false;
            for (float t = 0; t < 0.3f; t += Engine.RawDeltaTime) {
                if (Pl?.StateMachine.State == Player.StClimb) climbed = true;
                yield return null;
            }
            Release();
            Result(expect == climbed, $"climb {(side > 0 ? "right" : "left")} wall {(expect ? "allowed" : "refused")} ({why})",
                $"state StClimb seen={climbed}");
        }

        static IEnumerator Crouch(bool expect, string why) {
            yield return ResetPlayer();
            Hold(KDown);
            bool ducked = false;
            for (float t = 0; t < 0.25f; t += Engine.RawDeltaTime) {
                if (Pl != null && Pl.Ducking) ducked = true;
                yield return null;
            }
            Release();
            Result(expect == ducked, $"crouch {(expect ? "allowed" : "refused")} ({why})", $"Ducking seen={ducked}");
        }

        // in the air next to a wall, no grab: a jump press is a wall jump (speed away from the wall) or nothing
        static IEnumerator WallJump(int side, bool expect, string why) {
            yield return ResetPlayer();
            var p = Pl;
            var air = home - new Vector2(0, 56);
            if (side > 0) Wall(home.X + 4, air.Y - 64, 16, 80); else Wall(home.X - 20, air.Y - 64, 16, 80);
            yield return null;
            p = Pl;
            p.Position = air;
            p.Speed = Vector2.Zero;
            yield return 0.15f; // the jump grace time of the ground is over
            Hold(KJump);
            float sx = 0, sy = 0;
            for (float t = 0; t < 0.1f; t += Engine.RawDeltaTime) {
                if (Pl != null && Pl.Speed.Y < sy) { sy = Pl.Speed.Y; sx = Pl.Speed.X; }
                yield return null;
            }
            bool walljumped = sy < -50 && Math.Sign(sx) == -side;
            Release();
            Result(expect == walljumped, $"wall jump off {(side > 0 ? "right" : "left")} wall {(expect ? "allowed" : "refused")} ({why})",
                $"best upward speed {sy:0} with x speed {sx:0}");
        }

        // ------------------------------------------------------------------ interactables (absent / present / working)

        /// <summary>Entities of the loaded room that come from an EntityData of this item (Everest's SourceData, or
        /// same position as such an EntityData).</summary>
        static int CountItemEntities(string item, out int inData) {
            var lv = Lvl; var ld = lv.Session.LevelData; var off = new Vector2(ld.Bounds.Left, ld.Bounds.Top);
            var datas = ld.Entities.Where(e => ItemNames.ItemForEntity(e) == item).ToList();
            inData = datas.Count;
            int n = 0;
            foreach (var e in lv.Entities) {
                var sd = e.SourceData;
                if (sd != null ? ItemNames.ItemForEntity(sd) == item && datas.Any(d => d.ID == sd.ID)
                               : datas.Any(d => Vector2.DistanceSquared(d.Position + off, e.Position) < 4f)) n++;
            }
            return n;
        }

        /// <summary>Spikes attached to a locked block go with it: for each block item, a room of the seed with spikes
        /// on such a block; locked: none of those spikes loaded; received: all back.</summary>
        static IEnumerator AttachedTest() {
            SetOpt("interactable_shuffle", 1); SetOpt("dash_shuffle", 0);
            foreach (var item in new[] { "Pink Cassette Blocks", "Blue Cassette Blocks", "Yellow Cassette Blocks", "Green Cassette Blocks",
                                         "Traffic Blocks", "Move Blocks", "Swap Blocks", "Kevins", "Dream Blocks", "Core Blocks" }) {
                string area = null, room = null; int want = 0;
                foreach (var ak in ApState.Slot.ActiveAreas.OrderBy(a => a)) {
                    if (!ApState.Slot.Chapters.TryGetValue(ak, out var ch)) continue;
                    var statics = LayoutBuilder.RoomKeys(ak);
                    foreach (var r in ch.Rooms) {
                        var vk = string.IsNullOrEmpty(r.Variant) ? r.Room : r.Variant;
                        if (!statics.TryGetValue(vk, out var st)) continue;
                        var blocks = st.Level.Entities.Where(e => ItemNames.ItemForEntity(e) == item && e.Width > 0 && e.Height > 0)
                            .Select(e => new Rectangle((int)e.Position.X, (int)e.Position.Y, e.Width, e.Height)).ToList();
                        int n = blocks.Count == 0 ? 0 : st.Level.Entities.Count(e => ApHooks.AttachedTo(e, blocks));
                        if (n > 0) { area = ak; room = r.Room; want = n; break; }
                    }
                    if (room != null) break;
                }
                if (room == null) { Result(true, $"{item}: no spikes on such a block in this seed", "nothing to check"); continue; }
                int Count() {
                    var ld = Lvl.Session.LevelData; var off = new Vector2(ld.Bounds.Left, ld.Bounds.Top);
                    var blocks = ld.Entities.Where(e => ItemNames.ItemForEntity(e) == item && e.Width > 0 && e.Height > 0)
                        .Select(e => new Rectangle((int)e.Position.X, (int)e.Position.Y, e.Width, e.Height)).ToList();
                    var datas = ld.Entities.Where(e => ApHooks.AttachedTo(e, blocks)).ToList();
                    return Lvl.Entities.Count(en => (en is Spikes || en is TriggerSpikes || en is CrystalStaticSpinner) &&
                        datas.Any(d => Vector2.DistanceSquared(d.Position + off, en.Position) < 17f)); // an inactive cassette block sits 2 px lower, its spikes too
                }
                Own();
                yield return LoadRoom(area, room);
                int locked = Count();
                Shot($"attached_{item.Replace(" ", "")}_locked.png");
                Own(item);
                yield return LoadRoom(area, room);
                int got = Count();
                Result(locked == 0 && got == want, $"{item} in {area} {room}: attached spikes gone while locked, back once received",
                    $"{want} attached in the room data, loaded locked={locked}, received={got}");
            }
        }

        /// <summary>Pause menu on our maps: "Return to Map" is there even when the Prologue is not completed, and it
        /// takes the player back to the map (the game's own give-up confirmation, then the overworld).</summary>
        static IEnumerator PauseMenuTest() {
            yield return LoadRoom("1a", "2");
            // like a fresh randomizer save: Prologue not completed, no debug/cheat mode (the game shows the button then)
            var sd = SaveData.Instance;
            bool wasDebug = sd.DebugMode, wasCheat = sd.CheatMode, wasDone = sd.Areas_Safe[0].Modes[0].Completed;
            sd.DebugMode = false; sd.CheatMode = false; sd.Areas_Safe[0].Modes[0].Completed = false;
            bool prologueDone = sd.Areas_Safe[0].Modes[0].Completed;
            var lvl = Lvl;
            lvl.Pause(0, false, false);
            yield return 0.5f;
            TextMenu Menu() => lvl.Entities.OfType<TextMenu>().LastOrDefault();
            var labels = Menu()?.Items.OfType<TextMenu.Button>().Select(b => b.Label).ToList() ?? new List<string>();
            Shot("pause_menu.png");
            var ret = Menu()?.Items.OfType<TextMenu.Button>().FirstOrDefault(b => b.Label == Dialog.Clean("MENU_PAUSE_RETURN"));
            Result(ret != null, $"pause menu has Return to Map (Prologue completed in this save: {prologueDone})", string.Join(" | ", labels));
            if (ret == null) yield break;
            ret.OnPressed();
            yield return 0.6f;
            Shot("pause_return_confirm.png");
            var confirm = Menu();
            var yes = confirm?.Items.OfType<TextMenu.Button>().FirstOrDefault();
            Result(yes != null, "give-up confirmation shown", string.Join(" | ", confirm?.Items.OfType<TextMenu.Button>().Select(b => b.Label) ?? new string[0]));
            yes?.OnPressed();
            for (float t = 0; t < 10f && !(Engine.Scene is Overworld); t += Engine.RawDeltaTime) yield return null;
            Result(Engine.Scene is Overworld, "back on the map (overworld)", $"scene {Engine.Scene?.GetType().Name}");
            yield return 1.5f;
            Shot("pause_back_to_map.png");
            sd.DebugMode = wasDebug; sd.CheatMode = wasCheat; sd.Areas_Safe[0].Modes[0].Completed = wasDone;
        }

        /// <summary>One screenshot per kind of big notification (look of the icons).</summary>
        static IEnumerator ToastGallery() {
            yield return LoadRoom("1a", "1");
            var cases = new (string item, string game, string loc, bool toMe, bool fromMe)[] {
                ("Dash", "UltraRando", "", true, false), ("Climb", "UltraRando", "", true, false),
                ("Strawberry", "UltraRando", "", true, true), ("Raspberry", "UltraRando", "", true, true),
                ("Springs", "UltraRando", "", true, false), ("Celestial Resort A - Front Door Key", "UltraRando", "", true, false),
                ("Bomb Refill 30", "Majora's Mask Recompiled", "Forsaken City A - Crystal Heart", false, true),
                ("Bomb Refill 30", "Majora's Mask Recompiled", "Forsaken City A - Cassette", false, true),
                ("Bomb Refill 30", "Majora's Mask Recompiled", "Forsaken City A - Room 2 Strawberry", false, true),
                ("Hookshot", "Majora's Mask Recompiled", "Clock Town Chest", true, false),
                ("Forsaken City A - Crossing", "UltraRando", "", true, false),
                ("Up Dash", "UltraRando", "", true, false),
                ("Celestial Resort A - Front Door Key", "UltraRando", "Forsaken City A - Room 2 Strawberry", false, true),
            };
            for (int i = 0; i < cases.Length; i++) {
                var c = cases[i];
                ApHud.ClearToastsForTest();
                ApHud.ItemEvent(c.toMe, c.fromMe, c.item, c.game, c.loc, "Ami", Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement);
                yield return 0.3f;
                var t = ApHud.LastToastForTest;
                Result(t?.Icon != null, $"toast {i}: {c.item} ({c.loc}) has an icon", $"icon {t?.Icon?.AtlasPath} ({(t?.Icon?.Atlas == GFX.Gui ? "Gui" : "Game")}) {t?.Icon?.ClipRect.Width}x{t?.Icon?.ClipRect.Height}");
                Shot($"toast_{i:00}.png");
                yield return 0.2f;
            }
        }

        static IEnumerator InteractablesTest() {
            SetOpt("interactable_shuffle", 1); SetOpt("dash_shuffle", 0);
            var only = Opt("items", "");
            var items = ItemNames.Interactables.Where(i => only.Length == 0 || only.Split(',').Contains(i)).ToList();
            int i0 = int.Parse(Opt("from", "0")), n0 = int.Parse(Opt("count", "99"));
            foreach (var item in items.Skip(i0).Take(n0)) {
                // a room of the seed's built map that has this item's entities
                string area = null, room = null;
                foreach (var ak in ApState.Slot.ActiveAreas.OrderBy(a => a)) {
                    if (!ApState.Slot.Chapters.TryGetValue(ak, out var ch)) continue;
                    var statics = LayoutBuilder.RoomKeys(ak);
                    foreach (var r in ch.Rooms) {
                        var vk = string.IsNullOrEmpty(r.Variant) ? r.Room : r.Variant;
                        if (statics.TryGetValue(vk, out var st) && st.Level.Entities.Any(e => ItemNames.ItemForEntity(e) == item)) { area = ak; room = r.Room; break; }
                    }
                    if (room != null) break;
                }
                if (room == null) {
                    Result(true, $"{item}: never removed by the mod (not mapped to an entity: kept on purpose)", "no entity of the game is locked by this item");
                    continue;
                }
                Own();
                yield return LoadRoom(area, room);
                int locked = CountItemEntities(item, out int inData);
                Own(item);
                yield return LoadRoom(area, room);
                int unlocked = CountItemEntities(item, out _);
                Result(inData > 0 && locked == 0 && unlocked >= 1, $"{item} in {area} {room}: absent while locked, present once received",
                    $"{inData} in the room data, loaded locked={locked}, received={unlocked}");
                if (unlocked > 0) yield return InteractableWorks(item);
            }
        }

        /// <summary>Touch test for the objects that react to Madeline on contact.</summary>
        static IEnumerator InteractableWorks(string item) {
            Func<Entity, bool> isIt = e => (e.SourceData != null && ItemNames.ItemForEntity(e.SourceData) == item);
            var target = Lvl.Entities.FirstOrDefault(isIt);
            if (target == null) yield break;
            string what; Func<Player, bool> worked;
            switch (item) {
                case "Dash Refills": what = "dash given back"; worked = p => p.Dashes >= 1; break;
                case "Double Dash Refills": what = "two dashes given"; worked = p => p.Dashes >= 2; break;
                case "Feathers": what = "feather flight (StStarFly)"; worked = p => p.StateMachine.State == Player.StStarFly; break;
                case "Blue Boosters": what = "inside the bubble (StBoost)"; worked = p => p.StateMachine.State == Player.StBoost; break;
                case "Red Boosters": what = "inside the bubble (StBoost/StRedDash)"; worked = p => p.StateMachine.State == Player.StBoost || p.StateMachine.State == Player.StRedDash; break;
                case "Bumpers": what = "bounced (speed > 150)"; worked = p => p.Speed.Length() > 150; break;
                default: yield break;
            }
            var p0 = Pl;
            SaveData.Instance.Assists.Invincible = true;
            p0.Dashes = 0;
            p0.Speed = Vector2.Zero;
            p0.StateMachine.State = Player.StNormal;
            p0.Position = target.Center + new Vector2(0, 5);
            bool ok5 = false;
            for (float t = 0; t < 0.5f && !ok5; t += Engine.RawDeltaTime) { if (Pl != null && worked(Pl)) ok5 = true; yield return null; }
            SaveData.Instance.Assists.Invincible = false;
            Result(ok5, $"{item} works when touched: {what}", $"state {Pl?.StateMachine.State}, dashes {Pl?.Dashes}, speed {Pl?.Speed.Length():0}");
        }

        /// <summary>Summit gems (7A): a real dash into the gem sends its check.</summary>
        static IEnumerator GemsTest() {
            SetOpt("dash_shuffle", 0);
            Own();
            var gemKeys = ApState.Slot.LocationIds.Keys.Where(k => k.StartsWith("7a|") && k.Contains("|gem|")).ToList();
            foreach (var gk in gemKeys.Take(int.Parse(Opt("count", "3")))) {
                var parts = gk.Split('|');
                yield return LoadRoom("7a", parts[1]);
                var gem = Lvl.Entities.OfType<SummitGem>().FirstOrDefault(g => g.GID.ID.ToString() == parts[3]);
                if (gem == null) { Result(false, $"gem {gk}", "no SummitGem with this ID in the room"); continue; }
                SaveData.Instance.Assists.Invincible = true;
                Pl.Position = gem.Center + new Vector2(-30, 6); Pl.Speed = Vector2.Zero; Pl.RefillDash();
                Hold(KRight); yield return null;
                Hold(KRight, KDash); yield return null;
                Hold(KRight);
                yield return 0.6f;
                Release();
                SaveData.Instance.Assists.Invincible = false;
                long id = ApState.Slot.LocationIds[gk];
                Result(ApState.CheckedThisRunForTest(id), $"summit gem {gk}: a real dash into it sends its check", $"check {id} sent={ApState.CheckedThisRunForTest(id)}, gem still there={gem.Scene != null}");
            }
        }

        // ------------------------------------------------------------------ script

        static IEnumerator MechScript() {
            string area = Opt("area", "1a");
            if (!LayoutBuilder.TryParseAreaKey(area, out var key)) { report.AppendLine("mech: bad area"); yield break; }
            var session = new Session(key) { FirstLevel = false, StartedFromBeginning = false };
            string room = Opt("room", "");
            if (room.Length > 0) session.Level = room;
            SaveData.Instance.StartSession(session);
            LevelEnter.ErrorMessage = null;
            Engine.Scene = new LevelLoader(session);
            yield return (Func<bool>)(() => Pl != null && !Lvl.Transitioning && !Lvl.InCutscene && Pl.OnGround());
            if (Lvl?.InCutscene ?? false) { Lvl.SkipCutscene(); yield return 2f; }
            yield return 0.5f;
            if (Pl == null) { report.AppendLine("mech: no player"); yield break; }
            Pl.StateMachine.State = Player.StNormal;
            home = Pl.Position;
            report.AppendLine($"mech: {area} room {Lvl.Session.Level}, home {home}, keys dash={KDash} jump={KJump} grab={KGrab} " +
                              $"L/R/U/D={KLeft}/{KRight}/{KUp}/{KDown}, grab mode {Settings.Instance.GrabMode}, area active {ApHooks.ActiveAreaKey()}");

            var groups = new HashSet<string>(Opt("groups", "basic").Split(','));
            if (groups.Contains("shaftskip")) {
                // 3A Elevator Shaft (04-d): the dust wall between the right part ("south") and the left part
                // ("south-west"). Madeline is invincible and every Player.Die call (a deadly touch) is counted; a try
                // passes if she gets left of the wall (x < 100 in the room) with no deadly touch.
                SetOpt("dash_shuffle", 0); SetOpt("demo_dash_shuffle", 1);
                SaveData.Instance.Assists.Invincible = true;
                On.Celeste.Player.Die += CountDie;
                var lb = Lvl.Bounds;
                int y1 = int.Parse(Opt("ymin", "100")), y2 = int.Parse(Opt("ymax", "152")), st = int.Parse(Opt("ystep", "2"));
                int xa = int.Parse(Opt("xmin", Opt("startx", "176"))), xb = int.Parse(Opt("xmax", Opt("startx", "176"))), xs = int.Parse(Opt("xstep", "2"));
                var modes = Opt("only") == "owned" ? new[] { true } : Opt("only") == "locked" ? new[] { false } : new[] { false, true };
                foreach (bool owned in modes) {
                    if (owned) Own(ItemNames.DemoDash); else Own();
                    var through = new List<string>();
                    int tries = 0;
                    for (int x0 = xa; x0 <= xb; x0 += xs)
                    for (int y = y1; y <= y2; y += st) {
                        yield return ResetPlayer(true);
                        var p = Pl;
                        var start = new Vector2(lb.Left + x0, lb.Top + y);
                        p.Position = start;
                        if (p.CollideCheck<Solid>()) continue;
                        tries++;
                        p.Speed = Vector2.Zero;
                        dieCalls = 0;
                        Hold(KLeft); yield return null;
                        Hold(KLeft, KDemo); yield return null;
                        Hold(KLeft);
                        float minX = float.MaxValue; int diesBefore = -1;
                        for (float t = 0; t < 0.6f; t += Engine.RawDeltaTime) {
                            if (Pl == null) break;
                            float rx = Pl.X - lb.Left;
                            if (rx < minX) minX = rx;
                            if (rx < 100 && diesBefore < 0) diesBefore = dieCalls;
                            yield return null;
                        }
                        Release();
                        if (diesBefore == 0) through.Add($"({x0},{y})");
                        if (Opt("detail") == "1") report.AppendLine($"  {(owned ? "owned" : "locked")} x={x0} y={y}: min x {minX:0}, deadly touches {dieCalls} (before x<100: {diesBefore})");
                    }
                    Result(owned ? through.Count > 0 : through.Count == 0,
                        $"shaftskip 3a 04-d, Demo Dash {(owned ? "owned" : "locked")}: dust wall crossed with no deadly touch",
                        $"{through.Count}/{tries} starts went through ((x,y) in room: [{string.Join(" ", through)}])");
                }
                On.Celeste.Player.Die -= CountDie;
                SaveData.Instance.Assists.Invincible = false;
                Release();
                yield break;
            }
            if (groups.Contains("shaftreal")) {
                // the shaftskip played with real inputs from the floor right of the 04-d step (x 172, y 152):
                // "climb" = Left+Grab+Up up the step face then the demo dash k frames after the climb hop,
                // "jump" = a held jump then the demo dash k frames after the press. Invincible, deadly touches counted.
                SetOpt("dash_shuffle", 0); SetOpt("demo_dash_shuffle", 1);
                // own= list of items for the counter-checks (default: Demo Dash, climb not shuffled)
                var ownOpt = Opt("own", "Demo Dash");
                SetOpt("climb_shuffle", Opt("climbshuffle", "0") == "1" ? 1 : 0);
                Own(ownOpt.Split('+').Where(x => x.Length > 0).ToArray());
                report.AppendLine($"  shaftreal: owned [{string.Join(",", ApState.OwnedItems)}], climb_shuffle {ApState.Slot.Option("climb_shuffle", 0)}");
                SaveData.Instance.Assists.Invincible = true;
                On.Celeste.Player.Die += CountDie;
                var lb = Lvl.Bounds;
                var floor = new Vector2(lb.Left + int.Parse(Opt("fx", "172")), lb.Top + 152);
                foreach (var method in Opt("methods", "climb,jump").Split(',')) {
                    var ok4 = new List<int>();
                    for (int k = int.Parse(Opt("kmin", "0")); k <= int.Parse(Opt("kmax", "24")); k++) {
                        yield return ResetPlayer(true);
                        var p = Pl;
                        p.Position = floor; p.Speed = Vector2.Zero;
                        yield return 0.1f;
                        dieCalls = 0;
                        float startY = Pl.Y - lb.Top;
                        if (method.StartsWith("climbjump")) {
                            // grab the step face, climb for c frames, climb jump (Grab still held), dash k frames later
                            int c = int.Parse(method.Substring(9));
                            Hold(KLeft, KGrab, KUp);
                            for (float tw = 0; tw < 1f && Pl.StateMachine.State != Player.StClimb; tw += Engine.RawDeltaTime) yield return null;
                            for (int f = 0; f < c; f++) yield return null;
                            Hold(KLeft, KGrab, KJump); yield return null;
                            Hold(KLeft, KJump);
                        } else if (method == "climb") {
                            Hold(KLeft, KGrab, KUp);
                            bool climbed = false; float tw = 0;
                            while (tw < 2f) { // wait for the climb hop at the top of the step face
                                if (Pl.StateMachine.State == Player.StClimb) climbed = true;
                                else if (climbed) break;
                                tw += Engine.RawDeltaTime; yield return null;
                            }
                            Hold(KLeft);
                        } else {
                            Hold(KJump); yield return null;
                        }
                        for (int f = 0; f < k; f++) yield return null;
                        float yAtDash = Pl.Y - lb.Top;
                        Hold(KLeft, KDemo, method != "climb" ? KJump : Keys.None); yield return null;
                        Hold(KLeft);
                        float minX = float.MaxValue; int dies = -1;
                        for (float t = 0; t < 0.6f; t += Engine.RawDeltaTime) {
                            if (Pl == null) break;
                            float rx = Pl.X - lb.Left;
                            if (rx < minX) minX = rx;
                            if (rx < 100 && dies < 0) dies = dieCalls;
                            yield return null;
                        }
                        Release();
                        if (dies == 0) ok4.Add(k);
                        if (Opt("detail") == "1") report.AppendLine($"  {method} k={k}: y at dash {yAtDash:0.0}, min x {minX:0}, deadly touches {dieCalls} (before x<100: {dies})");
                    }
                    Result(true, $"shaftskip played by {method} (information)", $"frame delays that went through with no deadly touch: [{string.Join(",", ok4)}]");
                }
                On.Celeste.Player.Die -= CountDie;
                SaveData.Instance.Assists.Invincible = false;
                Release();
                yield break;
            }
            if (groups.Contains("checkpoint")) {
                // checkpoint of 1a room 6 (Crossing) on the current (shuffled) map: reached by a real transition,
                // registered by the game for the chapter select, then the chapter is started from it
                var cpArea = Opt("area", "1a"); var cp = Opt("cp", "6"); var via = Opt("via", "");
                LayoutBuilder.TryParseAreaKey(cpArea, out var ak);
                var modeStats = SaveData.Instance.Areas_Safe[ak.ID].Modes[(int)ak.Mode];
                modeStats.Checkpoints.Remove(cp);
                yield return LoadRoom(cpArea, via);
                yield return GoThrough(cp);
                long cpId = ApState.Slot.LocationIds.TryGetValue($"{cpArea}|{cp}|checkpoint", out var x1) ? x1 : -1;
                long roomId = ApState.Slot.LocationIds.TryGetValue($"{cpArea}|{cp}|room", out var x2) ? x2 : -1;
                Result(ApState.CheckedThisRunForTest(cpId) && ApState.CheckedThisRunForTest(roomId), $"walking into checkpoint room {cp}: its checks sent",
                    $"checkpoint check {cpId} sent={ApState.CheckedThisRunForTest(cpId)}, room check {roomId} sent={ApState.CheckedThisRunForTest(roomId)}");
                Result(modeStats.Checkpoints.Contains(cp), $"checkpoint {cp} unlocked for the chapter select after reaching it", $"save checkpoints [{string.Join(",", modeStats.Checkpoints)}]");
                // start the chapter from the checkpoint, as the chapter select does (new run: checks of this run forgotten)
                ApState.ClearOwnedForTest();
                var cpSession = new Session(ak, cp) { FirstLevel = false };
                SaveData.Instance.StartSession(cpSession);
                Engine.Scene = new LevelLoader(cpSession);
                yield return (Func<bool>)(() => Pl != null && !Lvl.Transitioning && Lvl.Session.Level == cp);
                yield return 0.5f;
                Result(Lvl.Session.Level == cp && LayoutBuilder.IsApLayout(Lvl.Session.MapData), $"chapter started from checkpoint {cp} on the shuffled map", $"room {Lvl.Session.Level}, AP layout {LayoutBuilder.IsApLayout(Lvl.Session.MapData)}");
                Result(ApState.CheckedThisRunForTest(roomId) && ApState.CheckedThisRunForTest(cpId), $"starting from checkpoint {cp}: the checks of that room are sent (once each)",
                    $"checkpoint check sent={ApState.CheckedThisRunForTest(cpId)}, room check sent={ApState.CheckedThisRunForTest(roomId)}");
                yield break;
            }
            if (groups.Contains("scan")) {
                // which rooms have entities of a given EntityData name / bool attribute (vanilla rooms of every active area)
                string ename = Opt("ename", "refill"), attr = Opt("attr", "");
                foreach (var ak in ApState.Slot.ActiveAreas.OrderBy(x => x)) {
                    var hits = LayoutBuilder.RoomKeys(ak).Values.Select(st => (st.Level.Name, n: st.Level.Entities.Count(e => e.Name == ename && (attr.Length == 0 || e.Bool(attr, false)))))
                        .Where(h => h.n > 0).Select(h => $"{h.Name}x{h.n}").Distinct().ToList();
                    if (hits.Count > 0) report.AppendLine($"scan {ename}{(attr.Length > 0 ? "[" + attr + "]" : "")} {ak}: {string.Join(" ", hits)}");
                }
                yield break;
            }
            if (groups.Contains("interact")) { yield return InteractablesTest(); yield break; }
            if (groups.Contains("toasts")) { yield return ToastGallery(); yield break; }
            if (groups.Contains("pause")) { yield return PauseMenuTest(); yield break; }
            if (groups.Contains("yaml")) {
                // in-game YAML generator: schema loaded, a preset applied, file written in the web generator's format
                var st = RandoModule.Instance.SavedData;
                bool ok = OuiYamlGenerator.LoadSchema();
                Result(ok && OuiYamlGenerator.Options.Count > 0, "options schema loaded", $"{OuiYamlGenerator.Options?.Count} options, {OuiYamlGenerator.Presets?.Count} presets");
                if (!ok) yield break;
                var pr = OuiYamlGenerator.Presets.First(p => p.name == "Expert");
                st.YamlValues = OuiYamlGenerator.Options.ToDictionary(o => o.Key, o => o.Default);
                foreach (var kv in pr.values) st.YamlValues[kv.Key] = kv.Value;
                st.YamlPreset = pr.name; st.YamlSlot = "Testeur";
                Result(OuiYamlGenerator.NameError("") != null && OuiYamlGenerator.NameError("a:b") != null && OuiYamlGenerator.NameError("Testeur") == null,
                    "slot name validation", "empty / ':' refused, 'Testeur' accepted");
                var path = OuiYamlGenerator.Write("Testeur");
                Result(System.IO.File.Exists(path), "YAML written", path);
                Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
                yield return 3f;
                (Engine.Scene as Overworld)?.Goto<OuiApConnect>();
                yield return 2f;
                var cm = (Engine.Scene as Overworld)?.Entities.OfType<TextMenu>().LastOrDefault();
                var yb = cm?.Items.OfType<TextMenu.Button>().FirstOrDefault(b => b.Label == Dialog.Clean("CMR_YAML_OPEN"));
                Result(yb != null, "connection page has the 'Create a YAML' button", $"{cm?.Items.Count} items");
                Shot("yaml_connect.png");
                yield return 0.5f;
                yb?.OnPressed();
                yield return 2f;
                var tm = (Engine.Scene as Overworld)?.Entities.OfType<TextMenu>().LastOrDefault();
                if (tm != null) ApState.Log($"yaml menu state: focused={tm.Focused} alpha={tm.Alpha} active={tm.Active} x={tm.X} width={tm.Width} scene={tm.Scene?.GetType().Name} tags={tm.Tag} depth={tm.Depth}");
                if (tm != null) ApState.Log("yaml menu items: " + string.Join(" | ", tm.Items.Take(14).Select(i => $"{i.GetType().Name} vis={i.Visible} h={i.Height():0} y={tm.GetYOffsetOf(i):0}")));
                Result(tm != null && tm.Items.Count > 20, "YAML menu built", $"items {tm?.Items.Count}, selection {tm?.Selection}, pos {tm?.Position}, height {tm?.Height}, visible {tm?.Visible}");
                Shot("yaml_menu.png");
                yield return 1f;
                if (tm != null) { tm.Selection = tm.Items.FindIndex(i => i is TextMenu.Slider); }
                yield return 1f;
                Shot("yaml_menu2.png");
                yield return 0.5f;
                yield break;
            }
            if (groups.Contains("doors")) {
                // keysanity: an owned key item gives a following key in its door's room; the door opens with the game's
                // animation and stays open; the key does not leave the room
                SetOpt("interactable_shuffle", 0); SetOpt("keysanity", 1);
                var lk = ApState.Slot.Locks.FirstOrDefault(kv => kv.Key.StartsWith("3a|") && kv.Value.Count == 1);
                Result(lk.Key != null, "a 3a room with one key door", lk.Key ?? "none");
                if (lk.Key == null) yield break;
                string droom = lk.Key.Split('|')[1];
                Own(lk.Value[0]);
                yield return LoadRoom("3a", droom);
                yield return 0.3f;
                var door = Lvl.Entities.OfType<LockBlock>().FirstOrDefault();
                int keys = Pl.Leader.Followers.Count(f => f.Entity is Key);
                Result(door != null && keys == 1, $"3a {droom}: door there, one key follows ({lk.Value[0]} owned)", $"door={door != null}, keys following {keys}");
                if (door == null) yield break;
                SaveData.Instance.Assists.Invincible = true;
                for (float t = 0; t < 4f && door.Scene != null; t += Engine.RawDeltaTime) { Pl.Position = door.Center + new Vector2(door.Width / 2 + 6, 0); Pl.Speed = Vector2.Zero; yield return null; }
                yield return 1.5f;
                Shot("door_opened.png");
                Result(door.Scene == null || !door.Collidable, $"3a {droom}: the key opened the door (game animation)", $"door in scene={door.Scene != null}, kept {ApHooks.DoorsKeptForTest}");
                yield return LoadRoom("3a", droom);
                yield return 0.3f;
                Result(!Lvl.Entities.OfType<LockBlock>().Any() && !Pl.Leader.Followers.Any(f => f.Entity is Key),
                    $"3a {droom} reloaded: door stays open, no key given again", $"doors {Lvl.Entities.OfType<LockBlock>().Count()}, keys {Pl.Leader.Followers.Count(f => f.Entity is Key)}");
                SaveData.Instance.Assists.Invincible = false;
                var other = ApState.Slot.Locks.FirstOrDefault(kv => kv.Key.StartsWith("3a|") && kv.Key != lk.Key);
                if (other.Key != null) {
                    Own(lk.Value[0]);
                    yield return LoadRoom("3a", droom);
                    yield return LoadRoom("3a", other.Key.Split('|')[1]);
                    yield return 0.3f;
                    Result(Lvl.Entities.OfType<LockBlock>().Any() && !Pl.Leader.Followers.Any(f => f.Entity is Key),
                        $"3a {other.Key.Split('|')[1]} (its key not owned): door closed, no key brought from elsewhere",
                        $"doors {Lvl.Entities.OfType<LockBlock>().Count()}, keys {Pl.Leader.Followers.Count(f => f.Entity is Key)}");
                }
                yield break;
            }
            if (groups.Contains("clear")) {
                // end of chapter on our map: the real end trigger of 1A; the clear check is made, the player stays
                foreach (var (ar, rm) in new[] { ("1a", "end"), ("2a", "end_6"), ("7a", "g-03") }) {
                    if (!ApState.Slot.ActiveAreas.Contains(ar)) continue;
                    yield return LoadRoom(ar, rm);
                    int before = ApHooks.StayedForTest;
                    string ckey = ar + "|clear";
                    bool openBefore = ApState.IsOpenLocation(ckey);
                    var trig = Lvl.Entities.OfType<EventTrigger>().FirstOrDefault();
                    if (trig != null) { Pl.Position = trig.Center; Pl.Speed = Vector2.Zero; }
                    else Lvl.CompleteArea(true, false, false);
                    for (float t = 0; t < 20f && ApHooks.StayedForTest == before; t += Engine.RawDeltaTime) yield return null;
                    yield return 2f;
                    var lv = Engine.Scene as Level;
                    var pl = lv?.Tracker.GetEntity<Player>();
                    Shot($"clear_{ar}.png");
                    Result(ApHooks.StayedForTest > before && lv != null && lv.Session.Level == rm && pl != null && pl.StateMachine.State == Player.StNormal && !lv.InCutscene
                        && openBefore && !ApState.IsOpenLocation(ckey),
                        $"{ar} {rm}: end of chapter ({(trig != null ? "real end trigger" : "CompleteArea")}) -> clear check made, player stays and plays",
                        $"stayed {ApHooks.StayedForTest - before}, scene {Engine.Scene?.GetType().Name}, room {lv?.Session.Level}, player state {pl?.StateMachine.State}, cutscene {lv?.InCutscene}, clear open before={openBefore} after={ApState.IsOpenLocation(ckey)}");
                    yield return 0.5f;
                }
                yield break;
            }
            if (groups.Contains("cutskip")) {
                // a cutscene started on our map is skipped by the mod (no SkipCutscene call here)
                yield return LoadRoom("5a", "b-16");
                var portal = Lvl.Entities.OfType<TempleMirrorPortal>().FirstOrDefault();
                if (portal == null) { Result(false, "mirror portal in 5a b-16", "none"); yield break; }
                int before = ApHud.SkippedForTest;
                Pl.Position = portal.Position + new Vector2(0, 8);
                Lvl.Add(new CS04_MirrorPortal(Pl, portal));
                for (float t = 0; t < 15f && (Engine.Scene as Level)?.Session.Level == "b-16"; t += Engine.RawDeltaTime) yield return null;
                yield return 1f;
                Result(ApHud.SkippedForTest > before && (Engine.Scene as Level)?.Session.Level == "c-00", "mirror cutscene skipped by itself, ends in c-00",
                    $"skipped {ApHud.SkippedForTest - before}, room {(Engine.Scene as Level)?.Session.Level}");
                yield break;
            }
            if (groups.Contains("seeds")) {
                // a strawberry with seeds: touch every seed, the berry forms, touch it: its check is made
                string sa = null, sr = null;
                foreach (var ak in ApState.Slot.ActiveAreas.OrderBy(x => x)) {
                    if (!ApState.Slot.Chapters.TryGetValue(ak, out var ch)) continue;
                    var statics = LayoutBuilder.RoomKeys(ak);
                    foreach (var r in ch.Rooms) {
                        var vk = string.IsNullOrEmpty(r.Variant) ? r.Room : r.Variant;
                        if (statics.TryGetValue(vk, out var st) && st.Level.Entities.Any(e => e.Name == "strawberry" && e.Nodes != null && e.Nodes.Length > 0)) { sa = ak; sr = r.Room; break; }
                    }
                    if (sr != null) break;
                }
                Result(sr != null, "a room with a seed strawberry", $"{sa} {sr}");
                if (sr == null) yield break;
                yield return LoadRoom(sa, sr);
                var berry = Lvl.Entities.OfType<Strawberry>().FirstOrDefault(b => b.ID.Level == sr);
                var seeds = Lvl.Entities.OfType<StrawberrySeed>().ToList();
                string bkey = berry == null ? "" : $"{sa}|{berry.ID.Level}|strawberry|{berry.ID.ID}";
                Result(berry != null && seeds.Count > 0 && ApState.IsOpenLocation(bkey), $"{sa} {sr}: seed strawberry is an open check", $"{seeds.Count} seeds, bkey {bkey}");
                SaveData.Instance.Assists.Invincible = true;
                foreach (var sd in seeds) { Pl.Position = sd.Position + new Vector2(0, 4); Pl.Speed = Vector2.Zero; yield return 0.4f; }
                for (float t = 0; t < 6f && !(berry?.Visible ?? false); t += Engine.RawDeltaTime) yield return null;
                yield return 2f;
                if (berry != null && berry.Scene != null) { Pl.Position = berry.Position + new Vector2(0, 4); Pl.Speed = Vector2.Zero; }
                for (float t = 0; t < 4f && ApState.IsOpenLocation(bkey); t += Engine.RawDeltaTime) yield return null;
                SaveData.Instance.Assists.Invincible = false;
                Result(!ApState.IsOpenLocation(bkey), $"{sa} {sr}: seeds then berry collected -> check made", $"open={ApState.IsOpenLocation(bkey)}");
                yield break;
            }
            if (groups.Contains("badge")) {
                yield return LoadRoom("1a", "2");
                var berries = Lvl.Entities.OfType<Strawberry>().ToList();
                int withBadge = berries.Count(b => b.Get<ApHooks.ApBadge>() != null);
                Result(berries.Count > 0 && withBadge == berries.Count, "1a 2: strawberries with an open check carry the Archipelago badge", $"{withBadge}/{berries.Count} ids {string.Join(",", berries.Select(b => b.ID.Level + "|" + b.ID.ID + " open=" + ApState.IsOpenLocation("1a|" + b.ID.Level + "|strawberry|" + b.ID.ID)))} has key={ApState.Slot.LocationIds.ContainsKey("1a|2|strawberry|11")}");
                if (berries.Count > 0) { Pl.Position = berries[0].Position + new Vector2(-40, 20); }
                yield return 0.5f;
                Shot("badge_1a_2.png");
                yield return 0.5f;
                yield break;
            }
            if (groups.Contains("dream")) {
                // 2A: dream blocks usable on a built map even if the mirror cutscene was not seen (Dream Blocks owned)
                SetOpt("interactable_shuffle", 1); SetOpt("dash_shuffle", 0);
                Own("Dream Blocks");
                string dRoom = null;
                var statics = LayoutBuilder.RoomKeys("2a");
                foreach (var r in ApState.Slot.Chapters["2a"].Rooms) {
                    var vk = string.IsNullOrEmpty(r.Variant) ? r.Room : r.Variant;
                    if (statics.TryGetValue(vk, out var st) && st.Level.Entities.Any(e => e.Name == "dreamBlock")) { dRoom = r.Room; break; }
                }
                Result(dRoom != null, "a 2a room with dream blocks", dRoom ?? "none");
                if (dRoom == null) yield break;
                yield return LoadRoom("2a", dRoom);
                var db = Lvl.Entities.OfType<DreamBlock>().FirstOrDefault();
                bool active = db != null && MonoMod.Utils.DynamicData.For(db).Get<bool>("playerHasDreamDash");
                Result(active && Lvl.Session.Inventory.DreamDash, $"2a {dRoom}: dream blocks active (dream dash given)", $"inventory dreamdash={Lvl.Session.Inventory.DreamDash}, block active={active}");
                if (db == null) yield break;
                // a real dash into the block from its left side
                Pl.Position = new Vector2(db.Left - 10, db.Center.Y + 6); Pl.Speed = Vector2.Zero; Pl.RefillDash();
                SaveData.Instance.Assists.Invincible = true;
                bool seen = false;
                Hold(KRight); yield return null; Hold(KRight, KDash); yield return null; Hold(KRight);
                for (float t = 0; t < 0.6f; t += Engine.RawDeltaTime) { if (Pl != null && Pl.StateMachine.State == Player.StDreamDash) seen = true; yield return null; }
                Release();
                SaveData.Instance.Assists.Invincible = false;
                Result(seen, $"2a {dRoom}: real dash into a dream block -> dream dash", $"state seen={seen}");
                yield break;
            }
            if (groups.Contains("portal")) {
                // 5A mirror portal on a built map (no room "void"): the cutscene must end in c-00, no crash
                yield return LoadRoom("5a", "b-16");
                var portal = Lvl.Entities.OfType<TempleMirrorPortal>().FirstOrDefault();
                Result(portal != null, "5a b-16 has the mirror portal", $"room {Lvl.Session.Level}, void in map={Lvl.Session.MapData.Get("void") != null}");
                if (portal == null) yield break;
                Pl.Position = portal.Position + new Vector2(0, 8);
                Lvl.Add(new CS04_MirrorPortal(Pl, portal));
                yield return 2f;
                Lvl.SkipCutscene();
                for (float t = 0; t < 15f && Lvl.Session.Level == "b-16"; t += Engine.RawDeltaTime) yield return null;
                yield return 2f;
                Shot("portal_after.png");
                Result(Engine.Scene is Level && Lvl.Session.Level == "c-00" && Pl != null, "mirror portal ends in c-00", $"scene {Engine.Scene?.GetType().Name}, room {(Engine.Scene as Level)?.Session.Level}, player {Pl != null}");
                yield break;
            }
            if (groups.Contains("hearts")) {
                foreach (var (ar, rm) in new[] { ("9a", "02"), ("9b", "01"), ("9c", "intro"), ("10a", "f-door") }) {
                    if (!ApState.Slot.ActiveAreas.Contains(ar)) { Result(true, $"{ar} not in this seed", "skipped"); continue; }
                    yield return LoadRoom(ar, rm);
                    int inData = Lvl.Session.LevelData.Entities.Count(e => e.Name == "heartGemDoor");
                    int loaded = Lvl.Entities.OfType<HeartGemDoor>().Count();
                    Result(Lvl.Session.Level == rm && inData > 0 && loaded == 0, $"crystal heart door open from the start in {ar} {rm}", $"in room data {inData}, loaded {loaded}");
                    Shot($"heartdoor_{ar}.png");
                }
                yield break;
            }
            if (groups.Contains("attached")) { yield return AttachedTest(); yield break; }
            if (groups.Contains("gems")) { yield return GemsTest(); yield break; }
            if (groups.Contains("detour")) { yield return DetourReturn(); yield break; }
            if (groups.Contains("probe")) {
                // self-debug: ASCII map of the room around a point (room coordinates), 2 px per character:
                // '#' solid, '*' dust/spinner collider, '.' free
                var lb = Lvl.Bounds;
                int px0 = int.Parse(Opt("px0", "96")), px1 = int.Parse(Opt("px1", "200")), py0 = int.Parse(Opt("py0", "96")), py1 = int.Parse(Opt("py1", "176"));
                var hazards = Lvl.Entities.Where(e => e is CrystalStaticSpinner || e is DustStaticSpinner || e.GetType().Name.Contains("Spinner")).ToList();
                report.AppendLine($"probe {Lvl.Session.Level} x {px0}..{px1}, y {py0}..{py1} (2 px per char), {hazards.Count} hazards");
                for (int y = py0; y <= py1; y += 2) {
                    var row = new System.Text.StringBuilder();
                    for (int x = px0; x <= px1; x += 2) {
                        var pt = new Vector2(lb.Left + x, lb.Top + y);
                        char c = Lvl.CollideCheck<Solid>(pt) ? '#' : hazards.Any(h => h.Collider != null && h.Collider.Collide(pt)) ? '*' : '.';
                        row.Append(c);
                    }
                    report.AppendLine($"  y{y,3} {row}");
                }
                yield break;
            }
            if (groups.Contains("explore")) {
                // self-debug: what is in the room (positions relative to the room), before/after its cutscene
                for (int pass = 0; pass < 2; pass++) {
                    var lb = Lvl.Bounds;
                    var lines = Lvl.Entities.Where(e => !(e is SolidTiles) && !(e is BackgroundTiles) && !(e is Decal))
                        .GroupBy(e => e.GetType().Name)
                        .Select(g => g.Key + "x" + g.Count() + "[" + string.Join(" ", g.Take(40).Select(e => $"({e.X - lb.Left:0},{e.Y - lb.Top:0}{(e.Collider != null ? $" {e.Width:0}x{e.Height:0}" : "")})")) + "]");
                    report.AppendLine($"explore pass {pass}: room {Lvl.Session.Level} bounds {lb}, player {Pl?.Position - new Vector2(lb.Left, lb.Top)}, inCutscene {Lvl.InCutscene}");
                    foreach (var l in lines) report.AppendLine("  " + l);
                    Shot($"explore_{Lvl.Session.Level}_{pass}.png");
                    yield return 0.5f;
                    if (pass == 0) {
                        var to = Opt("walkto", "");
                        if (to.Length > 0) { var xy = to.Split(','); Pl.Position = new Vector2(lb.Left + float.Parse(xy[0]), lb.Top + float.Parse(xy[1])); }
                        yield return 3f;
                        if (Lvl.InCutscene) { Lvl.SkipCutscene(); yield return 2f; }
                    }
                }
                yield break;
            }
            if (groups.Contains("combos")) {
                // every combination of owned directions (dash_shuffle 4): exactly the owned directions dash
                SetOpt("dash_shuffle", 4);
                var range = Opt("combos", "0-255").Split('-');
                int from = int.Parse(range[0]), to = int.Parse(range[1]);
                for (int mask = from; mask <= to; mask++) {
                    var dirs = Enumerable.Range(0, 8).Where(i => (mask & (1 << i)) != 0).ToList();
                    Own(dirs.Select(i => DirItem[Dn(Dirs[i].Item1, Dirs[i].Item2)]).ToArray());
                    int failsBefore = mechFail;
                    for (int i = 0; i < 8; i++) yield return Dash(Dirs[i].Item1, Dirs[i].Item2, dirs.Contains(i), $"mask {mask}", true);
                    report.AppendLine($"mech combo {mask:000} owned [{string.Join(",", dirs.Select(i => Dn(Dirs[i].Item1, Dirs[i].Item2)))}]: {(mechFail == failsBefore ? "8/8 PASS" : "FAIL")}");
                }
            }
            if (!groups.Contains("basic")) { Release(); yield break; }

            // dash, directional (dash_shuffle 4): nothing owned -> all refused; one direction owned -> only it
            SetOpt("dash_shuffle", 4); SetOpt("climb_shuffle", 0); SetOpt("crouch_shuffle", 0); SetOpt("wall_jump_shuffle", 0); SetOpt("demo_dash_shuffle", 0);
            Own();
            foreach (var (dx, dy) in Dirs) yield return Dash(dx, dy, false, "mode 4, nothing owned");
            foreach (var (dx, dy) in Dirs) {
                Own(DirItem[Dn(dx, dy)]);
                yield return Dash(dx, dy, true, "mode 4, own " + DirItem[Dn(dx, dy)]);
                yield return Dash(-dx, -dy, false, "mode 4, own " + DirItem[Dn(dx, dy)]);
            }
            // unified (mode 1)
            SetOpt("dash_shuffle", 1);
            Own();
            yield return Dash(1, 0, false, "mode 1, no Dash");
            Own(ItemNames.Dash);
            yield return Dash(1, 0, true, "mode 1, own Dash");
            yield return Dash(-1, -1, true, "mode 1, own Dash");
            // cardinal loose (2): a diagonal needs one of its cardinals
            SetOpt("dash_shuffle", 2);
            Own(ItemNames.UpDash);
            yield return Dash(0, -1, true, "mode 2, own Up");
            yield return Dash(1, -1, true, "mode 2, own Up");
            yield return Dash(1, 0, false, "mode 2, own Up");
            // cardinal restrictive (3): a diagonal needs both
            SetOpt("dash_shuffle", 3);
            Own(ItemNames.UpDash);
            yield return Dash(1, -1, false, "mode 3, own Up");
            Own(ItemNames.UpDash, ItemNames.RightDash);
            yield return Dash(1, -1, true, "mode 3, own Up+Right");
            // not shuffled
            SetOpt("dash_shuffle", 0);
            Own();
            yield return Dash(-1, 0, true, "mode 0, nothing owned");

            // direction changed during the freeze frames (Celeste fixes the direction a few frames after the press)
            SetOpt("dash_shuffle", 4);
            Own(ItemNames.RightDash);
            yield return DashVariant("switch", 0, -1, 1, 0, (d, c) => d.X > 0.5f && Math.Abs(d.Y) < 0.1f,
                "dash pressed aiming R (owned) then aimed U (locked): stays R");
            Own(ItemNames.RightDash, ItemNames.UpDash);
            yield return DashVariant("switch", 0, -1, 1, 0, (d, c) => d.Y < -0.5f && Math.Abs(d.X) < 0.1f,
                "dash pressed aiming R then aimed U, both owned: goes U (vanilla behaviour kept)");
            // water: SwimUpdate starts dashes without StartDash
            Own(ItemNames.RightDash);
            yield return SwimDash(0, -1, false, "Up locked");
            yield return SwimDash(1, 0, true, "Right owned");

            // Demo Dash (crouched dash)
            SetOpt("dash_shuffle", 0);
            SetOpt("demo_dash_shuffle", 1);
            Own();
            yield return DashVariant("demokey", 1, 0, 0, 0, (d, c) => !c && d.X > 0.5f, "demo key R, Demo Dash locked: normal dash, standing");
            yield return DashVariant("downswitch", 1, 0, 0, 0, (d, c) => !c && d.X > 0.5f, "down+dash then R, Demo Dash locked: standing");
            yield return DashVariant("demokey", 0, -1, 0, 0, (d, c) => !c && d.Y < -0.5f, "demo key U, Demo Dash locked: standing");
            yield return DashVariant("ground", 1, 1, 0, 0, (d, c) => c && d.X > 0.5f && Math.Abs(d.Y) < 0.1f,
                "ground down-right dash, Demo Dash locked: vanilla crouched dash slide kept", true);
            Own(ItemNames.DemoDash);
            yield return DashVariant("demokey", 1, 0, 0, 0, (d, c) => c && d.X > 0.5f, "demo key R, own Demo Dash: crouched");
            yield return DashVariant("downswitch", -1, 0, 0, 0, (d, c) => c && d.X < -0.5f, "down+dash then L, own Demo Dash: crouched");
            yield return DashVariant("demokey", 0, -1, 0, 0, (d, c) => c && d.Y < -0.5f, "demo key U, own Demo Dash: crouched");
            SetOpt("demo_dash_shuffle", 0);
            Own();
            yield return DashVariant("demokey", 1, 0, 0, 0, (d, c) => c && d.X > 0.5f, "demo key R, Demo Dash not shuffled: crouched");

            // climb
            SetOpt("climb_shuffle", 2);
            Own();
            yield return Climb(1, false, "split, nothing owned");
            yield return Climb(-1, false, "split, nothing owned");
            Own(ItemNames.LeftClimb);
            yield return Climb(-1, true, "split, own Left Climb");
            yield return Climb(1, false, "split, own Left Climb");
            Own(ItemNames.RightClimb);
            yield return Climb(1, true, "split, own Right Climb");
            SetOpt("climb_shuffle", 1);
            Own();
            yield return Climb(1, false, "single, no Climb");
            Own(ItemNames.Climb);
            yield return Climb(1, true, "single, own Climb");
            yield return Climb(-1, true, "single, own Climb");
            SetOpt("climb_shuffle", 0);
            Own();
            yield return Climb(-1, true, "not shuffled");

            // crouch
            SetOpt("crouch_shuffle", 1);
            Own();
            yield return Crouch(false, "no Crouch");
            Own(ItemNames.Crouch);
            yield return Crouch(true, "own Crouch");
            SetOpt("crouch_shuffle", 0);

            // wall jump
            SetOpt("wall_jump_shuffle", 1);
            Own();
            yield return WallJump(1, false, "no Wall Jump");
            yield return WallJump(-1, false, "no Wall Jump");
            Own(ItemNames.WallJump);
            yield return WallJump(1, true, "own Wall Jump");
            yield return WallJump(-1, true, "own Wall Jump");
            SetOpt("wall_jump_shuffle", 0);
            Own();
            yield return WallJump(1, true, "not shuffled");
            Release();
        }
    }
}
