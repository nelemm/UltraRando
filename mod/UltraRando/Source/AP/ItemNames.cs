// CMR (phase B): item names (Archipelago names, same as Open World's ItemName values) and the
// entity -> item table used by interactable shuffle. Keep in sync with mod/UltraRando/ENTITY_ITEMS.md.
using System.Collections.Generic;

namespace Celeste.Mod.UltraRando.AP {
    public static class ItemNames {
        public const string Dash = "Dash";
        public const string UpDash = "Up Dash", UpRightDash = "Up-Right Dash", RightDash = "Right Dash", DownRightDash = "Down-Right Dash";
        public const string DownDash = "Down Dash", DownLeftDash = "Down-Left Dash", LeftDash = "Left Dash", UpLeftDash = "Up-Left Dash";
        public const string Climb = "Climb", LeftClimb = "Left Climb", RightClimb = "Right Climb";
        public const string Crouch = "Crouch", WallJump = "Wall Jump", DemoDash = "Demo Dash";
        public const string Strawberry = "Strawberry";

        public static readonly string[] Movement = {
            Dash, UpDash, UpRightDash, RightDash, DownRightDash, DownDash, DownLeftDash, LeftDash, UpLeftDash,
            Climb, LeftClimb, RightClimb, Crouch, WallJump, DemoDash,
        };

        public static readonly string[] Interactables = {
            "Springs", "Traffic Blocks", "Pink Cassette Blocks", "Blue Cassette Blocks", "Dream Blocks", "Coins",
            "Strawberry Seeds", "Sinking Platforms", "Moving Platforms", "Blue Boosters", "Blue Clouds", "Move Blocks",
            "White Block", "Swap Blocks", "Red Boosters", "Blue Torches", "Yellow Torches", "Theo Crystal", "Feathers",
            "Bumpers", "Kevins", "Pink Clouds", "Badeline Boosters", "Fire and Ice Balls", "Core Toggles", "Core Blocks",
            "Pufferfish", "Jellyfish", "Breaker Boxes", "Dash Refills", "Double Dash Refills", "Yellow Cassette Blocks",
            "Green Cassette Blocks", "Dash Switches", "Seekers", "Bird",
        };

        public static readonly string[] Others = {
            Strawberry, "Raspberry", "Blue Raspberry", "Blueberry", "Blackberry", "Boysenberry", "Banana", "Cranberry",
            "Golden Raspberry", "Victory",
        };

        static HashSet<string> known;
        public static bool IsKnown(string name) {
            if (known == null) {
                known = new HashSet<string>(Movement);
                known.UnionWith(Interactables);
                known.UnionWith(Others);
            }
            return name != null && known.Contains(name);
        }

        /// <summary>
        /// Item that must be owned for this entity to be kept when interactable shuffle is on; null = never removed.
        /// Deliberately NOT mapped (needed for rooms to work or not an "object" lock): theoCrystal, keys/lockBlocks,
        /// heart gates, strawberry seeds, clutter, torches with a different colour, cutscene entities.
        /// </summary>
        public static string ItemForEntity(EntityData e) {
            switch (e.Name) {
                case "spring":
                case "wallSpringLeft":
                case "wallSpringRight":
                    return "Springs";
                case "zipMover": return "Traffic Blocks";
                case "cassetteBlock":
                    switch (e.Int("index", 0)) {
                        case 0: return "Blue Cassette Blocks";
                        case 1: return "Pink Cassette Blocks";
                        case 2: return "Yellow Cassette Blocks";
                        case 3: return "Green Cassette Blocks";
                        default: return null;
                    }
                case "dreamBlock": return "Dream Blocks";
                case "touchSwitch": return "Coins";
                case "sinkingPlatform": return "Sinking Platforms";
                case "movingPlatform": return "Moving Platforms";
                case "booster": return e.Bool("red", false) ? "Red Boosters" : "Blue Boosters";
                case "cloud": return e.Bool("fragile", false) ? "Pink Clouds" : "Blue Clouds";
                case "moveBlock": return "Move Blocks";
                case "whiteblock": return "White Block";
                case "swapBlock": return "Swap Blocks";
                case "torch": return "Blue Torches";
                case "infiniteStar": return "Feathers";
                case "bigSpinner": return "Bumpers";
                case "crushBlock": return "Kevins";
                case "badelineBoost": return "Badeline Boosters";
                case "fireBall": return "Fire and Ice Balls";
                case "coreModeToggle": return "Core Toggles";
                case "bounceBlock": return "Core Blocks";
                case "eyebomb": return "Pufferfish"; // Celeste's Puffer entity name
                case "glider": return "Jellyfish";
                case "lightningBlock": return "Breaker Boxes";
                case "refill": return e.Bool("twoDash", false) ? "Double Dash Refills" : "Dash Refills"; // Celeste reads "twoDash" (Refill ctor)
                case "dashSwitchH":
                case "dashSwitchV":
                    return "Dash Switches";
                case "seeker": return "Seekers";
                case "flingBird": return "Bird";
                default: return null;
            }
        }
    }
}
