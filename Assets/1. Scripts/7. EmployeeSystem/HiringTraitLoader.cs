// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/HiringTraitLoader.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the strength/weakness trait lists used by the Hiring Board from
/// Resources/EmployeeAssets/strengths.json and weaknesses.json. Each trait has a
/// short word and a description shown in a hover tooltip. Falls back to built-in
/// lists if the JSON is missing or malformed. Mirrors <see cref="EmployeeNameListLoader"/>.
/// </summary>
public static class HiringTraitLoader
{
    [Serializable] private class TraitEntry { public string word; public string description; }
    [Serializable] private class TraitList  { public List<TraitEntry> traits; }

    private static List<TraitEntry> _strengths;
    private static List<TraitEntry> _weaknesses;
    private static Dictionary<string, string> _descriptions; // word → description (case-insensitive)

    public static string RandomStrength()
    {
        EnsureLoaded();
        return PickWord(_strengths, "Reliable");
    }

    public static string RandomWeakness()
    {
        EnsureLoaded();
        return PickWord(_weaknesses, "Forgetful");
    }

    /// <summary>Description for a trait word, or empty string if unknown.</summary>
    public static string GetDescription(string word)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(word)) return "";
        return _descriptions.TryGetValue(word, out var d) ? d : "";
    }

    public static void Reload()
    {
        _strengths = null;
        _weaknesses = null;
        _descriptions = null;
    }

    // ── Internal ────────────────────────────────────────────────────────────────
    private static string PickWord(List<TraitEntry> list, string fallback)
    {
        if (list == null || list.Count == 0) return fallback;
        return list[UnityEngine.Random.Range(0, list.Count)].word;
    }

    private static void EnsureLoaded()
    {
        if (_descriptions != null) return;

        _strengths  = Load("EmployeeAssets/strengths",  FallbackStrengths());
        _weaknesses = Load("EmployeeAssets/weaknesses", FallbackWeaknesses());

        _descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddAll(_strengths);
        AddAll(_weaknesses);
    }

    private static void AddAll(List<TraitEntry> list)
    {
        if (list == null) return;
        foreach (var e in list)
            if (e != null && !string.IsNullOrEmpty(e.word) && !_descriptions.ContainsKey(e.word))
                _descriptions[e.word] = e.description ?? "";
    }

    private static List<TraitEntry> Load(string resourcePath, List<TraitEntry> fallback)
    {
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);
        if (asset != null)
        {
            try
            {
                var parsed = JsonUtility.FromJson<TraitList>(asset.text);
                if (parsed?.traits != null && parsed.traits.Count > 0)
                    return parsed.traits;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HiringTraitLoader] Failed to parse {resourcePath}, using fallback: {e.Message}");
            }
        }
        return fallback;
    }

    private static List<TraitEntry> FallbackStrengths() => new List<TraitEntry>
    {
        new TraitEntry { word = "Quick Learner", description = "Picks up new tasks fast; needs less training." },
        new TraitEntry { word = "Reliable",      description = "Shows up on time and gets the job done." },
        new TraitEntry { word = "Strong Back",   description = "Handles heavy cases all day without tiring." },
    };

    private static List<TraitEntry> FallbackWeaknesses() => new List<TraitEntry>
    {
        new TraitEntry { word = "Perfectionist",   description = "Slows down fussing over tiny details." },
        new TraitEntry { word = "Forgetful",       description = "Misplaces tools and forgets tasks." },
        new TraitEntry { word = "Slightly Clumsy", description = "Drops a case every now and then." },
    };
}
