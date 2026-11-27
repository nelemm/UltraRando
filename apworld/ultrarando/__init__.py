"""UltraRando: Archipelago world.

Room geometry from rhelmot's Celeste Randomizer (MIT), room logic from Celeste (Open World) 1.1.3 by PoryGone
(Modified MIT, see data/LICENSE_OpenWorld.txt). Our code: MIT (see LICENSE).
"""
from __future__ import annotations

import json
import logging
import os
from typing import Any

from BaseClasses import CollectionState, Item, ItemClassification, Location, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .Items import CMRItem, item_name_to_id, item_name_groups, CHECKPOINT_ITEMS, KEY_ITEMS
from .Options import CMROptions, cmr_option_groups, cmr_options_presets
from .core import names as N
from .core.layout import Layout, build_graph, evaluate, make_layout
from .core.rules import is_free, rule_or
from .core.static_data import get_data, region_name

logger = logging.getLogger("UltraRando")

GOAL_AREAS = {0: "7a", 1: "7b", 2: "7c", 3: "9a", 4: "9b", 5: "9c", 6: "10a"}
A_SIDES = ["0a", "1a", "2a", "3a", "4a", "5a", "6a", "7a"]
SLOT_DATA_VERSION = 1


class CMRLocation(Location):
    game = N.GAME


class CMRWebWorld(WebWorld):
    theme = "ice"
    option_groups = cmr_option_groups
    options_presets = cmr_options_presets
    tutorials = [Tutorial("Setup Guide", "How to play UltraRando with Archipelago.", "English",
                          "setup_en.md", "setup/en", ["UltraRando"])]


def _compile(rule, player: int):
    """Archipelago access rule from a tuple of alternatives (None when free)."""
    if is_free(rule):
        return None
    alts = [tuple(sorted(a)) for a in rule]
    if len(alts) == 1:
        a = alts[0]
        if len(a) == 1:
            name = a[0]
            return lambda state: state.has(name, player)
        return lambda state: state.has_all(a, player)
    return lambda state: any(state.has_all(a, player) for a in alts)


def _lock_keys(area) -> dict[str, list[str]]:
    """Rooms of an area that hold lockBlocks -> the key items that the Open World logic asks for in that room."""
    out: dict[str, list[str]] = {}
    for level, variants in area.levels.items():
        if "lockBlock" not in area.rooms[variants[0]].get("entities", []):
            continue
        ow = area.ow_rooms.get(level)
        if ow is None:
            continue
        toks: set[str] = set()
        for reg in ow.regions:
            for c in reg.get("connections", []) + reg.get("locations", []):
                for k in ("rule", "vm_rule", "assist_rule"):
                    for alt in c.get(k) or []:
                        toks.update(t for t in alt if "Key" in t)
        names = sorted(f"{ow.display} - {t}" for t in toks)
        if names:
            out[f"{area.key}|{level}"] = [n for n in names if n in KEY_ITEMS]
    return {k: v for k, v in out.items() if v}


