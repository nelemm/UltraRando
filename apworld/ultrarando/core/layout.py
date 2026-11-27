"""Per-area room placement (Python port of rhelmot's "Pathway" idea) and the logic graph built from a layout.

Pure Python (no Archipelago import), so tools can use it directly.
"""
from __future__ import annotations

import logging
from collections import defaultdict, deque

from .geometry import INCOMPATIBLE, OPPOSITE, compatible, adjacent_position, hole_touch_span, intersects, room_rects
from .names import ALL_ABILITIES
from .rules import DIFFICULTIES, FREE, IMPOSSIBLE, ow_rule, rhelmot_rule, rule_and, satisfied
from .static_data import Area, StaticData, region_name, room_region_name

logger = logging.getLogger("UltraRando")

# rhelmot hole kinds (EXPORT_FORMAT.md): "unknown" holes are never used as exits by rhelmot and are only entered
# with its "Enter strange holes" setting, so the logic never relies on them in either direction.
EXIT_KINDS = ("out", "inout")
ENTER_KINDS = ("in", "inout")


def _req_needs_state(req) -> bool:
    """True if a rhelmot <req> needs a key or a session flag (a lock block, clutter, a switch...)."""
    if req is None or req == "impossible":
        return req == "impossible"
    if "and" in req:
        return any(_req_needs_state(r) for r in req["and"])
    if "or" in req:
        return all(_req_needs_state(r) for r in req["or"])
    return "key" in req or "flag" in req


def entry_obstructed(area: Area, raw: dict, hole: int) -> bool:
    """True if Madeline cannot arrive in this room through `hole` without the room's own state being changed first
    (Open World door flagged "blocked": lock block, breakable wall, gate right behind the door; or a rhelmot req_in
    needing a key/flag: clutter, lock). Arriving there anyway would freeze the screen transition (the player is
    pushed into a solid), so such a hole is exit-only for the logic and, when it is linked to a non-vanilla
    neighbour, the mod puts a barrier on the neighbour's side ("no_enter" in slot_data)."""
    if raw["holes"][hole]["kind"] not in ENTER_KINDS:
        return False  # not an entrance at all: the hole kind already says so (rhelmot closes the other side of "out")
    door = area.hole_door.get((raw["level"], hole))
    if door is not None:
        o = area.ow_rooms.get(raw["level"])
        if o is not None and o.doors[door].get("blocked"):
            return True
    for nd in raw["nodes"].values():
        for he in nd.get("hole_edges", []):
            if he["hole"] == hole and _req_needs_state(he["req_in"]):
                return True
    return False


# --------------------------------------------------------------------------------------------- room models
def _req_difficulty(req) -> float:
    """Lowest rhelmot difficulty tier (0 easy .. 5 perfect) that satisfies a <req>, items ignored."""
    if req is None:
        return 0
    if req == "impossible":
        return float("inf")
    if "and" in req:
        return max([_req_difficulty(r) for r in req["and"]] or [0])
    if "or" in req:
        return min([_req_difficulty(r) for r in req["or"]] or [float("inf")])
    if "difficulty" in req:
        return DIFFICULTIES.index(req["difficulty"])
    return 0  # dashes, keys, flags: not a skill tier


def hole_difficulty(raw: dict, hole: int) -> tuple:
    """(exit, enter) skill tier of one hole: the easiest rhelmot hole edge of any node. No edge: 0 (no data)."""
    outs, ins = [], []
    for nd in raw.get("nodes", {}).values():
        for he in nd.get("hole_edges", []):
            if he["hole"] == hole:
                outs.append(_req_difficulty(he.get("req_out")))
                ins.append(_req_difficulty(he.get("req_in")))
    return (min(outs) if outs else 0, min(ins) if ins else 0)


