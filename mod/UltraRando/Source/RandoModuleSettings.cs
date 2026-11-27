using System;
using System.Collections.Generic;

namespace Celeste.Mod.UltraRando {
    public class RandoModuleSettings : EverestModuleSettings {
        [SettingIgnore]
        public int StartCounter { get; set; }

        [SettingIgnore]
        public Dictionary<uint, long> BestTimes { get; set; }

        [SettingIgnore]
        public Dictionary<string, RecordTuple> BestSetSeedTimes { get; set; }

        [SettingIgnore]
        public Dictionary<string, RecordTuple> BestRandomSeedTimes { get; set; }

        [SettingIgnore]
        public string CurrentVersion { get; set; }

        [SettingIgnore]
        public RandoSettings SavedSettings { get; set; }

        // CMR (phase B): Archipelago connection settings (edited from the custom mod options section)
        [SettingIgnore]
        public string ApServer { get; set; } = "localhost:38281";
        [SettingIgnore]
        public string ApSlot { get; set; } = "";
        [SettingIgnore]
        public string ApPassword { get; set; } = "";

        // CMR: "Climb" on the title screen opens the Archipelago connection screen (vanilla saves stay reachable
        // from it with "Play without Archipelago"); off = vanilla Climb.
        [SettingName("MODOPTIONS_CMR_CLIMBAP")]
        public bool ClimbOpensArchipelago { get; set; } = true;

        // CMR: on randomizer maps, a cutscene that the game lets you skip (pause menu "Skip cutscene") is skipped at once
        [SettingName("MODOPTIONS_CMR_SKIPCUTSCENES")]
        public bool SkipCutscenes { get; set; } = true;

        // CMR: on randomizer maps the end of a chapter (its end room can sit in the middle of the shuffled map) sends
        // the "Level Clear" check and records the clear, then the room is reloaded: the player keeps exploring
        [SettingName("MODOPTIONS_CMR_STAYAFTERCLEAR")]
        public bool StayAfterClear { get; set; } = true;

        // CMR: in-game YAML generator (AP/OuiYamlGenerator.cs): slot name, values, last preset applied
        [SettingIgnore] public string YamlSlot { get; set; } = "";
        [SettingIgnore] public System.Collections.Generic.Dictionary<string, string> YamlValues { get; set; } = new();
        [SettingIgnore] public string YamlPreset { get; set; }

        // CMR: help panel (room logic / technique glossary), see AP/HelpOverlay.cs. Default Insert: a key scan of
        // Celeste.dll (vanilla + Everest) and CelesteNet found F1/F2/F3 (debug), F5/F6 (debug map), Home/End/PageUp/
        // PageDown/Delete (CelesteNet chat, text boxes) in use, and Insert free. O/U only act while the panel is open.
        // HelpKey values saved as F6 or I (earlier defaults) are migrated to Insert in HelpOverlay.Load.
        [SettingName("CMH_SETTING_TOGGLE")]
        [DefaultButtonBinding(0, Microsoft.Xna.Framework.Input.Keys.Insert)]
        public ButtonBinding HelpKey { get; set; }

        [SettingName("CMH_SETTING_NEXT")]
        [DefaultButtonBinding(0, Microsoft.Xna.Framework.Input.Keys.O)]
        public ButtonBinding HelpNextKey { get; set; }

        [SettingName("CMH_SETTING_PREV")]
        [DefaultButtonBinding(0, Microsoft.Xna.Framework.Input.Keys.U)]
        public ButtonBinding HelpPreviousKey { get; set; }

        [SettingName("CMH_SETTING_TEXTSCALE")]
        [SettingRange(3, 8)]
        public int HelpTextScale { get; set; } = 4;

        public bool FastMenu { get; set; }
        public bool LazyLoading { get; set; } = true;
    }

    public struct RecordTuple {
        public long Item1;
        public string Item2;

        public static RecordTuple Create(long a, string b) {
            return new RecordTuple {
                Item1 = a,
                Item2 = b,
            };
        }
    }

}