class CMRWorld(World):
    """
    UltraRando: every chapter of Celeste is rebuilt from shuffled rooms, and Madeline's movement
    (dash directions, climb, crouch, wall jump) and the level objects are items.
    """
    game = N.GAME
    web = CMRWebWorld()
    options_dataclass = CMROptions
    options: CMROptions
    topology_present = True

    _data = get_data()
    item_name_to_id = item_name_to_id
    item_name_groups = item_name_groups
    location_name_to_id = {spec.name: spec.id for spec in _data.locations.values()}
    location_name_groups = {
        "Strawberries": {s.name for s in _data.locations.values() if s.kind == "strawberry"},
        "Rooms": {s.name for s in _data.locations.values() if s.kind == "room"},
        "Checkpoints": {s.name for s in _data.locations.values() if s.kind == "checkpoint"},
        "Level Clears": {s.name for s in _data.locations.values() if s.kind == "clear"},
    }

    active_areas: list[str]
    layouts: dict[str, Layout]
    goal_area: str
    strawberries_required: int

    # ------------------------------------------------------------------ setup
    def generate_early(self) -> None:
        o = self.options
        if o.logic_difficulty.value == 0:
            o.climb_shuffle.value = 0
        if o.wall_jump_shuffle:
            o.roomsanity.value = 1
        self.goal_area = GOAL_AREAS[o.goal_area.value]
        active = set(A_SIDES)
        if o.include_core:
            active.add("9a")
        if o.include_farewell:
            active.add("10a")
        if o.include_b_sides:
            active.update(f"{i}b" for i in range(1, 8))
            if o.include_core:
                active.add("9b")
        if o.include_c_sides:
            active.update(f"{i}c" for i in range(1, 8))
            if o.include_core:
                active.add("9c")
        active.add(self.goal_area)
        self.active_areas = [a for a in N.AREA_ORDER if a in active]
        self.layouts = {}
        self.created_keys: list[str] = []
        self.referenced_items: set[str] = set()
        self.checkpoint_items: dict[str, str] = {}  # item name -> "area|level", checkpoints usable in this seed
        self.key_items: set[str] = set()             # keysanity: key items of the active areas
        self.locks: dict[str, list[str]] = {}         # keysanity: "area|level" of a room with lockBlocks -> its keys

    # ------------------------------------------------------------------ regions
    def create_regions(self) -> None:
        data = self._data
        player = self.player
        o = self.options
        diff, room_diff = o.logic_difficulty.value, o.room_difficulty.value
        menu = Region("Menu", player, self.multiworld)
        self.multiworld.regions.append(menu)
        regions: dict[str, Region] = {"Menu": menu}

        for ak in self.active_areas:
            area = data.areas[ak]
            lay = make_layout(data, area, bool(o.room_shuffle), diff, room_diff, self.random,
                              coverage=o.room_coverage.value / 100)
            if lay.fallback:
                logger.warning(f"UltraRando: player {self.player_name}: area {ak} uses its vanilla layout")
            self.layouts[ak] = lay
            g = build_graph(data, area, lay, diff, room_diff)
            reach = evaluate(g)

            for rn in g.regions:
                if rn in reach.regions and rn not in regions:
                    r = Region(rn, player, self.multiworld)
                    regions[rn] = r
                    self.multiworld.regions.append(r)
            merged: dict[tuple[str, str], tuple] = {}
            for (u, v, rule) in g.edges:
                if u in reach.regions and v in reach.regions and rule:
                    merged[(u, v)] = rule_or(merged[(u, v)], rule) if (u, v) in merged else rule
            for (u, v), rule in merged.items():
                for alt in rule:
                    self.referenced_items.update(alt)
                regions[u].connect(regions[v], f"{u} -> {v}", _compile(rule, player))

            # the goal chapter is locked until enough strawberries are received (as in Open World); its checks can
            # hold items for earlier chapters, the logic sees them only once the strawberries are reachable
            wj = bool(o.wall_jump_shuffle) and ak != "0a"
            locked = ak == self.goal_area
            berries = lambda state: state.has(N.STRAWBERRY, player, self.strawberries_required)

            def entry(extra=None, wj=wj, locked=locked):
                conds = ([lambda state: state.has(N.WALL_JUMP, player)] if wj else []) + ([berries] if locked else [])                         + ([extra] if extra else [])
                if not conds:
                    return None
                return lambda state: all(c(state) for c in conds)

            for spawn in g.spawns:
                if spawn in regions:
                    menu.connect(regions[spawn], f"Menu -> {spawn}", entry())

            # checkpointsanity: with its item, the chapter can be started from the checkpoint room (chapter select).
            # Keys, clutter and breakers are session state in the game (lost when a new session starts at a
            # checkpoint) but permanent events in this logic: in a chapter that has such events the logic never
            # counts on the checkpoint start (the item still unlocks it in the chapter select).
            # With keysanity the keys are items (kept for good) and the mod keeps clutter, breaker and summit gems
            # in the save, so no session state is lost by a checkpoint start any more.
            teleport_in_logic = bool(o.keysanity) or not any(it in KEY_ITEMS for (_, it, _, _) in g.events)
            if o.checkpointsanity:
                for name, where in CHECKPOINT_ITEMS.items():
                    cak, level = where.split("|")
                    if cak != ak or level not in lay.placed:
                        continue
                    ow = area.ow_rooms.get(level)
                    if ow is not None and ow.checkpoint_region:
                        spawn = region_name(ak, level, ow.checkpoint_region)
                    else:
                        regs = [r for r in g.models[level].regions if r != g.models[level].room_region]
                        spawn = next((r for r in regs if r.endswith("_main")), regs[0] if regs else None)
                    if spawn is None or spawn not in regions:
                        continue
                    self.checkpoint_items[name] = where
                    if not teleport_in_logic:
                        continue
                    menu.connect(regions[spawn], f"Menu -> {name}", entry(lambda state, n=name: state.has(n, player)))

            for (spec, rn, rule, extra) in reach.locs:
                if spec.kind == "room" and not o.roomsanity:
                    continue
                if spec.kind == "checkpoint":
                    continue  # checkpoints are items (checkpointsanity) or vanilla unlocks, never checks
                loc = CMRLocation(player, spec.name, spec.id, regions[rn])
                self._set_loc_rule(loc, rule, extra)
                regions[rn].locations.append(loc)
                self.created_keys.append(spec.key)
                if spec.kind == "clear" and ak == self.goal_area:
                    goal = CMRLocation(player, N.VICTORY, None, regions[rn])
                    base = _compile(rule, player)
                    goal.access_rule = (lambda state, base=base: (base is None or base(state)) and
                                        state.has(N.STRAWBERRY, player, self.strawberries_required))
                    goal.place_locked_item(Item(N.VICTORY, ItemClassification.progression, None, player))
                    regions[rn].locations.append(goal)

            if o.keysanity:
                self.locks.update(_lock_keys(area))
            for (locname, item, rn, rule) in reach.events:
                if o.keysanity and item in KEY_ITEMS:
                    self.key_items.add(item)  # an item of the pool instead of an event at the key's spot
                    continue
                ev = CMRLocation(player, locname, None, regions[rn])
                self._set_loc_rule(ev, rule, [])
                ev.place_locked_item(Item(item, ItemClassification.progression, None, player))
                regions[rn].locations.append(ev)

        if not any(l.name == N.VICTORY for l in self.multiworld.get_locations(player)):
            raise RuntimeError(f"UltraRando: goal area {self.goal_area} cannot be cleared "
                               f"for player {self.player_name}")

    def _set_loc_rule(self, loc: Location, rule, extra) -> None:
        player = self.player
        for alt in rule:
            self.referenced_items.update(alt)
        base = _compile(rule, player)
        if not extra:
            if base is not None:
                loc.access_rule = base
            return
        others = []
        for (rn, rr) in extra:
            for alt in rr:
                self.referenced_items.update(alt)
            others.append((rn, _compile(rr, player)))

        def check(state, base=base, others=others):
            if base is not None and not base(state):
                return False
            for (rn, f) in others:
                if not state.can_reach_region(rn, player):
                    return False
                if f is not None and not f(state):
                    return False
            return True
        loc.access_rule = check

    # ------------------------------------------------------------------ items
    def create_item(self, name: str) -> CMRItem:
        if name == N.STRAWBERRY:
            cls = ItemClassification.progression_skip_balancing
        elif name == N.RASPBERRY:
            cls = ItemClassification.filler
        elif name in self.referenced_items or name in N.MOVEMENT_ITEMS or name in CHECKPOINT_ITEMS or name in KEY_ITEMS:
            cls = ItemClassification.progression
        else:
            cls = ItemClassification.useful
        return CMRItem(name, cls, self.item_name_to_id[name], self.player)

    def get_filler_item_name(self) -> str:
        return N.RASPBERRY

    def create_items(self) -> None:
        o = self.options
        pool: list[CMRItem] = []
        precollect: list[str] = []

        def add(names, shuffled: bool):
            for n in names:
                if shuffled:
                    pool.append(self.create_item(n))
                else:
                    precollect.append(n)

        ds = o.dash_shuffle.value
        if ds == 0:
            add([N.DASH], False)
        elif ds == 1:
            add([N.DASH], True)
        elif ds in (2, 3):
            add([N.U_DASH, N.R_DASH, N.D_DASH, N.L_DASH], True)
        else:
            add(N.DIR_DASHES, True)
        cs = o.climb_shuffle.value
        if cs == 0:
            add([N.CLIMB], False)
        elif cs == 1:
            add([N.CLIMB], True)
        else:
            add([N.R_CLIMB, N.L_CLIMB], True)
        add([N.CROUCH], bool(o.crouch_shuffle))
        if o.demo_dash_shuffle:  # off: no item at all, so the logic never counts on a demodash skip
            add([N.DEMO_DASH], True)
        add([N.WALL_JUMP], bool(o.wall_jump_shuffle))
        for name in self.checkpoint_items:  # checkpointsanity (empty otherwise)
            pool.append(self.create_item(name))
        for name in sorted(self.key_items):  # keysanity (empty otherwise)
            pool.append(self.create_item(name))

        if o.interactable_shuffle:
            used = set(self.referenced_items)
            for ak in self.active_areas:
                for lv in {r.ow_level for r in self._data.areas[ak].ow_rooms.values()}:
                    used.update(self._ow_level_items(lv))
            add([n for n in N.INTERACTABLES if n in used], True)
            add([n for n in N.INTERACTABLES if n not in used], False)
        else:
            add(N.INTERACTABLES, False)

        for n in precollect:
            self.multiworld.push_precollected(self.create_item(n))

        location_count = len(self.created_keys)
        overfill = len(pool) - location_count
        if overfill > 0:
            self.random.shuffle(pool)
            for _ in range(overfill):
                self.multiworld.push_precollected(pool.pop())

        real_total = max(0, min(o.total_strawberries.value, location_count - len(pool)))
        self.strawberries_required = int(real_total * (o.strawberries_required_percentage.value / 100))
        pool += [self.create_item(N.STRAWBERRY) for _ in range(self.strawberries_required)]
        for _ in range(real_total - self.strawberries_required):
            pool.append(CMRItem(N.STRAWBERRY, ItemClassification.useful, self.item_name_to_id[N.STRAWBERRY],
                                self.player))
        pool += [self.create_item(N.RASPBERRY) for _ in range(location_count - len(pool))]
        self.multiworld.itempool += pool

    _ow_items_cache: dict[str, list[str]] = {}

    def _ow_level_items(self, ow_level: str) -> list[str]:
        if not CMRWorld._ow_items_cache:
            from .core.static_data import _load_json
            for lv in _load_json("openworld_logic.json")["levels"]:
                CMRWorld._ow_items_cache[lv["name"]] = [N.TOKEN_TO_ITEM[t] for t in lv["items"]]
        return CMRWorld._ow_items_cache.get(ow_level, [])

    # ------------------------------------------------------------------ rules
    def set_rules(self) -> None:
        player = self.player
        self.multiworld.completion_condition[player] = lambda state: state.has(N.VICTORY, player)

    def collect(self, state: CollectionState, item: Item) -> bool:
        change = super().collect(state, item)
        if change:
            self._expand(state, item.name, 1)
        return change

    def remove(self, state: CollectionState, item: Item) -> bool:
        change = super().remove(state, item)
        if change:
            self._expand(state, item.name, -1)
        return change

    def _expand(self, state: CollectionState, name: str, sign: int) -> None:
        """Same derived abilities as Open World (Dash -> 8 directions, cardinal modes, Climb -> both sides)."""
        prog = state.prog_items[self.player]
        if name == N.DASH:
            for d in N.DIR_DASHES:
                prog[d] += sign
        ds = self.options.dash_shuffle.value
        if ds == 2:
            pairs = {N.U_DASH: (N.UR_DASH, N.UL_DASH), N.D_DASH: (N.DR_DASH, N.DL_DASH),
                     N.R_DASH: (N.UR_DASH, N.DR_DASH), N.L_DASH: (N.UL_DASH, N.DL_DASH)}
            for d in pairs.get(name, ()):
                prog[d] += sign
        elif ds == 3:
            diag = {N.U_DASH: ((N.R_DASH, N.UR_DASH), (N.L_DASH, N.UL_DASH)),
                    N.D_DASH: ((N.R_DASH, N.DR_DASH), (N.L_DASH, N.DL_DASH)),
                    N.R_DASH: ((N.U_DASH, N.UR_DASH), (N.D_DASH, N.DR_DASH)),
                    N.L_DASH: ((N.U_DASH, N.UL_DASH), (N.D_DASH, N.DL_DASH))}
            for other, d in diag.get(name, ()):
                if prog[other] > 0:
                    prog[d] += sign
        if name == N.CLIMB:
            prog[N.R_CLIMB] += sign
            prog[N.L_CLIMB] += sign

    # ------------------------------------------------------------------ output
    def fill_slot_data(self) -> dict[str, Any]:
        o = self.options
        data = self._data
        return {
            "version": SLOT_DATA_VERSION,
            "options": {
                "logic_difficulty": o.logic_difficulty.value,
                "room_difficulty": o.room_difficulty.value,
                "room_shuffle": o.room_shuffle.value,
                "dash_shuffle": o.dash_shuffle.value,
                "climb_shuffle": o.climb_shuffle.value,
                "crouch_shuffle": o.crouch_shuffle.value,
                "demo_dash_shuffle": o.demo_dash_shuffle.value,
                "wall_jump_shuffle": o.wall_jump_shuffle.value,
                "interactable_shuffle": o.interactable_shuffle.value,
                "include_b_sides": o.include_b_sides.value,
                "include_c_sides": o.include_c_sides.value,
                "include_core": o.include_core.value,
                "include_farewell": o.include_farewell.value,
                "goal_area": self.goal_area,
                "strawberries_required": self.strawberries_required,
                "goal_area_locked": 1,  # the mod refuses to enter the goal chapter before the strawberries
                "roomsanity": o.roomsanity.value,
                "checkpointsanity": o.checkpointsanity.value,
                "keysanity": o.keysanity.value,
                "death_link": o.death_link.value,
            },
            "active_areas": list(self.active_areas),
            "chapters": {ak: lay.to_slot(data.areas[ak], data, o.logic_difficulty.value, o.room_difficulty.value)
                         for ak, lay in self.layouts.items()},
            "location_ids": {key: data.locations[key].id for key in sorted(self.created_keys)},
            "item_ids": dict(self.item_name_to_id),
            "checkpoints": dict(self.checkpoint_items),
            "locks": {k: v for k, v in sorted(self.locks.items())},
        }

    def generate_output(self, output_directory: str) -> None:
        players = self.multiworld.get_game_players(self.game)
        seed = self.multiworld.seed_name
        name = f"{seed}_cmr_slot_data.json" if len(players) == 1 else f"{seed}_P{self.player}_cmr_slot_data.json"
        text = json.dumps(self.fill_slot_data(), indent=1)
        with open(os.path.join(output_directory, name), "w", encoding="utf-8") as f:
            f.write(text)
        try:  # also next to the final .zip, for `cmr_offline <path>` without unzipping
            from Utils import output_path
            with open(output_path(name), "w", encoding="utf-8") as f:
                f.write(text)
        except OSError as e:
            logger.warning(f"UltraRando: could not write {name} to the output folder: {e}")
