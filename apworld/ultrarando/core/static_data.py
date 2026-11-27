"""Static data: rooms.json geometry + Open World logic, door <-> hole mapping and the location table.

Nothing here depends on the seed or on the options, so the location table is stable.
"""
from __future__ import annotations

import json
import os
import pkgutil
from collections import Counter, defaultdict

from .geometry import hole_touch_span, OPPOSITE
from .names import AREA_ORDER

DIR_TO_SIDE = {"up": "Up", "down": "Down", "left": "Left", "right": "Right"}
LOCATION_BASE_ID = 0xC3D1000

# rooms.json pickup kind -> (contract kind, Open World location type)
PICKUP_KINDS = {
    "strawberry": ("strawberry", "strawberry"),
    "key": ("key", "key"),
    "cassette": ("cassette", "cassette"),
    "blackGem": ("heart", "crystal_heart"),
    "birdForsakenCityGem": ("heart", "crystal_heart"),
    "summitgem": ("gem", "gem"),
}
EVENT_TYPES = {"key", "gem", "clutter", "breaker"}

# Open World links the two halves of Farewell in code (Locations.py): 10a e-08 east -> 10b f-door west.
MANUAL_OW_CONNECTIONS = {"10a": [{"source_room": "e-08", "source_door": "east",
                                  "dest_room": "f-door", "dest_door": "west"}]}


def _load_json(name: str):
    pkg = (__package__ or "").rpartition(".")[0]
    if pkg:
        try:
            raw = pkgutil.get_data(pkg, "data/" + name)
            if raw is not None:
                return json.loads(raw.decode("utf-8"))
        except (OSError, ImportError, ValueError):
            pass
    path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", name)
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def _apply_extra_logic(ow: dict, extra: dict) -> None:
    """Our additions to the Open World logic (data/cmr_extra_logic.json): replace the rules of one connection."""
    for e in extra["connections"]:
        lv = next(l for l in ow["levels"] if l["name"] == e["level"])
        room = next(r for r in lv["rooms"] if r["name"] == e["room"])
        reg = next(r for r in room["regions"] if r["name"] == e["from"])
        conn = next(c for c in reg["connections"] if c["dest"] == e["to"])
        for k in ("rule", "vm_rule", "assist_rule"):
            if k in e:
                conn[k] = e[k]
    for e in extra.get("locations", []):
        lv = next(l for l in ow["levels"] if l["name"] == e["level"])
        room = next(r for r in lv["rooms"] if r["name"] == e["room"])
        reg = next(r for r in room["regions"] if r["name"] == e["region"])
        loc = next(l for l in reg["locations"] if l["name"] == e["location"])
        for k in ("rule", "vm_rule", "assist_rule"):
            add = e.get("add_" + k)
            if add:
                # a missing key falls back to "rule" (Open World semantics): extend that one
                base = loc.get(k, loc.get("rule", [])) or []
                loc[k] = base + [alt for alt in add if alt not in base]


def region_name(area: str, level: str, region: str) -> str:
    return f"{area}_{level}_{region}"


def room_region_name(area: str, level: str) -> str:
    return f"{area}_{level}_room"


class OWRoom:
    def __init__(self, ow_level: str, display: str, raw: dict):
        self.ow_level = ow_level
        self.display = display
        self.name: str = raw["name"]
        self.regions: list[dict] = raw["regions"]
        self.doors: dict[str, dict] = {d["name"]: d for d in raw["doors"]}
        self.checkpoint: str = raw.get("checkpoint") or ""
        self.checkpoint_region: str = raw.get("checkpoint_region") or ""


class LocSpec:
    """A location of the static table. `cands`: list of (level, ow region, ow location json, level display)."""

    def __init__(self, key: str, name: str, area: str, level: str | None, kind: str, cands=None):
        self.key = key
        self.name = name
        self.area = area
        self.level = level
        self.kind = kind
        self.cands = cands or []
        self.id = 0


class EventSpec:
    def __init__(self, area: str, level: str, region: str, owloc: dict, display: str, item: str):
        self.area, self.level, self.region, self.owloc, self.display, self.item = \
            area, level, region, owloc, display, item