class RoomModel:
    """Logic of one room (one variant) for a logic difficulty: regions, internal edges, hole attachments,
    locations and events."""

    def __init__(self, data: StaticData, area: Area, level: str, variant: str | None, diff: int, room_diff: int):
        self.area = area.key
        self.level = level
        self.variant = variant
        self.regions: list[str] = []
        self.edges: list[tuple[str, str, tuple]] = []
        self.attach: dict[int, tuple[str, tuple, tuple]] = {}
        self.spawns: list[str] = []
        self.locs: list[tuple] = []  # (LocSpec, region, rule, extra [(region, rule)])
        self.events: list[tuple] = []  # (location name, item name, region, rule)
        ak = area.key
        o = area.ow_rooms.get(level)
        raw = area.rooms.get(variant) if variant else None
        self.room_region = room_region_name(ak, level) if raw is not None else None
        if self.room_region:
            self.regions.append(self.room_region)

        if o is not None:
            for reg in o.regions:
                rn = region_name(ak, level, reg["name"])
                self.regions.append(rn)
                for c in reg.get("connections", []):
                    rule = ow_rule(c, diff, o.display)
                    if rule:
                        self.edges.append((rn, region_name(ak, level, c["dest"]), rule))
                if self.room_region:
                    self.edges.append((rn, self.room_region, FREE))
            if raw is not None:
                for h in raw["holes"]:
                    door = area.hole_door.get((level, h["index"]))
                    if door is None or h["kind"] == "none":
                        continue
                    dd = o.doors[door]
                    ex = FREE if (h["kind"] in EXIT_KINDS and not dd["closes_behind"]) else IMPOSSIBLE
                    en = FREE if (h["kind"] in ENTER_KINDS and not entry_obstructed(area, raw, h["index"])) else IMPOSSIBLE
                    self.attach[h["index"]] = (region_name(ak, level, door), ex, en)
            if level == area.ow_start_room:
                self.spawns.append(region_name(ak, level, area.ow_start_region))
        elif raw is not None:
            nodes = raw["nodes"]
            for node, nd in nodes.items():
                rn = region_name(ak, level, node)
                self.regions.append(rn)
                self.edges.append((rn, self.room_region, FREE))
                for e in nd.get("edges", []):
                    rule = rhelmot_rule(e["req_out"], room_diff)
                    if rule:
                        self.edges.append((rn, region_name(ak, level, e["to_node"]), rule))
                for he in nd.get("hole_edges", []):
                    h = raw["holes"][he["hole"]]
                    if h["kind"] in ("none", "unknown") or he["hole"] in self.attach:
                        continue
                    ex = rhelmot_rule(he["req_out"], room_diff) if h["kind"] in ("out", "inout") else IMPOSSIBLE
                    en = rhelmot_rule(he["req_in"], room_diff) if h["kind"] in ("in", "inout") else IMPOSSIBLE
                    self.attach[he["hole"]] = (rn, ex, en)
            if level == area.start_room:
                self.spawns.append(region_name(ak, level, "main" if "main" in nodes else next(iter(nodes))))

        # locations of the static table that belong to this room
        for spec in data.locations_by_room.get((ak, level), []):
            if spec.kind in ("room", "checkpoint"):
                if self.room_region:
                    self.locs.append((spec, self.room_region, FREE, []))
                continue
            first, extra = None, []
            for (lv, reg, owloc, disp) in spec.cands:
                item = (region_name(ak, lv, reg), ow_rule(owloc, diff, disp))
                if first is None:
                    first = item
                else:
                    extra.append(item)
            self.locs.append((spec, first[0], first[1], extra))
        for ev in data.events_by_room.get((ak, level), []):
            self.events.append((ev.item + " (event)", ev.item, region_name(ak, level, ev.region),
                                ow_rule(ev.owloc, diff, ev.display)))


_MODEL_CACHE: dict[tuple, RoomModel] = {}


def room_model(data: StaticData, area: Area, level: str, variant: str | None, diff: int, room_diff: int) -> RoomModel:
    key = (area.key, level, variant, diff, room_diff)
    m = _MODEL_CACHE.get(key)
    if m is None:
        m = _MODEL_CACHE[key] = RoomModel(data, area, level, variant, diff, room_diff)
    return m


