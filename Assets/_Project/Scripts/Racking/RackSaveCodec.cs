using UnityEngine;
using TMPro;
using System.Globalization;

/// <summary>
/// Serializes a committed (live) rack's aisle metadata into <see cref="PlacedObject.customData"/>
/// so it survives save/load. <c>customData</c> is the only per-object string the save system already
/// persists (see SavedObject), and racking objects don't otherwise use it.
///
/// Without this, a loaded rack came back with prefab-default labels ("A-01-01") because the rack
/// fields (aisle/bay/level/facing) live only in memory on the PlacedObject and were never written
/// to disk.
///
/// Format (pipe-delimited, InvariantCulture floats):
///   RACK|aisle|bay|levelIndex|levelChar|faceX|faceZ|travelX|travelZ
/// </summary>
public static class RackSaveCodec
{
    private const string PREFIX = "RACK";
    private const int FIELD_COUNT = 9; // PREFIX + 8 values

    public static bool IsRackData(string s) =>
        !string.IsNullOrEmpty(s) && s.StartsWith(PREFIX + "|");

    /// <summary>Encodes a live rack's metadata. Returns empty for non-live/non-rack objects.</summary>
    public static string Encode(PlacedObject po)
    {
        if (po == null || !po.isRackLive) return string.Empty;
        if (po.rackAisle < 0 || po.rackBay < 0 || po.rackLevelIndex < 0) return string.Empty;

        string levelChar = ResolveLevelChar(po);
        Vector3 face = po.rackAisleFacing;
        Vector3 travel = po.rackTravelDir.sqrMagnitude > 0.0001f
            ? po.rackTravelDir
            : TravelFromLabels(po.gameObject);

        var c = CultureInfo.InvariantCulture;
        return string.Join("|",
            PREFIX,
            po.rackAisle.ToString(c),
            po.rackBay.ToString(c),
            po.rackLevelIndex.ToString(c),
            levelChar,
            face.x.ToString("R", c), face.z.ToString("R", c),
            travel.x.ToString("R", c), travel.z.ToString("R", c));
    }

    /// <summary>Parses encoded rack metadata. Returns false for anything that isn't RACK data.</summary>
    public static bool TryDecode(string s, out int aisle, out int bay, out int level,
                                 out string levelChar, out Vector3 face, out Vector3 travel)
    {
        aisle = bay = level = -1;
        levelChar = "0";
        face = Vector3.zero;
        travel = Vector3.zero;

        if (!IsRackData(s)) return false;
        var parts = s.Split('|');
        if (parts.Length < FIELD_COUNT) return false;

        var c = CultureInfo.InvariantCulture;
        if (!int.TryParse(parts[1], NumberStyles.Integer, c, out aisle)) return false;
        if (!int.TryParse(parts[2], NumberStyles.Integer, c, out bay)) return false;
        if (!int.TryParse(parts[3], NumberStyles.Integer, c, out level)) return false;

        levelChar = string.IsNullOrEmpty(parts[4]) ? "0" : parts[4];
        face = new Vector3(ParseF(parts[5], c), 0f, ParseF(parts[6], c));
        travel = new Vector3(ParseF(parts[7], c), 0f, ParseF(parts[8], c));
        return true;
    }

    /// <summary>Applies decoded rack metadata onto a freshly-spawned rack's PlacedObject.</summary>
    public static void RestoreOnto(PlacedObject po, string s)
    {
        if (po == null) return;
        if (!TryDecode(s, out int aisle, out int bay, out int level,
                       out string levelChar, out Vector3 face, out Vector3 travel))
            return;

        po.isRackLive = true;
        po.rackAisle = aisle;
        po.rackBay = bay;
        po.rackLevelIndex = level;
        po.rackAisleFacing = face;
        po.rackTravelDir = travel;
        po.rackLevelChar = levelChar;
    }

    private static float ParseF(string s, CultureInfo c) =>
        float.TryParse(s, NumberStyles.Float, c, out float v) ? v : 0f;

    /// <summary>Reads the level char from an active label ("01-02-A1" → "A"), falling back to
    /// the stored field, then to the per-aisle designation scheme.</summary>
    private static string ResolveLevelChar(PlacedObject po)
    {
        if (!string.IsNullOrEmpty(po.rackLevelChar)) return po.rackLevelChar;

        foreach (var tmp in po.GetComponentsInChildren<TextMeshPro>(false)) // active only
        {
            string t = tmp.text;
            if (string.IsNullOrEmpty(t)) continue;
            var seg = t.Split('-');
            if (seg.Length >= 3 && seg[2].Length >= 2)
                return seg[2].Substring(0, seg[2].Length - 1);
        }
        return LocationNameGenerator.LevelChar(po.rackLevelIndex, AisleRegistry.GetDesignations(po.rackAisle));
    }

    /// <summary>Travel direction derived from the rack's own active labels: from its position-0
    /// label to its position-1 label. Falls back to the rack's run axis (local X).</summary>
    private static Vector3 TravelFromLabels(GameObject rack)
    {
        Vector3 p0 = Vector3.zero, p1 = Vector3.zero;
        bool h0 = false, h1 = false;
        foreach (var tmp in rack.GetComponentsInChildren<TextMeshPro>(false)) // active only
        {
            string t = tmp.text;
            if (string.IsNullOrEmpty(t)) continue;
            char last = t[t.Length - 1];
            if (last == '0' && !h0) { p0 = tmp.transform.position; h0 = true; }
            else if (last == '1' && !h1) { p1 = tmp.transform.position; h1 = true; }
        }
        if (h0 && h1)
        {
            Vector3 d = p1 - p0; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        Vector3 r = rack.transform.right; r.y = 0f;
        return r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;
    }
}