def _hint(name: str, axis: str) -> int:
    """Position hint from an Open World door name, along x (Up/Down sides) or y (Left/Right sides)."""
    if axis == "x":
        neg, pos = {"west", "left"}, {"east", "right"}
    else:
        neg, pos = {"north", "top", "up", "upper"}, {"south", "bottom", "down", "lower"}
    s = 0
    for t in name.split("-"):
        s += -1 if t in neg else (1 if t in pos else 0)
    return s


class Area:
    def __init__(self, key: str, raw: dict, ow_levels: list[dict]):
        self.key = key
        self.sid = raw["sid"]
        self.start_room: str = raw["start_room"]
        self.checkpoint_rooms: list[str] = raw.get("checkpoint_rooms", [])
        self.rooms: dict[str, dict] = raw["rooms"]
        self.levels: dict[str, list[str]] = defaultdict(list)
        for k, r in sorted(self.rooms.items(), key=lambda kv: (kv[1]["level"], kv[1]["variant"])):
            self.levels[r["level"]].append(k)
        self.levels = dict(sorted(self.levels.items()))
        self.display: str = ow_levels[0]["display_name"] if ow_levels else key

        self.ow_rooms: dict[str, OWRoom] = {}
        self.ow_conns: list[dict] = []
        self.ow_start_room: str | None = None
        self.ow_start_region: str | None = None
        for lv in ow_levels:
            for r in lv["rooms"]:
                o = OWRoom(lv["name"], lv["display_name"], r)
                self.ow_rooms[o.name] = o
                if o.checkpoint == "Start" and self.ow_start_room is None:
                    self.ow_start_room, self.ow_start_region = o.name, o.checkpoint_region
            self.ow_conns.extend(lv["room_connections"])
        self.ow_conns.extend(MANUAL_OW_CONNECTIONS.get(key, []))
        self.virtual = {n for n in self.ow_rooms if n not in self.levels}

        self.vanilla_pairs: list[tuple[str, int, str, int]] = self._vanilla_pairs()
        self.vanilla_links: list[tuple[str, int, str, int]] = self._dedupe(self.vanilla_pairs)
        self.door_hole: dict[tuple[str, str], int] = {}
        self.hole_door: dict[tuple[str, int], str] = {}
        self.conn_kind: list[str] = []
        self.locked_pairs: set[tuple[str, int, str, int]] = set()
        self.report: Counter = Counter()
        self._map_doors()

    # ----- geometry -----
    def base(self, level: str) -> dict:
        return self.rooms[level]

    def _vanilla_pairs(self):
        out = []
        levels = list(self.levels)
        for i, ka in enumerate(levels):
            A = self.rooms[ka]
            ab = A["bounds"]
            for kb in levels[i + 1:]:
                B = self.rooms[kb]
                bb = B["bounds"]
                for ha in A["holes"]:
                    for hb in B["holes"]:
                        if hole_touch_span(A, ab["x"], ab["y"], ha, B, bb["x"], bb["y"], hb):
                            out.append((ka, ha["index"], kb, hb["index"]))
        return out

    def _dedupe(self, pairs):
        """Vanilla links with each hole used once (a hole facing two holes keeps the best partner:
        usable kinds first, then the widest overlap)."""
        def score(p):
            ka, ia, kb, ib = p
            ha, hb = self.rooms[ka]["holes"][ia], self.rooms[kb]["holes"][ib]
            usable = ha["kind"] != "none" and hb["kind"] != "none"
            return (0 if usable else 1, -min(ha["size"], hb["size"]), p)
        used, out = set(), []
        for p in sorted(pairs, key=score):
            if (p[0], p[1]) in used or (p[2], p[3]) in used:
                continue
            used.add((p[0], p[1]))
            used.add((p[2], p[3]))
            out.append(p)
        return [p for p in pairs if p in out]

    def pairs_between(self, r: str, s: str):
        out = []
        for (ka, ia, kb, ib) in self.vanilla_pairs:
            if (ka, kb) == (r, s):
                out.append((ia, ib, (ka, ia, kb, ib)))
            elif (ka, kb) == (s, r):
                out.append((ib, ia, (ka, ia, kb, ib)))
        return out

    # ----- door <-> hole mapping -----
    def _map_doors(self):
        rep = self.report
        conn_kind = ["?"] * len(self.ow_conns)
        groups: dict[tuple, list[int]] = defaultdict(list)
        for i, c in enumerate(self.ow_conns):
            r, s = c["source_room"], c["dest_room"]
            if r in self.virtual or s in self.virtual:
                conn_kind[i] = "virtual"
                continue
            dr = self.ow_rooms[r].doors[c["source_door"]]
            ds = self.ow_rooms[s].doors[c["dest_door"]]
            if dr["direction"] == "special" or ds["direction"] == "special":
                conn_kind[i] = "special"
                continue
            if not self.pairs_between(r, s):
                conn_kind[i] = "nongeo"
                continue
            # normalise the pair (lower name first) and group by the side of the first room's door
            if r <= s:
                groups[(r, s, DIR_TO_SIDE[dr["direction"]])].append(i)
            else:
                groups[(s, r, DIR_TO_SIDE[ds["direction"]])].append(i)

        proposals: dict[tuple[str, str], set[int]] = defaultdict(set)
        group_result: dict[tuple, list[tuple[int, tuple[str, str, int], tuple[str, str, int]]] | None] = {}
        for (r, s, side), idxs in sorted(groups.items()):
            norm = []  # (conn index, door of r, door of s)
            for i in idxs:
                c = self.ow_conns[i]
                if c["source_room"] == r and c["dest_room"] == s:
                    norm.append((i, c["source_door"], c["dest_door"]))
                else:
                    norm.append((i, c["dest_door"], c["source_door"]))
            rr, ss = self.rooms[r], self.rooms[s]
            pairs = [(ia, ib, p) for (ia, ib, p) in self.pairs_between(r, s)
                     if rr["holes"][ia]["side"] == side and ss["holes"][ib]["side"] == OPPOSITE[side]]
            usable = [(ia, ib, p) for (ia, ib, p) in pairs
                      if rr["holes"][ia]["kind"] != "none" and ss["holes"][ib]["kind"] != "none"]
            # the same door can appear in two connections (one per direction): count distinct door couples
            couples = sorted({(dr, ds) for (_, dr, ds) in norm})
            result = None
            if len(couples) == 1 and len(usable) == 1:
                result = [(couples[0], usable[0])]
                rep["geo_unique"] += 1
            elif len(couples) == len(usable) > 1:
                axis = "x" if side in ("Up", "Down") else "y"
                keys = [(_hint(dr, axis), _hint(ds, axis)) for (dr, ds) in couples]
                if len(set(keys)) == len(keys):
                    order_c = [c for _, c in sorted(zip(keys, couples))]
                    order_p = sorted(usable, key=lambda t: rr["holes"][t[0]]["low"])
                    result = list(zip(order_c, order_p))
                    rep["name_hint"] += 1
            if result is not None and not self._consistent(r, s, norm, result):
                result = None
                rep["kind_conflict"] += 1
            group_result[(r, s, side)] = result
            if result is None:
                for (i, _, _) in norm:
                    conn_kind[i] = "locked"
                for (_, _, p) in pairs:
                    self.locked_pairs.add(p)
                rep["locked_group"] += 1
            else:
                for (dr, ds), (ia, ib, p) in result:
                    proposals[(r, dr)].add(ia)
                    proposals[(s, ds)].add(ib)
                for (i, _, _) in norm:
                    conn_kind[i] = "mapped"

        # a door proposed for two different holes is ambiguous: lock its groups
        bad = {k for k, v in proposals.items() if len(v) > 1}
        if bad:
            for (r, s, side), result in group_result.items():
                if result is None:
                    continue
                if any((r, dr) in bad or (s, ds) in bad for (dr, ds), _ in result):
                    for i in groups[(r, s, side)]:
                        conn_kind[i] = "locked"
                    for (ia, ib, p) in self.pairs_between(r, s):
                        if self.rooms[r]["holes"][ia]["side"] == side:
                            self.locked_pairs.add(p)
                    group_result[(r, s, side)] = None
                    rep["locked_conflict"] += 1
        for (r, s, side), result in group_result.items():
            if result is None:
                continue
            for (dr, ds), (ia, ib, p) in result:
                self.door_hole[(r, dr)] = ia
                self.door_hole[(s, ds)] = ib
        # a locked pair must not also use a mapped hole
        for (ka, ia, kb, ib) in self.locked_pairs:
            for (lv, h) in ((ka, ia), (kb, ib)):
                for k in [k for k, v in self.door_hole.items() if k[0] == lv and v == h]:
                    del self.door_hole[k]
        for (lv, d), h in self.door_hole.items():
            self.hole_door[(lv, h)] = d
        self.conn_kind = conn_kind
        for k in conn_kind:
            rep["conn_" + k] += 1
        doors = [(n, d) for n, o in self.ow_rooms.items() if n not in self.virtual
                 for d, dd in o.doors.items() if dd["direction"] != "special"]
        rep["doors"] = len(doors)
        rep["doors_mapped"] = sum(1 for k in doors if k in self.door_hole)
        rep["doors_special"] = sum(1 for n, o in self.ow_rooms.items() if n not in self.virtual
                                   for dd in o.doors.values() if dd["direction"] == "special")

    def _consistent(self, r: str, s: str, norm, result) -> bool:
        """The Open World source -> dest traversal of each connection of the group must be allowed by the rhelmot
        hole kinds (exit: out/inout, enter: in/inout) and by closes_behind on the exit door; otherwise the doors
        are not shuffled (kept vanilla)."""
        hole_of = {}
        for (dr, ds), (ia, ib, _) in result:
            hole_of[(r, dr)] = ia
            hole_of[(s, ds)] = ib
        for (i, _, _) in norm:
            c = self.ow_conns[i]
            src, dst = (c["source_room"], c["source_door"]), (c["dest_room"], c["dest_door"])
            # only the main (source -> dest) direction must survive; the way back may become one-way
            for (a, b) in [(src, dst)]:
                ka = self.rooms[a[0]]["holes"][hole_of[a]]["kind"]
                kb = self.rooms[b[0]]["holes"][hole_of[b]]["kind"]
                if ka not in ("out", "inout", "unknown") or kb not in ("in", "inout", "unknown"):
                    return False
                if self.ow_rooms[a[0]].doors[a[1]]["closes_behind"]:
                    return False
        return True

    def fixed_conns(self, vanilla: bool = False) -> list[dict]:
        """OW connections applied as-is (not through a layout link): special/warp, virtual rooms, locked pairs,
        non-adjacent rooms."""
        return [c for c, k in zip(self.ow_conns, self.conn_kind) if k != "mapped"]

    def warp_targets(self) -> list[tuple[str, str]]:
        """(room, room) couples linked by a warp/cutscene or a non-adjacent connection, through virtual rooms."""
        out = set()
        via: dict[str, set[str]] = defaultdict(set)
        for c, k in zip(self.ow_conns, self.conn_kind):
            special = (self.ow_rooms[c["source_room"]].doors[c["source_door"]]["direction"] == "special" or
                       self.ow_rooms[c["dest_room"]].doors[c["dest_door"]]["direction"] == "special")
            if k in ("special", "nongeo") or (k == "virtual" and special):
                via[c["source_room"]].add(c["dest_room"])
                via[c["dest_room"]].add(c["source_room"])
        for r in sorted(via):
            if r in self.virtual:
                continue
            seen, stack = {r}, [r]
            while stack:
                x = stack.pop()
                for y in via[x]:
                    if y in seen:
                        continue
                    seen.add(y)
                    if y in self.virtual:
                        stack.append(y)
                    else:
                        out.add((r, y))
        for lv, keys in self.levels.items():
            for w in self.rooms[keys[0]].get("warps") or []:
                if w in self.levels and w != lv:
                    out.add((lv, w))
                    out.add((w, lv))
        return sorted(out)