# --------------------------------------------------------------------------------------------- layout
class Layout:
    def __init__(self, area: str, vanilla: bool):
        self.area = area
        self.vanilla = vanilla
        self.placed: dict[str, tuple[str, int, int]] = {}  # level -> (variant key, x, y)
        self.links: list[tuple[str, int, str, int]] = []
        self.fallback = False

    def closed_holes(self, area: Area) -> list[tuple[str, int]]:
        used = set()
        for (a, ia, b, ib) in self.links:
            used.add((a, ia))
            used.add((b, ib))
        out = []
        for level, (variant, _, _) in self.placed.items():
            for h in area.rooms[variant]["holes"]:
                if (level, h["index"]) not in used:
                    out.append((level, h["index"]))
        return out

    def link_info(self, data: StaticData, area: Area, link, diff: int, room_diff: int) -> dict:
        """Extra slot_data fields of a link: "dir" = directions the logic may use ("ab", "ba", "both", "none" when
        the logic goes through an Open World connection of a locked vanilla pair instead), "obstructed" = rooms
        ("a", "b", "ab") whose hole of this link cannot be entered before the room's own obstacle is cleared
        (see entry_obstructed), "no_enter" = the same for non-vanilla links only: the mod closes the other side
        (vanilla pairs keep the vanilla behaviour)."""
        a, ia, b, ib = link
        va, vb = self.placed[a][0], self.placed[b][0]
        ma, mb = room_model(data, area, a, va, diff, room_diff), room_model(data, area, b, vb, diff, room_diff)
        ab = ba = False
        if ia in ma.attach and ib in mb.attach:
            _, exa, ena = ma.attach[ia]
            _, exb, enb = mb.attach[ib]
            ab, ba = bool(rule_and(exa, enb)), bool(rule_and(exb, ena))
        out = {"dir": "both" if ab and ba else "ab" if ab else "ba" if ba else "none"}
        ne = ""
        if entry_obstructed(area, area.rooms[va], ia):
            ne += "a"
        if entry_obstructed(area, area.rooms[vb], ib):
            ne += "b"
        if ne:
            out["obstructed"] = ne
            if (a, ia, b, ib) not in area.vanilla_pairs and (b, ib, a, ia) not in area.vanilla_pairs:
                out["no_enter"] = ne
        return out

    def to_slot(self, area: Area, data: StaticData | None = None, diff: int = 1, room_diff: int = 2) -> dict:
        links = []
        for l in self.links:
            d = {"a": l[0], "a_hole": l[1], "b": l[2], "b_hole": l[3]}
            if data is not None:
                d.update(self.link_info(data, area, l, diff, room_diff))
            links.append(d)
        return {
            "rooms": [{"room": lv, "variant": v, "x": x, "y": y} for lv, (v, x, y) in self.placed.items()],
            "links": links,
            "closed_holes": [{"room": lv, "hole": h} for (lv, h) in self.closed_holes(area)],
        }


def vanilla_layout(area: Area) -> Layout:
    lay = Layout(area.key, True)
    for level, keys in area.levels.items():
        b = area.rooms[keys[0]]["bounds"]
        lay.placed[level] = (keys[0], b["x"], b["y"])
    lay.links = list(area.vanilla_links)
    return lay


# --------------------------------------------------------------------------------------------- logic graph
class LogicGraph:
    def __init__(self):
        self.regions: list[str] = []
        self.edges: list[tuple[str, str, tuple]] = []
        self.locs: list[tuple] = []
        self.events: list[tuple] = []
        self.spawns: list[str] = []
        self.models: dict[str, RoomModel] = {}


