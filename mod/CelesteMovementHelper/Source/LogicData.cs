using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Celeste.Mod.CelesteMovementHelper;

/// <summary>
/// Room logic exported from the Celeste (Open World) apworld by tools/export_help_data.py.
/// A rule is a list of alternatives (OR); each alternative is a list of tokens (AND).
/// An empty rule means "no requirement", exactly like the apworld.
/// </summary>
public class TokenInfo
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "other";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("id")] public long? Id { get; set; }
}

public class RuleSet
{
    [JsonPropertyName("di")] public List<List<string>> DeveloperIntended { get; set; } = new();
    [JsonPropertyName("vm")] public List<List<string>> VanillaMovement { get; set; } = new();
    [JsonPropertyName("as")] public List<List<string>> Assist { get; set; } = new();

    public List<List<string>> For(LogicLevel level) => level switch
    {
        LogicLevel.VanillaMovement => VanillaMovement ?? new(),
        LogicLevel.AssistMode => Assist ?? new(),
        _ => DeveloperIntended ?? new(),
    };
}

public class ConnectionInfo : RuleSet
{
    [JsonPropertyName("from")] public string From { get; set; } = "";
    [JsonPropertyName("to")] public string To { get; set; } = "";
}

public class LocationInfo : RuleSet
{
    [JsonPropertyName("region")] public string Region { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
}

public class DoorInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("dir")] public string Dir { get; set; } = "";
}

public class RoomInfo
{
    [JsonPropertyName("connections")] public List<ConnectionInfo> Connections { get; set; } = new();
    [JsonPropertyName("locations")] public List<LocationInfo> Locations { get; set; } = new();
    [JsonPropertyName("doors")] public List<DoorInfo> Doors { get; set; } = new();
}

public class LevelInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("tokens")] public Dictionary<string, TokenInfo> Tokens { get; set; } = new();
    [JsonPropertyName("rooms")] public Dictionary<string, RoomInfo> Rooms { get; set; } = new();
}

public class LogicFile
{
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("tokens")] public Dictionary<string, TokenInfo> Tokens { get; set; } = new();
    [JsonPropertyName("levels")] public Dictionary<string, LevelInfo> Levels { get; set; } = new();
}

public static class LogicData
{
    public static LogicFile File { get; private set; }

    public static void Load()
    {
        try
        {
            using Stream s = typeof(LogicData).Assembly.GetManifestResourceStream("CMH.logic.json");
            if (s == null)
            {
                Logger.Log(LogLevel.Error, nameof(LogicData), "Embedded logic.json not found");
                return;
            }
            File = JsonSerializer.Deserialize<LogicFile>(s);
            Logger.Log(LogLevel.Info, nameof(LogicData), $"Loaded logic for {File?.Levels.Count ?? 0} levels");
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Error, nameof(LogicData), $"Could not load logic.json: {e}");
        }
    }

    /// <summary>"1a", "10c"… from the current session, or null for non-vanilla maps.</summary>
    public static string LevelKey(Session session)
    {
        if (session == null) return null;
        AreaData area = AreaData.Get(session.Area);
        if (area == null || area.SID == null || !area.SID.StartsWith("Celeste/")) return null;
        int mode = (int)session.Area.Mode;
        if (mode < 0 || mode > 2) return null;
        return $"{session.Area.ID}{(char)('a' + mode)}";
    }

    public static LevelInfo GetLevel(string key)
        => key != null && File != null && File.Levels.TryGetValue(key, out LevelInfo l) ? l : null;

    public static TokenInfo GetToken(LevelInfo level, string token)
    {
        if (level != null && level.Tokens.TryGetValue(token, out TokenInfo t)) return t;
        if (File != null && File.Tokens.TryGetValue(token, out t)) return t;
        return null;
    }
}
