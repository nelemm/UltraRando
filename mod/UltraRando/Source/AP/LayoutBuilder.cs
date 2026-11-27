// CMR (phase B): builds a MapData from slot_data.chapters[area] with rhelmot's machinery
// (LinkedRoom / LinkedMap.FillMap / StaticRoom.MakeLevelData), without any random generation, and installs it
// in place of the vanilla MapData while an AP/offline session is active.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.UltraRando.AP {
    public class LayoutBuildResult {
        public MapData Map;
        public int Rooms, Links, ExactLinks, LooseLinks, OneWayClosed;
        public List<string> Warnings = new List<string>();
    }

    public static class LayoutBuilder {
        public const string DynKey = "CMR_ApLayout";

        /// <summary>Entities/triggers that rhelmot's MakeLevelData strips and that we put back (vanilla checks,
        /// checkpoints, music, refill rules).</summary>
        static readonly HashSet<string> RestoreEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "strawberry", "key", "summitgem", "goldenBerry", "checkpoint",
        };
        static readonly HashSet<string> RestoreTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "noRefillTrigger", "musicTrigger", "musicFadeTrigger", "goldenBerryCollectTrigger", "lightFadeTrigger", "bloomFadeTrigger",
        };

        /// <summary>rooms.json keys ("3", "end#2") -> StaticRoom, same numbering as RoomsExport.</summary>
        public static Dictionary<string, StaticRoom> RoomKeys(string areaKey) {
            var result = new Dictionary<string, StaticRoom>();
            foreach (var r in RandoLogic.AllRooms) {
                if (RoomsExport.AreaKeyString(r.Area) != areaKey) continue;
                var sid = AreaData.Get(r.Area)?.SID;
                if (sid == null || !sid.StartsWith("Celeste/") || (int)r.Area.Mode > 2) continue;
                var key = r.Level.Name;
                if (result.ContainsKey(key)) {
                    int v = 2;
                    while (result.ContainsKey(key + "#" + v)) v++;
                    key = key + "#" + v;
                }
                result[key] = r;
            }
            return result;
        }

        public static bool TryParseAreaKey(string areaKey, out AreaKey key) {
            key = AreaKey.None;
            if (string.IsNullOrEmpty(areaKey) || areaKey.Length < 2) return false;
            char m = areaKey[areaKey.Length - 1];
            if (m < 'a' || m > 'c') return false;
            if (!int.TryParse(areaKey.Substring(0, areaKey.Length - 1), out int id)) return false;
            if (id < 0 || id >= AreaData.Areas.Count) return false;
            key = new AreaKey(id, (AreaMode)(m - 'a'));
            return true;
        }

        static readonly MethodInfo memberwiseClone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        public static LayoutBuildResult Build(string areaKey, ChapterLayout chapter, MapData vanilla) {
            var res = new LayoutBuildResult();
            if (!TryParseAreaKey(areaKey, out var key)) throw new Exception("bad area key " + areaKey);
            if (vanilla == null) throw new Exception("no vanilla map for " + areaKey);
            var statics = RoomKeys(areaKey);
            if (statics.Count == 0) throw new Exception("no rhelmot rooms for " + areaKey + " (ProcessAreas not run?)");

            // --- rooms
            var byKey = new Dictionary<string, LinkedRoom>();      // variant key and level name -> room
            var linked = new List<LinkedRoom>();
            var usedLevels = new HashSet<string>();
            string startLevel = null;
            try { startLevel = vanilla.StartLevel()?.Name; } catch { }
            var placed = new List<(string vkey, StaticRoom st, Vector2 pos)>();
            foreach (var lr in chapter.Rooms) {
                var vkey = string.IsNullOrEmpty(lr.Variant) ? lr.Room : lr.Variant;
                if (!statics.TryGetValue(vkey, out var st) && !statics.TryGetValue(lr.Room, out st)) {
                    res.Warnings.Add($"unknown room {vkey}");
                    continue;
                }
                if (!usedLevels.Add(st.Level.Name)) {
                    res.Warnings.Add($"level {st.Level.Name} placed twice, second copy ignored");
                    continue;
                }
                placed.Add((vkey, st, new Vector2(lr.X, lr.Y)));
            }
            if (placed.Count == 0) throw new Exception("layout has no usable room");

            // keep the start room at its vanilla position (MapData.StartLevel() looks for it there)
            Vector2 shift = Vector2.Zero;
            var start = placed.FirstOrDefault(p => p.st.Level.Name == startLevel);
            if (start.st != null) {
                shift = start.st.Level.Position - start.pos;
            } else {
                res.Warnings.Add($"start room {startLevel} not in layout");
            }
            foreach (var (vkey, st, pos) in placed) {
                var room = new LinkedRoom(st, pos + shift) { WarpMap = null };
                linked.Add(room);
                byKey[vkey] = room;
                byKey[st.Level.Name] = room;
            }

            // lock doors stay vanilla in V1: keep every lockBlock
            foreach (var r in linked) {
                foreach (var e in r.Static.Level.Entities) {
                    if (e.Name == "lockBlock") r.UsedKeyholes.Add(e.ID);
                }
            }

            // --- links
            foreach (var l in chapter.Links) {
                if (!byKey.TryGetValue(l.A, out var ra) || !byKey.TryGetValue(l.B, out var rb)) {
                    res.Warnings.Add($"link {l.A}:{l.AHole} - {l.B}:{l.BHole}: unknown room");
                    continue;
                }
                if (l.AHole < 0 || l.AHole >= ra.Static.Holes.Count || l.BHole < 0 || l.BHole >= rb.Static.Holes.Count) {
                    res.Warnings.Add($"link {l.A}:{l.AHole} - {l.B}:{l.BHole}: bad hole index");
                    continue;
                }
                var ha = ra.Static.Holes[l.AHole];
                var hb = rb.Static.Holes[l.BHole];
                // A must not be entered through this link: close B's side of it (and vice versa)
                if (l.NoEnter != null && l.NoEnter.Contains('a')) { rb.CMR_NoEnterHoles.Add(hb); res.OneWayClosed++; }
                if (l.NoEnter != null && l.NoEnter.Contains('b')) { ra.CMR_NoEnterHoles.Add(ha); res.OneWayClosed++; }
                var ea = FindOrMakeEdge(ra, ha, out var na);
                var eb = FindOrMakeEdge(rb, hb, out var nb);

                bool exact = ha.Side == hb.Side.Opposite() && ha.Compatible(hb) == GeometricOffset(ra, ha, rb);
                if (exact) {
                    var edge = new LinkedEdge { NodeA = na, NodeB = nb, StaticA = ea, StaticB = eb };
                    na.Edges.Add(edge);
                    nb.Edges.Add(edge);
                    res.ExactLinks++;
                } else {
                    // geometry does not match rhelmot's alignment for this pair: open each hole on its own
                    // (no partial blocking), so Bake never does arithmetic on an incompatible pair.
                    AddLooseEdge(na, ea, ha, nb);
                    AddLooseEdge(nb, eb, hb, na);
                    res.LooseLinks++;
                    res.Warnings.Add($"link {l.A}:{l.AHole} - {l.B}:{l.BHole} not aligned like rhelmot, opened without partial blocking");
                }
                res.Links++;
            }

            // --- bake through rhelmot
            var lmap = new LinkedMap();
            foreach (var r in linked) lmap.AddRoom(r);
            var map = (MapData)memberwiseClone.Invoke(vanilla, null);
            map.Levels = new List<LevelData>();
            map.Filler = new List<Rectangle>();
            var bakeSettings = new RandoSettings { RandomDecorations = false };
            StaticRoom.CMR_KeepCollectibles = true; // vanilla collectibles = our locations (see StaticRoom.SkipForAp)
            try { lmap.FillMap(map, bakeSettings, new Random(0)); } finally { StaticRoom.CMR_KeepCollectibles = false; }

            // --- vanilla names and stripped entities
            foreach (var r in linked) PostProcess(r);

            SetBounds(map);
            RefreshNameCache(map);
            new DynData<MapData>(map).Set(DynKey, (bool?)true);
            res.Map = map;
            res.Rooms = linked.Count;
            return res;
        }

        /// <summary>
        /// Everest keeps a level-name -> LevelData cache in MapData (levelsByName). MemberwiseClone copied the VANILLA
        /// cache, so MapData.Get(name) returned vanilla rooms at vanilla positions while the solids grid was built from
        /// our rooms: tile entities (ExitBlock, DashBlock...) then indexed outside the grid. Rebuild it from our levels.
        /// </summary>
        static void RefreshNameCache(MapData map) {
            var t = typeof(MapData);
            var regen = t.GetMethod("RegenerateLevelsByNameCache", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var field = t.GetField("levelsByName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) field.SetValue(map, null);
            if (regen != null) {
                regen.Invoke(map, null);
            } else if (field != null && typeof(Dictionary<string, LevelData>).IsAssignableFrom(field.FieldType)) {
                var d = new Dictionary<string, LevelData>();
                foreach (var l in map.Levels) d[l.Name] = l;
                field.SetValue(map, d);
            }
            foreach (var l in map.Levels) {
                if (!ReferenceEquals(map.Get(l.Name), l)) throw new Exception("MapData name cache still points to another level for " + l.Name);
            }
        }

        static int GeometricOffset(LinkedRoom a, Hole ha, LinkedRoom b) {
            switch (ha.Side) {
                case ScreenDirection.Up:
                case ScreenDirection.Down:
                    return (b.Bounds.Left - a.Bounds.Left) / 8;
                default:
                    return (b.Bounds.Top - a.Bounds.Top) / 8;
            }
        }

        static StaticEdge FindOrMakeEdge(LinkedRoom room, Hole hole, out LinkedNode node) {
            foreach (var n in room.Nodes.Values) {
                foreach (var e in n.Static.Edges) {
                    if (e.HoleTarget == hole) {
                        node = n;
                        return e;
                    }
                }
            }
            // hole without configured edge (kind "none"): synthetic edge on the main node
            node = room.Nodes["main"];
            return new StaticEdge { FromNode = node.Static, HoleTarget = hole, ReqIn = new Possible(), ReqOut = new Possible() };
        }

        static void AddLooseEdge(LinkedNode node, StaticEdge real, Hole hole, LinkedNode other) {
            var mirror = new Hole(hole.Side.Opposite(), hole.LowBound, hole.HighBound, hole.HighOpen) { Kind = HoleKind.InOut };
            var fake = new StaticEdge { FromNode = other.Static, HoleTarget = mirror, ReqIn = new Possible(), ReqOut = new Possible() };
            var edge = new LinkedEdge { NodeA = node, NodeB = other, StaticA = real, StaticB = fake };
            node.Edges.Add(edge);
        }

        static void PostProcess(LinkedRoom room) {
            var baked = room.BakedRoom;
            var orig = room.Static.Level;
            baked.Name = orig.Name;              // keep vanilla level names: location keys and cutscenes use them
            baked.Music = orig.Music;
            baked.HasCheckpoint = orig.HasCheckpoint;

            var present = new HashSet<int>(baked.Entities.Select(e => e.ID).Concat(baked.Triggers.Select(e => e.ID)));
            var restored = new HashSet<int>();
            foreach (var e in orig.Entities) {
                if (RestoreEntities.Contains(e.Name) && !baked.Entities.Any(x => x.ID == e.ID && x.Name == e.Name)) {
                    baked.Entities.Add(Clone(e, baked));
                    restored.Add(e.ID);
                }
            }
            foreach (var e in orig.Triggers) {
                if (RestoreTriggers.Contains(e.Name) && !baked.Triggers.Any(x => x.ID == e.ID && x.Name == e.Name)) {
                    baked.Triggers.Add(Clone(e, baked));
                    restored.Add(e.ID);
                }
            }
            if (restored.Count == 0) return;
            // entities added by Bake (barriers, beams) may have reused a restored ID: renumber them, otherwise a
            // collected strawberry (Session.DoNotLoad) would also stop the barrier with the same ID from loading.
            int max = 0;
            foreach (var e in baked.Entities.Concat(baked.Triggers).Concat(orig.Entities).Concat(orig.Triggers)) max = Math.Max(max, e.ID);
            var origIds = new HashSet<(int, string)>(orig.Entities.Concat(orig.Triggers).Select(e => (e.ID, e.Name)));
            foreach (var e in baked.Entities.Concat(baked.Triggers)) {
                if (restored.Contains(e.ID) && !origIds.Contains((e.ID, e.Name))) e.ID = ++max;
            }
        }

        static EntityData Clone(EntityData e, LevelData level) {
            return new EntityData {
                ID = e.ID, Name = e.Name, Level = level, Position = e.Position, Origin = e.Origin,
                Width = e.Width, Height = e.Height,
                Nodes = e.Nodes == null ? null : (Vector2[])e.Nodes.Clone(),
                Values = e.Values == null ? null : new Dictionary<string, object>(e.Values),
            };
        }

        static void SetBounds(MapData map) {
            int l = int.MaxValue, t = int.MaxValue, r = int.MinValue, b = int.MinValue;
            foreach (var lvl in map.Levels) {
                l = Math.Min(l, lvl.Bounds.Left); t = Math.Min(t, lvl.Bounds.Top);
                r = Math.Max(r, lvl.Bounds.Right); b = Math.Max(b, lvl.Bounds.Bottom);
            }
            map.Bounds = new Rectangle(l - 50, t - 50, r - l + 100, b - t + 100);
        }

        public static bool IsApLayout(MapData map) {
            return map != null && (new DynData<MapData>(map).Get<bool?>(DynKey) ?? false);
        }
    }

    /// <summary>Swaps vanilla MapData objects with built layouts, and restores them.</summary>
    public static class LayoutInstaller {
        static readonly Dictionary<(int, int), MapData> originals = new Dictionary<(int, int), MapData>();
        static readonly Dictionary<(int, int), MapData> built = new Dictionary<(int, int), MapData>();
        static SlotData builtFor;

        public static MapData Original(AreaKey key) {
            if (originals.TryGetValue((key.ID, (int)key.Mode), out var m)) return m;
            return AreaData.Get(key)?.Mode?[(int)key.Mode]?.MapData;
        }

        /// <summary>Install the layout for this area if the session wants it, else make sure vanilla is in place.</summary>
        public static bool Prepare(Session session) {
            var key = session.Area;
            var slot = ApState.Slot;
            if (builtFor != slot) RestoreAll();
            if (!ApState.AreaActive(key, out var areaKey) || !slot.Chapters.TryGetValue(areaKey, out var chapter)) {
                Restore(key);
                return false;
            }
            if (!ApState.SaveMatches(true)) {
                Restore(key);
                if (RandoSaves.CurrentIsRando) ApHud.Message("Cette sauvegarde appartient à une autre seed : carte vanilla");
                return false;
            }
            var k = (key.ID, (int)key.Mode);
            var mode = AreaData.Get(key).Mode[(int)key.Mode];
            if (!built.TryGetValue(k, out var map)) {
                var vanilla = Original(key);
                LayoutBuildResult res;
                try {
                    res = LayoutBuilder.Build(areaKey, chapter, vanilla);
                } catch (Exception e) {
                    ApState.Log($"layout build for {areaKey} failed: {e}");
                    ApHud.Message("Erreur de construction de la carte " + areaKey + " (voir log.txt)");
                    Restore(key);
                    return false;
                }
                foreach (var w in res.Warnings.Take(50)) ApState.Log($"layout {areaKey}: {w}");
                ApState.Log($"layout {areaKey} built: {res.Rooms} rooms, {res.Links} links ({res.LooseLinks} loose, {res.OneWayClosed} closed one-way)");
                map = res.Map;
                built[k] = map;
                builtFor = slot;
                if (!originals.ContainsKey(k)) originals[k] = vanilla;
            }
            mode.MapData = map;
            // the session may point to a checkpoint room that is not in this layout
            if (map.Get(session.Level) == null) {
                var startLvl = map.StartLevel() ?? map.Levels[0];
                ApState.Log($"room {session.Level} not in layout, starting at {startLvl.Name}");
                session.Level = startLvl.Name;
                session.RespawnPoint = null;
                session.StartCheckpoint = null;
            }
            return true;
        }

        public static void Restore(AreaKey key) {
            var k = (key.ID, (int)key.Mode);
            if (originals.TryGetValue(k, out var orig)) {
                var mode = AreaData.Get(key)?.Mode?[(int)key.Mode];
                if (mode != null) mode.MapData = orig;
            }
        }

        public static void RestoreAll() {
            foreach (var kv in originals) {
                if (kv.Key.Item1 < AreaData.Areas.Count) {
                    var mode = AreaData.Areas[kv.Key.Item1].Mode?[kv.Key.Item2];
                    if (mode != null) mode.MapData = kv.Value;
                }
            }
            originals.Clear();
            built.Clear();
            builtFor = null;
        }
    }
}
