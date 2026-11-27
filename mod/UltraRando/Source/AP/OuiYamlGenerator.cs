// CMR: in-game YAML generator. Built from CMRConfig/options_schema.json, the schema exported from the apworld by
// tools/export_options_schema.py (the web generator web/yaml-generator.html uses the same file): one source, so the
// two generators cannot drift apart. Writes <game folder>/UltraRando_yaml/<slot name>.yaml, same format as the web page.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Celeste.Mod.UI;
using Monocle;

namespace Celeste.Mod.UltraRando.AP {
    public class OuiYamlGenerator : GenericOui {
        static RandoModuleSettings S => RandoModule.Instance.SavedData;

        public class Opt {
            public string Key, Group, Name, Doc, Type, Default;
            public List<(string value, string name)> Choices = new();
            public int Min, Max;
        }
        public static List<Opt> Options;
        public static List<(string name, string fr)> Groups;
        public static List<(string name, string fr, Dictionary<string, string> values)> Presets;
        public static string Game = "UltraRando";
        public static string LastWrittenForTest;

        TextMenu.SubHeader status;

        protected override bool IsDeeperThan(Oui other) => other is OuiMainMenu || other is OuiApConnect || other == null;

        /// <summary>Loads the shared schema (once). French texts when the game is in French.</summary>
        public static bool LoadSchema() {
            if (Options != null) return true;
            try {
                if (!Everest.Content.TryGet("CMRConfig/options_schema", out ModAsset asset) && !Everest.Content.TryGet("CMRConfig/options_schema.json", out asset)) { ApState.Log("yaml generator: schema missing"); return false; }
                using var stream = asset.Stream;
                using var doc = JsonDocument.Parse(stream);
                bool fr = global::Celeste.Settings.Instance?.Language == "french";
                var r = doc.RootElement;
                Game = r.GetProperty("game").GetString();
                string Fr(JsonElement e, string en) => fr && e.ValueKind == JsonValueKind.String ? e.GetString() : en;
                Groups = r.GetProperty("groups").EnumerateArray().Select(g => (g.GetProperty("name").GetString(), Fr(g.GetProperty("fr"), g.GetProperty("name").GetString()))).ToList();
                Options = new List<Opt>();
                foreach (var o in r.GetProperty("options").EnumerateArray()) {
                    var frp = o.GetProperty("fr");
                    var opt = new Opt {
                        Key = o.GetProperty("key").GetString(), Group = o.GetProperty("group").GetString(), Type = o.GetProperty("type").GetString(),
                        Name = o.GetProperty("name").GetString(), Doc = o.GetProperty("doc").GetString(),
                    };
                    if (fr && frp.ValueKind == JsonValueKind.Array && frp[0].ValueKind == JsonValueKind.String) { opt.Name = frp[0].GetString(); opt.Doc = frp[1].GetString(); }
                    var def = o.GetProperty("default");
                    opt.Default = def.ValueKind == JsonValueKind.True ? "true" : def.ValueKind == JsonValueKind.False ? "false" : def.ToString();
                    if (opt.Type == "choice")
                        foreach (var c in o.GetProperty("choices").EnumerateArray())
                            opt.Choices.Add((c.GetProperty("value").GetString(), Fr(c.GetProperty("fr"), c.GetProperty("name").GetString())));
                    if (opt.Type == "range") { opt.Min = o.GetProperty("min").GetInt32(); opt.Max = o.GetProperty("max").GetInt32(); }
                    Options.Add(opt);
                }
                Presets = r.GetProperty("presets").EnumerateArray().Select(p => (p.GetProperty("name").GetString(), Fr(p.GetProperty("fr"), p.GetProperty("name").GetString()),
                    p.GetProperty("values").EnumerateObject().ToDictionary(v => v.Name, v => v.Value.ValueKind == JsonValueKind.True ? "true"
                        : v.Value.ValueKind == JsonValueKind.False ? "false" : v.Value.ToString()))).ToList();
                return true;
            } catch (Exception e) { ApState.Log("yaml generator: schema failed: " + e.Message); Options = null; return false; }
        }

        /// <summary>Current values: saved ones, else the defaults (unknown keys of an older schema are dropped).</summary>
        public static Dictionary<string, string> Values() {
            var v = Options.ToDictionary(o => o.Key, o => o.Default);
            foreach (var kv in S.YamlValues ?? new Dictionary<string, string>())
                if (v.ContainsKey(kv.Key) && Valid(Options.First(o => o.Key == kv.Key), kv.Value)) v[kv.Key] = kv.Value;
            return v;
        }

        static bool Valid(Opt o, string v) => o.Type switch {
            "toggle" => v == "true" || v == "false",
            "choice" => o.Choices.Any(c => c.value == v),
            "range" => int.TryParse(v, out int n) && n >= o.Min && n <= o.Max,
            _ => false,
        };

        static void Set(string key, string value) {
            S.YamlValues ??= new Dictionary<string, string>();
            S.YamlValues[key] = value;
            RandoModule.Instance.SaveSettings();
        }

        public static string YamlText(string slot, Dictionary<string, string> values, string preset) {
            var sb = new StringBuilder();
            sb.Append("# ").Append(Game).Append(" — ").Append(preset != null ? "preset " + preset : "custom").Append('\n');
            sb.Append("name: ").Append(JsonSerializer.Serialize(slot)).Append('\n');
            sb.Append("game: ").Append(Game).Append('\n').Append(Game).Append(":\n");
            foreach (var o in Options) sb.Append("  ").Append(o.Key).Append(": ").Append(values[o.Key]).Append('\n');
            return sb.ToString();
        }