class StaticData:
    def __init__(self):
        rooms = _load_json("rooms.json")
        ow = _load_json("openworld_logic.json")
        _apply_extra_logic(ow, _load_json("cmr_extra_logic.json"))
        by_area: dict[str, list[dict]] = defaultdict(list)
        for lv in ow["levels"]:
            a = "10a" if lv["name"].startswith("10") else lv["name"]
            by_area[a].append(lv)
        self.areas: dict[str, Area] = {}
        for key in AREA_ORDER:
            if key in rooms["areas"]:
                self.areas[key] = Area(key, rooms["areas"][key], by_area.get(key, []))
        self.locations: dict[str, LocSpec] = {}
        self.events: dict[str, list[EventSpec]] = defaultdict(list)
        self.skipped_pickups: list[str] = []
        self._build_locations()

    def _add(self, spec: LocSpec):
        if spec.key in self.locations:
            raise ValueError(f"duplicate location key {spec.key}")
        self.locations[spec.key] = spec

    def _build_locations(self):
        for ak, area in self.areas.items():
            disp = area.display
            for level, keys in area.levels.items():
                raw = area.rooms[keys[0]]
                o = area.ow_rooms.get(level)
                self._add(LocSpec(f"{ak}|{level}|room", f"{disp} - Room {level}", ak, level, "room"))
                if o is not None and o.checkpoint not in ("", "Start"):
                    self._add(LocSpec(f"{ak}|{level}|checkpoint", f"{disp} - {o.checkpoint}", ak, level, "checkpoint"))
                elif o is None and level in area.checkpoint_rooms and level not in (area.start_room, area.ow_start_room):
                    self._add(LocSpec(f"{ak}|{level}|checkpoint", f"{disp} - Checkpoint {level}", ak, level, "checkpoint"))
                groups: dict[str, list[dict]] = defaultdict(list)
                for p in raw.get("pickups", []):
                    if p["kind"] in PICKUP_KINDS:
                        groups[p["kind"]].append(p)
                for pk, pks in sorted(groups.items()):
                    kind, owtype = PICKUP_KINDS[pk]
                    pks = sorted(pks, key=lambda p: p["entity_id"])
                    cands = []
                    if o is not None:
                        for reg in o.regions:
                            for loc in reg.get("locations", []):
                                if loc["type"] == owtype:
                                    cands.append((level, reg["name"], loc, o.display))
                    if not cands:
                        self.skipped_pickups.extend(f"{ak}|{level}|{kind}|{p['entity_id']}" for p in pks)
                        continue
                    exact = len(pks) == 1 and len(cands) == 1
                    by_name = sorted(cands, key=lambda c: c[2]["display_name"])
                    for i, p in enumerate(pks):
                        if kind == "strawberry":
                            if len(pks) == 1:
                                suffix = "Moon Berry" if exact and cands[0][2]["display_name"] == "Moon Berry" \
                                    else f"Room {level} Strawberry"
                            else:
                                suffix = f"Room {level} Strawberry {i + 1}"
                        elif kind in ("key", "gem"):
                            if len(pks) == len(cands):
                                suffix = by_name[i][2]["display_name"]
                            else:
                                suffix = f"Room {level} {kind.capitalize()} {i + 1}"
                        elif kind == "cassette":
                            suffix = "Cassette" if len(pks) == 1 else f"Cassette {i + 1}"
                        else:
                            suffix = "Crystal Heart" if len(pks) == 1 else f"Crystal Heart {i + 1}"
                        self._add(LocSpec(f"{ak}|{level}|{kind}|{p['entity_id']}", f"{disp} - {suffix}", ak, level,
                                          kind, cands if not exact else [cands[0]]))
            # level clear and events (any Open World room of the area, virtual ones included)
            for name, o in area.ow_rooms.items():
                for reg in o.regions:
                    for loc in reg.get("locations", []):
                        if loc["type"] == "level_clear":
                            self._add(LocSpec(f"{ak}|clear", f"{disp} - Level Clear", ak, name, "clear",
                                              [(name, reg["name"], loc, o.display)]))
                        elif loc["type"] in EVENT_TYPES:
                            self.events[ak].append(EventSpec(ak, name, reg["name"], loc, o.display,
                                                             f"{o.display} - {loc['display_name']}"))
        names = Counter(s.name for s in self.locations.values())
        dup = [n for n, c in names.items() if c > 1]
        if dup:
            raise ValueError(f"duplicate location names: {dup}")
        for i, key in enumerate(sorted(self.locations)):
            self.locations[key].id = LOCATION_BASE_ID + i
        self.locations_by_room: dict[tuple[str, str], list[LocSpec]] = defaultdict(list)
        for key in sorted(self.locations):
            spec = self.locations[key]
            self.locations_by_room[(spec.area, spec.level)].append(spec)
        self.locations_by_area: dict[str, list[LocSpec]] = defaultdict(list)
        for key in sorted(self.locations):
            self.locations_by_area[self.locations[key].area].append(self.locations[key])
        self.events_by_room: dict[tuple[str, str], list[EventSpec]] = defaultdict(list)
        for ak, evs in self.events.items():
            for ev in evs:
                self.events_by_room[(ak, ev.level)].append(ev)


_DATA: StaticData | None = None


def get_data() -> StaticData:
    global _DATA
    if _DATA is None:
        _DATA = StaticData()
    return _DATA
