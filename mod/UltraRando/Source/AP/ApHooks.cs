// CMR (phase B): game hooks for the Archipelago session: layout installation, movement locks, interactable
// removal, location checks, DeathLink. Own code; ExtendedVariantMode (MIT) was only used as a reference for
// which Player methods to hook.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.UltraRando.AP {
    public static class ApHooks {
        public static void Load() {
            On.Celeste.LevelLoader.ctor += OnLevelLoaderCtor;
            On.Celeste.LevelEnter.Go += OnLevelEnterGo;
            On.Celeste.LockBlock.TryOpen += OnLockBlockTryOpen;
            On.Celeste.OuiChapterPanel.Render += OnChapterPanelRender;
            On.Celeste.OuiChapterPanel.UpdateStats += OnChapterPanelStats;
            IL.Celeste.StrawberriesCounter.Render += PatchCounterIcon;
            On.Celeste.Level.CompleteArea_bool_bool_bool += OnCompleteArea;
            var swap = typeof(OuiChapterPanel).GetNestedTypes(System.Reflection.BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.StartsWith("<SwapRoutine>"))?.GetMethod("MoveNext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (swap != null) checkpointHook = new MonoMod.RuntimeDetour.ILHook(swap, PatchFirstCheckpoint);
            portalHook = new MonoMod.RuntimeDetour.ILHook(
                typeof(CS04_MirrorPortal).GetNestedType("<>c__DisplayClass9_0", System.Reflection.BindingFlags.NonPublic)
                    .GetMethod("<OnEnd>b__0", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                PatchMirrorPortal);
            On.Celeste.Level.LoadLevel += OnLoadLevel;
            On.Celeste.Player.StartDash += OnStartDash;
            On.Celeste.SaveData.SetCheckpoint += OnSetCheckpoint;
            On.Celeste.Session.SetFlag += OnSessionSetFlag;
            On.Celeste.Player.CorrectDashPrecision += OnCorrectDashPrecision;
            On.Celeste.Player.ClimbCheck += OnClimbCheck;
            On.Celeste.Player.WallJumpCheck += OnWallJumpCheck;
            On.Celeste.Player.Update += OnPlayerUpdate;
            On.Celeste.Player.Die += OnPlayerDie;
            On.Celeste.Strawberry.OnCollect += OnStrawberryCollect;
            On.Celeste.Strawberry.Added += OnStrawberryAdded;
            On.Celeste.Key.OnPlayer += OnKeyPlayer;
            On.Celeste.SummitGem.SmashRoutine += OnGemSmash;
            On.Celeste.HeartGem.Collect += OnHeartCollect;
            On.Celeste.Cassette.OnPlayer += OnCassettePlayer;
            Everest.Events.Level.OnComplete += OnComplete;
            ApMenuHooks.Load();
            HelpOverlay.Load();
        }

        public static void Unload() {
            On.Celeste.LevelLoader.ctor -= OnLevelLoaderCtor;
            On.Celeste.LevelEnter.Go -= OnLevelEnterGo;
            On.Celeste.LockBlock.TryOpen -= OnLockBlockTryOpen;
            On.Celeste.OuiChapterPanel.Render -= OnChapterPanelRender;
            On.Celeste.OuiChapterPanel.UpdateStats -= OnChapterPanelStats;
            IL.Celeste.StrawberriesCounter.Render -= PatchCounterIcon;
            On.Celeste.Level.CompleteArea_bool_bool_bool -= OnCompleteArea;
            portalHook?.Dispose(); portalHook = null;
            checkpointHook?.Dispose(); checkpointHook = null;
            On.Celeste.Level.LoadLevel -= OnLoadLevel;
            On.Celeste.Player.StartDash -= OnStartDash;
            On.Celeste.SaveData.SetCheckpoint -= OnSetCheckpoint;
            On.Celeste.Session.SetFlag -= OnSessionSetFlag;
            On.Celeste.Player.CorrectDashPrecision -= OnCorrectDashPrecision;
            On.Celeste.Player.ClimbCheck -= OnClimbCheck;
            On.Celeste.Player.WallJumpCheck -= OnWallJumpCheck;
            On.Celeste.Player.Update -= OnPlayerUpdate;
            On.Celeste.Player.Die -= OnPlayerDie;
            On.Celeste.Strawberry.OnCollect -= OnStrawberryCollect;
            On.Celeste.Strawberry.Added -= OnStrawberryAdded;
            On.Celeste.Key.OnPlayer -= OnKeyPlayer;
            On.Celeste.SummitGem.SmashRoutine -= OnGemSmash;
            On.Celeste.HeartGem.Collect -= OnHeartCollect;
            On.Celeste.Cassette.OnPlayer -= OnCassettePlayer;
            Everest.Events.Level.OnComplete -= OnComplete;
            ApMenuHooks.Unload();
            HelpOverlay.Unload();
        }

        // ------------------------------------------------------------------ "is the current level an AP area"

        static AreaKey cachedArea = AreaKey.None;
        static SlotData cachedSlot;
        static string cachedKey;
        static bool cachedActive;

        /// <summary>Area key ("1a") if the scene is a level of an active area of the current session, else null.</summary>
        public static string ActiveAreaKey() {
            if (!ApState.Active || !(Engine.Scene is Level level) || level.Session == null) return null;
            var area = level.Session.Area;
            if (area != cachedArea || cachedSlot != ApState.Slot) {
                cachedArea = area;
                cachedSlot = ApState.Slot;
                cachedActive = ApState.AreaActive(area, out cachedKey) && ApState.SaveMatches(false);
            }
            return cachedActive ? cachedKey : null;
        }

        static bool LocksHere => ActiveAreaKey() != null;

        // ------------------------------------------------------------------ layout

        static void OnLevelLoaderCtor(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
            try {
                cachedArea = AreaKey.None;
                LayoutInstaller.Prepare(session);
                ApState.RestoreState(session); // clutter, breaker, summit gems kept for good
            } catch (Exception e) {
                ApState.Log("layout prepare failed: " + e);
            }
            orig(self, session, startPosition);
        }

        static void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
            var data = self.Session?.LevelData;
            List<EntityData> saved = null;
            List<EntityID> keysToGive = null;
            string areaKey = null;
            try {
                areaKey = ApState.Active ? ActiveAreaKeyFor(self) : null;
                bool interact = ApState.Slot.Option("interactable_shuffle", 0) != 0 && !ApState.OwnsEverything;
                // keysanity: the doors of the key items owned are open for good (not loaded); with several keys for one
                // room (Farewell power source) one door per key owned
                List<string> roomKeys = null;
                if (areaKey != null && data != null) ApState.Slot.Locks.TryGetValue(areaKey + "|" + data.Name, out roomKeys);
                int openDoors = roomKeys == null || ApState.Slot.Option("keysanity", 0) == 0 ? 0
                              : roomKeys.Count == 1 ? (ApState.Has(roomKeys[0]) ? int.MaxValue : 0) : roomKeys.Count(ApState.Has);
                // dream dash: the game only gives it after the first mirror cutscene of 2A; on a shuffled map a dream
                // block room can come first (stuck behind solid dream blocks). The "Dream Blocks" item decides instead.
                if (areaKey != null && self.Session != null && !self.Session.Inventory.DreamDash) {
                    var inv = self.Session.Inventory; inv.DreamDash = true; self.Session.Inventory = inv;
                }
                // crystal heart doors (Core A/B/C, Farewell) are open from the start: the logic never counts hearts
                bool heartDoor = data != null && data.Entities.Exists(e => e.Name == "heartGemDoor");
                // the Prologue's climbing tutorial bird stops the player to show the controls: not on our maps
                bool tutorialBird = data != null && data.Entities.Exists(IsClimbTutorialBird);
                if (areaKey != null && data != null && (interact || openDoors > 0 || heartDoor || tutorialBird)) {
                    var filtered = new List<EntityData>(data.Entities.Count);
                    int removed = 0;
                    // keysanity doors: a door already opened stays open (not loaded); for the others that a key item opens,
                    // a key follows the player (spawned below) and the door opens with the game's own animation
                    var doors = new HashSet<EntityData>(data.Entities.Where(e => e.Name == "lockBlock" && ApState.IsKept(self.Session.Area, DoorState(data.Name, e.ID))));
                    keysToGive = data.Entities.Where(e => e.Name == "lockBlock").OrderBy(e => e.ID).Take(openDoors)
                        .Where(e => !doors.Contains(e)).Select(e => new EntityID(data.Name, KeyIdBase + e.ID)).ToList();
                    foreach (var e in data.Entities) {
                        if (doors.Contains(e) || e.Name == "heartGemDoor" || IsClimbTutorialBird(e)) { removed++; continue; }
                        var item = interact ? ItemNames.ItemForEntity(e) : null;
                        if (item != null && !ApState.Has(item)) { removed++; continue; }
                        filtered.Add(e);
                    }
                    // spikes and spinners attached to a block that is not loaded (StaticMover in the game) would float
                    // in the air: they go with it
                    if (removed > 0) {
                        var gone = data.Entities.Where(e => !filtered.Contains(e) && e.Width > 0 && e.Height > 0)
                                                .Select(e => new Rectangle((int)e.Position.X, (int)e.Position.Y, e.Width, e.Height)).ToList();
                        int before = filtered.Count;
                        // kept if a block still loaded holds it too (blocks of two cassette colours can overlap)
                        var kept = filtered.Where(e => e.Width > 0 && e.Height > 0 && ItemNames.ItemForEntity(e) != null)
                                           .Select(e => new Rectangle((int)e.Position.X, (int)e.Position.Y, e.Width, e.Height)).ToList();
                        if (gone.Count > 0) filtered.RemoveAll(e => AttachedTo(e, gone) && !AttachedTo(e, kept));
                        removed += before - filtered.Count;
                    }
                    if (removed > 0) {
                        ApState.Log($"{areaKey}|{data.Name}: {removed} locked interactable(s) not loaded");
                        saved = data.Entities;
                        data.Entities = filtered;
                    }
                }
            } catch (Exception e) {
                ApState.Log("interactable filter failed: " + e);
            }
            try {
                orig(self, playerIntro, isFromLoader);
            } finally {
                if (saved != null) data.Entities = saved;
            }
            if (areaKey != null) {
                var give = keysToGive ?? new List<EntityID>();
                self.OnEndOfFrame += () => {
                    try {
                        var pl = self.Tracker.GetEntity<Player>();
                        if (pl == null) return;
                        // a given key belongs to its room (a key of the game opens any door): it does not leave with the player
                        foreach (var f in pl.Leader.Followers.ToList())
                            if (f.Entity is Key k && k.ID.ID >= KeyIdBase && !give.Contains(k.ID) && !k.StartedUsing) { pl.Leader.LoseFollower(f); k.RemoveSelf(); }
                        foreach (var id in give) {
                            bool has = pl.Leader.Followers.Any(f => f.Entity is Key k && k.ID.Equals(id));
                            if (!has) { self.Add(new Key(pl, id)); KeysGivenForTest++; }
                        }
                    } catch (Exception e) { ApState.Log("key follower failed: " + e.Message); }
                };
            }
            try {
                ApHud.Ensure(self);
                if (isFromLoader && ApState.BoundButInactive) {
                    var sv = ApState.Save;
                    ApHud.Message("ATTENTION : sauvegarde liée à " + Text.Sanitize(sv.SlotName, 24) + " mais seed indisponible");
                    ApHud.Message("(pas de serveur ni de cache) : cartes VANILLA. Connecte-toi (Options des mods).");
                }
                if (areaKey != null && data != null) {
                    var level = self.Session.Level;
                    ApState.Check($"{areaKey}|{level}|room");
                    if (IsCheckpointRoom(self.Session.Area, level)) ApState.Check($"{areaKey}|{level}|checkpoint");
                }
            } catch (Exception e) {
                ApState.Log("room check failed: " + e);
            }
        }

        const int KeyIdBase = 7000000; // ids of the follower keys given for key items (never an id of the map)
        public static int KeysGivenForTest, DoorsKeptForTest;
        static string DoorState(string room, int id) => $"door:{room}:{id}";

        /// <summary>A door opened (by any key): it stays open for good on our maps.</summary>
        static void OnLockBlockTryOpen(On.Celeste.LockBlock.orig_TryOpen orig, LockBlock self, Player player, Follower fol) {
            orig(self, player, fol);
            try {
                if (ActiveAreaKey() != null && self.Scene is Level lv) { ApState.KeepState(DoorState(lv.Session.Level, self.ID.ID)); DoorsKeptForTest++; }
            } catch (Exception e) { ApState.Log("door keep failed: " + e.Message); }
        }

        static bool IsClimbTutorialBird(EntityData e) => e.Name == "bird" && e.Attr("mode", "") == "ClimbingTutorial";

        /// <summary>Does this spike/spinner entity sit on one of these solids (the side its base touches, as the
        /// game's StaticMover would attach it)?</summary>
        internal static bool AttachedTo(EntityData e, List<Rectangle> solids) {
            int x = (int)e.Position.X, y = (int)e.Position.Y;
            Rectangle probe;
            string n = e.Name ?? "";
            if (n.EndsWith("Up")) probe = new Rectangle(x, y, Math.Max(1, e.Width), 1);          // spikes pointing up: block below
            else if (n.EndsWith("Down")) probe = new Rectangle(x, y - 1, Math.Max(1, e.Width), 1); // pointing down: block above
            else if (n.EndsWith("Left")) probe = new Rectangle(x, y, 1, Math.Max(1, e.Height));    // pointing left: block on the right
            else if (n.EndsWith("Right")) probe = new Rectangle(x - 1, y, 1, Math.Max(1, e.Height)); // pointing right: block on the left
            else if (n == "spinner" && e.Bool("attachToSolid")) probe = new Rectangle(x - 4, y - 4, 8, 8);
            else return false;
            if (!(n.StartsWith("spikes") || n.StartsWith("triggerSpikes") || n == "spinner")) return false;
            foreach (var r in solids) if (r.Intersects(probe)) return true;
            return false;
        }

        static MonoMod.RuntimeDetour.ILHook portalHook, checkpointHook;

        /// <summary>Chapter select: the game pre-selects the LAST checkpoint while the chapter is not completed (the first
        /// once it is). On randomizer saves the first one is pre-selected, as for a completed chapter.</summary>
        static void PatchFirstCheckpoint(ILContext il) {
            var c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdfld<AreaModeStats>("Completed"))) {
                c.EmitDelegate<Func<bool, bool>>(completed => completed || RandoSaves.CurrentIsRando);
            } else ApState.Log("first checkpoint patch: AreaModeStats.Completed not found");
        }

        /// <summary>5A mirror portal: the game sends Madeline to the room "void", which our built maps do not have
        /// (crash in Level.Bounds). Then she goes to "c-00", where "void" leads, as the game already does in B-side.</summary>
        static void PatchMirrorPortal(MonoMod.Cil.ILContext il) {
            var c = new MonoMod.Cil.ILCursor(il);
            if (c.TryGotoNext(MonoMod.Cil.MoveType.After, i => i.MatchLdstr("void")))
                c.EmitDelegate<Func<string, string>>(PortalTarget);
            else ApState.Log("mirror portal patch: 'void' not found");
        }

        public static string PortalTarget(string room) =>
            Engine.Scene is Level l && l.Session.MapData?.Get(room) == null && l.Session.MapData?.Get("c-00") != null ? "c-00" : room;

        public static int StayedForTest;

        /// <summary>End of a chapter on our maps: the clear is recorded and its check sent (RegisterAreaComplete, which
        /// raises Everest's OnComplete), but the player stays: the room is reloaded instead of the end screen.</summary>
        static ScreenWipe OnCompleteArea(On.Celeste.Level.orig_CompleteArea_bool_bool_bool orig, Level self, bool spotlightWipe, bool skipScreenWipe, bool skipCompleteScreen) {
            if (!(RandoModule.Instance?.SavedData?.StayAfterClear ?? true) || ActiveAreaKey() == null)
                return orig(self, spotlightWipe, skipScreenWipe, skipCompleteScreen);
            self.RegisterAreaComplete();
            StayedForTest++;
            ApState.Log("chapter clear in " + self.Session.Level + ": staying in the level");
            ApHud.Message("Chapitre terminé : tu peux continuer à explorer");
            self.OnEndOfFrame += () => {
                Engine.TimeRate = 1f;
                if (self.InCutscene) self.EndCutscene();
                self.Completed = false;
                self.Reload();
            };
            return null;
        }

        public const string EpilogueSid = "Celeste/8-Epilogue";
        public static int EpilogueBlockedForTest;

        /// <summary>The Epilogue is blocked on randomizer saves: the game shows an error screen and goes back to the map.</summary>
        public static string PanelGoalTextForTest;

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StrawberriesCounter, object> apCounters = new();
        public static string PanelCounterForTest;

        /// <summary>Chapter select on a randomizer save: the panel's strawberry counter shows this chapter's checks of the
        /// seed (Archipelago icon, done / total), as the berries of the game are not what the seed counts.</summary>
        static void OnChapterPanelStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);
            try {
                var counter = MonoMod.Utils.DynamicData.For(self).Get<StrawberriesCounter>("strawberries");
                if (counter == null) return;
                if (!ApState.Active || !RandoSaves.CurrentIsRando || !ApState.AreaActive(self.Area, out var key)) { apCounters.Remove(counter); return; }
                var done = ApJournal.Checked();
                var locs = ApJournal.AreaLocations(key).ToList();
                counter.Amount = locs.Count(kv => done.Contains(kv.Value));
                counter.OutOf = locs.Count;
                counter.ShowOutOf = true;
                apCounters.AddOrUpdate(counter, null);
                PanelCounterForTest = $"{key} {counter.Amount}/{counter.OutOf}";
            } catch (Exception e) { ApState.Log("chapter panel counter failed: " + e.Message); }
        }

        static void PatchCounterIcon(ILContext il) {
            var c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchCallvirt<Atlas>("get_Item"))) {
                c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                c.EmitDelegate<Func<MTexture, StrawberriesCounter, MTexture>>((tex, self) =>
                    apCounters.TryGetValue(self, out _) && GFX.Gui.Has("cmr/apcounter") ? GFX.Gui["cmr/apcounter"] : tex);
            } else ApState.Log("counter icon patch: texture lookup not found");
        }


        /// <summary>Chapter select: the goal chapter's panel tells its strawberry requirement in its lower (grey) part.</summary>
        static void OnChapterPanelRender(On.Celeste.OuiChapterPanel.orig_Render orig, OuiChapterPanel self) {
            orig(self);
            PanelGoalTextForTest = null;
            try {
                if (!self.Visible || !ApState.Active || !RandoSaves.CurrentIsRando || ApState.Slot.Option("goal_area_locked", 0) == 0) return;
                if (!ApState.AreaActive(self.Area, out var key) || !ApState.Slot.StringOptions.TryGetValue("goal_area", out var goal) || key != goal) return;
                int need = ApState.Slot.Option("strawberries_required", 0), have = ApState.StrawberriesReceived;
                string text = $"Fraises : {have} / {need}";
                PanelGoalTextForTest = text;
                // under the panel card, centred on its content (the card's height changes when it opens)
                var d = MonoMod.Utils.DynamicData.For(self);
                var at = self.Position + d.Get<Vector2>("contentOffset") + new Vector2(0f, d.Get<float>("height") + 190f); // below the mode tabs
                ActiveFont.DrawOutline(text, at, new Vector2(0.5f, 0f), Vector2.One * 0.8f, have >= need ? Calc.HexToColor("9be36b") : Color.White, 2f, Color.Black);
            } catch (Exception e) { ApState.Log("chapter panel goal text failed: " + e.Message); }
        }

        public static int GoalBlockedForTest;

        /// <summary>Goal chapter still locked: slot_data goal_area_locked and fewer strawberries received than
        /// strawberries_required (offline test mode: never locked). Returns the message, or null when open.</summary>
        public static string GoalLockMessage(AreaKey area) {
            if (!ApState.Active || ApState.Slot.Option("goal_area_locked", 0) == 0 || ApState.Mode == ApMode.Offline) return null;
            if (!ApState.AreaActive(area, out var key) || !ApState.Slot.StringOptions.TryGetValue("goal_area", out var goal) || key != goal) return null;
            int need = ApState.Slot.Option("strawberries_required", 0), have = ApState.StrawberriesReceived;
            return have >= need ? null : $"Ce chapitre est l'objectif : il s'ouvre avec {need} fraises reçues ({have} / {need}).";
        }

        static void OnLevelEnterGo(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData) {
            if (RandoSaves.CurrentIsRando && session?.Area.SID == EpilogueSid) {
                EpilogueBlockedForTest++;
                ApState.Log("epilogue blocked on a randomizer save");
                LevelEnter.ErrorMessage = "L'Épilogue est désactivé dans le randomizer.";
                orig(new Session(new AreaKey(1).SetSID("")), false); // invalid area: error screen, then back to the map
                return;
            }
            string goalLock = session == null ? null : GoalLockMessage(session.Area);
            if (goalLock != null && RandoSaves.CurrentIsRando) {
                GoalBlockedForTest++;
                ApState.Log("goal chapter locked: " + goalLock);
                LevelEnter.ErrorMessage = goalLock;
                orig(new Session(new AreaKey(1).SetSID("")), false); // error screen, then back to the map
                return;
            }
            orig(session, fromSaveData);
        }

        static string ActiveAreaKeyFor(Level level) {
            if (!ApState.AreaActive(level.Session.Area, out var key)) return null;
            return ApState.SaveMatches(true) ? key : null;
        }

        static bool IsCheckpointRoom(AreaKey area, string levelName) {
            var vanilla = LayoutInstaller.Original(area);
            var lvl = vanilla?.Get(levelName);
            if (lvl != null && lvl.HasCheckpoint) return true;
            var cps = AreaData.Get(area)?.Mode?[(int)area.Mode]?.Checkpoints;
            if (cps != null) foreach (var c in cps) if (c.Level == levelName) return true;
            return false;
        }

        // ------------------------------------------------------------------ movement locks

        static string DashItem(int dx, int dy) {
            if (dy < 0) return dx < 0 ? ItemNames.UpLeftDash : dx > 0 ? ItemNames.UpRightDash : ItemNames.UpDash;
            if (dy > 0) return dx < 0 ? ItemNames.DownLeftDash : dx > 0 ? ItemNames.DownRightDash : ItemNames.DownDash;
            return dx < 0 ? ItemNames.LeftDash : ItemNames.RightDash;
        }

        static string Cardinal(int dx, int dy) => dy < 0 ? ItemNames.UpDash : dy > 0 ? ItemNames.DownDash : dx < 0 ? ItemNames.LeftDash : ItemNames.RightDash;

        public static bool DashAllowed(int dx, int dy) {
            var slot = ApState.Slot;
            if (slot == null) return true;
            switch (slot.Option("dash_shuffle", 0)) {
                case 0: return true;
                case 1: return ApState.Has(ItemNames.Dash);
                case 2: // cardinal loose: a diagonal needs one of its two cardinals
                    if (dx != 0 && dy != 0) return ApState.Has(Cardinal(0, dy)) || ApState.Has(Cardinal(dx, 0));
                    return ApState.Has(Cardinal(dx, dy));
                case 3: // cardinal restrictive: a diagonal needs both
                    if (dx != 0 && dy != 0) return ApState.Has(Cardinal(0, dy)) && ApState.Has(Cardinal(dx, 0));
                    return ApState.Has(Cardinal(dx, dy));
                case 4: return ApState.Has(DashItem(dx, dy));
                default: return true;
            }
        }

        static bool CrouchLocked => ApState.Slot != null && ApState.Slot.Option("crouch_shuffle", 0) != 0 && !ApState.Has(ItemNames.Crouch);

        static float lastDenySound;

        static bool DemoLocked => ApState.Slot != null && ApState.Slot.Option("demo_dash_shuffle", 0) != 0 && !ApState.Has(ItemNames.DemoDash);

        /// <summary>Aim of the last dash press that StartDash allowed (the final direction is only fixed a few frames
        /// later, in the dash coroutine: see OnCorrectDashPrecision).</summary>
        static Vector2? pressAim;

        /// <summary>
        /// Dash lock, in Player.StartDash (called once CanDash is true). A locked dash: the press is consumed, the player
        /// stays in its current state and no dash is spent. Booster exits and water dashes do not go through StartDash:
        /// see OnCorrectDashPrecision.
        /// </summary>
        static int OnStartDash(On.Celeste.Player.orig_StartDash orig, Player self) {
            pressAim = null;
            if (!LocksHere) return orig(self);
            Vector2 aim = Input.GetAimVector(self.Facing);
            int dx = aim.X > 0.3f ? 1 : aim.X < -0.3f ? -1 : 0;
            int dy = aim.Y > 0.3f ? 1 : aim.Y < -0.3f ? -1 : 0;
            if (dx == 0 && dy == 0) dx = (int)self.Facing;
            if (DashAllowed(dx, dy)) pressAim = new Vector2(dx, dy);
            if (!DashAllowed(dx, dy)) {
                Input.Dash.ConsumePress();
                Input.CrouchDash.ConsumePress();
                if (Engine.Scene.TimeActive - lastDenySound > 0.25f) {
                    lastDenySound = Engine.Scene.TimeActive;
                    Audio.Play("event:/game/general/assist_dash_aim", self.Position);
                }
                return self.StateMachine.State;
            }
            return orig(self);
        }

        /// <summary>
        /// The dash coroutines (normal and red booster) fix the dash direction here, a few frames after the press
        /// (Celeste reads the aim again after the freeze frames). Two locks are enforced on that final direction:
        /// - a locked direction: the dash keeps the allowed direction of the press (StartDash path); a water dash
        ///   (SwimUpdate starts it without StartDash) in a locked direction goes nowhere;
        /// - Demo Dash: a crouched dash (crouch-dash button, or down held at the press then another aim) that does not
        ///   go downwards stands Madeline up when the Demo Dash is locked. A down/down-diagonal dash stays crouched
        ///   (vanilla dash slide on the ground, needed for hypers).
        /// Booster exits keep any direction (documented limitation: the bubble always launches).
        /// </summary>
        static Vector2 OnCorrectDashPrecision(On.Celeste.Player.orig_CorrectDashPrecision orig, Player self, Vector2 dir) {
            var v = orig(self, dir);
            if (!LocksHere) { pressAim = null; return v; }
            int dx = Math.Sign(Math.Round(v.X, 3)), dy = Math.Sign(Math.Round(v.Y, 3));
            if ((dx != 0 || dy != 0) && !DashAllowed(dx, dy)) {
                if (pressAim != null) {
                    ApState.Log($"dash direction changed after the press to a locked one ({dx},{dy}): kept ({pressAim.Value.X},{pressAim.Value.Y})");
                    v = orig(self, pressAim.Value);
                } else if (self.StateMachine.PreviousState == Player.StSwim) {
                    v = Vector2.Zero;
                }
            }
            pressAim = null;
            if (DemoLocked && self.Ducking && v.Y <= 0f && self.CanUnDuck) self.Ducking = false;
            return v;
        }

        /// <summary>checkpointsanity: reaching a checkpoint no longer unlocks it, only its item does (ApState.ApplyCheckpoints).</summary>
        static void OnSessionSetFlag(On.Celeste.Session.orig_SetFlag orig, Session self, string flag, bool setTo) {
            orig(self, flag, setTo);
            try { ApState.OnSessionFlag(flag, setTo); } catch (Exception e) { ApState.Log("flag hook failed: " + e.Message); }
        }

        static bool OnSetCheckpoint(On.Celeste.SaveData.orig_SetCheckpoint orig, SaveData self, AreaKey area, string level) {
            if (ApState.CheckpointLocked(area, level)) return false;
            return orig(self, area, level);
        }

        static bool OnClimbCheck(On.Celeste.Player.orig_ClimbCheck orig, Player self, int dir, int yAdd) {
            if (!orig(self, dir, yAdd)) return false;
            if (!LocksHere) return true;
            switch (ApState.Slot.Option("climb_shuffle", 0)) {
                case 1: return ApState.Has(ItemNames.Climb);
                case 2: return ApState.Has(dir < 0 ? ItemNames.LeftClimb : ItemNames.RightClimb);
                default: return true;
            }
        }

        /// <summary>Same hook as the V1 helper: "no wall here" blocks wall jumps, climb-jump kicks and wallbounces.</summary>
        static bool OnWallJumpCheck(On.Celeste.Player.orig_WallJumpCheck orig, Player self, int dir) {
            if (LocksHere && ApState.Slot.Option("wall_jump_shuffle", 0) != 0 && !ApState.Has(ItemNames.WallJump)) return false;
            return orig(self, dir);
        }

        /// <summary>Crouch lock: holding down on the ground (normal state) does not duck. Ducking forced by the game
        /// (e.g. after a down-diagonal dash) still happens; the crouch-dash button is blocked in OnCanDash.</summary>
        static void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
            int saved = Input.MoveY.Value;
            bool patched = false;
            if (saved > 0 && self.StateMachine.State == Player.StNormal && CrouchLocked && LocksHere && self.OnGround()) {
                Input.MoveY.Value = 0;
                patched = true;
            }
            try {
                orig(self);
            } finally {
                if (patched) Input.MoveY.Value = saved;
            }
        }

        // ------------------------------------------------------------------ checks

        static void Collected(string kind, EntityID id) {
            var area = ActiveAreaKey();
            if (area == null) return;
            ApState.Check($"{area}|{id.Level}|{kind}|{id.ID}");
        }

        /// <summary>Entity id of the closest entity of one of these names in the current room (for entities that
        /// do not keep their EntityID).</summary>
        static void CollectedAt(string kind, Entity entity, params string[] names) {
            var area = ActiveAreaKey();
            if (area == null || !(entity.Scene is Level level)) return;
            var data = level.Session.LevelData;
            EntityData best = null;
            float bestD = 32 * 32;
            foreach (var e in data.Entities) {
                if (Array.IndexOf(names, e.Name) < 0) continue;
                float d = Vector2.DistanceSquared(e.Position + data.Position, entity.Position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null) {
                // the 1A heart appears away from its birdForsakenCityGem marker: a room has one heart / one cassette
                var only = data.Entities.Where(e => Array.IndexOf(names, e.Name) >= 0).ToList();
                if (only.Count == 1) best = only[0];
            }
            if (best == null) {
                ApState.Log($"{kind} collected in {data.Name} but no matching entity data found");
                return;
            }
            ApState.Check($"{area}|{data.Name}|{kind}|{best.ID}");
        }

        static void OnStrawberryCollect(On.Celeste.Strawberry.orig_OnCollect orig, Strawberry self) {
            orig(self);
            try { Collected(self.Golden ? "golden" : "strawberry", self.ID); } catch (Exception e) { ApState.Log("strawberry check failed: " + e); }
        }

        /// <summary>A strawberry that holds a check not made yet carries a small Archipelago badge.</summary>
        static void OnStrawberryAdded(On.Celeste.Strawberry.orig_Added orig, Strawberry self, Scene scene) {
            orig(self, scene);
            try {
                // the scene being loaded (Engine.Scene can still be the LevelLoader)
                string area = null;
                if (ApState.Active && scene is Level lv && lv.Session != null && ApState.AreaActive(lv.Session.Area, out var k) && ApState.SaveMatches(false)) area = k;
                string bkey = $"{area}|{self.ID.Level}|{(self.Golden ? "golden" : "strawberry")}|{self.ID.ID}";
                if (area != null && ApState.IsOpenLocation(bkey))
                    self.Add(new ApBadge(bkey));
            } catch (Exception e) { ApState.Log("strawberry badge failed: " + e.Message); }
        }

        public class ApBadge : Component {
            static MTexture tex, filler;
            readonly string key;
            public ApBadge(string key) : base(false, true) { this.key = key; }
            public bool Filler => ApState.HoldsFiller(key);
            public override void Render() {
                tex ??= GFX.Game.Has("cmr/apbadge") ? GFX.Game["cmr/apbadge"] : null;
                if (tex == null || Entity == null) return;
                // top right of the berry, bobbing with it
                var s = Entity.Get<Sprite>();
                var at = Entity.Position + (s?.Position ?? Vector2.Zero) + new Vector2(5f, -11f);
                // filler inside: the same logo in dark grey-blue (cmr/apbadge_filler, built from apbadge's brightness)
                filler ??= GFX.Game.Has("cmr/apbadge_filler") ? GFX.Game["cmr/apbadge_filler"] : tex;
                (ApState.HoldsFiller(key) ? filler : tex).DrawCentered(new Vector2((int)at.X, (int)at.Y));
            }
        }

        static void OnKeyPlayer(On.Celeste.Key.orig_OnPlayer orig, Key self, Player player) {
            orig(self, player);
            try { Collected("key", self.ID); } catch (Exception e) { ApState.Log("key check failed: " + e); }
        }

        static System.Collections.IEnumerator OnGemSmash(On.Celeste.SummitGem.orig_SmashRoutine orig, SummitGem self, Player player, Level level) {
            try { Collected("gem", self.GID); ApState.KeepState("gem:" + self.GemID); } catch (Exception e) { ApState.Log("gem check failed: " + e); }
            return orig(self, player, level);
        }

        static void OnHeartCollect(On.Celeste.HeartGem.orig_Collect orig, HeartGem self, Player player) {
            // 1A: the heart is created by ForsakenCitySatellite once the bird code is entered, next to the
            // "birdForsakenCityGem" entity (location 1a|s1|heart|14), not from a "blackGem" entity
            try { if (!self.IsFake) CollectedAt("heart", self, "blackGem", "birdForsakenCityGem"); } catch (Exception e) { ApState.Log("heart check failed: " + e); }
            orig(self, player);
        }

        static void OnCassettePlayer(On.Celeste.Cassette.orig_OnPlayer orig, Cassette self, Player player) {
            orig(self, player);
            try { CollectedAt("cassette", self, "cassette"); } catch (Exception e) { ApState.Log("cassette check failed: " + e); }
        }

        static void OnComplete(Level level) {
            try {
                if (!ApState.AreaActive(level.Session.Area, out var key)) return;
                if (!ApState.SaveMatches(true)) return;
                ApState.Complete(key);
            } catch (Exception e) {
                ApState.Log("clear check failed: " + e);
            }
        }

        // ------------------------------------------------------------------ DeathLink

        static PlayerDeadBody OnPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats) {
            var body = orig(self, direction, evenIfInvincible, registerDeathInStats);
            try {
                if (body != null && !DeathLinkBridge.Killing && ApState.Mode == ApMode.Online && ApState.Slot.Option("death_link", 0) != 0 &&
                    ApState.SaveMatches(false)) {
                    ApClient.SendDeath(ApState.SlotName + " est mort(e) dans Celeste");
                }
            } catch (Exception e) {
                ApState.Log("DeathLink send failed: " + e);
            }
            return body;
        }
    }
}
