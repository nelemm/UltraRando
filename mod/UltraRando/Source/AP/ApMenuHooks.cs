// CMR: title-screen integration. "Climb" opens the Archipelago connection screen (OuiApConnect) instead of the
// vanilla file select; the vanilla saves stay reachable from that screen ("Play without Archipelago").
using System;
using System.Collections;

namespace Celeste.Mod.UltraRando.AP {
    public static class ApMenuHooks {
        public static void Load() {
            On.Celeste.OuiMainMenu.OnBegin += OnBegin;
            On.Celeste.OuiMainMenu.Enter += OnMainMenuEnter;
            Everest.Events.MainMenu.OnCreateButtons += OnCreateButtons;
            ApJournal.Load();
        }

        static readonly System.Reflection.FieldInfo climbLabel =
            typeof(MainMenuClimb).GetField("label", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        /// <summary>Title screen like the reference (docs/ref_openworld_ui/01.webp): "Climb" becomes "Connect" (vanilla
        /// mountain icon kept, no Open World logo), plus an entry right below it to play the normal game.</summary>
        static void OnCreateButtons(OuiMainMenu menu, System.Collections.Generic.List<MenuButton> buttons) {
            try {
                if (!RandoModule.Instance.SavedData.ClimbOpensArchipelago) return;
                int i = buttons.FindIndex(b => b is MainMenuClimb);
                if (i < 0) return;
                climbLabel?.SetValue(buttons[i], Dialog.Clean("CMR_MENU_CONNECT"));
                buttons.Insert(i + 1, new MainMenuSmallButton("CMR_MENU_VANILLA", "menu/play", menu, Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, () => {
                    Audio.Play("event:/ui/main/whoosh_list_out");
                    Audio.Play("event:/ui/main/button_climb");
                    ApClient.Disconnect();
                    menu.Overworld.Goto<OuiFileSelect>();
                }));
            } catch (Exception e) {
                ApState.Log("title screen buttons failed: " + e.Message);
            }
        }

        public static void Unload() {
            On.Celeste.OuiMainMenu.OnBegin -= OnBegin;
            On.Celeste.OuiMainMenu.Enter -= OnMainMenuEnter;
            Everest.Events.MainMenu.OnCreateButtons -= OnCreateButtons;
            ApJournal.Unload();
        }

        static void OnBegin(On.Celeste.OuiMainMenu.orig_OnBegin orig, OuiMainMenu self) {
            ApState.Log($"Climb pressed (ClimbOpensArchipelago={RandoModule.Instance.SavedData.ClimbOpensArchipelago}, PlayMode={Celeste.PlayMode})");
            // note: Everest's debug mode (enabled on this PC) sets PlayMode=Debug, so it must not disable this
            if (!RandoModule.Instance.SavedData.ClimbOpensArchipelago) {
                orig(self);
                return;
            }
            Audio.Play("event:/ui/main/whoosh_list_out");
            Audio.Play("event:/ui/main/button_climb");
            self.Overworld.Goto<OuiApConnect>();
        }

        /// <summary>Back on the main menu: the Archipelago session ends (like leaving a multiworld game), so a vanilla
        /// save played next can never receive items, DeathLinks or send checks. Offline test sessions are kept.</summary>
        static IEnumerator OnMainMenuEnter(On.Celeste.OuiMainMenu.orig_Enter orig, OuiMainMenu self, Oui from) {
            try {
                if (ApState.Mode == ApMode.Online || ApState.Mode == ApMode.Cached || ApClient.Connecting) {
                    ApState.Log("main menu: Archipelago session closed");
                    ApClient.Disconnect();
                }
            } catch (Exception e) {
                ApState.Log("main menu disconnect failed: " + e.Message);
            }
            return orig(self, from);
        }
    }
}
