// CMR: help panel (default key Insert; it was F6 in V1), ported from our V1 helper mod (mod/CelesteMovementHelper, same author, same project).
// It now reads what the player owns from ApState (our own Archipelago client) instead of the Open World mod.
//   Insert cycles: room logic -> technique glossary -> hidden.  O/U scroll (room) or change technique (glossary).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public enum Ownership { Owned, Missing, Unknown }

    public class HelpOverlay : Entity {
        public static HelpOverlay Current { get; private set; }

        enum Page { Hidden, Room, Techniques }

        Page page = Page.Hidden;
        int scroll;
        static int techniqueIndex; // kept between rooms

        static readonly Color Green = Calc.HexToColor("7CE07C");
        static readonly Color Red = Calc.HexToColor("FF8080");
        static readonly Color Gold = Calc.HexToColor("FFD866");
        static readonly Color Grey = Calc.HexToColor("B8B8B8");
        static readonly Color Cyan = Calc.HexToColor("80D8FF");

        readonly struct Line {
            public readonly string Text;
            public readonly Color Color;
            public readonly int Indent;
            public Line(string text, Color color, int indent = 0) { Text = text; Color = color; Indent = indent; }
        }

        public static void Load() {
            HelpData.Load();
            MigrateHelpKey();
            On.Celeste.Level.LoadLevel += OnLoadLevel;
        }

        /// <summary>Earlier defaults were F6 (Everest debug map) and I (Nelem's dash key): move them to Insert.</summary>
        static void MigrateHelpKey() {
            try {
                var s = S;
                var keys = s?.HelpKey?.Keys;
                if (keys == null || keys.Count != 1) return;
                if (keys[0] != Microsoft.Xna.Framework.Input.Keys.F6 && keys[0] != Microsoft.Xna.Framework.Input.Keys.I) return;
                ApState.Log($"help: key {keys[0]} (old default) -> Insert");
                keys[0] = Microsoft.Xna.Framework.Input.Keys.Insert;
                RandoModule.Instance.SaveSettings();
            } catch (Exception e) {
                ApState.Log("help: key migration failed: " + e.Message);
            }
        }

        public static void Unload() {
            On.Celeste.Level.LoadLevel -= OnLoadLevel;
        }

        static void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes intro, bool isFromLoader) {
            orig(self, intro, isFromLoader);
            try {
                if (Current == null || Current.Scene != self) self.Add(new HelpOverlay());
            } catch (Exception e) {
                ApState.Log("help: could not add the overlay: " + e);
            }
        }

        public HelpOverlay() {
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate | Tags.TransitionUpdate;
            Depth = -100000;
            Current = this;
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            if (Current == this) Current = null;
        }

        static RandoModuleSettings S => RandoModule.Instance?.SavedData;

        public override void Update() {
            base.Update();
            var s = S;
            if (s == null) return;
            if (s.HelpKey != null && s.HelpKey.Pressed) {
                s.HelpKey.ConsumePress();
                page = page switch { Page.Hidden => Page.Room, Page.Room => Page.Techniques, _ => Page.Hidden };
                scroll = 0;
            }
            if (page == Page.Hidden) return;

            int delta = 0;
            if (s.HelpNextKey != null && s.HelpNextKey.Pressed) { s.HelpNextKey.ConsumePress(); delta = 1; }
            if (s.HelpPreviousKey != null && s.HelpPreviousKey.Pressed) { s.HelpPreviousKey.ConsumePress(); delta = -1; }
            if (delta == 0) return;
            if (page == Page.Techniques && HelpData.Techniques.Count > 0) {
                techniqueIndex = (techniqueIndex + delta + HelpData.Techniques.Count) % HelpData.Techniques.Count;
                scroll = 0;
            } else {
                scroll = Math.Max(0, scroll + delta * 5);
            }
        }

        public override void Render() {
            base.Render();
            if (page == Page.Hidden) return;
            DrawPanel(page == Page.Room ? BuildRoomLines() : BuildTechniqueLines());
        }

        // ------------------------------------------------------------------ what the player owns

        static readonly Dictionary<string, (int dx, int dy)> DashDirs = new() {
            { "u", (0, -1) }, { "ur", (1, -1) }, { "r", (1, 0) }, { "dr", (1, 1) },
            { "d", (0, 1) }, { "dl", (-1, 1) }, { "l", (-1, 0) }, { "ul", (-1, -1) },
        };

        static Ownership Of(bool b) => b ? Ownership.Owned : Ownership.Missing;

        /// <summary>Same item semantics as the apworld and as the in-game locks (ApHooks).</summary>
        public static Ownership Has(string token, HelpToken info) {
            var slot = ApState.Slot;
            if (!ApState.Active || slot == null) return Ownership.Unknown;
            if (ApState.OwnsEverything) return token == "cannot_access" ? Ownership.Missing : Ownership.Owned;

            if (token.StartsWith("any_dash_")) {
                var best = Ownership.Missing;
                foreach (string d in token.Substring(9).Split('_')) {
                    if (!DashDirs.TryGetValue(d, out var v)) { best = Ownership.Unknown; continue; }
                    if (ApHooks.DashAllowed(v.dx, v.dy)) return Ownership.Owned;
                }
                return best;
            }
            if (token.EndsWith("_dash") && DashDirs.TryGetValue(token.Substring(0, token.Length - 5), out var dir))
                return Of(ApHooks.DashAllowed(dir.dx, dir.dy));

            switch (token) {
                case "r_climb":
                case "l_climb":
                    switch (slot.Option("climb_shuffle", 0)) {
                        case 0: return Ownership.Owned;
                        case 1: return Of(ApState.Has(ItemNames.Climb));
                        default: return Of(ApState.Has(token == "l_climb" ? ItemNames.LeftClimb : ItemNames.RightClimb));
                    }
                case "crouch": return Of(slot.Option("crouch_shuffle", 0) == 0 || ApState.Has(ItemNames.Crouch));
                // not shuffled: the generator never counts on a demodash skip (gold = not part of this seed's logic)
                case "demo_dash": return slot.Option("demo_dash_shuffle", 0) == 0 ? Ownership.Unknown : Of(ApState.Has(ItemNames.DemoDash));
                case "cannot_access": return Ownership.Missing;
            }

            if (info == null) return Ownership.Unknown;
            switch (info.Kind) {
                case "interactable": return Of(slot.Option("interactable_shuffle", 0) == 0 || ApState.Has(info.Name));
                case "key":
                case "gem": return Of(ApState.Has(info.Name));
                default: return Ownership.Unknown; // events (hotel clutter, Farewell breaker): not tracked
            }
        }

        // ------------------------------------------------------------------ text helpers

        static string T(string key, string fallback) => Dialog.Has(key) ? Dialog.Clean(key) : fallback;

        static bool French => Settings.Instance?.Language == "french";

        static string TokenLabel(string token, HelpToken info) {
            if (token.StartsWith("any_dash_")) {
                var dirs = token.Substring(9).Split('_').Select(d => T("CMH_DIR_" + d.ToUpperInvariant(), d));
                return T("CMH_ANY_DASH", "Dash") + " (" + string.Join(" / ", dirs) + ")";
            }
            string key = "CMH_TOK_" + token.ToUpperInvariant().Replace(' ', '_');
            if (Dialog.Has(key)) return Dialog.Clean(key);
            return info?.Name ?? token;
        }

        static string LogicName(int logic) => logic switch {
            0 => T("CMH_LOGIC_DI", "Developer Intended"),
            2 => T("CMH_LOGIC_AS", "Assist Mode"),
            _ => T("CMH_LOGIC_VM", "Vanilla Movement"),
        };

        // ------------------------------------------------------------------ page 1: room logic

        static void AddRule(List<Line> lines, List<List<string>> rule, HelpLevel level) {
            if (rule == null || rule.Count == 0) {
                lines.Add(new Line(T("CMH_FREE", "no requirement"), Green, 2));
                return;
            }
            bool first = true;
            foreach (var alt in rule) {
                var missing = new List<string>();
                var labels = new List<string>();
                bool unknown = false;
                foreach (string token in alt) {
                    var info = HelpData.GetToken(level, token);
                    string label = TokenLabel(token, info);
                    labels.Add(label);
                    switch (Has(token, info)) {
                        case Ownership.Missing: missing.Add(label); break;
                        case Ownership.Unknown: unknown = true; break;
                    }
                }
                string text = (first ? "" : T("CMH_OR", "or") + " ") + string.Join(" + ", labels);
                first = false;
                Color color;
                if (missing.Count > 0) {
                    text += "   [" + T("CMH_MISSING", "missing") + ": " + string.Join(", ", missing) + "]";
                    color = Red;
                } else {
                    color = unknown ? Gold : Green;
                }
                lines.Add(new Line(text, color, 2));
            }
        }

        List<Line> BuildRoomLines(string roomOverride = null) {
            var lines = new List<Line>();
            var session = (Scene as Level)?.Session;
            var level = HelpData.GetLevel(HelpData.LevelKey(session));
            if (level == null) {
                lines.Add(new Line(T("CMH_NOT_VANILLA", "No randomizer logic for this map (vanilla chapters only)."), Grey));
                lines.Add(new Line(T("CMH_ROOM_FOOTER", "F7/F8: scroll - F6: technique glossary"), Grey));
                return lines;
            }

            var slot = ApState.Slot;
            int logic = slot?.Option("logic_difficulty", 1) ?? 1;
            string roomName = roomOverride ?? session.Level;
            lines.Add(new Line($"{level.Name} - {T("CMH_ROOM", "room")} {roomName}", Cyan));

            if (!ApState.Active || slot == null) {
                lines.Add(new Line(T("CMH_NOT_AP", "No Archipelago game running: owned items are unknown (gold)."), Gold));
            } else {
                lines.Add(new Line($"{T("CMH_LOGIC", "Logic")}: {LogicName(logic)}   ({T("CMH_LOGIC_FROM_SEED", "from the seed")})", Grey));
                if (ApState.Mode == ApMode.Cached)
                    lines.Add(new Line(T("CMH_NOT_CONNECTED", "Not connected to the server: owned items may be out of date."), Gold));
                if (!ApState.AreaActive(session.Area, out _))
                    lines.Add(new Line(T("CMH_AREA_INACTIVE", "This chapter is not part of the seed: nothing is locked here."), Gold));
                if (slot.Option("wall_jump_shuffle", 0) != 0) {
                    bool locked = !ApState.Has(ItemNames.WallJump);
                    lines.Add(new Line($"{T("CMH_WALLJUMP", "Wall Jump")}: {(locked ? T("CMH_LOCKED", "locked") : T("CMH_UNLOCKED", "unlocked"))}",
                        locked ? Red : Green));
                }
            }

            if (!level.Rooms.TryGetValue(roomName, out var room)) {
                lines.Add(new Line(T("CMH_ROOM_UNKNOWN", "This room is not described by the logic."), Grey));
                lines.Add(new Line(T("CMH_ROOM_FOOTER", "F7/F8: scroll - F6: technique glossary"), Grey));
                return lines;
            }

            lines.Add(new Line(T("CMH_LEGEND", "Green = you can, Red = missing item, Gold = cannot check"), Grey));
            if (room.Connections.Count > 0) {
                lines.Add(new Line(T("CMH_PASSAGES", "Paths inside the room (region -> region):"), Color.White));
                foreach (var c in room.Connections) {
                    lines.Add(new Line($"{c.From} -> {c.To}", Color.White, 1));
                    AddRule(lines, c.For(logic), level);
                }
            }
            if (room.Locations.Count > 0) {
                lines.Add(new Line(T("CMH_LOCATIONS", "Checks in this room:"), Color.White));
                foreach (var l in room.Locations) {
                    lines.Add(new Line($"{l.Name}  ({T("CMH_FROM_REGION", "from")} {l.Region})", Color.White, 1));
                    AddRule(lines, l.For(logic), level);
                }
            }
            if (room.Doors.Count > 0) {
                string doors = string.Join(", ", room.Doors.Select(d => $"{d.Name} ({T("CMH_DIR_DOOR_" + d.Dir.ToUpperInvariant(), d.Dir)})"));
                lines.Add(new Line($"{T("CMH_DOORS", "Exits")}: {doors}", Grey));
            }
            AddSeedExits(lines, session, roomName);
            lines.Add(new Line(T("CMH_ROOM_FOOTER", "F7/F8: scroll - F6: technique glossary"), Grey));
            return lines;
        }

        // where each opening of this room leads in the seed's layout (slot_data links), cached per room
        static string exitsKey;
        static List<Line> exitsCache;

        static void AddSeedExits(List<Line> lines, Session session, string roomName) {
            var slot = ApState.Slot;
            if (slot == null || !ApState.Active || !ApState.AreaActive(session.Area, out var areaKey)) return;
            if (!slot.Chapters.TryGetValue(areaKey, out var ch)) return;
            string key = ApState.Seed + "|" + areaKey + "|" + roomName;
            if (key != exitsKey) {
                exitsKey = key;
                exitsCache = BuildSeedExits(ch, areaKey, roomName);
            }
            lines.AddRange(exitsCache);
        }

        static List<Line> BuildSeedExits(ChapterLayout ch, string areaKey, string roomName) {
            var res = new List<Line>();
            try {
                var statics = LayoutBuilder.RoomKeys(areaKey);
                var mine = ch.Rooms.FirstOrDefault(r => r.Room == roomName);
                if (mine == null) return res;
                var vkey = string.IsNullOrEmpty(mine.Variant) ? mine.Room : mine.Variant;
                if (!statics.TryGetValue(vkey, out var st) && !statics.TryGetValue(roomName, out st)) return res;
                var entries = new List<(int order, Line line)>();
                foreach (var l in ch.Links) {
                    bool isA = l.A == roomName, isB = l.B == roomName;
                    if (!isA && !isB) continue;
                    int hole = isA ? l.AHole : l.BHole;
                    string other = isA ? l.B : l.A;
                    if (hole < 0 || hole >= st.Holes.Count) continue;
                    var side = st.Holes[hole].Side;
                    string sideName = T("CMH_DIR_DOOR_" + side.ToString().ToUpperInvariant(), side.ToString());
                    string me = isA ? "a" : "b", them = isA ? "b" : "a";
                    string dir = l.Dir ?? "both";
                    bool leave = dir == "both" || dir == (me + them);
                    bool arrive = dir == "both" || dir == (them + me);
                    bool blocked = l.NoEnter != null && l.NoEnter.Contains(me[0]);
                    string how = blocked ? T("CMH_EXIT_ONLY_BLOCKED", "exit only (closed behind you)")
                        : leave && arrive ? T("CMH_EXIT_BOTH", "both ways")
                        : leave ? T("CMH_EXIT_LEAVE", "logic: way out")
                        : arrive ? T("CMH_EXIT_ARRIVE", "logic: way in")
                        : T("CMH_EXIT_UNUSED", "not used by the logic");
                    var line = new Line($"{sideName} -> {T("CMH_ROOM", "room")} {other}   ({how})", leave || arrive ? Color.White : Grey, 1);
                    entries.Add(((int)side * 100 + hole, line));
                }
                if (entries.Count == 0) return res;
                res.Add(new Line(T("CMH_SEED_EXITS", "Where the openings lead in this seed:"), Cyan));
                res.AddRange(entries.OrderBy(e => e.order).Select(e => e.line));
            } catch (Exception e) {
                ApState.Log("help: seed exits failed: " + e.Message);
            }
            return res;
        }

        // ------------------------------------------------------------------ page 2: techniques

        static List<Line> BuildTechniqueLines() {
            var lines = new List<Line>();
            var all = HelpData.Techniques;
            if (all.Count == 0) {
                lines.Add(new Line(T("CMH_NO_TECH", "No technique data."), Grey));
                return lines;
            }
            var t = all[Math.Clamp(techniqueIndex, 0, all.Count - 1)];
            lines.Add(new Line($"{t.Name}   ({techniqueIndex + 1}/{all.Count})", Cyan));
            if (t.Aka.Count > 0) lines.Add(new Line($"{T("CMH_AKA", "Also called")}: {string.Join(", ", t.Aka)}", Grey));
            lines.Add(new Line($"{T("CMH_CATEGORY", "Level")}: {T("CMH_CAT_" + t.Category.ToUpperInvariant(), t.Category)}", Gold));
            if (t.Requires.Count > 0)
                lines.Add(new Line($"{T("CMH_REQUIRES", "Needs")}: {string.Join(", ", t.Requires.Select(r => T("CMH_ABIL_" + r.ToUpperInvariant(), r)))}", Color.White));
            string explanation = French || string.IsNullOrEmpty(t.ExplanationEn) ? t.ExplanationFr : t.ExplanationEn;
            if (!string.IsNullOrEmpty(explanation)) lines.Add(new Line(explanation, Color.White));
            if (t.Inputs.Count > 0) {
                lines.Add(new Line(T("CMH_INPUTS", "Inputs:"), Color.White));
                for (int i = 0; i < t.Inputs.Count; i++) lines.Add(new Line($"{i + 1}. {t.Inputs[i]}", Color.White, 1));
            }
            if (!string.IsNullOrEmpty(t.Timing)) lines.Add(new Line($"{T("CMH_TIMING", "Timing")}: {t.Timing}", Color.White));
            if (!string.IsNullOrEmpty(t.VanillaWhere)) lines.Add(new Line($"{T("CMH_WHERE", "Taught / first needed")}: {t.VanillaWhere}", Grey));
            if (t.Verified && t.Sources.Count > 0) {
                lines.Add(new Line(T("CMH_SOURCES", "Sources:"), Color.White));
                foreach (var src in t.Sources) lines.Add(new Line($"{src.Title} - {src.Url}", Cyan, 1));
            } else {
                lines.Add(new Line(T("CMH_UNVERIFIED", "Not verified: no reliable source found."), Red));
            }
            lines.Add(new Line(T("CMH_TECH_FOOTER", "F7/F8: next/previous technique - F6: close"), Grey));
            return lines;
        }

        // ------------------------------------------------------------------ drawing

        static List<string> Wrap(string text, float maxWidth, float scale) {
            var result = new List<string>();
            var sb = new StringBuilder();
            foreach (string word in text.Split(' ')) {
                string candidate = sb.Length == 0 ? word : sb + " " + word;
                if (sb.Length > 0 && ActiveFont.Measure(candidate).X * scale > maxWidth) {
                    result.Add(sb.ToString());
                    sb.Clear();
                    sb.Append(word);
                } else {
                    sb.Clear();
                    sb.Append(candidate);
                }
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        void DrawPanel(List<Line> lines) {
            float scale = Math.Clamp(S?.HelpTextScale ?? 4, 3, 8) / 10f;
            const float left = 40f, top = 40f, width = 1840f, maxHeight = 1000f, indentStep = 40f;
            float lineHeight = ActiveFont.LineHeight * scale;

            var wrapped = new List<Line>();
            foreach (var l in lines) {
                var parts = Wrap(l.Text, width - 40f - l.Indent * indentStep, scale);
                for (int i = 0; i < parts.Count; i++) wrapped.Add(new Line(parts[i], l.Color, l.Indent + (i > 0 ? 1 : 0)));
            }

            int visible = Math.Max(1, (int)((maxHeight - 40f) / lineHeight));
            int maxScroll = Math.Max(0, wrapped.Count - visible);
            if (scroll > maxScroll) scroll = maxScroll;
            int count = Math.Min(visible, wrapped.Count - scroll);
            float height = count * lineHeight + 40f;
            Draw.Rect(left, top, width, height, Color.Black * 0.8f);

            float y = top + 20f;
            for (int i = scroll; i < scroll + count; i++) {
                var l = wrapped[i];
                ActiveFont.DrawOutline(l.Text, new Vector2(left + 20f + l.Indent * indentStep, y), Vector2.Zero,
                    Vector2.One * scale, l.Color, 2f, Color.Black);
                y += lineHeight;
            }
            if (maxScroll > 0) {
                ActiveFont.DrawOutline($"{scroll + count}/{wrapped.Count}", new Vector2(left + width - 20f, top + height - 10f),
                    new Vector2(1f, 1f), Vector2.One * scale * 0.8f, Grey, 2f, Color.Black);
            }
        }

        // ------------------------------------------------------------------ self-test (SelfTest.Saves.cs, step "help")

        /// <summary>0 = hidden, 1 = room logic, 2 = techniques.</summary>
        public void SetPageForTest(int p) { page = (Page)p; scroll = 0; }

        /// <summary>Text of the room page with each line's colour, for the self-test report.</summary>
        public string DumpRoomPage(string room = null) {
            var sb = new StringBuilder();
            foreach (var l in BuildRoomLines(room)) {
                string c = l.Color == Green ? "GREEN" : l.Color == Red ? "RED" : l.Color == Gold ? "GOLD" : l.Color == Grey ? "grey" : l.Color == Cyan ? "cyan" : "white";
                sb.Append("    ").Append(new string(' ', 2 * l.Indent)).Append('[').Append(c).Append("] ").AppendLine(l.Text);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ console

        [Command("cmr_help", "UltraRando: what the F6 help reads (owned movement items)")]
        static void CmdHelp() {
            void Log(string s) => Engine.Commands.Log(s);
            Log($"ApState mode {ApState.Mode}, active {ApState.Active}, owns everything {ApState.OwnsEverything}");
            foreach (var d in DashDirs.Keys) Log($"  {d}_dash: {Has(d + "_dash", null)}");
            Log($"  l_climb: {Has("l_climb", null)}  r_climb: {Has("r_climb", null)}  crouch: {Has("crouch", null)}");
            Log($"  logic levels {HelpData.Logic?.Levels.Count ?? 0}, techniques {HelpData.Techniques.Count}");
        }
    }
}
