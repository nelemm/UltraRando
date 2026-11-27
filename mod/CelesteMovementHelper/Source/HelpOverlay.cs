using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CelesteMovementHelper;

/// <summary>
/// HUD panel, toggled with F6 (configurable):
///   page 1 = what the randomizer logic expects in the current room (and what you already have),
///   page 2 = technique glossary with explanations and sources. F7/F8 scroll or change technique.
/// </summary>
public class HelpOverlay : Entity
{
    public static HelpOverlay Current { get; private set; }

    private enum Page { Hidden, Room, Techniques }

    private Page page = Page.Hidden;
    private int scroll;
    private int techniqueIndex;

    private static readonly Color Green = Calc.HexToColor("7CE07C");
    private static readonly Color Red = Calc.HexToColor("FF8080");
    private static readonly Color Gold = Calc.HexToColor("FFD866");
    private static readonly Color Grey = Calc.HexToColor("B8B8B8");
    private static readonly Color Cyan = Calc.HexToColor("80D8FF");

    private readonly struct Line
    {
        public readonly string Text;
        public readonly Color Color;
        public readonly int Indent;
        public Line(string text, Color color, int indent = 0) { Text = text; Color = color; Indent = indent; }
    }

    public HelpOverlay()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate | Tags.TransitionUpdate;
        Depth = -100000;
        Current = this;
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        if (Current == this) Current = null;
    }

    public override void Update()
    {
        base.Update();
        CelesteMovementHelperSettings s = CelesteMovementHelperModule.Settings;
        if (s == null) return;

        if (s.ToggleHelp != null && s.ToggleHelp.Pressed)
        {
            s.ToggleHelp.ConsumePress();
            page = page switch { Page.Hidden => Page.Room, Page.Room => Page.Techniques, _ => Page.Hidden };
            scroll = 0;
        }
        if (page == Page.Hidden) return;

        int delta = 0;
        if (s.NextEntry != null && s.NextEntry.Pressed) { s.NextEntry.ConsumePress(); delta = 1; }
        if (s.PreviousEntry != null && s.PreviousEntry.Pressed) { s.PreviousEntry.ConsumePress(); delta = -1; }
        if (delta == 0) return;

        if (page == Page.Techniques && TechniqueData.All.Count > 0)
        {
            techniqueIndex = (techniqueIndex + delta + TechniqueData.All.Count) % TechniqueData.All.Count;
            scroll = 0;
        }
        else
        {
            scroll = Math.Max(0, scroll + delta * 5);
        }
    }

    public override void Render()
    {
        base.Render();
        if (page == Page.Hidden) return;

        List<Line> lines = page == Page.Room ? BuildRoomLines() : BuildTechniqueLines();
        DrawPanel(lines);
    }

    // ---------------------------------------------------------------- text helpers

    private static string T(string key, string fallback)
        => Dialog.Has(key) ? Dialog.Clean(key) : fallback;

    private static bool French => Settings.Instance?.Language == "french";

    private static string TokenLabel(string token, TokenInfo info)
    {
        if (token.StartsWith("any_dash_"))
        {
            IEnumerable<string> dirs = token.Substring(9).Split('_').Select(d => T("CMH_DIR_" + d.ToUpperInvariant(), d));
            return T("CMH_ANY_DASH", "Dash") + " (" + string.Join(" / ", dirs) + ")";
        }
        string key = "CMH_TOK_" + token.ToUpperInvariant().Replace(' ', '_');
        if (Dialog.Has(key)) return Dialog.Clean(key);
        return info?.Name ?? token;
    }

    private static string LogicName(LogicLevel l) => l switch
    {
        LogicLevel.DeveloperIntended => T("CMH_LOGIC_DI", "Developer Intended"),
        LogicLevel.AssistMode => T("CMH_LOGIC_AS", "Assist Mode"),
        _ => T("CMH_LOGIC_VM", "Vanilla Movement"),
    };

    // ---------------------------------------------------------------- page 1: room logic

    private void AddRule(List<Line> lines, List<List<string>> rule, LevelInfo level, string levelKey)
    {
        if (rule == null || rule.Count == 0)
        {
            lines.Add(new Line(T("CMH_FREE", "no requirement"), Green, 2));
            return;
        }

        bool first = true;
        foreach (List<string> alt in rule)
        {
            var missing = new List<string>();
            bool unknown = false;
            var labels = new List<string>();
            foreach (string token in alt)
            {
                TokenInfo info = LogicData.GetToken(level, token);
                labels.Add(TokenLabel(token, info));
                switch (OpenWorldBridge.Has(token, info, levelKey))
                {
                    case Ownership.Missing: missing.Add(TokenLabel(token, info)); break;
                    case Ownership.Unknown: unknown = true; break;
                }
            }

            string prefix = first ? "" : T("CMH_OR", "or") + " ";
            first = false;
            string text = prefix + string.Join(" + ", labels);
            Color color;
            if (missing.Count > 0)
            {
                text += "   [" + T("CMH_MISSING", "missing") + ": " + string.Join(", ", missing) + "]";
                color = Red;
            }
            else
            {
                color = unknown ? Gold : Green;
            }
            lines.Add(new Line(text, color, 2));
        }
    }

    private List<Line> BuildRoomLines()
    {
        var lines = new List<Line>();
        Level scene = Scene as Level;
        Session session = scene?.Session;
        string levelKey = LogicData.LevelKey(session);
        LevelInfo level = LogicData.GetLevel(levelKey);
        LogicLevel logic = CelesteMovementHelperModule.Settings.Logic;

        if (level == null)
        {
            lines.Add(new Line(T("CMH_NOT_VANILLA", "No randomizer logic for this map (vanilla chapters only)."), Grey));
            return lines;
        }

        string roomName = session.Level;
        lines.Add(new Line($"{level.Name} - {T("CMH_ROOM", "room")} {roomName}", Cyan));
        lines.Add(new Line($"{T("CMH_LOGIC", "Logic")}: {LogicName(logic)}   ({T("CMH_LOGIC_HINT", "change it in Mod Options to match your YAML")})", Grey));
        if (!OpenWorldBridge.Available)
            lines.Add(new Line(T("CMH_NO_OPENWORLD", "Archipelago Open World mod not found: owned items are unknown (gold)."), Gold));
        else if (!OpenWorldBridge.Connected)
            lines.Add(new Line(T("CMH_NOT_CONNECTED", "Not connected to Archipelago: owned items may be out of date."), Gold));

        if (CelesteMovementHelperModule.Settings.WallJumpShuffle)
        {
            bool locked = CelesteMovementHelperModule.WallJumpLocked;
            lines.Add(new Line($"{T("CMH_WALLJUMP", "Wall Jump")}: {(locked ? T("CMH_LOCKED", "locked") : T("CMH_UNLOCKED", "unlocked"))}",
                locked ? Red : Green));
        }

        if (!level.Rooms.TryGetValue(roomName, out RoomInfo room))
        {
            lines.Add(new Line(T("CMH_ROOM_UNKNOWN", "This room is not described by the logic."), Grey));
            return lines;
        }

        lines.Add(new Line(T("CMH_LEGEND", "Green = you can, Red = missing item, Gold = cannot check"), Grey));

        if (room.Connections.Count > 0)
        {
            lines.Add(new Line(T("CMH_PASSAGES", "Paths inside the room (region -> region):"), Color.White));
            foreach (ConnectionInfo c in room.Connections)
            {
                lines.Add(new Line($"{c.From} -> {c.To}", Color.White, 1));
                AddRule(lines, c.For(logic), level, levelKey);
            }
        }

        if (room.Locations.Count > 0)
        {
            lines.Add(new Line(T("CMH_LOCATIONS", "Checks in this room:"), Color.White));
            foreach (LocationInfo l in room.Locations)
            {
                lines.Add(new Line($"{l.Name}  ({T("CMH_FROM_REGION", "from")} {l.Region})", Color.White, 1));
                AddRule(lines, l.For(logic), level, levelKey);
            }
        }

        if (room.Doors.Count > 0)
        {
            string doors = string.Join(", ", room.Doors.Select(d => $"{d.Name} ({T("CMH_DIR_DOOR_" + d.Dir.ToUpperInvariant(), d.Dir)})"));
            lines.Add(new Line($"{T("CMH_DOORS", "Exits")}: {doors}", Grey));
        }

        lines.Add(new Line(T("CMH_ROOM_FOOTER", "F7/F8: scroll - F6: technique glossary"), Grey));
        return lines;
    }

    // ---------------------------------------------------------------- page 2: techniques

    private List<Line> BuildTechniqueLines()
    {
        var lines = new List<Line>();
        if (TechniqueData.All.Count == 0)
        {
            lines.Add(new Line(T("CMH_NO_TECH", "No technique data."), Grey));
            return lines;
        }

        Technique t = TechniqueData.All[techniqueIndex];
        lines.Add(new Line($"{t.Name}   ({techniqueIndex + 1}/{TechniqueData.All.Count})", Cyan));
        if (t.Aka.Count > 0)
            lines.Add(new Line($"{T("CMH_AKA", "Also called")}: {string.Join(", ", t.Aka)}", Grey));
        lines.Add(new Line($"{T("CMH_CATEGORY", "Level")}: {T("CMH_CAT_" + t.Category.ToUpperInvariant(), t.Category)}", Gold));
        if (t.Requires.Count > 0)
            lines.Add(new Line($"{T("CMH_REQUIRES", "Needs")}: {string.Join(", ", t.Requires.Select(r => T("CMH_ABIL_" + r.ToUpperInvariant(), r)))}", Color.White));

        string explanation = French || string.IsNullOrEmpty(t.ExplanationEn) ? t.ExplanationFr : t.ExplanationEn;
        if (!string.IsNullOrEmpty(explanation)) lines.Add(new Line(explanation, Color.White));

        if (t.Inputs.Count > 0)
        {
            lines.Add(new Line(T("CMH_INPUTS", "Inputs:"), Color.White));
            for (int i = 0; i < t.Inputs.Count; i++)
                lines.Add(new Line($"{i + 1}. {t.Inputs[i]}", Color.White, 1));
        }
        if (!string.IsNullOrEmpty(t.Timing))
            lines.Add(new Line($"{T("CMH_TIMING", "Timing")}: {t.Timing}", Color.White));
        if (!string.IsNullOrEmpty(t.VanillaWhere))
            lines.Add(new Line($"{T("CMH_WHERE", "Taught / first needed")}: {t.VanillaWhere}", Grey));

        if (t.Verified && t.Sources.Count > 0)
        {
            lines.Add(new Line(T("CMH_SOURCES", "Sources:"), Color.White));
            foreach (TechniqueSource src in t.Sources)
                lines.Add(new Line($"{src.Title} - {src.Url}", Cyan, 1));
        }
        else
        {
            lines.Add(new Line(T("CMH_UNVERIFIED", "Not verified: no reliable source found."), Red));
        }

        lines.Add(new Line(T("CMH_TECH_FOOTER", "F7/F8: next/previous technique - F6: close"), Grey));
        return lines;
    }

    // ---------------------------------------------------------------- drawing

    private static List<string> Wrap(string text, float maxWidth, float scale)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        foreach (string word in text.Split(' '))
        {
            string candidate = sb.Length == 0 ? word : sb + " " + word;
            if (sb.Length > 0 && ActiveFont.Measure(candidate).X * scale > maxWidth)
            {
                result.Add(sb.ToString());
                sb.Clear();
                sb.Append(word);
            }
            else
            {
                sb.Clear();
                sb.Append(candidate);
            }
        }
        if (sb.Length > 0) result.Add(sb.ToString());
        return result;
    }

    private void DrawPanel(List<Line> lines)
    {
        float scale = CelesteMovementHelperModule.Settings.TextScale / 10f;
        const float left = 40f, top = 40f, width = 1840f, maxHeight = 1000f, indentStep = 40f;
        float lineHeight = ActiveFont.LineHeight * scale;

        var wrapped = new List<Line>();
        foreach (Line l in lines)
        {
            float maxWidth = width - 40f - l.Indent * indentStep;
            List<string> parts = Wrap(l.Text, maxWidth, scale);
            for (int i = 0; i < parts.Count; i++)
                wrapped.Add(new Line(parts[i], l.Color, l.Indent + (i > 0 ? 1 : 0)));
        }

        int visible = Math.Max(1, (int)((maxHeight - 40f) / lineHeight));
        int maxScroll = Math.Max(0, wrapped.Count - visible);
        if (scroll > maxScroll) scroll = maxScroll;

        int count = Math.Min(visible, wrapped.Count - scroll);
        float height = count * lineHeight + 40f;
        Draw.Rect(left, top, width, height, Color.Black * 0.8f);

        float y = top + 20f;
        for (int i = scroll; i < scroll + count; i++)
        {
            Line l = wrapped[i];
            ActiveFont.DrawOutline(l.Text, new Vector2(left + 20f + l.Indent * indentStep, y), Vector2.Zero,
                Vector2.One * scale, l.Color, 2f, Color.Black);
            y += lineHeight;
        }

        if (maxScroll > 0)
        {
            string more = $"{scroll + count}/{wrapped.Count}";
            ActiveFont.DrawOutline(more, new Vector2(left + width - 20f, top + height - 10f), new Vector2(1f, 1f),
                Vector2.One * scale * 0.8f, Grey, 2f, Color.Black);
        }
    }
}
