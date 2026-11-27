# `rooms.json` export format (version 1)

Written by `Source/Export/RoomsExport.cs` from rhelmot's processed `StaticRoom`s
(`RandoLogic.AllRooms`, built by `ProcessAreas` from `CMRConfig/Celeste/*.rando.yaml`).
Only vanilla areas (SID starting with `Celeste/`), A/B/C sides.

## How to produce it

- Console command (`~` in game, debug mode): `cmr_export [output_path]`.
  Default output: `<Celeste>/CMR_export/rooms.json`.
- Automatic, at area-data load (game start), if one of these files exists:
  `<Celeste>/CMR_EXPORT_ON_LOAD` (works with the zip install) or
  `<Celeste>/Mods/UltraRando/EXPORT_ON_LOAD` (folder install).
- `python tools/build_rando_mod.py --install --export` does build, install, flag, launch, wait,
  kill, unflag and copies the result to `data/rooms.json`.

The file is written to `rooms.json.tmp` then renamed, so its existence means it is complete.

## Top level

```jsonc
{
  "version": 1,
  "generator": "UltraRando 0.1.0",
  "collectable_entity_names": ["strawberry", "key", "summitgem"],  // rhelmot's CollectableNames
  "areas": { "<area key>": Area, ... }
}
```

Area key = `"<AreaKey.ID><a|b|c>"` (same as the V1 helper and Open World): `0a`, `1a`, `1b`, `1c`, ..., `9c`, `10a`.
Farewell is a single area `10a` (Open World splits it into `10a`/`10b`/`10c`).

## Area

```jsonc
{
  "sid": "Celeste/1-ForsakenCity", "area_id": 1, "mode": 0,        // mode 0=A 1=B 2=C
  "start_room": "1",                                                // MapData.StartLevel()
  "checkpoint_rooms": ["6", "9b"],                                  // ModeProperties.Checkpoints levels, in order
  "rooms": { "<room key>": Room, ... }
}
```

**Room key** = vanilla level name. rhelmot's configs sometimes list the same level several times
with different hole configurations (e.g. `end` as an ending, and `end` as a pass-through). The first
entry has key `"<level>"`, the next ones `"<level>#2"`, `"<level>#3"`... (`variant` field).
rhelmot never places two variants of the same level in one map (placing one removes every entry with
the same `rhelmot_name` from the remaining pool).

Credits rooms `credits-*` of 7a are moved by rhelmot into `8a` (`level_source_area: "7a"`).

## Room

```jsonc
{
  "rhelmot_name": "Celeste/1-ForsakenCity/A/3",
  "level": "3", "variant": 1,
  "level_source_area": "7a",          // only present when the LevelData belongs to another area's map
  "bounds": {"x": 0, "y": -184, "w": 320, "h": 184},   // vanilla LevelData.Bounds, pixels
  "extra_space": [ {"x":..,"y":..,"w":..,"h":..} ],     // extra rectangles reserved, RELATIVE to the room position
  "is_start": false,                  // level == area start_room
  "is_end": false,                    // rhelmot ReqEnd is not "impossible" (room can end a map)
  "req_end": "impossible",            // <req>
  "checkpoint": false,                // LevelData.HasCheckpoint or listed in checkpoint_rooms
  "hub": false, "has_heart": false, "worth": 1.9,       // rhelmot fields
  "holes": [ Hole, ... ],             // StaticRoom.Holes order; "index" = position in this list
  "nodes": { "main": Node, ... },
  "unassigned_collectables": [ Collectable ],   // rhelmot collectables not attached to any node (unreachable for rhelmot)
  "pickups": [ {"kind": "strawberry|goldenBerry|key|summitgem|heartGem|blackGem|cassette|birdForsakenCityGem|memorialTextController|fakeHeart",
                "x": 120, "y": 64, "entity_id": 12} ],  // every such entity of the level (positions relative to the room)
  "entities": ["spring", "zipMover", ...],              // distinct LevelData.Entities names, sorted
  "triggers": ["cameraOffsetTrigger", ...],             // distinct LevelData.Triggers names, sorted
  "spawns": [[x, y], ...],                              // LevelData.Spawns relative to the room
  "warps": ["intro-01-future"]                          // levels reached through warp_edges (keep vanilla)
}
```

