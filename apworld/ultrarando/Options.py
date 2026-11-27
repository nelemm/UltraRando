from dataclasses import dataclass

from Options import Choice, DeathLink, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, Toggle


class LogicDifficulty(Choice):
    """
    Movement the logic expects (Open World rules).
    Developer Intended: what the developers expect a first-time player to know in each room.
    Vanilla Movement: any movement or trick taught anywhere in the base game.
    Assist Mode: invincibility, infinite dashes and infinite stamina.
    """
    display_name = "Logic Difficulty"
    option_developer_intended = 0
    option_vanilla_movement = 1
    option_assist_mode = 2
    default = 1


class RoomShuffle(DefaultOnToggle):
    """
    Shuffle the rooms inside each chapter (never across chapters). The start and end rooms stay those of the game.
    Off: every chapter keeps its vanilla layout.
    """
    display_name = "Room Shuffle"


class RoomCoverage(Range):
    """
    Room shuffle: share of a chapter's rooms the generator tries to place before it keeps a layout.
    Higher = more rooms in each map (more variety, a little longer generation). Rooms that only have one usable
    opening (dead ends) can stay out whatever the value.
    """
    display_name = "Room Coverage %"
    range_start = 50
    range_end = 100
    default = 90


class RoomDifficulty(Choice):
    """
    Skill level used for the few rooms that Open World does not describe (rhelmot's requirements).
    """
    display_name = "Room Difficulty"
    option_easy = 0
    option_normal = 1
    option_hard = 2
    option_expert = 3
    option_master = 4
    option_perfect = 5
    default = 2


class DashShuffle(Choice):
    """
    How dashing is shuffled.
    None: not shuffled. Unified: one `Dash` item gives the 8 directions.
    Cardinal Loose: 4 items; a direction also gives its two neighbouring diagonals.
    Cardinal Restrictive: 4 items; a diagonal needs both of its cardinal directions.
    Octal: one item per direction.
    """
    display_name = "Dash Shuffle"
    option_none = 0
    option_unified = 1
    option_cardinal_loose = 2
    option_cardinal_restrictive = 3
    option_octal = 4
    default = 0


class ClimbShuffle(Choice):
    """
    How climbing is shuffled (forced to None with Developer Intended logic).
    Unified: one `Climb` item. Split: `Left Climb` and `Right Climb`.
    """
    display_name = "Climb Shuffle"
    option_none = 0
    option_unified = 1
    option_split = 2
    default = 0


class CrouchShuffle(Toggle):
    """Shuffle the ability to crouch."""
    display_name = "Crouch Shuffle"


class WallJumpShuffle(Toggle):
    """
    Shuffle the ability to wall jump (and wallbounce).
    Logic: every chapter except the Prologue requires Wall Jump. Forces Roomsanity on.
    """
    display_name = "Wall Jump Shuffle"


class DemoDashShuffle(Toggle):
    """
    Shuffle the Demo Dash (Celeste's "crouch dash": a dash that keeps Madeline's crouched 4 px hitbox, with the
    crouch-dash button or by holding down when dashing then aiming elsewhere). Until it is received, a dash that does
    not go downwards stands Madeline up. Logic (Vanilla Movement and Assist only): with Demo Dash + Left Dash +
    Left Climb (grab the step face, climb jump, demodash), the 3A Elevator Shaft dust wall (room 04-d) can be crossed
    ("shaftskip", frame-perfect), skipping the detour and the Presidential Suite Key. Off: demo dashes are free and the logic never expects the skip.
    """
    display_name = "Demo Dash Shuffle"


class Keysanity(DefaultOnToggle):
    """
    Keys are items ("Celestial Resort A - Front Door Key"), found anywhere in the multiworld. Owning one opens its
    door for good (every session, every start); picking a key up in a level sends that spot's check.
    Off: keys work as in the base game (picked up in the level, lost when a new session starts).
    """
    display_name = "Keysanity"


class InteractableShuffle(DefaultOnToggle):
    """
    Shuffle the interactable objects (Springs, Traffic Blocks, Feathers...): an object only exists once received.
    Off: every object is given at the start.
    """
    display_name = "Interactable Shuffle"


class IncludeBSides(Toggle):
    """Include the B-Sides."""
    display_name = "B-Sides"


class IncludeCSides(Toggle):
    """Include the C-Sides."""
    display_name = "C-Sides"


class IncludeCore(Toggle):
    """Include Chapter 8 - Core (and its B/C-Sides if those are included)."""
    display_name = "Core"


class IncludeFarewell(Toggle):
    """Include Chapter 9 - Farewell (one chapter, both halves)."""
    display_name = "Farewell"


class GoalArea(Choice):
    """Chapter to clear (with the required Strawberries) to win. It is always included."""
    display_name = "Goal Area"
    option_the_summit_a = 0
    option_the_summit_b = 1
    option_the_summit_c = 2
    option_core_a = 3
    option_core_b = 4
    option_core_c = 5
    option_farewell = 6
    default = 0


