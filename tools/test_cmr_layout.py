"""Unit-style checks of the UltraRando layouts (pure Python, no Archipelago needed).

usage : python tools/test_cmr_layout.py [--seeds 3] [--slot_data <file>...]

1. room_shuffle off: every area's layout is exactly the vanilla one (rooms.json bounds, variant 1, all
   vanilla hole pairs linked, other holes closed).
2. room_shuffle on (several seeds, every area, every logic difficulty): one variant per level, no room
   overlap (rooms + extra_space), every link joins two facing holes at the exact rhelmot position, the
   start room keeps its vanilla position, the level clear is reachable, every placed room is reachable, every
   direction the logic uses through a link exits by an out/inout hole and enters by an in/inout hole whose entry is
   not obstructed (lock, breakable wall, clutter; such holes of non-vanilla links get "no_enter").
3. Optional: a generated <seed>_cmr_slot_data.json is checked the same way (vanilla equality when its
   room_shuffle option is 0).
"""
import argparse
import json
import os
import random
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "apworld", "ultrarando"))

from core.geometry import compatible, adjacent_position, hole_touch_span, intersects, room_rects, INCOMPATIBLE  # noqa
from core.layout import (Layout, build_graph, evaluate, clear_reachable, make_layout, vanilla_layout,  # noqa
                         entry_obstructed, EXIT_KINDS, ENTER_KINDS)
from core.static_data import get_data  # noqa

errors: list[str] = []


def check(cond: bool, msg: str) -> None:
    if not cond:
        errors.append(msg)


def check_vanilla(area, chapter: dict, label: str) -> None:
    exp_rooms = {lv: (keys[0], area.rooms[keys[0]]["bounds"]["x"], area.rooms[keys[0]]["bounds"]["y"])
                 for lv, keys in area.levels.items()}
    got_rooms = {r["room"]: (r["variant"], r["x"], r["y"]) for r in chapter["rooms"]}
    check(got_rooms == exp_rooms, f"{label}: rooms differ from vanilla")
    exp_links = {frozenset([(a, ia), (b, ib)]) for (a, ia, b, ib) in area.vanilla_links}
    got_links = {frozenset([(l["a"], l["a_hole"]), (l["b"], l["b_hole"])]) for l in chapter["links"]}
    check(got_links == exp_links, f"{label}: links differ from the vanilla hole pairs")
    used = {x for l in exp_links for x in l}
    exp_closed = {(lv, h["index"]) for lv, (v, _, _) in exp_rooms.items() for h in area.rooms[v]["holes"]
                  if (lv, h["index"]) not in used}
    got_closed = {(c["room"], c["hole"]) for c in chapter["closed_holes"]}
    check(got_closed == exp_closed, f"{label}: closed holes differ")


def check_geometry(area, chapter: dict, label: str) -> None:
    rooms = {r["room"]: r for r in chapter["rooms"]}
    check(len(rooms) == len(chapter["rooms"]), f"{label}: a level is placed twice")
    for r in chapter["rooms"]:
        check(area.rooms[r["variant"]]["level"] == r["room"], f"{label}: variant {r['variant']} is not {r['room']}")
    rects = [(r["room"], rc) for r in chapter["rooms"] for rc in room_rects(area.rooms[r["variant"]], r["x"], r["y"])]
    for i in range(len(rects)):
        for j in range(i + 1, len(rects)):
            if rects[i][0] != rects[j][0]:
                check(not intersects(rects[i][1], rects[j][1]), f"{label}: {rects[i][0]} overlaps {rects[j][0]}")
    seen = set()
    for l in chapter["links"]:
        A, B = rooms[l["a"]], rooms[l["b"]]
        ra, rb = area.rooms[A["variant"]], area.rooms[B["variant"]]
        ha, hb = ra["holes"][l["a_hole"]], rb["holes"][l["b_hole"]]
        check(hole_touch_span(ra, A["x"], A["y"], ha, rb, B["x"], B["y"], hb),
              f"{label}: link {l} does not join facing holes")
        for k in ((l["a"], l["a_hole"]), (l["b"], l["b_hole"])):
            check(k not in seen, f"{label}: hole {k} linked twice")
            seen.add(k)
    for c in chapter["closed_holes"]:
        check((c["room"], c["hole"]) not in seen, f"{label}: hole {c} both linked and closed")
    start = area.start_room
    if start in rooms:
        b = area.rooms[area.levels[start][0]]["bounds"]
        check((rooms[start]["x"], rooms[start]["y"]) == (b["x"], b["y"]), f"{label}: start room moved")
    else:
        errors.append(f"{label}: start room {start} missing")


