"""Room geometry helpers: exact Python port of rhelmot's hole compatibility and placement math.

See mod/UltraRando/EXPORT_FORMAT.md ("Placement math"). MIT (UltraRando), port of
rhelmot's Celeste Randomizer (MIT).
"""
from __future__ import annotations

INCOMPATIBLE = -2 ** 31
OPPOSITE = {"Up": "Down", "Down": "Up", "Left": "Right", "Right": "Left"}


def r8(n: int) -> int:
    """n rounded up to a multiple of 8."""
    return -((-n) // 8) * 8


def low_open(h) -> bool:
    return h["low"] == 0


def both_open(h) -> bool:
    return low_open(h) and h["high_open"]


def half_open(h) -> bool:
    return low_open(h) != h["high_open"]


def closed(h) -> bool:
    return not low_open(h) and not h["high_open"]


def size(h) -> int:
    return h["high"] - h["low"] + 1


def compatible(a, b) -> int:
    """Exact port of Hole.Compatible. `a` = hole of the placed room, `b` = hole of the new room.

    Returns the along-axis offset (tiles) of B's origin relative to A's, or INCOMPATIBLE.
    """
    if (b["side"] == "Up" and a["side"] not in ("Up", "Left")) or \
       (b["side"] == "Left" and a["side"] not in ("Left", "Up")):
        r = compatible(b, a)
        return r if r == INCOMPATIBLE else -r
    align_low, align_high = a["low"] - b["low"], a["high"] - b["high"]
    if a["side"] == "Up" and b["side"] == "Down":
        one_way_up = a["kind"] == "out" or b["kind"] in ("in", "unknown")
        one_way_down = a["kind"] in ("in", "unknown") or b["kind"] == "out"
        top_is_larger = size(a) <= size(b)
        if a["launch"] is not None:
            if b["launch"] is not None:
                return INCOMPATIBLE if b["launch"] == -1 else a["launch"] - b["launch"]
            if closed(b):
                return a["launch"] - (b["low"] + b["high"]) // 2
            if b["high_open"]:
                return a["launch"] - (b["low"] + 2)
            if low_open(b):
                return a["launch"] - (b["high"] - 2)
        elif both_open(a) or both_open(b):
            if both_open(a) and both_open(b) and size(a) == size(b):
                return 0
        elif half_open(a) or half_open(b):
            if low_open(a) == low_open(b):
                return align_high if low_open(a) else align_low
            elif low_open(a) != b["high_open"]:
                if (one_way_up and top_is_larger) or (one_way_down and not top_is_larger):
                    return align_high if (low_open(a) or low_open(b)) else align_low
        else:
            if size(a) == size(b):
                return align_low
            if (one_way_up and top_is_larger) or (one_way_down and not top_is_larger):
                return align_low if (size(a) + size(b)) % 2 == 0 else align_high
    if a["side"] == "Left" and b["side"] == "Right":
        if a["high_open"] or b["high_open"]:
            if a["high_open"] and b["high_open"]:
                return align_high
        elif low_open(a) or low_open(b):
            if low_open(a) and low_open(b):
                return align_high
            if closed(a) and size(a) > 2:
                return align_high
            if closed(b) and size(b) > 2:
                return align_high
        else:
            if size(a) == size(b) or (size(a) > 2 and size(b) > 2):
                return align_high
    return INCOMPATIBLE


def adjacent_position(ax: int, ay: int, aw: int, ah: int, side: str, off: int, bw: int, bh: int) -> tuple[int, int]:
    """Top-left of room B when A's hole on `side` is linked to B with offset `off` (tiles)."""
    if side == "Up":
        return ax + off * 8, ay - r8(bh)
    if side == "Down":
        return ax + off * 8, ay + r8(ah)
    if side == "Left":
        return ax - r8(bw), ay + off * 8
    return ax + r8(aw), ay + off * 8


def intersects(r1, r2) -> bool:
    """XNA Rectangle.Intersects on (x, y, w, h) tuples (strict: touching rooms do not intersect)."""
    return r1[0] < r2[0] + r2[2] and r2[0] < r1[0] + r1[2] and r1[1] < r2[1] + r2[3] and r2[1] < r1[1] + r1[3]


def room_rects(room, x: int, y: int) -> list[tuple[int, int, int, int]]:
    """The room rectangle plus its extra_space rectangles, for a room placed at (x, y)."""
    b = room["bounds"]
    rects = [(x, y, b["w"], b["h"])]
    for e in room.get("extra_space") or []:
        rects.append((x + e["x"], y + e["y"], e["w"], e["h"]))
    return rects


def hole_touch_span(room_a, ax, ay, ha, room_b, bx, by, hb) -> bool:
    """True if hole ha of A (at ax, ay) and hole hb of B (at bx, by) face each other and overlap."""
    if hb["side"] != OPPOSITE[ha["side"]]:
        return False
    ab, bb = room_a["bounds"], room_b["bounds"]
    s = ha["side"]
    if s == "Up":
        touch = by + r8(bb["h"]) == ay
        base_a, base_b = ax, bx
    elif s == "Down":
        touch = ay + r8(ab["h"]) == by
        base_a, base_b = ax, bx
    elif s == "Left":
        touch = bx + r8(bb["w"]) == ax
        base_a, base_b = ay, by
    else:
        touch = ax + r8(ab["w"]) == bx
        base_a, base_b = ay, by
    if not touch:
        return False
    lo = max(base_a + 8 * ha["low"], base_b + 8 * hb["low"])
    hi = min(base_a + 8 * ha["high"], base_b + 8 * hb["high"])
    return lo <= hi