        public static string NameError(string slot) {
            if (string.IsNullOrWhiteSpace(slot) || slot.Length > 16) return Dialog.Clean("CMR_YAML_ERR_NAME");
            if (slot.IndexOfAny(new[] { '{', '}', ':', '\n', '\r' }) >= 0) return Dialog.Clean("CMR_YAML_ERR_CHARS");
            return null;
        }

        /// <summary>Writes the YAML; returns the path or throws.</summary>
        public static string Write(string slot) {
            var dir = Path.Combine(Everest.PathGame, "UltraRando_yaml");
            Directory.CreateDirectory(dir);
            var file = new string(slot.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray());
            var path = Path.Combine(dir, (file.Length > 0 ? file : "player") + ".yaml");
            File.WriteAllText(path, YamlText(slot, Values(), S.YamlPreset), new UTF8Encoding(false));
            LastWrittenForTest = path;
            return path;
        }

        protected override Entity ReloadMenu() {
            var menu = new TextMenu();
            menu.Add(new TextMenu.Header(Dialog.Clean("CMR_YAML_TITLE")));
            if (!LoadSchema()) {
                menu.Add(new TextMenu.SubHeader(Dialog.Clean("CMR_YAML_NO_SCHEMA"), false));
                menu.OnCancel = Back;
                return menu;
            }
            // slot name (same text field screen as the connection page)
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("CMR_YAML_NAME")));
            string Shown() => string.IsNullOrEmpty(S.YamlSlot) ? Dialog.Clean("CMR_AP_NONE") : Text.Sanitize(S.YamlSlot, 16);
            menu.Add(new TextMenu.Button(Shown()).Pressed(() => {
                Audio.Play(SFX.ui_main_savefile_rename_start);
                Overworld.Goto<OuiModOptionString>().Init<OuiYamlGenerator>(S.YamlSlot ?? "", v => {
                    S.YamlSlot = Text.Sanitize(v, 16);
                    RandoModule.Instance.SaveSettings();
                }, 16, 0);
            }));
            // presets: set several options at once, everything stays editable
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("CMR_YAML_PRESETS")));
            foreach (var p in Presets) {
                var b = new TextMenu.Button(p.fr + (S.YamlPreset == p.name ? "  *" : ""));
                b.Pressed(() => {
                    Audio.Play(SFX.ui_main_button_select);
                    S.YamlValues = Options.ToDictionary(o => o.Key, o => o.Default);
                    foreach (var kv in p.values) S.YamlValues[kv.Key] = kv.Value;
                    S.YamlPreset = p.name;
                    RandoModule.Instance.SaveSettings();
                    Reload();
                });
                menu.Add(b);
            }
            var values = Values();
            foreach (var g in Groups) {
                menu.Add(new TextMenu.SubHeader(g.fr));
                foreach (var o in Options.Where(o => o.Group == g.name)) {
                    TextMenu.Item item;
                    string key = o.Key;
                    if (o.Type == "toggle") {
                        item = new TextMenu.OnOff(o.Name, values[key] == "true").Change(v => { Set(key, v ? "true" : "false"); S.YamlPreset = null; });
                    } else if (o.Type == "choice") {
                        int idx = Math.Max(0, o.Choices.FindIndex(c => c.value == values[key]));
                        var ch = o.Choices;
                        item = new TextMenu.Slider(o.Name, i => ch[i].name, 0, ch.Count - 1, idx).Change(i => { Set(key, ch[i].value); S.YamlPreset = null; });
                    } else {
                        item = new TextMenu.Slider(o.Name, i => i.ToString(), o.Min, o.Max, int.Parse(values[key])).Change(i => { Set(key, i.ToString()); S.YamlPreset = null; });
                    }
                    menu.Add(item);
                    // descriptions wrapped: one long line widens the whole menu and pushes its lines off screen
                    if (!string.IsNullOrEmpty(o.Doc)) item.AddDescription(menu, Wrap(o.Doc, 70));
                }
            }
            menu.Add(new TextMenu.SubHeader("", false));
            menu.Add(new TextMenu.Button(Dialog.Clean("CMR_YAML_WRITE")).Pressed(() => {
                var err = NameError(S.YamlSlot);
                if (err != null) { Audio.Play(SFX.ui_main_button_invalid); status.Title = err; return; }
                try {
                    var path = Write(S.YamlSlot);
                    Audio.Play(SFX.ui_main_button_select);
                    status.Title = Dialog.Clean("CMR_YAML_WRITTEN") + " " + path;
                } catch (Exception e) {
                    Audio.Play(SFX.ui_main_button_invalid);
                    status.Title = Text.Sanitize(e.Message, 80);
                }
            }));
            status = new TextMenu.SubHeader("", false);
            menu.Add(status);
            menu.OnCancel = Back;
            return menu;
        }

        /// <summary>Rebuild the menu in place (after a preset), keeping the selected line.</summary>
        void Reload() {
            var old = Menu as TextMenu;
            int sel = old?.Selection ?? 0;
            var fresh = (TextMenu)ReloadMenu();
            fresh.X = old?.X ?? 960f;
            Scene.Add(fresh);
            old?.RemoveSelf();
            Menu = fresh;
            fresh.Selection = Math.Min(sel, fresh.Items.Count - 1);
            fresh.Active = true;
        }

        static string Wrap(string text, int width) {
            var sb = new StringBuilder();
            int col = 0;
            foreach (var word in text.Split(' ')) {
                if (col > 0 && col + 1 + word.Length > width) { sb.Append('\n'); col = 0; }
                else if (col > 0) { sb.Append(' '); col++; }
                sb.Append(word); col += word.Length;
            }
            return sb.ToString();
        }

        void Back() {
            Audio.Play(SFX.ui_main_button_back);
            Overworld.Goto<OuiApConnect>();
        }
    }
}