def check_dirs(area, chapter: dict, label: str) -> None:
    """Every direction the logic uses through a link ("dir") leaves by an exit hole (out/inout) and arrives by an
    entry hole (in/inout) whose entry is not obstructed (lock, breakable wall, clutter...); obstructed entries of
    non-vanilla links are closed by the mod ("no_enter")."""
    rooms = {r["room"]: r for r in chapter["rooms"]}
    vp = set(area.vanilla_pairs)
    for l in chapter["links"]:
        a, b = l["a"], l["b"]
        ra, rb = area.rooms[rooms[a]["variant"]], area.rooms[rooms[b]["variant"]]
        ha, hb = ra["holes"][l["a_hole"]], rb["holes"][l["b_hole"]]
        d = l.get("dir")
        check(d in ("ab", "ba", "both", "none"), f"{label}: link {a}-{b} has no dir")
        for (x, hx, rx, y, hy, ry, used) in ((a, ha, ra, b, hb, rb, d in ("ab", "both")),
                                             (b, hb, rb, a, ha, ra, d in ("ba", "both"))):
            if used:
                check(hx["kind"] in EXIT_KINDS, f"{label}: logic exits {x} by a {hx['kind']} hole")
                check(hy["kind"] in ENTER_KINDS, f"{label}: logic enters {y} by a {hy['kind']} hole")
                check(not entry_obstructed(area, ry, hy["index"]), f"{label}: logic enters {y} hole {hy['index']} (obstructed)")
        vanilla = (a, l["a_hole"], b, l["b_hole"]) in vp or (b, l["b_hole"], a, l["a_hole"]) in vp
        exp = ("a" if entry_obstructed(area, ra, l["a_hole"]) else "") + ("b" if entry_obstructed(area, rb, l["b_hole"]) else "")
        check(l.get("no_enter", "") == ("" if vanilla else exp), f"{label}: link {a}-{b} no_enter {l.get('no_enter')} != {exp}")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--seeds", type=int, default=3)
    p.add_argument("--slot_data", nargs="*", default=[])
    a = p.parse_args()
    data = get_data()
    n_shuffled = n_fallback = 0
    for ak, area in data.areas.items():
        for diff in (0, 1, 2):
            lay = make_layout(data, area, False, diff, 2, random.Random(0))
            check_vanilla(area, lay.to_slot(area), f"{ak} vanilla d{diff}")
            g = build_graph(data, area, lay, diff, 2)
            check(clear_reachable(evaluate(g), area), f"{ak} vanilla d{diff}: level clear unreachable")
            for seed in range(a.seeds):
                lay = make_layout(data, area, True, diff, 2, random.Random(seed * 7 + diff))
                label = f"{ak} shuffled d{diff} s{seed}"
                ch = lay.to_slot(area)
                check_geometry(area, ch, label)
                if lay.fallback:
                    n_fallback += 1
                    continue
                n_shuffled += 1
                check_dirs(area, lay.to_slot(area, data, diff, 2), label)
                g = build_graph(data, area, lay, diff, 2)
                reach = evaluate(g)
                check(clear_reachable(reach, area), f"{label}: level clear unreachable")
                for lv in lay.placed:
                    check(any(r in reach.regions for r in g.models[lv].regions), f"{label}: room {lv} unreachable")
                # a root room left only by a cutscene (6a "start") must never be entered through a link
                for root in {area.start_room, area.ow_start_room} - {None}:
                    if root in area.levels and not any(ex for (_, ex, _) in g.models[root].attach.values()) and                             not any(root in (x, y) for (x, _, y, _) in area.vanilla_links):
                        check(not any(root in (l[0], l[2]) for l in lay.links), f"{label}: detached room {root} is linked")
    print(f"layouts: {n_shuffled} shuffled, {n_fallback} vanilla fallbacks")
    for path in a.slot_data:
        sd = json.load(open(path, encoding="utf-8"))
        for ak, ch in sd["chapters"].items():
            check_geometry(data.areas[ak], ch, f"{os.path.basename(path)} {ak}")
            # a room is placed once, in one of its variants (rhelmot "room#2" copies are the same room)
            base = [(r.get("variant") or r["room"]).split("#")[0] for r in ch["rooms"]]
            check(len(set(base)) == len(base), f"{os.path.basename(path)} {ak}: a room placed twice")
            check_dirs(data.areas[ak], ch, f"{os.path.basename(path)} {ak}")
            if not sd["options"]["room_shuffle"]:
                check_vanilla(data.areas[ak], ch, f"{os.path.basename(path)} {ak}")
        ids = {s.key: s.id for s in data.locations.values()}
        check(all(ids.get(k) == v for k, v in sd["location_ids"].items()), f"{path}: location ids differ")
        print(f"checked {path} (room_shuffle={sd['options']['room_shuffle']})")
    if errors:
        print(f"{len(errors)} ERRORS:")
        for e in errors[:60]:
            print("  " + e)
        return 1
    print("all checks OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
