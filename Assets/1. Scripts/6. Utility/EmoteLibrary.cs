using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads every expression bubble out of <c>Resources/Emotes</c> exactly once and
/// caches them as runtime sprites, shared across all agents.
///
/// The PNGs in that folder are imported as plain Textures (not Sprite assets), so we
/// build Sprites from them at load time. Sprite.Create only references the texture for
/// rendering — it does NOT require the texture to be CPU-readable, so this is cheap and
/// needs no asset reimport. The pool is static, so 1000 agents share one set of sprites.
/// </summary>
public static class EmoteLibrary
{
    public const string ResourceFolder = "Emotes";

    private static Sprite[]                     _all;
    private static Dictionary<string, Sprite>   _byName;

    /// <summary>Every bubble in Resources/Emotes (loaded on first access).</summary>
    public static Sprite[] All
    {
        get { EnsureLoaded(); return _all; }
    }

    /// <summary>Look up a single bubble by file name, e.g. "emote_exclamations". Null if missing.</summary>
    public static Sprite Get(string name)
    {
        EnsureLoaded();
        return _byName.TryGetValue(name, out var s) ? s : null;
    }

    private static void EnsureLoaded()
    {
        if (_all != null) return;

        var textures = Resources.LoadAll<Texture2D>(ResourceFolder);
        var list     = new List<Sprite>(textures.Length);
        _byName      = new Dictionary<string, Sprite>(textures.Length);

        foreach (var tex in textures)
        {
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = tex.name;

            list.Add(sprite);
            _byName[tex.name] = sprite;
        }

        _all = list.ToArray();

        if (_all.Length == 0)
            Debug.LogWarning($"[EmoteLibrary] No textures found in Resources/{ResourceFolder}.");
    }
}
