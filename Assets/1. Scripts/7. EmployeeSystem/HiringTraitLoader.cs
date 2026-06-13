// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/HiringTraitLoader.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the strength/weakness word lists used by the Hiring Board from
/// Resources/EmployeeAssets/strengths.json and weaknesses.json.
/// Falls back to built-in lists if the JSON is missing or malformed.
/// Mirrors the pattern of <see cref="EmployeeNameListLoader"/>.
/// </summary>
public static class HiringTraitLoader
{
    [Serializable]
    private class TraitList { public List<string> traits; }

    private static List<string> _strengths;
    private static List<string> _weaknesses;

    public static string RandomStrength()
    {
        EnsureLoaded();
        return _strengths[UnityEngine.Random.Range(0, _strengths.Count)];
    }

    public static string RandomWeakness()
    {
        EnsureLoaded();
        return _weaknesses[UnityEngine.Random.Range(0, _weaknesses.Count)];
    }

    public static void Reload()
    {
        _strengths = null;
        _weaknesses = null;
    }

    private static void EnsureLoaded()
    {
        if (_strengths == null)
            _strengths = Load("EmployeeAssets/strengths", FallbackStrengths());
        if (_weaknesses == null)
            _weaknesses = Load("EmployeeAssets/weaknesses", FallbackWeaknesses());
    }

    private static List<string> Load(string resourcePath, List<string> fallback)
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

    private static List<string> FallbackStrengths() => new List<string>
    {
        "Quick Learner", "Hard Worker", "Reliable", "Team Player", "Detail Oriented",
        "Forklift Certified", "Punctual", "Strong", "Calm", "Organized"
    };

    private static List<string> FallbackWeaknesses() => new List<string>
    {
        "Perfectionist", "Slightly Clumsy", "Forgetful", "Impatient", "Stubborn",
        "Talkative", "Easily Bored", "Slow Starter", "Disorganized", "Overconfident"
    };
}