def build_graph(data: StaticData, area: Area, lay: Layout, diff: int, room_diff: int) -> LogicGraph:
    g = LogicGraph()
    present: set[str] = set()
    for level, (variant, _, _) in lay.placed.items():
        g.models[level] = room_model(data, area, level, variant, diff, room_diff)
        present.add(level)
    for v in sorted(area.virtual):
        g.models[v] = room_model(data, area, v, None, diff, room_diff)
        present.add(v)
    seen = set()
    for m in g.models.values():
        for r in m.regions:
            if r not in seen:
                seen.add(r)
                g.regions.append(r)
        g.edges.extend(m.edges)
        g.locs.extend(m.locs)
        g.events.extend(m.events)
        g.spawns.extend(m.spawns)
    for (a, ia, b, ib) in lay.links:
        ma, mb = g.models[a], g.models[b]
        if ia in ma.attach and ib in mb.attach:
            ra, exa, ena = ma.attach[ia]
            rb, exb, enb = mb.attach[ib]
            r1 = rule_and(exa, enb)
            if r1:
                g.edges.append((ra, rb, r1))
            r2 = rule_and(exb, ena)
            if r2:
                g.edges.append((rb, ra, r2))
    for c in area.fixed_conns():
        r, s = c["source_room"], c["dest_room"]
        if r not in present or s not in present:
            continue
        rr, sr = region_name(area.key, r, c["source_door"]), region_name(area.key, s, c["dest_door"])
        if rr not in seen or sr not in seen:
            continue
        g.edges.append((rr, sr, FREE))
        if not area.ow_rooms[s].doors[c["dest_door"]]["closes_behind"]:
            g.edges.append((sr, rr, FREE))
    return g


class Reach:
    regions: set
    locs: list
    events: list
    items: set


def evaluate(g: LogicGraph, base_items=ALL_ABILITIES) -> Reach:
    """Reachability with every movement/interactable item, keys and other events earned at their location."""
    items = set(base_items)
    adj: dict[str, list[tuple[str, tuple]]] = defaultdict(list)
    for (u, v, rule) in g.edges:
        adj[u].append((v, rule))
    got_events: set[int] = set()
    while True:
        reached = set(g.spawns)
        dq = deque(g.spawns)
        while dq:
            u = dq.popleft()
            for (v, rule) in adj[u]:
                if v not in reached and satisfied(rule, items):
                    reached.add(v)
                    dq.append(v)
        new = False
        for i, (_, item, reg, rule) in enumerate(g.events):
            if i not in got_events and reg in reached and satisfied(rule, items):
                got_events.add(i)
                items.add(item)
                new = True
        if not new:
            break
    res = Reach()
    res.regions = reached
    res.items = items
    res.events = [g.events[i] for i in sorted(got_events)]
    res.locs = [l for l in g.locs
                if l[1] in reached and satisfied(l[2], items) and all(r in reached and satisfied(rr, items)
                                                                      for r, rr in l[3])]
    return res


def clear_reachable(reach: Reach, area: Area) -> bool:
    return any(spec.kind == "clear" for (spec, *_rest) in reach.locs)


# --------------------------------------------------------------------------------------------- placement
class _Unit:
    def __init__(self, members: list[str], offsets: dict[str, tuple[int, int]], links):
        self.members = members
        self.offsets = offsets  # member -> (dx, dy) from the unit origin (vanilla relative positions)
        self.links = links      # locked vanilla pairs inside the unit
        self.link_holes = {(p[0], p[1]) for p in links} | {(p[2], p[3]) for p in links}


def _units(area: Area) -> list[_Unit]:
    parent = {lv: lv for lv in area.levels}

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for (a, _, b, _) in sorted(area.locked_pairs):
        parent[find(a)] = find(b)
    groups: dict[str, list[str]] = defaultdict(list)
    for lv in area.levels:
        groups[find(lv)].append(lv)
    units = []
    for root in sorted(groups):
        mem = sorted(groups[root])
        b0 = area.rooms[area.levels[mem[0]][0]]["bounds"]
        offs = {}
        for m in mem:
            b = area.rooms[area.levels[m][0]]["bounds"]
            offs[m] = (b["x"] - b0["x"], b["y"] - b0["y"])
        links = [p for p in sorted(area.locked_pairs) if p[0] in offs and p in area.vanilla_links]
        units.append(_Unit(mem, offs, links))
    return units


def end_levels(data: StaticData, area: Area) -> set[str]:
    """Real rooms holding the level clear (or leading to the virtual room that holds it)."""
    out = set()
    for spec in data.locations_by_area.get(area.key, []):
        if spec.kind == "clear":
            lv = spec.cands[0][0]
            if lv in area.levels:
                out.add(lv)
            else:
                for c in area.ow_conns:
                    if c["dest_room"] == lv and c["source_room"] in area.levels:
                        out.add(c["source_room"])
    return out


