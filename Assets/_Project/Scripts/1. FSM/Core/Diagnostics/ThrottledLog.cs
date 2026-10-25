using System.Collections.Generic;
using UnityEngine;

/// <summary>Logs a warning at most once per interval per key. The same line used to repeat hundreds of times a session
/// (a loader retrying a lane every tick, selectors skipping the same pick), which buried the real problems in the console.</summary>
public static class ThrottledLog
{
    private static readonly Dictionary<string, float> _last = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _last.Clear();

    public static void Warn(string key, string message, float seconds = 60f)
    {
        float now = Time.realtimeSinceStartup;
        if (_last.TryGetValue(key, out float t) && now - t < seconds) return;
        _last[key] = now;
        Debug.LogWarning(message);
    }
}
