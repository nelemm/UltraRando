// CMR: data of the F6 help panel, ported from our V1 helper mod (mod/CelesteMovementHelper).
//   HelpData/logic.json      = per-room logic of Celeste (Open World) 1.1.3 by PoryGone (Modified MIT), exported by
//                              tools/export_help_data.py. A rule is a list of alternatives (OR) of tokens (AND);
//                              an empty rule means "no requirement", exactly like the apworld.
//   HelpData/techniques.json = docs/techniques.json (named techniques with their sources).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Celeste.Mod.UltraRando.AP {
    public class HelpToken {
        [JsonPropertyName("kind")] public string Kind { get; set; } = "other";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("id")] public long? Id { get; set; }
    }

    public class HelpRuleSet {
        [JsonPropertyName("di")] public List<List<string>> DeveloperIntended { get; set; } = new();
        [JsonPropertyName("vm")] public List<List<string>> VanillaMovement { get; set; } = new();
        [JsonPropertyName("as")] public List<List<string>> Assist { get; set; } = new();

        /// <summary>0 = Developer Intended, 1 = Vanilla Movement, 2 = Assist (slot option logic_difficulty).</summary>
        public List<List<string>> For(int logic) => logic switch {
            1 => VanillaMovement ?? new(),
            2 => Assist ?? new(),
            _ => DeveloperIntended ?? new(),
        };
    }

    public class HelpConnection : HelpRuleSet {
        [JsonPropertyName("from")] public string From { get; set; } = "";
        [JsonPropertyName("to")] public string To { get; set; } = "";
    }

    public class HelpLocation : HelpRuleSet {
        [JsonPropertyName("region")] public string Region { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
    }

    public class HelpDoor {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("dir")] public string Dir { get; set; } = "";
    }

    public class HelpRoom {
        [JsonPropertyName("connections")] public List<HelpConnection> Connections { get; set; } = new();
        [JsonPropertyName("locations")] public List<HelpLocation> Locations { get; set; } = new();
        [JsonPropertyName("doors")] public List<HelpDoor> Doors { get; set; } = new();
    }

    public class HelpLevel {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("tokens")] public Dictionary<string, HelpToken> Tokens { get; set; } = new();
        [JsonPropertyName("rooms")] public Dictionary<string, HelpRoom> Rooms { get; set; } = new();
    }

    public class HelpLogicFile {
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        [JsonPropertyName("tokens")] public Dictionary<string, HelpToken> Tokens { get; set; } = new();
        [JsonPropertyName("levels")] public Dictionary<string, HelpLevel> Levels { get; set; } = new();
    }

    public class TechniqueSource {
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    public class Technique {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("aka")] public List<string> Aka { get; set; } = new();
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
        [JsonPropertyName("inputs")] public List<string> Inputs { get; set; } = new();
        [JsonPropertyName("timing")] public string Timing { get; set; } = "";
        [JsonPropertyName("explanation_fr")] public string ExplanationFr { get; set; } = "";
        [JsonPropertyName("explanation_en")] public string ExplanationEn { get; set; } = "";
        [JsonPropertyName("vanilla_where")] public string VanillaWhere { get; set; } = "";
        [JsonPropertyName("sources")] public List<TechniqueSource> Sources { get; set; } = new();
        [JsonPropertyName("verified")] public bool Verified { get; set; }
    }

    class TechniqueFile {
        [JsonPropertyName("techniques")] public List<Technique> Techniques { get; set; } = new();
    }

    public static class HelpData {
        static readonly string[] CategoryOrder = { "basic", "intermediate", "advanced", "expert" };

        public static HelpLogicFile Logic { get; private set; }
        public static List<Technique> Techniques { get; private set; } = new();

        static T LoadResource<T>(string name) where T : class {
            try {
                using Stream s = typeof(HelpData).Assembly.GetManifestResourceStream(name);
                if (s == null) { ApState.Log("help: embedded " + name + " not found"); return null; }
                return JsonSerializer.Deserialize<T>(s);
            } catch (Exception e) {
                ApState.Log("help: could not load " + name + ": " + e.Message);
                return null;
            }
        }

        public static void Load() {
            Logic = LoadResource<HelpLogicFile>("CMR.help_logic.json");
            Techniques = LoadResource<TechniqueFile>("CMR.techniques.json")?.Techniques ?? new();
            Techniques.Sort((a, b) => {
                int c = Array.IndexOf(CategoryOrder, a.Category).CompareTo(Array.IndexOf(CategoryOrder, b.Category));
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            ApState.Log($"help: logic for {Logic?.Levels.Count ?? 0} levels, {Techniques.Count} techniques");
        }

        /// <summary>"1a", "10c"… for a vanilla chapter, else null.</summary>
        public static string LevelKey(Session session) {
            if (session == null) return null;
            AreaData area = AreaData.Get(session.Area);
            if (area?.SID == null || !area.SID.StartsWith("Celeste/")) return null;
            int mode = (int)session.Area.Mode;
            if (mode < 0 || mode > 2) return null;
            return $"{session.Area.ID}{(char)('a' + mode)}";
        }

        public static HelpLevel GetLevel(string key)
            => key != null && Logic != null && Logic.Levels.TryGetValue(key, out var l) ? l : null;

        public static HelpToken GetToken(HelpLevel level, string token) {
            if (level != null && level.Tokens.TryGetValue(token, out var t)) return t;
            if (Logic != null && Logic.Tokens.TryGetValue(token, out t)) return t;
            return null;
        }
    }
}
