using System;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.CelesteMovementHelper;

public enum LogicLevel
{
    DeveloperIntended = 0,
    VanillaMovement = 1,
    AssistMode = 2,
}

public class CelesteMovementHelperSettings : EverestModuleSettings
{
    [SettingName("CMH_SETTING_TOGGLE")]
    [DefaultButtonBinding(0, Keys.F6)]
    public ButtonBinding ToggleHelp { get; set; }

    [SettingName("CMH_SETTING_NEXT")]
    [DefaultButtonBinding(0, Keys.F7)]
    public ButtonBinding NextEntry { get; set; }

    [SettingName("CMH_SETTING_PREV")]
    [DefaultButtonBinding(0, Keys.F8)]
    public ButtonBinding PreviousEntry { get; set; }

    /// <summary>Must match the seed's "logic_difficulty" (Open World does not send it to the mod).</summary>
    [SettingName("CMH_SETTING_LOGIC")]
    public LogicLevel Logic { get; set; } = LogicLevel.VanillaMovement;

    /// <summary>Must match "wall_jump_shuffle" of the YAML (CelesteMovementRando apworld fork only).</summary>
    [SettingName("CMH_SETTING_WALLJUMP")]
    public bool WallJumpShuffle { get; set; } = false;

    [SettingName("CMH_SETTING_TEXTSCALE")]
    [SettingRange(3, 8)]
    public int TextScale { get; set; } = 4;
}

public class CelesteMovementHelperModule : EverestModule
{
    public static CelesteMovementHelperModule Instance { get; private set; }

    public override Type SettingsType => typeof(CelesteMovementHelperSettings);
    public static CelesteMovementHelperSettings Settings => (CelesteMovementHelperSettings)Instance._Settings;

    public CelesteMovementHelperModule()
    {
        Instance = this;
        Logger.SetLogLevel(nameof(CelesteMovementHelperModule), LogLevel.Info);
    }

    public override void Load()
    {
        LogicData.Load();
        TechniqueData.Load();
        On.Celeste.Level.LoadLevel += OnLoadLevel;
        On.Celeste.Player.WallJumpCheck += OnWallJumpCheck;
    }

    public override void Unload()
    {
        On.Celeste.Level.LoadLevel -= OnLoadLevel;
        On.Celeste.Player.WallJumpCheck -= OnWallJumpCheck;
    }

    public static bool WallJumpLocked
        => Settings != null && Settings.WallJumpShuffle && !OpenWorldBridge.HasReceived(OpenWorldBridge.WallJumpItemId);

    /// <summary>
    /// WallJumpCheck is what the player code asks before a wall jump, a wall climb-jump kick or a wallbounce.
    /// Answering "no wall here" blocks those three moves without touching climbing itself.
    /// </summary>
    private static bool OnWallJumpCheck(On.Celeste.Player.orig_WallJumpCheck orig, Player self, int dir)
    {
        if (WallJumpLocked) return false;
        return orig(self, dir);
    }

    private static void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        orig(self, playerIntro, isFromLoader);
        try
        {
            if (HelpOverlay.Current == null || HelpOverlay.Current.Scene != self)
            {
                self.Add(new HelpOverlay());
            }
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Error, nameof(CelesteMovementHelperModule), $"Could not add help overlay: {e}");
        }
    }
}