## Hole

```jsonc
{"index": 0, "side": "Up|Down|Left|Right", "low": 2, "high": 6, "low_open": false, "high_open": false,
 "size": 5, "kind": "none|in|out|inout|unknown", "launch": null}
```

- `low`/`high`: tile indexes (8 px) along the side, inclusive. Along axis: x for Up/Down, y for Left/Right.
- `low_open` is derived: `low == 0` (hole touches the room corner). `high_open`: hole touches the other corner.
- `kind` (from the config): `none` = not usable; `in` = can only be entered; `out` = can only be exited;
  `inout` both; `unknown` = not configured (rhelmot only enters those with the "Enter strange holes" setting,
  and never exits through them).
- `launch`: tile index for an Up hole entered by a launch (bubble/feather/spring), else `null`.
- Holes with `kind: none` have no hole_edge. Hole edges of `unknown` holes were assigned by rhelmot
  to the nearest node (both reqs derived from the kind: out = impossible).

## Node

```jsonc
{
  "edges":      [ {"to_node": "x", "req_out": <req>, "req_in": <req>} ],   // same room. req_out: this node -> to_node,
                                                                            // req_in: to_node -> this node. Each internal
                                                                            // edge appears in both nodes, mirrored.
  "hole_edges": [ {"hole": 0, "req_out": <req>, "req_in": <req>} ],       // req_out: leave through the hole, req_in: arrive
  "warp_edges": [ {"to_room": "intro-01-future", "to_area": "10a", "to_node": "main", "req_out": <req>, "req_in": <req>} ],
  "custom_warps": [ {"req_out": <req>, "req_in": <req>} ],   // cutscene/heart warp out of the room (CustomWarp); target chosen by rhelmot
  "flags_set":  [ {"flag": "name", "state": "set|unset"} ],  // reaching this node sets/unsets a session flag
  "collectables": [ Collectable ]
}
```

A room whose heart/cassette ends the level (`has_heart`) gets a `custom_warps` entry
(`req_out: null`, `req_in: "impossible"`).

## Collectable (rhelmot)

```jsonc
{"index": 0, "kind": "strawberry|key|summitgem|spot", "x": 120, "y": 64, "entity_id": 12, "winged": false, "must_fly": false}
```

- `index` = position in `StaticRoom.Collectables` (sorted by y then x); `null` for a config-only spot.
- `kind` = name of the entity at that position, or `spot` when the config adds a free position (no entity).
- `x`,`y`: position relative to the room (entity position).
- `must_fly`: rhelmot only puts winged berries there.

rhelmot treats these positions as generic item slots. Hearts, cassettes, golden berries, crystal hearts
(`blackGem`) are **not** rhelmot collectables: see `pickups`.

## `<req>`

- `null` = free (rhelmot `Possible`), `"impossible"`.
- `{"and": [<req>...]}`, `{"or": [<req>...]}`.
- `{"dashes": "zero|one|two"}`: needs at least that many dashes (rhelmot `NumDashes`, no "infinite").
- `{"difficulty": "easy|normal|hard|expert|master|perfect"}`: player skill at least that.
- `{"key": <keyholeID>}`: needs a key (rhelmot: any key; the id is the lockBlock entity id).
- `{"flag": "name", "state": "set|unset"}`.

Difference with ARCHITECTURE.md: dashes and difficulty are separate leaf objects, combined by `and`
(rhelmot always adds a difficulty leaf, default `easy`). A config requirement typically exports as
`{"and": [{"dashes": "one"}, {"difficulty": "normal"}]}`.

## Placement math (rhelmot, `Receipt.cs` ConnectAndMapReceipt.Do, `Hole.Compatible`, `StaticRoom.AdjacentPosition`)

Room A is placed at `(ax, ay)` (top-left, pixels); its size is `(aw, ah)` = A's `bounds.w/h`.
We link A's hole `a` (side `S`) to room B's hole `b`. B's size is `(bw, bh)`.

1. `off = compatible(a, b)`; if `INCOMPATIBLE`, the link is impossible.
2. `r8(n)` = `n` rounded up to a multiple of 8.
3. Position of B:

