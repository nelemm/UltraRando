"""Item names and Open World rule tokens.

The item names are those of Celeste (Open World) 1.1.3 by PoryGone (Modified MIT, see data/LICENSE_OpenWorld.txt):
the token -> name table below is taken from its Names/ItemName.py. "Wall Jump" is ours.
"""
from __future__ import annotations

GAME = "UltraRando"

STRAWBERRY = "Strawberry"
RASPBERRY = "Raspberry"
VICTORY = "Victory"

DASH = "Dash"
U_DASH, UR_DASH, R_DASH, DR_DASH = "Up Dash", "Up-Right Dash", "Right Dash", "Down-Right Dash"
D_DASH, DL_DASH, L_DASH, UL_DASH = "Down Dash", "Down-Left Dash", "Left Dash", "Up-Left Dash"
DIR_DASHES = [U_DASH, UR_DASH, R_DASH, DR_DASH, D_DASH, DL_DASH, L_DASH, UL_DASH]
CLIMB, R_CLIMB, L_CLIMB = "Climb", "Right Climb", "Left Climb"
CROUCH = "Crouch"
WALL_JUMP = "Wall Jump"
DEMO_DASH = "Demo Dash"  # ours: the crouched dash (Celeste's "crouch dash"), see data/cmr_extra_logic.json

MOVEMENT_ITEMS = [DASH] + DIR_DASHES + [CLIMB, R_CLIMB, L_CLIMB, CROUCH, WALL_JUMP]

# Open World ItemName attribute -> item name (only what the logic tokens use).
TOKEN_TO_ITEM: dict[str, str] = {
    "dash": DASH,
    "u_dash": U_DASH, "ur_dash": UR_DASH, "r_dash": R_DASH, "dr_dash": DR_DASH,
    "d_dash": D_DASH, "dl_dash": DL_DASH, "l_dash": L_DASH, "ul_dash": UL_DASH,
    "climb": CLIMB, "r_climb": R_CLIMB, "l_climb": L_CLIMB,
    "crouch": CROUCH,
    "demo_dash": DEMO_DASH,

    "springs": "Springs",
    "traffic_blocks": "Traffic Blocks",
    "pink_cassette_blocks": "Pink Cassette Blocks",
    "blue_cassette_blocks": "Blue Cassette Blocks",
    "dream_blocks": "Dream Blocks",
    "coins": "Coins",
    "strawberry_seeds": "Strawberry Seeds",
    "sinking_platforms": "Sinking Platforms",
    "moving_platforms": "Moving Platforms",
    "blue_boosters": "Blue Boosters",
    "blue_clouds": "Blue Clouds",
    "move_blocks": "Move Blocks",
    "white_block": "White Block",
    "swap_blocks": "Swap Blocks",
    "red_boosters": "Red Boosters",
    "torches": "Blue Torches",
    "yellow_torches": "Yellow Torches",
    "theo_crystal": "Theo Crystal",
    "feathers": "Feathers",
    "bumpers": "Bumpers",
    "kevin_blocks": "Kevins",
    "pink_clouds": "Pink Clouds",
    "badeline_boosters": "Badeline Boosters",
    "fire_ice_balls": "Fire and Ice Balls",
    "core_toggles": "Core Toggles",
    "core_blocks": "Core Blocks",
    "pufferfish": "Pufferfish",
    "jellyfish": "Jellyfish",
    "breaker_boxes": "Breaker Boxes",
    "dash_refills": "Dash Refills",
    "double_dash_refills": "Double Dash Refills",
    "yellow_cassette_blocks": "Yellow Cassette Blocks",
    "green_cassette_blocks": "Green Cassette Blocks",
    "dash_switches": "Dash Switches",
    "seekers": "Seekers",
    "bird": "Bird",

    # events (locked items at their Open World locations)
    "brown_clutter": "Celestial Resort A - Brown Clutter",
    "green_clutter": "Celestial Resort A - Green Clutter",
    "pink_clutter": "Celestial Resort A - Pink Clutter",
    "power_source_breaker": "Farewell - Power Source - Breaker Disable",

    "cannot_access": "Cannot Access",
}
CANNOT_ACCESS = "Cannot Access"

# Interactable items, in the order of Open World's interactable_item_data_table. The order fixes the IDs.
INTERACTABLE_TOKENS = [
    "springs", "traffic_blocks", "pink_cassette_blocks", "blue_cassette_blocks",
    "dream_blocks", "coins", "strawberry_seeds",
    "sinking_platforms",
    "moving_platforms", "blue_boosters", "blue_clouds", "move_blocks", "white_block",
    "swap_blocks", "red_boosters", "torches", "yellow_torches", "theo_crystal",
    "feathers", "bumpers", "kevin_blocks",
    "pink_clouds", "badeline_boosters",
    "fire_ice_balls", "core_toggles", "core_blocks",
    "pufferfish", "jellyfish", "breaker_boxes", "dash_refills", "double_dash_refills",
    "yellow_cassette_blocks", "green_cassette_blocks", "bird",
    "dash_switches", "seekers",
]
INTERACTABLES = [TOKEN_TO_ITEM[t] for t in INTERACTABLE_TOKENS]
USEFUL_INTERACTABLES = {"Blue Torches", "Yellow Torches"}

DIRECTION_TOKENS = {"u", "ur", "r", "dr", "d", "dl", "l", "ul"}

# Every item a fully-equipped player has (used to validate layouts "with full items").
# DEMO_DASH is left out on purpose: room placement never relies on a demodash skip (pixel-perfect)
ALL_ABILITIES = frozenset(MOVEMENT_ITEMS + INTERACTABLES)

AREA_ORDER = ["0a", "1a", "1b", "1c", "2a", "2b", "2c", "3a", "3b", "3c", "4a", "4b", "4c",
              "5a", "5b", "5c", "6a", "6b", "6c", "7a", "7b", "7c", "9a", "9b", "9c", "10a"]
