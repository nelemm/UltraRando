// CMR (phase B): slot_data model and bounded parser (contract: ARCHITECTURE.md, "Contrat figé (phase B)").
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Celeste.Mod.UltraRando.AP {
    public class LayoutRoom {
        public string Room;     // vanilla level name
        public string Variant;  // rooms.json key ("6a", "6a#2"); may be null -> Room
        public int X, Y;        // pixels, top-left
    }

    public class LayoutLink {
        public string A, B;
        public int AHole, BHole;
        /// <summary>Optional logical direction(s) from the apworld: "ab", "ba", "both", "none" (null = not given).</summary>
        public string Dir;
        /// <summary>Optional: rooms ("a", "b", "ab") that must not be entered through this link (entry obstructed
        /// by a lock, a breakable wall, clutter...): the mod closes the other room's hole behind the link.</summary>
        public string NoEnter;
        /// <summary>Optional: rooms whose hole of this link has an obstacle right behind it (lock, breakable wall,
        /// clutter) that must be cleared from inside first (self-test only).</summary>
        public string Obstructed;
    }

    public class LayoutHole {
        public string Room;
        public int Hole;
    }

    public class ChapterLayout {
        public List<LayoutRoom> Rooms = new List<LayoutRoom>();
        public List<LayoutLink> Links = new List<LayoutLink>();
        public List<LayoutHole> ClosedHoles = new List<LayoutHole>();
    }

    public class SlotData {
        public const long MaxFileBytes = 64L * 1024 * 1024;
        const int MaxName = 128;
        const int MaxListItems = 20000;
        const int MaxLocations = 200000;

        public int Version;
        public Dictionary<string, int> Options = new Dictionary<string, int>();
        public Dictionary<string, string> StringOptions = new Dictionary<string, string>();
        public Dictionary<string, ChapterLayout> Chapters = new Dictionary<string, ChapterLayout>();
        public Dictionary<string, long> LocationIds = new Dictionary<string, long>();
        public HashSet<string> ActiveAreas = new HashSet<string>();
        /// <summary>Offline test only: null = every item owned.</summary>
        public List<string> OwnedItems;
        /// <summary>checkpointsanity: checkpoint item name -> "area|level" ("Forsaken City A - Crossing" -> "1a|6").</summary>
        public Dictionary<string, string> Checkpoints = new Dictionary<string, string>();
        /// <summary>keysanity: "area|level" of a room with lockBlocks -> the key items that open them.</summary>
        public Dictionary<string, List<string>> Locks = new Dictionary<string, List<string>>();

        public int Option(string name, int def = 0) => Options.TryGetValue(name, out var v) ? v : def;

        public static SlotData LoadFile(string path) {
            var fi = new FileInfo(path);
            if (!fi.Exists) throw new FileNotFoundException("slot_data file not found", path);
            if (fi.Length > MaxFileBytes) throw new InvalidDataException($"slot_data file too large ({fi.Length} bytes)");
            return Parse(File.ReadAllBytes(path));
        }

        public static SlotData Parse(string json) => Parse(Encoding.UTF8.GetBytes(json ?? "{}"));

        public static SlotData Parse(byte[] utf8) {
            if (utf8.Length > MaxFileBytes) throw new InvalidDataException("slot_data too large");
            var opts = new JsonDocumentOptions { MaxDepth = 32, AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            using var doc = JsonDocument.Parse(utf8, opts);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("slot_data is not an object");
            var sd = new SlotData();
            sd.Version = GetInt(root, "version", 0);

            if (root.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Object) {
                foreach (var p in options.EnumerateObject()) {
                    if (sd.Options.Count > 256) break;
                    int? v = AsInt(p.Value);
                    if (v != null) sd.Options[Clean(p.Name)] = v.Value;
                    else if (p.Value.ValueKind == JsonValueKind.String) sd.StringOptions[Clean(p.Name)] = Clean(p.Value.GetString());
                }
            }

            if (root.TryGetProperty("active_areas", out var active) && active.ValueKind == JsonValueKind.Array) {
                foreach (var a in active.EnumerateArray()) {
                    if (a.ValueKind == JsonValueKind.String && sd.ActiveAreas.Count < 256) sd.ActiveAreas.Add(Clean(a.GetString()));
                }
            }

            if (root.TryGetProperty("location_ids", out var locs) && locs.ValueKind == JsonValueKind.Object) {
                foreach (var p in locs.EnumerateObject()) {
                    if (sd.LocationIds.Count >= MaxLocations) break;
                    if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt64(out long id)) {
                        sd.LocationIds[Clean(p.Name, 256)] = id;
                    }
                }
            }

            if (root.TryGetProperty("checkpoints", out var cps) && cps.ValueKind == JsonValueKind.Object) {
                foreach (var p in cps.EnumerateObject()) {
                    if (p.Value.ValueKind == JsonValueKind.String && sd.Checkpoints.Count < MaxListItems) sd.Checkpoints[Clean(p.Name)] = Clean(p.Value.GetString());
                }
            }
            if (root.TryGetProperty("locks", out var lks) && lks.ValueKind == JsonValueKind.Object) {
                foreach (var p in lks.EnumerateObject()) {
                    if (p.Value.ValueKind != JsonValueKind.Array || sd.Locks.Count >= MaxListItems) continue;
                    sd.Locks[Clean(p.Name)] = p.Value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => Clean(v.GetString())).Take(16).ToList();
                }
            }
            if (root.TryGetProperty("owned_items", out var owned) && owned.ValueKind == JsonValueKind.Array) {
                sd.OwnedItems = new List<string>();
                foreach (var it in owned.EnumerateArray()) {
                    if (it.ValueKind == JsonValueKind.String && sd.OwnedItems.Count < MaxListItems) sd.OwnedItems.Add(Clean(it.GetString()));
                }
            }

            if (root.TryGetProperty("chapters", out var chapters) && chapters.ValueKind == JsonValueKind.Object) {
                foreach (var cp in chapters.EnumerateObject()) {
                    if (sd.Chapters.Count >= 256 || cp.Value.ValueKind != JsonValueKind.Object) continue;
                    var ch = new ChapterLayout();
                    var c = cp.Value;
                    if (c.TryGetProperty("rooms", out var rooms) && rooms.ValueKind == JsonValueKind.Array) {
                        foreach (var r in rooms.EnumerateArray()) {
                            if (ch.Rooms.Count >= MaxListItems || r.ValueKind != JsonValueKind.Object) continue;
                            var room = GetStr(r, "room");
                            if (room == null) continue;
                            ch.Rooms.Add(new LayoutRoom {
                                Room = room, Variant = GetStr(r, "variant"),
                                X = GetInt(r, "x", 0), Y = GetInt(r, "y", 0),
                            });
                        }
                    }
                    if (c.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array) {
                        foreach (var l in links.EnumerateArray()) {
                            if (ch.Links.Count >= MaxListItems || l.ValueKind != JsonValueKind.Object) continue;
                            var a = GetStr(l, "a"); var b = GetStr(l, "b");
                            if (a == null || b == null) continue;
                            ch.Links.Add(new LayoutLink { A = a, B = b, AHole = GetInt(l, "a_hole", -1), BHole = GetInt(l, "b_hole", -1),
                                                       Dir = GetStr(l, "dir"), NoEnter = GetStr(l, "no_enter"),
                                                       Obstructed = GetStr(l, "obstructed") });
                        }
                    }
                    if (c.TryGetProperty("closed_holes", out var closed) && closed.ValueKind == JsonValueKind.Array) {
                        foreach (var h in closed.EnumerateArray()) {
                            if (ch.ClosedHoles.Count >= MaxListItems || h.ValueKind != JsonValueKind.Object) continue;
                            var room = GetStr(h, "room");
                            if (room == null) continue;
                            ch.ClosedHoles.Add(new LayoutHole { Room = room, Hole = GetInt(h, "hole", -1) });
                        }
                    }
                    sd.Chapters[Clean(cp.Name)] = ch;
                }
            }
            return sd;
        }

        static string Clean(string s, int max = MaxName) => Text.Sanitize(s, max);

        static string GetStr(JsonElement e, string name) {
            if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String) return Clean(v.GetString());
            return null;
        }

        static int GetInt(JsonElement e, string name, int def) {
            if (e.TryGetProperty(name, out var v)) return AsInt(v) ?? def;
            return def;
        }

        static int? AsInt(JsonElement v) {
            switch (v.ValueKind) {
                case JsonValueKind.Number:
                    if (v.TryGetInt32(out int i)) return i;
                    if (v.TryGetDouble(out double d) && d > int.MinValue && d < int.MaxValue) return (int)d;
                    return null;
                case JsonValueKind.True: return 1;
                case JsonValueKind.False: return 0;
                default: return null;
            }
        }
    }

    /// <summary>Bounded handling of every string that comes from the server or a slot_data file.</summary>
    public static class Text {
        public static string Sanitize(string s, int max = 64) {
            if (string.IsNullOrEmpty(s)) return "";
            if (max < 1) max = 1;
            var sb = new StringBuilder(Math.Min(s.Length, max + 1));
            foreach (char ch in s) {
                if (sb.Length >= max) break;
                if (char.IsControl(ch) || char.IsSurrogate(ch) || ch == '﻿' || (ch >= '​' && ch <= '‏') || (ch >= '‪' && ch <= '‮')) continue;
                sb.Append(ch);
            }
            if (s.Length > max && sb.Length >= max) {
                sb.Length = Math.Max(0, max - 3);
                sb.Append("...");
            }
            return sb.ToString();
        }

        /// <summary>Filename-safe token: [A-Za-z0-9_-], bounded.</summary>
        public static string FileToken(string s, int max = 40) {
            var sb = new StringBuilder();
            foreach (char ch in s ?? "") {
                if (sb.Length >= max) break;
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_') sb.Append(ch);
                else sb.Append('_');
            }
            return sb.Length == 0 ? "_" : sb.ToString();
        }
    }
}