class TotalStrawberries(Range):
    """Maximum number of Strawberries in the item pool."""
    display_name = "Total Strawberries"
    range_start = 0
    range_end = 202
    default = 50


class StrawberriesRequiredPercentage(Range):
    """Percentage of the existing Strawberries needed to win."""
    display_name = "Strawberries Required %"
    range_start = 0
    range_end = 100
    default = 50


class Roomsanity(DefaultOnToggle):
    """Entering each room sends a check."""
    display_name = "Roomsanity"


class Checkpointsanity(DefaultOnToggle):
    """
    Each checkpoint is an item ("Forsaken City A - Crossing"): it appears in the chapter select only once received
    (reaching it does not unlock it), and the logic knows that with it the chapter can be started from that room.
    Checkpoints are not checks: entering a room still sends that room's own checks.
    Off: checkpoints unlock by reaching them, as in the base game, and the logic always starts at the beginning.
    """
    display_name = "Checkpointsanity"


cmr_option_groups = [
    OptionGroup("Goal", [GoalArea, TotalStrawberries, StrawberriesRequiredPercentage]),
    OptionGroup("Chapters", [IncludeBSides, IncludeCSides, IncludeCore, IncludeFarewell, RoomShuffle, RoomCoverage]),
    OptionGroup("Logic", [LogicDifficulty, RoomDifficulty]),
    OptionGroup("Movement", [DashShuffle, ClimbShuffle, CrouchShuffle, WallJumpShuffle, DemoDashShuffle,
                             InteractableShuffle]),
    OptionGroup("Locations", [Roomsanity, Checkpointsanity, Keysanity]),
]


@dataclass
class CMROptions(PerGameCommonOptions):
    logic_difficulty: LogicDifficulty
    room_shuffle: RoomShuffle
    room_difficulty: RoomDifficulty
    room_coverage: RoomCoverage
    dash_shuffle: DashShuffle
    climb_shuffle: ClimbShuffle
    crouch_shuffle: CrouchShuffle
    wall_jump_shuffle: WallJumpShuffle
    demo_dash_shuffle: DemoDashShuffle
    interactable_shuffle: InteractableShuffle
    include_b_sides: IncludeBSides
    include_c_sides: IncludeCSides
    include_core: IncludeCore
    include_farewell: IncludeFarewell
    goal_area: GoalArea
    total_strawberries: TotalStrawberries
    strawberries_required_percentage: StrawberriesRequiredPercentage
    roomsanity: Roomsanity
    checkpointsanity: Checkpointsanity
    keysanity: Keysanity
    death_link: DeathLink


# Difficulty presets (Archipelago WebWorld.options_presets; also exported to the YAML generators by
# tools/export_options_schema.py). Room difficulty = rhelmot tiers (which rooms and passages the maps may use);
# logic difficulty = Open World tiers (which items the logic asks for); the movement shuffles set how much the
# player's moves are taken away.
cmr_options_presets = {
    "Beginner": {
        "logic_difficulty": "developer_intended", "room_difficulty": "easy", "room_coverage": 70,
        "dash_shuffle": "unified", "climb_shuffle": "unified", "crouch_shuffle": False, "wall_jump_shuffle": False,
        "demo_dash_shuffle": False, "total_strawberries": 30, "strawberries_required_percentage": 50,
    },
    "Normal": {
        "logic_difficulty": "vanilla_movement", "room_difficulty": "normal", "room_coverage": 90,
        "dash_shuffle": "unified", "climb_shuffle": "unified", "crouch_shuffle": True, "wall_jump_shuffle": False,
        "demo_dash_shuffle": False, "total_strawberries": 50, "strawberries_required_percentage": 50,
    },
    "Advanced": {
        "logic_difficulty": "vanilla_movement", "room_difficulty": "hard", "room_coverage": 90,
        "dash_shuffle": "cardinal_loose", "climb_shuffle": "split", "crouch_shuffle": True, "wall_jump_shuffle": True,
        "demo_dash_shuffle": False, "total_strawberries": 50, "strawberries_required_percentage": 60,
    },
    "Expert": {
        "logic_difficulty": "vanilla_movement", "room_difficulty": "expert", "room_coverage": 100,
        "dash_shuffle": "octal", "climb_shuffle": "split", "crouch_shuffle": True, "wall_jump_shuffle": True,
        "demo_dash_shuffle": True, "total_strawberries": 60, "strawberries_required_percentage": 70,
    },
    "Maximum Variety": {
        "logic_difficulty": "vanilla_movement", "room_difficulty": "master", "room_coverage": 100,
        "dash_shuffle": "octal", "climb_shuffle": "split", "crouch_shuffle": True, "wall_jump_shuffle": True,
        "demo_dash_shuffle": True, "interactable_shuffle": True, "keysanity": True, "roomsanity": True,
        "checkpointsanity": True, "include_b_sides": True, "include_c_sides": True, "include_core": True,
        "include_farewell": True, "total_strawberries": 100, "strawberries_required_percentage": 60,
    },
}
