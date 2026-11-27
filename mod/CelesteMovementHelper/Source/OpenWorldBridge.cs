using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.CelesteMovementHelper;

public enum Ownership
{
    Owned,
    Missing,
    Unknown,
}

/// <summary>
/// Read-only view of the "Archipelago Open World" mod state, through reflection.
/// We never call or modify anything in that mod: its licence forbids redistributing modified copies,
/// so this helper stays a separate mod that only reads public members.
/// </summary>
public static class OpenWorldBridge
{
    private const string OpenWorldModName = "Archipelago Open World";

    private static bool searched;
    private static PropertyInfo saveDataProp;   // Celeste_MultiworldModule.SaveData (static)
    private static PropertyInfo managerProp;    // ArchipelagoManager.Instance (static)

    private static void Find()
    {
        if (searched) return;
        searched = true;
        try
        {
            EverestModule module = Everest.Modules.FirstOrDefault(m => m.Metadata?.Name == OpenWorldModName);
            if (module == null) return;
            Type modType = module.GetType();
            saveDataProp = modType.GetProperty("SaveData", BindingFlags.Public | BindingFlags.Static);
            Type mgrType = modType.Assembly.GetType("Celeste.Mod.Celeste_Multiworld.ArchipelagoManager");
            managerProp = mgrType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, nameof(OpenWorldBridge), $"Open World mod not readable: {e.Message}");
        }
    }

    private static object SaveData { get { Find(); try { return saveDataProp?.GetValue(null); } catch { return null; } } }
    private static object Manager { get { Find(); try { return managerProp?.GetValue(null); } catch { return null; } } }

    public static bool Available => SaveData != null;

    public static bool Connected
    {
        get
        {
            object mgr = Manager;
            object ready = mgr == null ? null : Member(mgr, "Ready");
            return ready is bool b && b;
        }
    }

    private static object Member(object obj, string name)
    {
        if (obj == null) return null;
        Type t = obj.GetType();
        FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(obj);
        PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return p?.GetValue(obj);
    }

    private static int ManagerInt(string name, int fallback)
    {
        try { return Member(Manager, name) is int i ? i : fallback; } catch { return fallback; }
    }

    private static Ownership Flag(string name)
    {
        try
        {
            object v = Member(SaveData, name);
            return v is bool b ? (b ? Ownership.Owned : Ownership.Missing) : Ownership.Unknown;
        }
        catch { return Ownership.Unknown; }
    }

    private static Ownership InDictionary(string dictName, long id)
    {
        try
        {
            if (Member(SaveData, dictName) is not IDictionary dict) return Ownership.Unknown;
            return dict.Contains(id) && dict[id] is bool b && b ? Ownership.Owned : Ownership.Missing;
        }
        catch { return Ownership.Unknown; }
    }

    private static readonly (string token, string flag)[] DashFlags =
    {
        ("u", "UpDash"), ("ur", "UpRightDash"), ("r", "RightDash"), ("dr", "DownRightDash"),
        ("d", "DownDash"), ("dl", "DownLeftDash"), ("l", "LeftDash"), ("ul", "UpLeftDash"),
    };

    private static Ownership Dash(string dir)
    {
        // Unshuffled moves are precollected items, so the save flags are always right.
        // -1 = slot data without "dash_shuffle" (old apworld): Open World then grants every direction.
        if (ManagerInt("DashShuffle", 0) == -1) return Ownership.Owned;
        foreach ((string token, string flag) in DashFlags)
        {
            if (token == dir) return Flag(flag);
        }
        return Ownership.Unknown;
    }

    /// <summary>Same ID scheme as Items.add_interactable_to_table in the apworld.</summary>
    private static long InteractableId(long baseId, string levelKey)
    {
        int split = ManagerInt("SplitInteractables", 0);
        if (split == 0 || string.IsNullOrEmpty(levelKey)) return baseId;
        int level = int.Parse(levelKey.Substring(0, levelKey.Length - 1));
        char side = levelKey[^1];
        return split switch
        {
            1 => baseId + 0x6000 + 0x40 * level,                                            // per level
            2 => baseId + 0x5000 + (side == 'b' ? 0x100 : side == 'c' ? 0x200 : 0),          // per side
            3 => baseId + 0x7000 + 0x100 * level + (side == 'b' ? 0x40 : side == 'c' ? 0x80 : 0), // per level and side
            _ => baseId,
        };
    }

    private static Ownership Interactable(long baseId, string levelKey)
    {
        // Interactables that are not in the item pool always work.
        try
        {
            if (Member(Manager, "ExistentInteractables") is IList existent && existent.Count > 0 && !existent.Contains(baseId))
                return Ownership.Owned;
        }
        catch { }
        return InDictionary("Interactables", InteractableId(baseId, levelKey));
    }

    // ---------------------------------------------------------------- items added by our apworld fork

    /// <summary>"Wall Jump" item of the CelesteMovementRando apworld fork (Items.py: celeste_base_id + 0xF000).</summary>
    public const long WallJumpItemId = 0xCA1F000;

    private static int cachedQueueCount = -1;
    private static object cachedQueue;
    private static readonly System.Collections.Generic.HashSet<long> receivedIds = new();

    /// <summary>
    /// True if the item was received from the server. Reads ArchipelagoManager.ItemQueue (public list of
    /// (index, ItemInfo)); Open World ignores item IDs it does not know, so our extra items only live there.
    /// </summary>
    public static bool HasReceived(long itemId)
    {
        try
        {
            if (Member(Manager, "ItemQueue") is not IList queue) return false;
            if (!ReferenceEquals(queue, cachedQueue) || queue.Count != cachedQueueCount)
            {
                receivedIds.Clear();
                foreach (object entry in queue)
                {
                    object info = entry?.GetType().GetProperty("Item2")?.GetValue(entry);
                    if (info?.GetType().GetProperty("ItemId")?.GetValue(info) is long id) receivedIds.Add(id);
                }
                cachedQueue = queue;
                cachedQueueCount = queue.Count;
            }
            return receivedIds.Contains(itemId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Debug console command (open the console with ~ in debug mode).</summary>
    [Monocle.Command("cmh_items", "Celeste Movement Helper: show what the helper reads from Archipelago Open World")]
    private static void CmdItems()
    {
        void Log(string s) => Monocle.Engine.Commands.Log(s);
        Log($"Open World found: {Available}, connected: {Connected}");
        Log($"DashShuffle={ManagerInt("DashShuffle", -99)} ClimbShuffle={ManagerInt("ClimbShuffle", -99)} SplitInteractables={ManagerInt("SplitInteractables", -99)}");
        foreach ((string token, string flag) in DashFlags) Log($"  {flag}: {Flag(flag)}");
        Log($"  RightClimb: {Flag("RightClimb")}  LeftClimb: {Flag("LeftClimb")}  Crouch: {Flag("Crouch")}");
        Log($"  Wall Jump received: {HasReceived(WallJumpItemId)}  (setting WallJumpShuffle={CelesteMovementHelperModule.Settings?.WallJumpShuffle}, locked={CelesteMovementHelperModule.WallJumpLocked})");
        Log($"  Items in queue: {(Member(Manager, "ItemQueue") is IList q ? q.Count : -1)}");
    }

    public static Ownership Has(string token, TokenInfo info, string levelKey)
    {
        if (!Available) return Ownership.Unknown;

        if (token.StartsWith("any_dash_"))
        {
            Ownership best = Ownership.Missing;
            foreach (string dir in token.Substring(9).Split('_'))
            {
                Ownership o = Dash(dir);
                if (o == Ownership.Owned) return Ownership.Owned;
                if (o == Ownership.Unknown) best = Ownership.Unknown;
            }
            return best;
        }

        switch (token)
        {
            case "u_dash": return Dash("u");
            case "ur_dash": return Dash("ur");
            case "r_dash": return Dash("r");
            case "dr_dash": return Dash("dr");
            case "d_dash": return Dash("d");
            case "dl_dash": return Dash("dl");
            case "l_dash": return Dash("l");
            case "ul_dash": return Dash("ul");
            case "r_climb": return ManagerInt("ClimbShuffle", 0) == -1 ? Ownership.Owned : Flag("RightClimb");
            case "l_climb": return ManagerInt("ClimbShuffle", 0) == -1 ? Ownership.Owned : Flag("LeftClimb");
            case "crouch": return Flag("Crouch");
            case "cannot_access": return Ownership.Missing;
        }

        if (info == null) return Ownership.Unknown;
        switch (info.Kind)
        {
            case "interactable" when info.Id.HasValue: return Interactable(info.Id.Value, levelKey);
            case "key" when info.Id.HasValue: return InDictionary("KeyItems", info.Id.Value);
            case "gem" when info.Id.HasValue: return InDictionary("GemItems", info.Id.Value);
            default: return Ownership.Unknown;
        }
    }
}
