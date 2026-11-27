// CMR: in-level HUD: small text lines (connection status), big item/check notifications (one at a time, with the
// Celeste icon of the item or of the check), and the room name for 4 seconds on entering a room.
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Enums;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public class ApHud : Entity {
        const int MaxLines = 6;
        const float Duration = 4f;
        public const float RoomNameDuration = 4f;
        const float ToastDuration = 3f, ToastFast = 1.5f;
        const string CelesteGame = "UltraRando";
        static readonly List<(string text, float until)> lines = new List<(string, float)>();
        static float clock;
        static ApHud current;

        // ---------------------------------------------------------------- big notifications
        public class Toast {
            public MTexture Icon; public Color Tint = Color.White;
            public List<(string text, Color color)> Parts = new List<(string, Color)>();
        }
        static readonly Queue<Toast> toasts = new Queue<Toast>();
        static Toast shown;
        static float shownUntil;
        public static Toast LastToastForTest;
        public static int ToastsForTest;

        // ---------------------------------------------------------------- room name
        static string lastRoom;
        static string roomText;
        static float roomUntil;
        static bool roomPending;
        static float cutsceneTime;
        public static int SkippedForTest;
        public static string RoomTextForTest => roomText != null && clock < roomUntil ? roomText : null;
        public static float RoomShownAtForTest;

        public static void Message(string text) {
            text = Text.Sanitize(text, 90);
            if (text.Length == 0) return;
            ApState.Log("hud: " + text);
            lines.Add((text, clock + Duration));
            while (lines.Count > 20) lines.RemoveAt(0);
        }

        public static void Push(Toast t) {
            ApState.Log("toast: " + string.Concat(t.Parts.ConvertAll(p => p.text)));
            toasts.Enqueue(t);
            ToastsForTest++;
            LastToastForTest = t;
            while (toasts.Count > 30) toasts.Dequeue();
        }

        static readonly Color ColAdvancement = Calc.HexToColor("af99ef"), ColUseful = Calc.HexToColor("6d8be8"),
            ColFiller = Calc.HexToColor("00eeee"), ColTrap = Calc.HexToColor("fa8072"),
            ColMe = Calc.HexToColor("ee00ee"), ColOther = Calc.HexToColor("fafad2");

        static Color ItemColor(ItemFlags f) =>
            (f & ItemFlags.Advancement) != 0 ? ColAdvancement : (f & ItemFlags.NeverExclude) != 0 ? ColUseful
            : (f & ItemFlags.Trap) != 0 ? ColTrap : ColFiller;

        /// <summary>An item of the server (main thread): received from another player, found for myself, or sent
        /// to another player. Icon: the Celeste item when it is a Celeste item, else the check it came from when the
        /// check is mine, else the Archipelago icon.</summary>
        public static void ItemEvent(bool toMe, bool fromMe, string item, string itemGame, string location, string other, ItemFlags flags) {
            item = Text.Sanitize(item, 48); other = Text.Sanitize(other, 32); location = Text.Sanitize(location, 64);
            var t = new Toast();
            MTexture icon = null; Color tint = Color.White;
            if (itemGame == CelesteGame) (icon, tint) = ItemIcon(item);
            if (icon == null && fromMe) (icon, tint) = LocationIcon(location);
            t.Icon = icon ?? ApIcon(); t.Tint = icon != null ? tint : Color.White;
            var ic = ItemColor(flags);
            if (toMe && !fromMe) { t.Parts.Add(("Reçu ", Color.White)); t.Parts.Add((item, ic)); t.Parts.Add((" de ", Color.White)); t.Parts.Add((other, ColOther)); }
            else if (toMe) { t.Parts.Add(("Trouvé ", Color.White)); t.Parts.Add((item, ic)); }
            else { t.Parts.Add(("Envoyé ", Color.White)); t.Parts.Add((item, ic)); t.Parts.Add((" à ", Color.White)); t.Parts.Add((other, ColOther)); }
            t.Parts.Add((".", Color.White));
            Push(t);
        }

        /// <summary>A check made without a server (offline mode): the check's icon and its name.</summary>
        public static void CheckEvent(string key, string locationName) {
            var (icon, tint) = LocationIcon(locationName ?? key);
            var t = new Toast { Icon = icon ?? ApIcon(), Tint = icon != null ? tint : Color.White };
            t.Parts.Add(("Check : ", Color.White)); t.Parts.Add((Text.Sanitize(locationName ?? key, 64), ColFiller));
            Push(t);
        }

        static MTexture ApIcon() => ApJournal.Tex(GFX.Gui, "cmr/archipelago");
        static MTexture G(string path) => ApJournal.Tex(GFX.Game, path);
        static MTexture Hd(string gui, string game) => ApJournal.Tex(GFX.Gui, gui) ?? G(game); // chapter-panel size icon, else the sprite

        public static (MTexture, Color) ItemIcon(string item) {
            if (item == null) return (null, Color.White);
            if (ApItemsPage.Icons.TryGetValue(item, out var ic))
                return (G(ic.tex), ic.tint != null ? Calc.HexToColor(ic.tint) : Color.White);
            if (item.EndsWith(" Dash") || item == ItemNames.Dash) return (G(item == ItemNames.DemoDash ? "characters/player/duck" : "characters/player/dash00"), Color.White);
            if (item.Contains("Climb")) return (G("characters/player/climb00"), Color.White);
            if (item == ItemNames.Crouch) return (G("characters/player/duck"), Color.White);
            if (item == ItemNames.WallJump) return (G("characters/player/jumpFast00"), Color.White);
            if (item == ItemNames.Strawberry) return (Hd("collectables/strawberry", "collectables/strawberry/normal00"), Color.White);
            if (item == "Raspberry") return (G("collectables/ghostberry/idle00") ?? G("collectables/strawberry/normal00"), Color.White);
            if (item.EndsWith(" Key") || item.Contains(" Key ")) return (G("collectables/key/idle00"), Color.White);
            // checkpoint items ("Forsaken City A - Crossing"): listed in slot_data; the only other Celeste items named
            // "<chapter> - <name>" are keys (handled above), so the name is enough when the list is not there
            if ((ApState.Slot != null && ApState.Slot.Checkpoints.ContainsKey(item)) || item.Contains(" - "))
                return (Hd("checkpoint", "objects/checkpoint/flag00"), Color.White);
            return (null, Color.White);
        }

        /// <summary>Icon of a check, from its AP name ("... Room 5 Strawberry") or its slot_data key ("1a|5|strawberry|21").</summary>
        public static (MTexture, Color) LocationIcon(string loc) {
            if (string.IsNullOrEmpty(loc)) return (null, Color.White);
            string l = loc.ToLowerInvariant();
            if (l.Contains("strawberry")) return (Hd("collectables/strawberry", "collectables/strawberry/normal00"), Color.White);
            if (l.Contains("heart")) return (Hd("collectables/heartgem/0/spin00", "collectables/heartgem/0/00"), Color.White);
            if (l.Contains("cassette")) return (Hd("collectables/cassette", "collectables/cassette/idle00"), Color.White);
            if (l.Contains(" key") || l.Contains("|key")) return (G("collectables/key/idle00"), Color.White);
            if (l.Contains("gem")) return (G("collectables/summitgems/0/gem00"), Color.White);
            if (l.Contains("clear")) return (G("objects/checkpoint/flag00"), Color.White);
            return (G("characters/player/idle00"), Color.White); // room check
        }

        public static void Ensure(Level level) {
            if (current != null && current.Scene == level) return;
            current = new ApHud();
            level.Add(current);
        }

        public ApHud() {
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;
            Depth = -100000;
        }

        public override void Update() {
            base.Update();
            clock += Engine.RawDeltaTime;
            lines.RemoveAll(l => l.until < clock);
            if (shown != null && clock >= shownUntil) shown = null;
            if (shown == null && toasts.Count > 0) {
                shown = toasts.Dequeue();
                shownUntil = clock + (toasts.Count >= 4 ? 0.8f : toasts.Count >= 1 ? ToastFast : ToastDuration); // a queue (roomsanity: one check per room) goes faster
            }
            // cutscenes: on our maps, skipped as soon as the game lets you skip them (same as the pause menu's
            // "Skip cutscene"), after a short moment so the cutscene has set itself up
            var cl = Scene as Level;
            if (cl != null && cl.InCutscene && !cl.SkippingCutscene && !cl.Paused && !cl.Transitioning &&
                (RandoModule.Instance?.SavedData?.SkipCutscenes ?? true) && ApHooks.ActiveAreaKey() != null) {
                cutsceneTime += Engine.RawDeltaTime;
                if (cutsceneTime > 0.3f) { cutsceneTime = 0f; SkippedForTest++; ApState.Log("cutscene skipped in " + cl.Session.Level); cl.SkipCutscene(); }
            } else cutsceneTime = 0f;
            // room name: once per room change (a transition loads the level once; a respawn in the same room does not count)
            if (Scene is Level lv && ApState.Active && lv.Session?.Level != null) {
                string key = lv.Session.Area.SID + "|" + lv.Session.Level;
                if (key != lastRoom) {
                    lastRoom = key;
                    roomText = "Room: " + lv.Session.Level;
                    roomPending = true;
                    roomUntil = float.MaxValue;
                }
                // the 4 seconds start once the room is on screen (end of the screen transition)
                if (roomPending && !lv.Transitioning) {
                    roomPending = false;
                    roomUntil = clock + RoomNameDuration;
                    RoomShownAtForTest = clock;
                }
            }
        }

        public static float ClockForTest => clock;
        public static void ClearToastsForTest() { toasts.Clear(); shown = null; }

        public override void Render() {
            base.Render();
            float y = 230f;
            if (ApState.Active && ApState.Mode != ApMode.Online && ApState.SaveMatches(false)) {
                ActiveFont.DrawOutline(ApState.Mode == ApMode.Offline ? "AP : hors ligne (test)" : "AP : déconnecté",
                    new Vector2(20f, y), Vector2.Zero, Vector2.One * 0.4f, Color.Orange, 2f, Color.Black);
                y += 30f;
            }
            int start = System.Math.Max(0, lines.Count - MaxLines);
            for (int i = start; i < lines.Count; i++) {
                ActiveFont.DrawOutline(lines[i].text, new Vector2(20f, y), Vector2.Zero, Vector2.One * 0.5f, Color.White, 2f, Color.Black);
                y += 36f;
            }
            if (shown != null) DrawToast(shown);
            if (roomText != null && clock < roomUntil)
                ActiveFont.DrawOutline(roomText, new Vector2(60f, 1040f), new Vector2(0f, 1f), Vector2.One * 0.8f, Calc.HexToColor("ff4fc8"), 2f, Color.Black);
        }

        static void DrawToast(Toast t) {
            const float x0 = 120f, y0 = 60f, w = 1760f, h = 150f;
            Draw.Rect(x0, y0, w, h, Color.Black * 0.85f);
            Draw.HollowRect(x0, y0, w, h, Color.White);
            Draw.HollowRect(x0 + 1, y0 + 1, w - 2, h - 2, Color.White);
            Draw.HollowRect(x0 + 2, y0 + 2, w - 4, h - 4, Color.White);
            if (t.Icon != null) {
                // trimmed pixels, centred. Gui textures (high resolution) are smoothed and fitted to 110 px; game sprites
                // (pixel art, 8 to 32 px) get a whole-number scale with point sampling so the pixels stay sharp
                var clip = t.Icon.ClipRect;
                int big = System.Math.Max(1, System.Math.Max(clip.Width, clip.Height));
                bool pixel = t.Icon.Atlas != GFX.Gui;
                float s = pixel ? System.Math.Max(1, (int)(110f / big)) : 110f / big;
                if (pixel) {
                    Draw.SpriteBatch.End();
                    Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Engine.ScreenMatrix);
                }
                Draw.SpriteBatch.Draw(t.Icon.Texture.Texture_Safe, new Vector2((int)(x0 + 80f), (int)(y0 + h / 2f)), clip, t.Tint, 0f,
                    new Vector2(clip.Width / 2, clip.Height / 2), s, SpriteEffects.None, 0f);
                if (pixel) {
                    Draw.SpriteBatch.End();
                    Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Engine.ScreenMatrix);
                }
            }
            float total = 0f;
            foreach (var p in t.Parts) total += ActiveFont.Measure(p.text).X;
            float scale = System.Math.Min(0.9f, (w - 200f) / System.Math.Max(1f, total));
            float x = x0 + 165f;
            foreach (var p in t.Parts) {
                ActiveFont.DrawOutline(p.text, new Vector2(x, y0 + h / 2f), new Vector2(0f, 0.5f), Vector2.One * scale, p.color, 2f, Color.Black);
                x += ActiveFont.Measure(p.text).X * scale;
            }
        }
    }
}