| side of `a` | B.x | B.y |
|---|---|---|
| Up    | `ax + off*8`    | `ay - r8(bh)` |
| Down  | `ax + off*8`    | `ay + r8(ah)` |
| Left  | `ax - r8(bw)`   | `ay + off*8`  |
| Right | `ax + r8(aw)`   | `ay + off*8`  |

   `off` is the shift, in tiles, of B's along-axis origin relative to A's: tile `t` of B's side
   faces tile `t + off` of A's side (`off = a.low - b.low` aligns the low ends,
   `a.high - b.high` the high ends; launch holes align `a.launch` with the given point of `b`).
   The perpendicular coordinate always makes the two rooms touch.
4. B's rectangle `(B.x, B.y, bw, bh)` plus each `extra_space` rect shifted by `(B.x, B.y)` must not
   intersect any placed room rectangle or extra rect (`LinkedMap.AreaFree`). Intersection is strict
   (XNA `Rectangle.Intersects`): `r1.x < r2.x+r2.w and r2.x < r1.x+r1.w and r1.y < r2.y+r2.h and r2.y < r1.y+r1.h`;
   rooms that only touch do not intersect.
5. The link is usable from A to B if A's hole edge `req_out` and B's hole edge `req_in` are satisfiable;
   rhelmot never picks a target hole of kind `unknown` unless "Enter strange holes" is on.
6. Optional rhelmot heuristic (`LinkedMap.HoleFree`), checked before using a hole of a placed room:
   take the hole's `LowCoord`/`HighCoord` (see `Hole.cs`) pushed outward by 180 px (Up/Down) or 320 px
   (Left/Right); if either point is inside an already placed room, the hole is not used.

Rooms reached by warp edges (`warp_edges`) are placed "anywhere" (`LinkRoomAnywhere`): at
`A + (±1200 or 0, ±1200 or 0)` px, doubling the distance until `AreaFree`.

Unlinked holes when the map is baked (`LinkedRoom.Bake`): Left/Right holes are closed with an
`invisibleBarrier`, Up holes get barriers on their sides (`gateTopHole`), Down holes stay open (death pit).
A hole linked to an `out` hole of the other room is blocked behind the player; a hole larger than the
one it is linked to is partially blocked.

### `compatible(a, b)` in Python (exact port of `Hole.Compatible`, `a` = hole of the placed room)

```python
INCOMPATIBLE = -2**31

def low_open(h):  return h["low"] == 0
def both_open(h): return low_open(h) and h["high_open"]
def half_open(h): return low_open(h) != h["high_open"]
def closed(h):    return not low_open(h) and not h["high_open"]
def size(h):      return h["high"] - h["low"] + 1

def compatible(a, b):
    if (b["side"] == "Up" and a["side"] not in ("Up", "Left")) or \
       (b["side"] == "Left" and a["side"] not in ("Left", "Up")):
        r = compatible(b, a)
        return r if r == INCOMPATIBLE else -r
    align_low, align_high = a["low"] - b["low"], a["high"] - b["high"]
    if a["side"] == "Up" and b["side"] == "Down":        # b's room is above a's room
        one_way_up = a["kind"] == "out" or b["kind"] in ("in", "unknown")
        one_way_down = a["kind"] in ("in", "unknown") or b["kind"] == "out"
        top_is_larger = size(a) <= size(b)
        if a["launch"] is not None:
            if b["launch"] is not None:
                return INCOMPATIBLE if b["launch"] == -1 else a["launch"] - b["launch"]
            if closed(b):      return a["launch"] - (b["low"] + b["high"]) // 2
            if b["high_open"]: return a["launch"] - (b["low"] + 2)
            if low_open(b):    return a["launch"] - (b["high"] - 2)
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
    if a["side"] == "Left" and b["side"] == "Right":     # b's room is left of a's room
        if a["high_open"] or b["high_open"]:
            if a["high_open"] and b["high_open"]:
                return align_high
        elif low_open(a) or low_open(b):
            if low_open(a) and low_open(b):     return align_high
            if closed(a) and size(a) > 2:       return align_high
            if closed(b) and size(b) > 2:       return align_high
        else:
            if size(a) == size(b) or (size(a) > 2 and size(b) > 2):
                return align_high
    return INCOMPATIBLE
```

`launch` values are ints; `(x+y)//2` matches C# integer division because bounds are non-negative.
