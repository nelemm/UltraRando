using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Celeste.Mod.CelesteMovementHelper;

public class TechniqueSource
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}

public class Technique
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("aka")] public List<string> Aka { get; set; } = new();
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
    [JsonPropertyName("inputs")] public List<string> Inputs { get; set; } = new();
    [JsonPropertyName("timing")] public string Timing { get; set; } = "";
    [JsonPropertyName("explanation_fr")] public string ExplanationFr { get; set; } = "";
    [JsonPropertyName("explanation_en")] public string ExplanationEn { get; set; } = "";
    [JsonPropertyName("vanilla_where")] public string VanillaWhere { get; set; } = "";
    [JsonPropertyName("sources")] public List<TechniqueSource> Sources { get; set; } = new();
    [JsonPropertyName("verified")] public bool Verified { get; set; }
}

public class TechniqueFile
{
    [JsonPropertyName("techniques")] public List<Technique> Techniques { get; set; } = new();
}

public static class TechniqueData
{
    private static readonly string[] CategoryOrder = { "basic", "intermediate", "advanced", "expert" };

    public static List<Technique> All { get; private set; } = new();

    public static void Load()
    {
        try
        {
            using Stream s = typeof(TechniqueData).Assembly.GetManifestResourceStream("CMH.techniques.json");
            if (s == null)
            {
                Logger.Log(LogLevel.Warn, nameof(TechniqueData), "Embedded techniques.json not found");
                return;
            }
            TechniqueFile f = JsonSerializer.Deserialize<TechniqueFile>(s);
            All = f?.Techniques ?? new();
            All.Sort((a, b) =>
            {
                int c = Array.IndexOf(CategoryOrder, a.Category).CompareTo(Array.IndexOf(CategoryOrder, b.Category));
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            Logger.Log(LogLevel.Info, nameof(TechniqueData), $"Loaded {All.Count} techniques");
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Error, nameof(TechniqueData), $"Could not load techniques.json: {e}");
        }
    }
}
