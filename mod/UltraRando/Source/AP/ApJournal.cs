// CMR: journal pages of a randomizer save, laid out like the Open World journal (behaviour/layout reference:
// docs/ref_openworld_ui, screenshots made by the user). Own code; only the vanilla journal API (OuiJournalPage.Table,
// TextCell, IconCell) and VANILLA game textures loaded at runtime (GFX.Journal / GFX.Gui / GFX.Game) are used,
// nothing from Open World's mod (its code and graphics are "All rights reserved").
// Pages, inserted after the vanilla cover (which already shows the save name = slot name):
//   LOCATIONS (per chapter: heart / cassette / clear, strawberries, checkpoints, keys, rooms; totals + deaths),
//   KEYS (per chapter, named keys greyed until picked up; summit gems row), MOVEMENT (Madeline sprites, greyed until
//   unlocked, incl. Wall Jump), ITEMS (object grid, greyed until received).
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public static class ApJournal {
        public static void Load() => Everest.Events.Journal.OnEnter += OnEnter;
        public static void Unload() => Everest.Events.Journal.OnEnter -= OnEnter;

        /// <summary>A seed is being played with the loaded save (online, cached or the offline test).</summary>
        public static bool Show => ApState.Slot != null && ApState.Active && (ApState.Mode == ApMode.Offline || ApState.SaveMatches(false));

        static void OnEnter(OuiJournal journal, Oui from) {
            try {
                if (!Show) return;
                int at = Math.Min(1, journal.Pages.Count);
                // pages from the seed itself (rebuilt each time the journal opens, so never stale): 12 chapters per
                // locations page, and the keys / movement / items pages only when the seed has something to show there
                var pages = new List<OuiJournalPage>();
                int n = LocationAreas().Count;
                for (int first = 0; first < Math.Max(1, n); first += ApLocationsPage.PerPage)
                    pages.Add(new ApLocationsPage(journal, first, first + ApLocationsPage.PerPage >= n));
                var kinds = SeedKinds();
                if (kinds.Contains("key") || kinds.Contains("gem")) pages.Add(new ApKeysPage(journal));
                if (ApMovementPage.AnyShuffled()) pages.Add(new ApMovementPage(journal));
                if (ApState.Slot.Option("interactable_shuffle", 0) != 0) pages.Add(new ApItemsPage(journal));
                journal.Pages.InsertRange(at, pages);
                ApState.Log("journal pages: " + string.Join(",", pages.Select(pg => pg.GetType().Name)));
            } catch (Exception e) {
                ApState.Log("journal page failed: " + e);
            }
        }

        // ------------------------------------------------------------------ data helpers

        public static HashSet<long> Checked() {
            var save = ApState.Save;
            return save != null && ApState.Mode != ApMode.Offline ? new HashSet<long>(save.CheckedLocations) : new HashSet<long>();
        }

        /// <summary>Location keys of the seed for one area, split as "area|level|kind|id" / "area|level|room|..." / "area|clear".</summary>
        public static IEnumerable<KeyValuePair<string, long>> AreaLocations(string area) =>
            ApState.Slot.LocationIds.Where(kv => kv.Key.StartsWith(area + "|", StringComparison.Ordinal));

        public static string Kind(string locKey) {
            var p = locKey.Split('|');
            if (p.Length == 2) return p[1];          // clear
            if (p.Length == 3) return p[2];          // room / checkpoint
            return p.Length >= 4 ? p[2] : "";        // strawberry / key / gem / heart / cassette / golden
        }

        public static string AreaName(string areaKey, bool withSide) {
            if (!LayoutBuilder.TryParseAreaKey(areaKey, out var key)) return Text.Sanitize(areaKey, 8);
            var data = AreaData.Get(key);
            string name = data != null ? Dialog.Clean(data.Name) : areaKey;
            return withSide || key.Mode != AreaMode.Normal ? name + " " + (char)('A' + (int)key.Mode) : name;
        }

        /// <summary>Kinds of checks of the seed (slot_data location keys): only these get a column in the journal.</summary>
        public static HashSet<string> SeedKinds() => new HashSet<string>(ApState.Slot.LocationIds.Keys.Select(Kind));

        /// <summary>Active chapters that hold at least one check of the seed.</summary>
        public static List<string> LocationAreas() => Areas.Where(a => AreaLocations(a).Any()).ToList();

        public static IEnumerable<string> Areas => ApState.Slot.ActiveAreas
            .Select(a => (a, ok: LayoutBuilder.TryParseAreaKey(a, out var k), k))
            .Where(t => t.ok).OrderBy(t => (int)t.k.Mode).ThenBy(t => t.k.ID).Select(t => t.a);

        /// <summary>Vanilla key and gem names by room. Room/name pairs taken from the Open World apworld's level data
        /// (CelesteLevelData.json, "Modified MIT", see apworld/ultrarando/data/LICENSE_OpenWorld.txt).</summary>
        public static readonly Dictionary<string, string[]> KeyNames = new Dictionary<string, string[]> {
            { "3a|s3", new[] { "Front Door Key" } }, { "3a|02-b", new[] { "Hallway Key 1" } }, { "3a|07-b", new[] { "Hallway Key 2" } },
            { "3a|09-b", new[] { "Huge Mess Key" } }, { "3a|02-c", new[] { "Presidential Suite Key" } },
            { "5a|a-08", new[] { "Entrance Key" } }, { "5a|b-04", new[] { "Depths Key" } }, { "5a|d-04", new[] { "Search Key 1", "Search Key 2" } },
            { "5a|d-15", new[] { "Search Key 3" } }, { "5b|b-02", new[] { "Central Chamber Key 1", "Central Chamber Key 2" } },
            { "7a|f-07", new[] { "2500 M Key" } },
            { "10a|d-04", new[] { "Power Source Key 1" } }, { "10a|d-03", new[] { "Power Source Key 2" } }, { "10a|d-01", new[] { "Power Source Key 3" } },
            { "10a|d-02", new[] { "Power Source Key 4" } }, { "10a|d-05", new[] { "Power Source Key 5" } },
        };
        public static readonly string[] GemRooms = { "a-06", "b-02d", "c-06c", "d-05b", "e-01c", "f-02b" };

        public static MTexture Tex(Atlas atlas, string path) => atlas != null && atlas.Has(path) ? atlas[path] : null;
    }

    /// <summary>Common drawing: text/tables with the default batch, then pixel-art sprites with point sampling.</summary>
    public abstract class ApJournalPageBase : OuiJournalPage {
        protected static readonly Color Ink = Color.Black * 0.7f;
        protected static readonly Color Stripe = Calc.HexToColor("b9b4e0") * 0.25f;
        protected static readonly Color Greyed = new Color(0.30f, 0.30f, 0.34f, 1f);
        readonly List<(MTexture tex, Vector2 pos, float scale, Color color, float rot, SpriteEffects fx)> sprites = new();

        protected ApJournalPageBase(OuiJournal journal) : base(journal) { PageTexture = "page"; }

        protected abstract void DrawContent();

        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);
            sprites.Clear();
            Draw.SpriteBatch.Begin();
            try { DrawContent(); } catch (Exception e) { ApState.Log("journal draw failed: " + e.Message); }
            Draw.SpriteBatch.End();
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            // Centre the trimmed (visible) pixels rather than the padded frame: MTexture.DrawCentered pivots on the
            // padded frame and does not mirror DrawOffset when flipped, which pushed rotated dash sprites off-centre.
            foreach (var s in sprites) {
                var clip = s.tex.ClipRect;
                Draw.SpriteBatch.Draw(s.tex.Texture.Texture_Safe, s.pos, clip, s.color, s.rot,
                    new Vector2(clip.Width / 2f, clip.Height / 2f), s.scale, s.fx, 0f);
            }
            Draw.SpriteBatch.End();
        }

        /// <summary>Pixel-art sprite centred on <paramref name="pos"/>, fitted into a box of <paramref name="box"/> px.</summary>
        public void Sprite(MTexture tex, Vector2 pos, float box, bool owned, float rot = 0f, SpriteEffects fx = SpriteEffects.None, Color? tint = null) {
            if (tex == null) return;
            float scale = Math.Min(10f, box / Math.Max(tex.Width, tex.Height));
            Color c = owned ? (tint ?? Color.White) : Greyed;
            sprites.Add((tex, pos, scale, c, rot, fx));
        }

        protected void Title(string text, float x = 70f, float justify = 0f) {
            ActiveFont.Draw(text, new Vector2(x, 70f), new Vector2(justify, 0.5f), Vector2.One * 1.3f, Color.Black * 0.75f);
            Draw.Rect(0f, 128f, 1610f, 3f, Calc.HexToColor("b9b4e0") * 0.8f);
        }

        protected static void Label(string text, Vector2 pos, float scale, Vector2? justify = null) =>
            ActiveFont.Draw(text, pos, justify ?? new Vector2(0.5f, 0.5f), Vector2.One * scale, Ink);

        /// <summary>Two-line wrap for item / key names under OW-style grids.</summary>
        protected static string Wrap(string s) {
            if (s.Length <= 9 || !s.Contains(' ')) return s;
            int mid = s.Length / 2, best = -1;
            for (int i = 0; i < s.Length; i++) if (s[i] == ' ' && (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid))) best = i;
            return s.Substring(0, best) + "\n" + s.Substring(best + 1);
        }
    }

    /// <summary>Table cell drawing a vanilla texture (pixel art drawn in the point-sampled pass of the page).</summary>
    public class SpriteCell : OuiJournalPage.Cell {
        readonly ApJournalPageBase page;
        readonly MTexture tex;
        readonly float box;
        readonly bool owned;
        readonly float width;
        public SpriteCell(ApJournalPageBase page, MTexture tex, float box, float width = 0f, bool owned = true) { this.page = page; this.tex = tex; this.box = box; this.width = width > 0f ? width : box; this.owned = owned; }
        public override float Width() => width;
        public override void Render(Vector2 center, float columnWidth) => page.Sprite(tex, center, box, owned);
    }

    // ====================================================================== LOCATIONS

    public class ApLocationsPage : ApJournalPageBase {
        readonly Table table;

        public const int PerPage = 12;
        public static string ColumnsForTest;
        public static List<string> RowsForTest;
        public static int TotalForTest;
        readonly bool last;

        public ApLocationsPage(OuiJournal journal, int from = 0, bool last = true) : base(journal) {
            this.last = last;
            var done = ApJournal.Checked();
            var kinds = ApJournal.SeedKinds();
            bool H = kinds.Contains("heart"), C = kinds.Contains("cassette"), L = kinds.Contains("clear"),
                 S = kinds.Contains("strawberry") || kinds.Contains("golden"), K = kinds.Contains("key"), G = kinds.Contains("gem"),
                 P = kinds.Contains("checkpoint"), R = kinds.Contains("room");
            table = new Table().AddColumn(new TextCell(Dialog.Clean("CMR_JOURNAL_LOCATIONS"), new Vector2(0f, 0.5f), 1f, Color.Black * 0.75f, 400f, false));
            if (H) table.AddColumn(new IconCell("heartgem0", 64f));
            if (C) table.AddColumn(new IconCell("cassette", 64f));
            if (L) table.AddColumn(new IconCell("clear", 64f));
            if (S) table.AddColumn(new IconCell("strawberry", 150f));
            if (P) table.AddColumn(new SpriteCell(this, ApJournal.Tex(GFX.Gui, "checkpoint") ?? ApJournal.Tex(GFX.Game, "objects/checkpoint/flag00"), 72f, 150f));
            if (K) table.AddColumn(new SpriteCell(this, ApJournal.Tex(GFX.Game, "collectables/key/idle00"), 64f, 150f));
            if (G) table.AddColumn(new SpriteCell(this, ApJournal.Tex(GFX.Game, "collectables/summitgems/0/gem00"), 56f, 150f));
            if (R) table.AddColumn(new TextCell(Dialog.Clean("CMR_JOURNAL_ROOMS"), new Vector2(0.5f, 0.5f), 0.6f, Color.Black * 0.75f, 170f, true));
            int all = 0, allDone = 0;
            var areas = ApJournal.LocationAreas();
            foreach (var area in areas) {
                var all1 = ApJournal.AreaLocations(area).ToList();
                all += all1.Count;
                allDone += all1.Count(kv => done.Contains(kv.Value));
            }
            foreach (var area in areas.Skip(from).Take(PerPage)) {
                var locs = ApJournal.AreaLocations(area).ToList();
                var row = table.AddRow();
                row.Add(new TextCell(ApJournal.AreaName(area, false), new Vector2(1f, 0.5f), 0.6f, TextColor));
                if (H) row.Add(Dot(locs, "heart", "heartgem0", done));
                if (C) row.Add(Dot(locs, "cassette", "cassette", done));
                if (L) row.Add(Dot(locs, "clear", "clear", done));
                if (S) row.Add(Count(locs, done, "strawberry", "golden"));
                if (P) row.Add(Count(locs, done, "checkpoint"));
                if (K) row.Add(Count(locs, done, "key"));
                if (G) row.Add(Count(locs, done, "gem"));
                if (R) row.Add(Count(locs, done, "room"));
            }
            ColumnsForTest = string.Join(",", new[] { ("heart", H), ("cassette", C), ("clear", L), ("strawberry", S), ("checkpoint", P), ("key", K), ("gem", G), ("room", R) }.Where(t => t.Item2).Select(t => t.Item1));
            RowsForTest = areas.Skip(from).Take(PerPage).ToList();
            TotalForTest = all;
            ApState.Log($"journal locations page {from}: columns [{ColumnsForTest}] rows [{string.Join(",", RowsForTest)}] total {all}");
            totals = $"{allDone} / {all}";
            deaths = SaveData.Instance?.TotalDeaths ?? 0;
        }

        readonly string totals;
        readonly int deaths;

        Cell Dot(List<KeyValuePair<string, long>> locs, string kind, string icon, HashSet<long> done) {
            var mine = locs.Where(kv => ApJournal.Kind(kv.Key) == kind).ToList();
            if (mine.Count > 0 && mine.All(kv => done.Contains(kv.Value))) return new IconCell(icon, 64f);
            return new IconCell("dot", 64f);
        }

        Cell Count(List<KeyValuePair<string, long>> locs, HashSet<long> done, params string[] kinds) {
            var mine = locs.Where(kv => kinds.Contains(ApJournal.Kind(kv.Key))).ToList();
            if (mine.Count == 0) return new IconCell("dot", 150f);
            return new TextCell($"{mine.Count(kv => done.Contains(kv.Value))} / {mine.Count}", new Vector2(0.5f, 0.5f), 0.6f, TextColor);
        }

        protected override void DrawContent() {
            table.Render(new Vector2(60f, 30f));
            if (!last) return;
            float y = 30f + table.Height() + 30f;
            Draw.Rect(60f, y - 6f, 1490f, 4f, Calc.HexToColor("b9b4e0"));
            Draw.Rect(60f, y, 1490f, 70f, Stripe);
            ActiveFont.Draw(Dialog.Clean("CMR_JOURNAL_TOTALS"), new Vector2(380f, y + 35f), new Vector2(1f, 0.5f), Vector2.One * 0.8f, Ink);
            ActiveFont.Draw(totals, new Vector2(620f, y + 35f), new Vector2(0.5f, 0.5f), Vector2.One * 0.7f, Ink);
            ActiveFont.Draw(Dialog.Clean("CMR_JOURNAL_DEATHS"), new Vector2(1250f, y + 35f), new Vector2(1f, 0.5f), Vector2.One * 0.7f, Ink);
            ActiveFont.Draw(deaths.ToString(), new Vector2(1400f, y + 35f), new Vector2(0.5f, 0.5f), Vector2.One * 0.7f, Ink);
            Draw.Rect(60f, y + 70f, 1490f, 4f, Calc.HexToColor("b9b4e0"));
        }
    }

    // ====================================================================== KEYS

    public class ApKeysPage : ApJournalPageBase {
        readonly List<(string area, List<(string name, bool got)> keys)> rows = new();
        readonly List<(int index, bool exists, bool got)> gems = new();

        public ApKeysPage(OuiJournal journal) : base(journal) {
            var done = ApJournal.Checked();
            foreach (var area in ApJournal.Areas) {
                var keys = ApJournal.AreaLocations(area).Where(kv => ApJournal.Kind(kv.Key) == "key")
                    .Select(kv => (parts: kv.Key.Split('|'), id: kv.Value))
                    .OrderBy(t => KeyOrder(area, t.parts[1])).ThenBy(t => t.parts[1], StringComparer.Ordinal).ThenBy(t => int.TryParse(t.parts[3], out var n) ? n : 0).ToList();
                if (keys.Count == 0) continue;
                var list = new List<(string, bool)>();
                var perRoom = new Dictionary<string, int>();
                foreach (var (parts, id) in keys) {
                    string room = parts[1];
                    perRoom.TryGetValue(room, out int k);
                    perRoom[room] = k + 1;
                    string name = ApJournal.KeyNames.TryGetValue(area + "|" + room, out var names) && k < names.Length ? names[k] : Dialog.Clean("CMR_JOURNAL_KEY") + " " + room;
                    list.Add((name, done.Contains(id)));
                }
                rows.Add((area, list));
            }
            if (ApState.Slot.ActiveAreas.Contains("7a")) {
                var gemLocs = ApJournal.AreaLocations("7a").Where(kv => ApJournal.Kind(kv.Key) == "gem").ToList();
                for (int i = 0; i < ApJournal.GemRooms.Length; i++) {
                    var mine = gemLocs.Where(kv => kv.Key.Split('|')[1] == ApJournal.GemRooms[i]).ToList();
                    gems.Add((i, mine.Count > 0, mine.Count > 0 && mine.All(kv => done.Contains(kv.Value))));
                }
            }
        }

        /// <summary>Position of the room in the vanilla key order (KeyNames is written in game order).</summary>
        static int KeyOrder(string area, string room) {
            int i = 0;
            foreach (var k in ApJournal.KeyNames.Keys) { if (k == area + "|" + room) return i; i++; }
            return int.MaxValue;
        }

        protected override void DrawContent() {
            Title(Dialog.Clean("CMR_JOURNAL_KEYS_TITLE"));
            float y = 170f;
            float rowH = rows.Count <= 4 ? 170f : 140f;
            if (rows.Count == 0) Label(Dialog.Clean("CMR_JOURNAL_NO_KEYS"), new Vector2(805f, 300f), 0.7f);
            foreach (var (area, keys) in rows) {
                Draw.Rect(60f, y + rowH * 0.45f, 1250f, rowH * 0.45f, Stripe);
                Label(ApJournal.AreaName(area, true), new Vector2(80f, y + rowH * 0.67f), 0.45f, new Vector2(0f, 0.5f));
                for (int i = 0; i < keys.Count && i < 8; i++) {
                    float x = 400f + i * 125f;
                    Label(Wrap(keys[i].name), new Vector2(x, y + rowH * 0.22f), 0.38f);
                    Sprite(ApJournal.Tex(GFX.Game, "collectables/key/idle00"), new Vector2(x, y + rowH * 0.68f), 70f, keys[i].got);
                }
                y += rowH;
            }
            if (gems.Count > 0) {
                y += 20f;
                Draw.Rect(60f, y, 1250f, 110f, Stripe);
                Label(Dialog.Clean("CMR_JOURNAL_GEMS"), new Vector2(160f, y + 55f), 0.6f);
                foreach (var (i, exists, got) in gems) {
                    var tex = ApJournal.Tex(GFX.Game, $"collectables/summitgems/{i}/gem00");
                    Sprite(tex, new Vector2(400f + i * 125f, y + 55f), 80f, got);
                    if (!exists) Label("-", new Vector2(400f + i * 125f, y + 100f), 0.4f);
                }
            }
        }
    }

    // ====================================================================== MOVEMENT

    public class ApMovementPage : ApJournalPageBase {
        // (item, row, column, sprite, direction the sprite must face (dash) or (±1,0) for left/right facing)
        readonly List<(string item, int row, int col, string tex, int dx, int dy)> cells = new();
        static readonly float[] RowY = { 235f, 400f, 565f, 755f };

        /// <summary>Does the seed shuffle any movement (otherwise the page has nothing to show)?</summary>
        public static bool AnyShuffled() {
            var o = ApState.Slot;
            return o.Option("dash_shuffle", 0) != 0 || o.Option("climb_shuffle", 0) != 0 || o.Option("crouch_shuffle", 0) != 0
                || o.Option("wall_jump_shuffle", 0) != 0 || o.Option("demo_dash_shuffle", 0) != 0;
        }

        public ApMovementPage(OuiJournal journal) : base(journal) {
            var slot = ApState.Slot;
            const string dash = "characters/player/dash00";
            switch (slot.Option("dash_shuffle", 0)) {
                case 1:
                    cells.Add((ItemNames.Dash, 1, 1, dash, 1, 0));
                    break;
                case 2:
                case 3:
                    cells.Add((ItemNames.UpDash, 0, 1, dash, 0, -1));
                    cells.Add((ItemNames.LeftDash, 1, 0, dash, -1, 0));
                    cells.Add((ItemNames.RightDash, 1, 2, dash, 1, 0));
                    cells.Add((ItemNames.DownDash, 2, 1, dash, 0, 1));
                    break;
                case 4:
                    cells.Add((ItemNames.UpLeftDash, 0, 0, dash, -1, -1));
                    cells.Add((ItemNames.UpDash, 0, 1, dash, 0, -1));
                    cells.Add((ItemNames.UpRightDash, 0, 2, dash, 1, -1));
                    cells.Add((ItemNames.LeftDash, 1, 0, dash, -1, 0));
                    cells.Add((ItemNames.RightDash, 1, 2, dash, 1, 0));
                    cells.Add((ItemNames.DownLeftDash, 2, 0, dash, -1, 1));
                    cells.Add((ItemNames.DownDash, 2, 1, dash, 0, 1));
                    cells.Add((ItemNames.DownRightDash, 2, 2, dash, 1, 1));
                    break;
            }
            switch (slot.Option("climb_shuffle", 0)) {
                case 1: cells.Add((ItemNames.Climb, 3, 0, "characters/player/climb00", 1, 0)); break;
                case 2:
                    cells.Add((ItemNames.LeftClimb, 3, 0, "characters/player/climb00", -1, 0));
                    cells.Add((ItemNames.RightClimb, 3, 1, "characters/player/climb00", 1, 0));
                    break;
            }
            if (slot.Option("crouch_shuffle", 0) != 0) cells.Add((ItemNames.Crouch, 3, 2, "characters/player/duck", 1, 0));
            if (slot.Option("wall_jump_shuffle", 0) != 0) cells.Add((ItemNames.WallJump, 3, 3, "characters/player/jumpFast00", -1, 0));
            if (slot.Option("demo_dash_shuffle", 0) != 0) cells.Add((ItemNames.DemoDash, 3, 4, "characters/player/duck", 1, 0));
        }

        protected override void DrawContent() {
            Title(Dialog.Clean("CMR_JOURNAL_MOVEMENT_TITLE"));
            foreach (var y in RowY) Draw.Rect(80f, y - 30f, 1420f, 60f, Stripe);
            if (cells.Count == 0) Label(Dialog.Clean("CMR_JOURNAL_NO_MOVEMENT"), new Vector2(805f, 300f), 0.7f);
            foreach (var (item, row, col, tex, dx, dy) in cells) {
                var pos = new Vector2(520f + col * 190f, RowY[row]);
                bool owned = ApState.Has(item);
                // the dash sprite faces right: flip it for leftward directions, then rotate towards (dx, dy)
                bool flip = dx < 0;
                float rot = tex.EndsWith("dash00") ? (flip ? (float)Math.Atan2(-dy, -dx) : (float)Math.Atan2(dy, dx)) : 0f;
                Sprite(ApJournal.Tex(GFX.Game, tex), pos, 170f, owned, rot, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
                Label(item, pos + new Vector2(0f, 92f), 0.36f);
            }
        }
    }

    // ====================================================================== ITEMS

    public class ApItemsPage : ApJournalPageBase {
        // display order of the reference screenshots, then the objects the reference page did not show
        static readonly string[] Order = {
            "Springs", "Traffic Blocks", "Dash Refills", "Pink Cassette Blocks", "Blue Cassette Blocks", "Dream Blocks", "Coins", "Strawberry Seeds",
            "Sinking Platforms", "Moving Platforms", "Blue Clouds", "Pink Clouds", "Blue Boosters", "Move Blocks", "White Block",
            "Swap Blocks", "Red Boosters", "Dash Switches", "Seekers", "Theo Crystal", "Blue Torches", "Yellow Torches",
            "Feathers", "Bumpers", "Kevins", "Badeline Boosters",
            "Double Dash Refills", "Yellow Cassette Blocks", "Green Cassette Blocks", "Fire and Ice Balls", "Core Toggles", "Core Blocks",
            "Pufferfish", "Jellyfish", "Breaker Boxes", "Bird",
        };

        internal static readonly Dictionary<string, (string tex, string tint)> Icons = new Dictionary<string, (string, string)> {
            { "Springs", ("objects/spring/00", null) }, { "Traffic Blocks", ("objects/zipmover/light01", null) },
            { "Dash Refills", ("objects/refill/idle00", null) }, { "Double Dash Refills", ("objects/refillTwo/idle00", null) },
            { "Pink Cassette Blocks", ("objects/cassetteblock/solid", "f049be") }, { "Blue Cassette Blocks", ("objects/cassetteblock/solid", "49aaf0") },
            { "Yellow Cassette Blocks", ("objects/cassetteblock/solid", "fcdc3a") }, { "Green Cassette Blocks", ("objects/cassetteblock/solid", "38e04e") },
            { "Dream Blocks", ("objects/dreamblock/particles", null) }, { "Coins", ("objects/touchswitch/icon00", null) },
            { "Strawberry Seeds", ("collectables/strawberry/seed00", null) }, { "Sinking Platforms", ("objects/woodPlatform/default", null) },
            { "Moving Platforms", ("objects/woodPlatform/cliffside", null) }, { "Blue Clouds", ("objects/clouds/cloud00", null) },
            { "Pink Clouds", ("objects/clouds/fragile00", null) }, { "Blue Boosters", ("objects/booster/booster00", null) },
            { "Red Boosters", ("objects/booster/boosterRed00", null) }, { "Move Blocks", ("objects/moveBlock/base", null) },
            { "White Block", ("objects/whiteblock", null) }, { "Swap Blocks", ("objects/swapblock/block", null) },
            { "Dash Switches", ("objects/temple/dashButton00", null) }, { "Seekers", ("characters/monsters/predator00", null) },
            { "Theo Crystal", ("characters/theoCrystal/idle00", null) }, { "Blue Torches", ("objects/temple/litTorch00", "6aa7ff") },
            { "Yellow Torches", ("objects/temple/litTorch00", "ffd35c") }, { "Feathers", ("objects/flyFeather/idle00", null) },
            { "Bumpers", ("objects/Bumper/Idle22", null) }, { "Kevins", ("objects/crushblock/idle_face", null) },
            { "Badeline Boosters", ("objects/badelineboost/idle00", null) }, { "Fire and Ice Balls", ("objects/fireball/fireball00", null) },
            { "Core Toggles", ("objects/coreFlipSwitch/switch01", null) }, { "Core Blocks", ("objects/BumpBlockNew/fire00", null) },
            { "Pufferfish", ("objects/puffer/idle00", null) }, { "Jellyfish", ("objects/glider/idle0", null) },
            { "Breaker Boxes", ("objects/breakerBox/Idle00", null) }, { "Bird", ("characters/bird/fly00", null) },
        };

        public ApItemsPage(OuiJournal journal) : base(journal) { }

        protected override void DrawContent() {
            Title(Dialog.Clean("CMR_JOURNAL_ITEMS_TITLE"), 1540f, 1f);
            bool shuffled = ApState.Slot.Option("interactable_shuffle", 0) != 0;
            if (!shuffled) Label(Dialog.Clean("CMR_JOURNAL_ITEMS_NOT_SHUFFLED"), new Vector2(805f, 960f), 0.45f);
            const int cols = 9;
            const float colW = 160f, rowH = 174f, x0 = 120f, y0 = 140f;
            for (int i = 0; i < Order.Length; i++) {
                int r = i / cols, c = i % cols;
                if (c == 0) Draw.Rect(40f, y0 + r * rowH + 92f, colW * cols + 40f, 86f, Stripe);
                var name = Order[i];
                var center = new Vector2(x0 + c * colW, y0 + r * rowH);
                Label(Wrap(name), center + new Vector2(0f, 45f), 0.36f);
                bool owned = !shuffled || ApState.Has(name);
                if (name.EndsWith("Cassette Blocks") && Icons.TryGetValue(name, out var cb)) {
                    // the texture is a tile sheet: four corner tiles make a closed block
                    var t = ApJournal.Tex(GFX.Game, cb.tex);
                    if (t != null) {
                        int ts = 8, w = 24, hh = 24; // 4x4 tile sheet: the outer 3x3 tiles are the block, column 3 holds inner corners
                        var tint = owned ? Calc.HexToColor(cb.tint) : Greyed;
                        var pos = center + new Vector2(0f, 135f);
                        foreach (var (sx, sy, ox, oy) in new[] { (0, 0, -1, -1), (w - ts, 0, 1, -1), (0, hh - ts, -1, 1), (w - ts, hh - ts, 1, 1) })
                            Sprite(t.GetSubtexture(sx, sy, ts, ts), pos + new Vector2(ox * 20f, oy * 20f), 40f, owned, 0f, SpriteEffects.None, tint);
                    }
                    continue;
                }
                if (name == "Dream Blocks") {
                    var pos = center + new Vector2(0f, 135f);
                    Draw.Rect(pos.X - 40f, pos.Y - 40f, 80f, 80f, owned ? Color.Black : Greyed);
                    Draw.HollowRect(pos.X - 40f, pos.Y - 40f, 80f, 80f, owned ? Color.White : Color.Gray);
                    Draw.HollowRect(pos.X - 39f, pos.Y - 39f, 78f, 78f, owned ? Color.White : Color.Gray);
                    continue;
                }
                if (Icons.TryGetValue(name, out var ic)) {
                    Sprite(ApJournal.Tex(GFX.Game, ic.tex), center + new Vector2(0f, 135f), 88f, owned, 0f, SpriteEffects.None,
                        ic.tint != null ? Calc.HexToColor(ic.tint) : (Color?)null);
                }
            }
        }
    }
}