class _Placer:
    def __init__(self, data: StaticData, area: Area, diff: int, room_diff: int, random):
        self.data, self.area, self.diff, self.room_diff, self.random = data, area, diff, room_diff, random
        self.lay = Layout(area.key, False)
        self.rects: list[tuple] = []
        self.used: set[tuple[str, int]] = set()
        self.adj: dict[str, list[str]] = defaultdict(list)
        self.reached: set[str] = set()
        self.units = _units(area)
        self.unit_of = {m: u for u in self.units for m in u.members}
        self.unplaced = list(self.units)
        self.dead: set[tuple[str, int]] = set()
        self.region_room: dict[str, str] = {}
        self._exp_cache: dict[tuple, int] = {}
        self.end_levels = end_levels(data, area)
        self.detached: set[str] = set()
        for v in sorted(area.virtual):
            self._add_model(room_model(data, area, v, None, diff, room_diff))

    # graph helpers
    def _bfs(self, start: str):
        if start in self.reached:
            return
        self.reached.add(start)
        dq = deque([start])
        while dq:
            u = dq.popleft()
            for v in self.adj[u]:
                if v not in self.reached:
                    self.reached.add(v)
                    dq.append(v)

    def _edge(self, u: str, v: str):
        self.adj[u].append(v)
        if u in self.reached and v not in self.reached:
            self._bfs(v)

    def _add_model(self, m: RoomModel):
        for r in m.regions:
            self.region_room[r] = m.level
        for (u, v, rule) in m.edges:
            if rule:
                self._edge(u, v)

    def model(self, level: str) -> RoomModel:
        variant = self.lay.placed[level][0] if level in self.lay.placed else None
        return room_model(self.data, self.area, level, variant, self.diff, self.room_diff)

    def _link_edges(self, a: str, ia: int, b: str, ib: int):
        ma, mb = self.model(a), self.model(b)
        if ia in ma.attach and ib in mb.attach:
            ra, exa, ena = ma.attach[ia]
            rb, exb, enb = mb.attach[ib]
            if exa and enb:
                self._edge(ra, rb)
            if exb and ena:
                self._edge(rb, ra)

    def _fixed_edges(self):
        present = set(self.lay.placed) | self.area.virtual
        for c in self.area.fixed_conns():
            r, s = c["source_room"], c["dest_room"]
            if r in present and s in present:
                rr = region_name(self.area.key, r, c["source_door"])
                sr = region_name(self.area.key, s, c["dest_door"])
                self._edge(rr, sr)
                if not self.area.ow_rooms[s].doors[c["dest_door"]]["closes_behind"]:
                    self._edge(sr, rr)

    def place_unit(self, unit: _Unit, variant: str | None, ox: int, oy: int, link=None):
        self.unplaced.remove(unit)
        for m in unit.members:
            v = variant if (variant and len(unit.members) == 1) else self.area.levels[m][0]
            dx, dy = unit.offsets[m]
            self.lay.placed[m] = (v, ox + dx, oy + dy)
            self.rects.extend(room_rects(self.area.rooms[v], ox + dx, oy + dy))
        for m in unit.members:
            mm = self.model(m)
            self._add_model(mm)
        for p in unit.links:
            self.lay.links.append(p)
            self.used.add((p[0], p[1]))
            self.used.add((p[2], p[3]))
            self._link_edges(*p)
        if link is not None:
            self.lay.links.append(link)
            self.used.add((link[0], link[1]))
            self.used.add((link[2], link[3]))
            self._link_edges(*link)
        self._fixed_edges()
        for m in unit.members:
            for s in self.model(m).spawns:
                self._bfs(s)

    def area_free(self, rects) -> bool:
        for r in rects:
            for q in self.rects:
                if intersects(r, q):
                    return False
        return True

    def unit_rects(self, unit: _Unit, variant: str | None, ox: int, oy: int):
        out = []
        for m in unit.members:
            v = variant if (variant and len(unit.members) == 1) else self.area.levels[m][0]
            dx, dy = unit.offsets[m]
            out.extend(room_rects(self.area.rooms[v], ox + dx, oy + dy))
        return out

    def place_root(self, level: str):
        unit = self.unit_of[level]
        if unit not in self.unplaced:
            return
        m0 = unit.members[0]
        b = self.area.rooms[self.area.levels[m0][0]]["bounds"]
        self.place_unit(unit, None, b["x"], b["y"])

    def place_anywhere(self, src: str, target: str) -> bool:
        unit = self.unit_of[target]
        m0 = unit.members[0]
        b = self.area.rooms[self.area.levels[m0][0]]["bounds"]
        if self.area_free(self.unit_rects(unit, None, b["x"], b["y"])):
            self.place_unit(unit, None, b["x"], b["y"])
            return True
        _, sx, sy = self.lay.placed[src]
        dist = 1200
        for _ in range(12):
            dirs = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
            self.random.shuffle(dirs)
            for dx, dy in dirs:
                ox, oy = sx + dx * dist, sy + dy * dist
                if self.area_free(self.unit_rects(unit, None, ox, oy)):
                    self.place_unit(unit, None, ox, oy)
                    return True
            dist *= 2
        return False

    STRICT = True  # grow only from exits that the real logic reaches (keys and events earned where they are)

    def _strict_reach(self) -> Reach:
        return evaluate(build_graph(self.data, self.area, self.lay, self.diff, self.room_diff))

    def entered(self, level: str, reach: Reach | None = None) -> bool:
        regions = reach.regions if reach is not None else self.reached
        return any(r in regions for r in self.model(level).regions)

    def tier_ok(self, m: RoomModel, h: int, way: int) -> bool:
        """Room difficulty: a shuffled link never uses a hole that rhelmot rates above the chosen tier (way 0 = leave
        through it, 1 = enter). Vanilla links are not filtered: the vanilla game is completable."""
        raw = self.area.rooms.get(m.variant) if m.variant else None
        return raw is None or hole_difficulty(raw, h)[way] <= self.room_diff

    def frontier(self, reach: Reach | None = None):
        out = []
        for level in sorted(self.lay.placed):
            m = self.model(level)
            for h, (reg, ex, _) in sorted(m.attach.items()):
                if (level, h) in self.used or (level, h) in self.dead or not self.tier_ok(m, h, 0):
                    continue
                if reach is not None:
                    if reg in reach.regions and satisfied(ex, reach.items):
                        out.append((level, h))
                elif ex and reg in self.reached:
                    out.append((level, h))
        return out

    def candidates(self, side: str):
        want = OPPOSITE[side]
        out = []
        for unit in self.unplaced:
            if any(m in self.detached for m in unit.members):
                continue
            variants = self.area.levels[unit.members[0]] if len(unit.members) == 1 else [None]
            for v in variants:
                for m in unit.members:
                    vk = v if v else self.area.levels[m][0]
                    mm = room_model(self.data, self.area, m, vk, self.diff, self.room_diff)
                    for h, (_, _, en) in mm.attach.items():
                        if (m, h) in unit.link_holes:
                            continue
                        if en and self.area.rooms[vk]["holes"][h]["side"] == want and self.tier_ok(mm, h, 1):
                            out.append((unit, v, m, vk, h))
        return out

    def expanding(self, unit: _Unit, vk: str, m: str, h: int) -> int:
        """Number of other exits reachable when entering `m` through hole `h` (optimistic rules)."""
        key = (vk, m, h, len(unit.members))
        r = self._exp_cache.get(key)
        if r is None:
            models = [room_model(self.data, self.area, mm, vk if len(unit.members) == 1 else self.area.levels[mm][0],
                                 self.diff, self.room_diff) for mm in unit.members]
            adj = defaultdict(list)
            for mo in models:
                for (u, v, rule) in mo.edges:
                    if rule:
                        adj[u].append(v)
            for (a, ia, b, ib) in unit.links:
                ma = models[unit.members.index(a)]
                mb = models[unit.members.index(b)]
                if ia in ma.attach and ib in mb.attach:
                    ra, exa, ena = ma.attach[ia]
                    rb, exb, enb = mb.attach[ib]
                    if exa and enb:
                        adj[ra].append(rb)
                    if exb and ena:
                        adj[rb].append(ra)
            start = models[unit.members.index(m)].attach[h][0]
            seen, dq = {start}, deque([start])
            while dq:
                u = dq.popleft()
                for v in adj[u]:
                    if v not in seen:
                        seen.add(v)
                        dq.append(v)
            r = sum(1 for mo in models for hh, (reg, ex, _) in mo.attach.items()
                    if reg in seen and ex and (mo.level, hh) != (m, h) and (mo.level, hh) not in unit.link_holes)
            self._exp_cache[key] = r
        return r

    def try_link(self, level: str, h: int, n_frontier: int) -> bool:
        variant, ax, ay = self.lay.placed[level]
        A = self.area.rooms[variant]
        ha = A["holes"][h]
        cands = self.candidates(ha["side"])
        self.random.shuffle(cands)
        if n_frontier <= 3:
            # the map could stop growing: try rooms that lead further first
            cands.sort(key=lambda c: -min(2, self.expanding(c[0], c[3], c[2], c[4])))
        if n_frontier >= 2 and any(e not in self.lay.placed for e in self.end_levels):
            # place the end room on a side branch sooner or later
            bias = 0.1 + 0.9 * len(self.lay.placed) / max(1, len(self.area.levels))
            if self.random.random() < bias:
                cands.sort(key=lambda c: 0 if any(mm in self.end_levels for mm in c[0].members) else 1)
        for (unit, v, m, vk, hb_i) in cands:
            B = self.area.rooms[vk]
            hb = B["holes"][hb_i]
            off = compatible(ha, hb)
            if off == INCOMPATIBLE:
                continue
            bx, by = adjacent_position(ax, ay, A["bounds"]["w"], A["bounds"]["h"], ha["side"], off,
                                       B["bounds"]["w"], B["bounds"]["h"])
            if not hole_touch_span(A, ax, ay, ha, B, bx, by, hb):
                continue  # rhelmot "launch" alignment that leaves the two openings apart (7a e-11 hole 2)
            dx, dy = unit.offsets[m]
            ox, oy = bx - dx, by - dy
            if not self.area_free(self.unit_rects(unit, v, ox, oy)):
                continue
            self.place_unit(unit, v, ox, oy, (level, h, m, hb_i))
            return True
        return False

    def run(self) -> Layout:
        roots = [self.area.start_room]
        if self.area.ow_start_room and self.area.ow_start_room in self.area.levels:
            roots.append(self.area.ow_start_room)
        # A root without any exit nor vanilla link (intro cutscene rooms: 6a "start", a 320x3160 column; 10a
        # "intro-00-past") is left by a cutscene warp, never by a screen edge. Placed first at its vanilla position,
        # it walls off the growth of the map (6a: most exits face right and hit the column). Place it last.
        detached = [r for r in roots if self._detached(r)]
        if len(detached) == len(roots):  # 10a: the intro room is the only root, its warps grow the map from it
            detached = []
        self.detached = set(detached)  # never linked to an exit: they are only entered by spawning (walk test 6a)
        for r in roots:
            if r not in detached:
                self.place_root(r)
        # lock vanilla adjacency between the root rooms (e.g. 7a a-00-intro -> a-00)
        if len(roots) == 2 and self.unit_of[roots[0]] is not self.unit_of[roots[1]]:
            for (a, ia, b, ib) in self.area.vanilla_links:
                if {a, b} == set(roots) and (a, ia) not in self.used and (b, ib) not in self.used:
                    self.lay.links.append((a, ia, b, ib))
                    self.used.add((a, ia))
                    self.used.add((b, ib))
                    self._link_edges(a, ia, b, ib)
        warps = self.area.warp_targets()
        while True:
            progress = False
            reach = self._strict_reach() if self.STRICT else None
            for (src, dst) in warps:
                if src in self.lay.placed and dst not in self.lay.placed and self.entered(src, reach):
                    if self.place_anywhere(src, dst):
                        progress = True
            if progress and reach is not None:
                reach = self._strict_reach()
            fr = self.frontier(reach)
            if not fr:
                if progress:
                    continue
                break
            level, h = self.random.choice(fr)
            if not self.try_link(level, h, len(fr)):
                self.dead.add((level, h))
        for r in detached:
            self.place_free(r)
        self._keep_start_in_place()
        return self.lay

    def _keep_start_in_place(self) -> None:
        """Translate the whole map so that the start room keeps its vanilla position (a detached start room may
        have been put elsewhere). Relative positions, hence every link, are unchanged."""
        st = self.area.start_room
        if st not in self.lay.placed:
            return
        v, x, y = self.lay.placed[st]
        b = self.area.rooms[v]["bounds"]
        dx, dy = b["x"] - x, b["y"] - y
        if dx or dy:
            self.lay.placed = {lv: (vv, xx + dx, yy + dy) for lv, (vv, xx, yy) in self.lay.placed.items()}
            self.rects = [(r[0] + dx, r[1] + dy) + tuple(r[2:]) for r in self.rects]

    def _detached(self, level: str) -> bool:
        m = room_model(self.data, self.area, level, self.area.levels[level][0], self.diff, self.room_diff)
        if any(ex for (_, ex, _) in m.attach.values()):
            return False
        return not any(level in (a, b) for (a, _, b, _) in self.area.vanilla_links)

    def place_free(self, level: str) -> None:
        """Place a room that links to nothing: its vanilla position if free, else the nearest free spot found
        by moving away from it (the mod shifts the whole map back so the start room sits at its vanilla spot)."""
        unit = self.unit_of[level]
        if unit not in self.unplaced:
            return
        b = self.area.rooms[self.area.levels[unit.members[0]][0]]["bounds"]
        dist = 0
        for _ in range(16):
            dirs = [(0, 0)] if dist == 0 else [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
            for dx, dy in dirs:
                ox, oy = b["x"] + dx * dist, b["y"] + dy * dist
                if self.area_free(self.unit_rects(unit, None, ox, oy)):
                    self.place_unit(unit, None, ox, oy)
                    return
            dist = 1200 if dist == 0 else dist * 2
        raise RuntimeError(f"no free spot for {self.area.key} {level}")


def prune_unreachable(data: StaticData, area: Area, lay: Layout, diff: int, room_diff: int) -> Reach:
    while True:
        g = build_graph(data, area, lay, diff, room_diff)
        reach = evaluate(g)
        drop = [lv for lv in lay.placed if not any(r in reach.regions for r in g.models[lv].regions)]
        if not drop:
            return reach
        for lv in drop:
            del lay.placed[lv]
        lay.links = [l for l in lay.links if l[0] in lay.placed and l[2] in lay.placed]


def make_layout(data: StaticData, area: Area, shuffle: bool, diff: int, room_diff: int, random,
                attempts: int | None = None, coverage: float = 0.9) -> Layout:
    """Shuffled layout: best of several greedy placements (most rooms placed, end reachable), else vanilla.

    Stops as soon as `coverage` of the rooms are placed (option room_coverage, 90 % by default). The number of tries depends only on the area size (never on
    time) so that a seed always gives the same layout; 8000 // rooms gives about 1 to 3 s for the big areas and
    +2 to +10 points of placed rooms over 24 tries (tools/layout_stats.py, 26/11)."""
    if shuffle:
        best = None
        n = len(area.levels)
        if attempts is None:
            attempts = max(24, 8000 // max(1, n))
        for _ in range(attempts):
            lay = _Placer(data, area, diff, room_diff, random).run()
            reach = prune_unreachable(data, area, lay, diff, room_diff)
            if clear_reachable(reach, area) and (best is None or len(lay.placed) > len(best.placed)):
                best = lay
                if len(best.placed) >= coverage * n:
                    break
        if best is not None:
            return best
        logger.warning(f"UltraRando: could not shuffle area {area.key} after {attempts} tries, "
                       f"using the vanilla layout")
        lay = vanilla_layout(area)
        lay.fallback = True
        return lay
    return vanilla_layout(area)
