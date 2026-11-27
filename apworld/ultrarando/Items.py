from BaseClasses import Item

from .core.names import (GAME, STRAWBERRY, RASPBERRY, DASH, DIR_DASHES, CLIMB, R_CLIMB, L_CLIMB, CROUCH, WALL_JUMP,
                         DEMO_DASH, INTERACTABLES)
from .core.static_data import get_data

# Checkpoint items (checkpointsanity): one per checkpoint of the static table, named like the checkpoint
# ("Forsaken City A - Crossing", as in Open World). Sorted by key so the IDs never move.
CHECKPOINT_ITEMS: dict[str, str] = {s.name: s.key.rsplit("|", 1)[0]
                                    for s in sorted(get_data().locations.values(), key=lambda s: s.key)
                                    if s.kind == "checkpoint"}
# Key items (keysanity): the keys of the logic ("Celestial Resort A - Front Door Key"), sorted so the IDs never move.
KEY_ITEMS: list[str] = sorted({ev.item for evs in get_data().events.values() for ev in evs if ev.item.endswith(" Key")
                               or " Key " in ev.item})

ITEM_BASE_ID = 0xC3D0000


class CMRItem(Item):
    game = GAME


def _item_table() -> dict[str, int]:
    """IDs are fixed (see ID_RANGES.md): never renumber, only append."""
    t = {STRAWBERRY: ITEM_BASE_ID + 0x00, RASPBERRY: ITEM_BASE_ID + 0x01}
    for i, name in enumerate([DASH] + DIR_DASHES + [CLIMB, R_CLIMB, L_CLIMB, CROUCH, WALL_JUMP, DEMO_DASH]):
        t[name] = ITEM_BASE_ID + 0x10 + i
    for i, name in enumerate(INTERACTABLES):
        t[name] = ITEM_BASE_ID + 0x40 + i
    for i, name in enumerate(CHECKPOINT_ITEMS):
        t[name] = ITEM_BASE_ID + 0x100 + i
    for i, name in enumerate(KEY_ITEMS):
        t[name] = ITEM_BASE_ID + 0x200 + i
    return t


item_name_to_id: dict[str, int] = _item_table()

item_name_groups: dict[str, set[str]] = {
    "Dashes": {DASH, *DIR_DASHES},
    "Climbs": {CLIMB, R_CLIMB, L_CLIMB},
    "Movement": {DASH, *DIR_DASHES, CLIMB, R_CLIMB, L_CLIMB, CROUCH, WALL_JUMP, DEMO_DASH},
    "Interactables": set(INTERACTABLES),
    "Checkpoints": set(CHECKPOINT_ITEMS),
    "Keys": set(KEY_ITEMS),
}
