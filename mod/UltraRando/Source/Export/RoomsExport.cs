// UltraRando: export of rhelmot's processed StaticRooms (vanilla areas only) to rooms.json.
// Format documented in mod/UltraRando/EXPORT_FORMAT.md. Keep both in sync.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltraRando {
    public static class RoomsExport {
        public const int FormatVersion = 1;

        public static string DefaultOutputPath => Path.Combine(Everest.PathGame, "CMR_export", "rooms.json");

        // Flag files that trigger an export when area data is loaded. The first one works for a zip install.
        public static IEnumerable<string> FlagFiles => new[] {
            Path.Combine(Everest.PathGame, "CMR_EXPORT_ON_LOAD"),
            Path.Combine(Everest.PathGame, "Mods", "UltraRando", "EXPORT_ON_LOAD"),
        };

        internal static void ExportIfFlagged() {
            try {
                if (FlagFiles.Any(File.Exists)) {
                    Logger.Log(LogLevel.Info, "UltraRando", "Export flag found, exporting rooms.json");
                    Export(DefaultOutputPath);
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Error, "UltraRando", "cmr_export failed: " + e);
            }
        }

        [Command("cmr_export", "UltraRando: export vanilla room geometry/logic to rooms.json (optional arg: output path)")]
        public static void CmdExport(string path = null) {
            try {
                var outPath = string.IsNullOrEmpty(path) ? DefaultOutputPath : path;
                var n = Export(outPath);
                Engine.Commands?.Log($"cmr_export: {n} rooms written to {outPath}");
            } catch (Exception e) {
                Logger.Log(LogLevel.Error, "UltraRando", "cmr_export failed: " + e);
                Engine.Commands?.Log("cmr_export failed: " + e.Message);
            }
        }

        public static string AreaKeyString(AreaKey key) {
            return $"{key.ID}{(char)('a' + (int)key.Mode)}";
        }

        public static int Export(string outPath) {
            RandoLogic.ProcessAreas();
            var collectableNames = RandoModule.Instance.MetaConfig?.CollectableNames ?? new List<string>();

            // map each vanilla LevelData object to the area that owns it (rhelmot moves some rooms between areas)
            var levelOwner = new Dictionary<LevelData, AreaKey>();
            foreach (var area in AreaData.Areas) {
                if (area.SID == null || !area.SID.StartsWith("Celeste/")) continue;
                for (int m = 0; m < area.Mode.Length && m < 3; m++) {
                    var md = area.Mode[m]?.MapData;
                    if (md == null) continue;
                    foreach (var lvl in md.Levels) levelOwner[lvl] = new AreaKey(area.ID, (AreaMode)m);
                }
            }

            var rooms = RandoLogic.AllRooms.Where(r => {
                var sid = AreaData.Get(r.Area)?.SID;
                return sid != null && sid.StartsWith("Celeste/") && (int)r.Area.Mode <= 2;
            }).ToList();

            var byArea = new SortedDictionary<string, List<StaticRoom>>(Comparer<string>.Create(CompareAreaKeys));
            foreach (var r in rooms) {
                var k = AreaKeyString(r.Area);
                if (!byArea.TryGetValue(k, out var l)) byArea[k] = l = new List<StaticRoom>();
                l.Add(r);
            }

            var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true })) {
                w.WriteStartObject();
                w.WriteNumber("version", FormatVersion);
                w.WriteString("generator", "UltraRando " + (RandoModule.Instance?.Metadata?.VersionString ?? "?"));
                w.WriteStartArray("collectable_entity_names");
                foreach (var n in collectableNames) w.WriteStringValue(n);
                w.WriteEndArray();
                w.WriteStartObject("areas");
                foreach (var kv in byArea) {
                    var first = kv.Value[0];
                    var areaData = AreaData.Get(first.Area);
                    var mode = areaData.Mode[(int)first.Area.Mode];
                    var map = mode?.MapData;
                    w.WriteStartObject(kv.Key);
                    w.WriteString("sid", areaData.SID);
                    w.WriteNumber("area_id", first.Area.ID);
                    w.WriteNumber("mode", (int)first.Area.Mode);
                    string startLevel = null;
                    try { startLevel = map?.StartLevel()?.Name; } catch { }
                    w.WriteString("start_room", startLevel);
                    var checkpointLevels = new HashSet<string>((mode?.Checkpoints ?? new CheckpointData[0]).Select(c => c.Level));
                    w.WriteStartArray("checkpoint_rooms");
                    foreach (var c in mode?.Checkpoints ?? new CheckpointData[0]) w.WriteStringValue(c.Level);
                    w.WriteEndArray();

                    w.WriteStartObject("rooms");
                    var used = new HashSet<string>();
                    foreach (var room in kv.Value) {
                        // rhelmot's config may list the same level several times (alternate configurations, e.g.
                        // the end room as an ending and as a pass-through). Keys: "<level>", "<level>#2", ...
                        var key = room.Level.Name;
                        int variant = 1;
                        if (used.Contains(key)) {
                            variant = 2;
                            while (used.Contains(key + "#" + variant)) variant++;
                            key = key + "#" + variant;
                        }
                        used.Add(key);
                        w.WritePropertyName(key);
                        WriteRoom(w, room, collectableNames, levelOwner, startLevel, checkpointLevels, variant);
                    }
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                w.WriteEndObject();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            var tmp = outPath + ".tmp";
            File.WriteAllBytes(tmp, ms.ToArray());
            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmp, outPath);
            Logger.Log(LogLevel.Info, "UltraRando", $"cmr_export: wrote {rooms.Count} rooms in {byArea.Count} areas to {outPath}");
            return rooms.Count;
        }

        private static int CompareAreaKeys(string a, string b) {
            int na = int.Parse(a.Substring(0, a.Length - 1)), nb = int.Parse(b.Substring(0, b.Length - 1));
            if (na != nb) return na.CompareTo(nb);
            return a[a.Length - 1].CompareTo(b[b.Length - 1]);
        }

        private static void WriteRoom(Utf8JsonWriter w, StaticRoom room, List<string> collectableNames,
                Dictionary<LevelData, AreaKey> levelOwner, string startLevel, HashSet<string> checkpointLevels, int variant) {
            var lvl = room.Level;
            w.WriteStartObject();
            w.WriteString("rhelmot_name", room.Name);
            w.WriteString("level", lvl.Name);
            w.WriteNumber("variant", variant);
            if (levelOwner.TryGetValue(lvl, out var owner) && (owner.ID != room.Area.ID || owner.Mode != room.Area.Mode)) {
                w.WriteString("level_source_area", AreaKeyString(owner));
            }

            w.WriteStartObject("bounds");
            w.WriteNumber("x", lvl.Bounds.X);
            w.WriteNumber("y", lvl.Bounds.Y);
            w.WriteNumber("w", lvl.Bounds.Width);
            w.WriteNumber("h", lvl.Bounds.Height);
            w.WriteEndObject();

            w.WriteStartArray("extra_space");
            foreach (var r in room.ExtraSpace) {
                w.WriteStartObject();
                w.WriteNumber("x", r.X); w.WriteNumber("y", r.Y); w.WriteNumber("w", r.Width); w.WriteNumber("h", r.Height);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteBoolean("is_start", lvl.Name == startLevel);
            w.WriteBoolean("is_end", !(room.ReqEnd is Impossible));
            w.WritePropertyName("req_end");
            WriteReq(w, room.ReqEnd);
            w.WriteBoolean("checkpoint", lvl.HasCheckpoint || checkpointLevels.Contains(lvl.Name));
            w.WriteBoolean("hub", room.Hub);
            w.WriteBoolean("has_heart", room.HasHeart);
            w.WriteNumber("worth", room.Worth);

            // holes, in StaticRoom.Holes order
            w.WriteStartArray("holes");
            for (int i = 0; i < room.Holes.Count; i++) {
                var h = room.Holes[i];
                w.WriteStartObject();
                w.WriteNumber("index", i);
                w.WriteString("side", h.Side.ToString());
                w.WriteNumber("low", h.LowBound);
                w.WriteNumber("high", h.HighBound);
                w.WriteBoolean("low_open", h.LowOpen);
                w.WriteBoolean("high_open", h.HighOpen);
                w.WriteNumber("size", h.Size);
                w.WriteString("kind", h.Kind.ToString().ToLowerInvariant());
                if (h.Launch.HasValue) w.WriteNumber("launch", h.Launch.Value); else w.WriteNull("launch");
                w.WriteEndObject();
            }
            w.WriteEndArray();

            // nodes
            var allCollectables = room.Collectables;
            w.WriteStartObject("nodes");
            foreach (var node in room.Nodes.Values) {
                w.WriteStartObject(node.Name);
                var edges = new List<StaticEdge>();
                var holeEdges = new List<StaticEdge>();
                var warps = new List<StaticEdge>();
                var customWarps = new List<StaticEdge>();
                foreach (var e in node.Edges) {
                    if (e.HoleTarget != null) holeEdges.Add(e);
                    else if (e.NodeTarget != null && e.NodeTarget.ParentRoom == room) edges.Add(e);
                    else if (e.NodeTarget != null) warps.Add(e);
                    else customWarps.Add(e);
                }

                w.WriteStartArray("edges");
                foreach (var e in edges) {
                    w.WriteStartObject();
                    w.WriteString("to_node", e.NodeTarget.Name);
                    w.WritePropertyName("req_out"); WriteReq(w, e.ReqOut);
                    w.WritePropertyName("req_in"); WriteReq(w, e.ReqIn);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WriteStartArray("hole_edges");
                foreach (var e in holeEdges) {
                    w.WriteStartObject();
                    w.WriteNumber("hole", room.Holes.IndexOf(e.HoleTarget));
                    w.WritePropertyName("req_out"); WriteReq(w, e.ReqOut);
                    w.WritePropertyName("req_in"); WriteReq(w, e.ReqIn);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WriteStartArray("warp_edges");
                foreach (var e in warps) {
                    w.WriteStartObject();
                    w.WriteString("to_room", e.NodeTarget.ParentRoom.Level.Name);
                    w.WriteString("to_area", AreaKeyString(e.NodeTarget.ParentRoom.Area));
                    w.WriteString("to_node", e.NodeTarget.Name);
                    w.WritePropertyName("req_out"); WriteReq(w, e.ReqOut);
                    w.WritePropertyName("req_in"); WriteReq(w, e.ReqIn);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WriteStartArray("custom_warps");
                foreach (var e in customWarps) {
                    w.WriteStartObject();
                    w.WritePropertyName("req_out"); WriteReq(w, e.ReqOut);
                    w.WritePropertyName("req_in"); WriteReq(w, e.ReqIn);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WriteStartArray("flags_set");
                foreach (var f in node.FlagSetters) {
                    w.WriteStartObject();
                    w.WriteString("flag", f.Item1);
                    w.WriteString("state", f.Item2 ? "set" : "unset");
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WriteStartArray("collectables");
                foreach (var c in node.Collectables) {
                    WriteCollectable(w, c, allCollectables.IndexOf(c), lvl, collectableNames);
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndObject();

            // collectables rhelmot found but could not attach to any node
            w.WriteStartArray("unassigned_collectables");
            for (int i = 0; i < allCollectables.Count; i++) {
                if (allCollectables[i].ParentNode == null) WriteCollectable(w, allCollectables[i], i, lvl, collectableNames);
            }
            w.WriteEndArray();

            // other pickups/checks that rhelmot does not model as collectables
            w.WriteStartArray("pickups");
            foreach (var e in lvl.Entities) {
                if (!PickupNames.Contains(e.Name)) continue;
                w.WriteStartObject();
                w.WriteString("kind", e.Name);
                w.WriteNumber("x", e.Position.X);
                w.WriteNumber("y", e.Position.Y);
                w.WriteNumber("entity_id", e.ID);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("entities");
            foreach (var n in lvl.Entities.Select(e => e.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal)) w.WriteStringValue(n);
            w.WriteEndArray();
            w.WriteStartArray("triggers");
            foreach (var n in lvl.Triggers.Select(e => e.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal)) w.WriteStringValue(n);
            w.WriteEndArray();

            w.WriteStartArray("spawns");
            foreach (var s in lvl.Spawns) {
                w.WriteStartArray();
                w.WriteNumberValue(s.X - lvl.Bounds.X);
                w.WriteNumberValue(s.Y - lvl.Bounds.Y);
                w.WriteEndArray();
            }
            w.WriteEndArray();

            // warps: other rooms reachable from this one by warp edges (cutscenes/teleports)
            w.WriteStartArray("warps");
            foreach (var target in room.Nodes.Values.SelectMany(n => n.Edges)
                         .Where(e => e.HoleTarget == null && e.NodeTarget != null && e.NodeTarget.ParentRoom != room)
                         .Select(e => e.NodeTarget.ParentRoom.Level.Name).Distinct()) {
                w.WriteStringValue(target);
            }
            w.WriteEndArray();

            w.WriteEndObject();
        }

        private static readonly HashSet<string> PickupNames = new HashSet<string> {
            "strawberry", "goldenBerry", "memorialTextController", "key", "summitgem", "heartGem", "blackGem",
            "cassette", "birdForsakenCityGem", "fakeHeart",
        };

        private static void WriteCollectable(Utf8JsonWriter w, StaticCollectable c, int index, LevelData lvl, List<string> collectableNames) {
            w.WriteStartObject();
            if (index >= 0) w.WriteNumber("index", index); else w.WriteNull("index");
            var ent = lvl.Entities.FirstOrDefault(e => collectableNames.Contains(e.Name) &&
                Math.Abs(e.Position.X - c.Position.X) < 0.5f && Math.Abs(e.Position.Y - c.Position.Y) < 0.5f);
            w.WriteString("kind", ent?.Name ?? "spot");
            w.WriteNumber("x", c.Position.X);
            w.WriteNumber("y", c.Position.Y);
            if (ent != null) {
                w.WriteNumber("entity_id", ent.ID);
                w.WriteBoolean("winged", ent.Bool("winged"));
            } else {
                w.WriteNull("entity_id");
            }
            w.WriteBoolean("must_fly", c.MustFly);
            w.WriteEndObject();
        }

        private static string DashName(NumDashes d) {
            switch (d) {
                case NumDashes.Zero: return "zero";
                case NumDashes.One: return "one";
                case NumDashes.Two: return "two";
                default: return d.ToString().ToLowerInvariant();
            }
        }

        public static void WriteReq(Utf8JsonWriter w, Requirement r) {
            switch (r) {
                case null:
                case Possible _:
                    w.WriteNullValue();
                    return;
                case Impossible _:
                    w.WriteStringValue("impossible");
                    return;
                case Conjunction c:
                    w.WriteStartObject();
                    w.WriteStartArray("and");
                    foreach (var ch in c.Children) WriteReq(w, ch);
                    w.WriteEndArray();
                    w.WriteEndObject();
                    return;
                case Disjunction d:
                    w.WriteStartObject();
                    w.WriteStartArray("or");
                    foreach (var ch in d.Children) WriteReq(w, ch);
                    w.WriteEndArray();
                    w.WriteEndObject();
                    return;
                case DashRequirement dr:
                    w.WriteStartObject();
                    w.WriteString("dashes", DashName(dr.Dashes));
                    w.WriteEndObject();
                    return;
                case SkillRequirement sr:
                    w.WriteStartObject();
                    w.WriteString("difficulty", sr.Difficulty.ToString().ToLowerInvariant());
                    w.WriteEndObject();
                    return;
                case KeyRequirement kr:
                    w.WriteStartObject();
                    w.WriteNumber("key", kr.KeyholeID);
                    w.WriteEndObject();
                    return;
                case FlagRequirement fr:
                    w.WriteStartObject();
                    w.WriteString("flag", fr.Flag);
                    w.WriteString("state", fr.Set ? "set" : "unset");
                    w.WriteEndObject();
                    return;
                default:
                    w.WriteStartObject();
                    w.WriteString("unknown", r.GetType().Name);
                    w.WriteEndObject();
                    return;
            }
        }
    }
}
