using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Gives every placed MHE charging station (MHE_Station prefab) a letter, shown on its "AlphaID" label: the
/// first one placed is A, the second B, and so on (Tad, 2026-10-17; cosmetic for now). Same rules as dock door
/// numbers: a letter never changes once given, it is saved in PlacedObject.customData ("station=A"), a new
/// station takes the LOWEST free letter, and deleting one frees its letter for the next. After Z comes AA, AB...
/// </summary>
[DisallowMultipleComponent]
public class MHEStationLetter : MonoBehaviour
{
    private const string Key = "station=";
    private static readonly List<MHEStationLetter> s_all = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_all.Clear();

    public string Letter { get; private set; }

    private PlacedObject _po;
    private TMP_Text _label;
    private float _nextCheck;

    private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
    private void OnDisable() => s_all.Remove(this);

    /// <summary>A real placed station, not the build-menu hover ghost (same test as PalletJackParkingSpot).</summary>
    private bool IsReal
    {
        get
        {
            if (_po == null) _po = GetComponent<PlacedObject>();
            return _po != null && _po.data != null && _po.enabled && isActiveAndEnabled;
        }
    }

    private void Update()
    {
        // Checked a frame or more after spawning: a save load writes customData right AFTER Instantiate, so reading
        // it in Awake/Start would see it empty and hand out a new letter.
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;
        if (!IsReal) return;

        string saved = Read(_po.customData);
        if (!string.IsNullOrEmpty(saved) && saved != Letter) Letter = saved;
        if (string.IsNullOrEmpty(Letter)) Letter = LowestFree();
        if (Read(_po.customData) != Letter) _po.customData = Key + Letter;
        ApplyLabel();
    }

    private void ApplyLabel()
    {
        if (_label == null)
        {
            var t = transform.Find("AlphaID");
            _label = t != null ? t.GetComponentInChildren<TMP_Text>(true) : GetComponentInChildren<TMP_Text>(true);
        }
        if (_label != null && _label.text != Letter) _label.text = Letter;
    }

    private string LowestFree()
    {
        var used = new HashSet<string>();
        foreach (var s in s_all)
        {
            if (s == null || s == this) continue;
            string l = !string.IsNullOrEmpty(s.Letter) ? s.Letter : Read(s._po != null ? s._po.customData : null);
            if (!string.IsNullOrEmpty(l)) used.Add(l);
        }
        for (int i = 0; i < 702; i++)
        {
            string l = ToLetters(i);
            if (!used.Contains(l)) return l;
        }
        return "?";
    }

    /// <summary>0 = A, 25 = Z, 26 = AA ...</summary>
    private static string ToLetters(int i)
    {
        string s = "";
        i++;
        while (i > 0) { i--; s = (char)('A' + i % 26) + s; i /= 26; }
        return s;
    }

    private static string Read(string customData)
        => !string.IsNullOrEmpty(customData) && customData.StartsWith(Key) ? customData.Substring(Key.Length) : null;

    /// <summary>The station letter a pallet jack is parked on, or null.</summary>
    public static string LetterNear(Vector3 point, float radius)
    {
        foreach (var s in s_all)
        {
            if (s == null || string.IsNullOrEmpty(s.Letter)) continue;
            Vector3 d = s.transform.position - point; d.y = 0f;
            if (d.magnitude <= radius) return s.Letter;
        }
        return null;
    }
}
