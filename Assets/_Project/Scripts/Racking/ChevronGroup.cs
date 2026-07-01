using System.Collections.Generic;

/// <summary>
/// Tracks the chevron side-teams of a single rack collection and enforces that only ONE
/// side is the "selected" (green) team at a time — the side of the last chevron the player
/// clicked. Selection is keyed by side name ("neg"/"pos") so it survives chevron refreshes
/// (chevrons are recreated as a collection grows or a neighbouring row blocks a side).
/// </summary>
public class ChevronGroup
{
    private readonly Dictionary<string, List<ChevronController>> _sides = new();
    private string _selectedKey;

    public string SelectedKey => _selectedKey;

    /// <summary>Registers/updates the chevrons that make up a named side.</summary>
    public void SetSide(string key, List<ChevronController> members)
    {
        _sides[key] = members;
    }

    /// <summary>Marks a side as the collection's selected (green) team.</summary>
    public void SelectSideKey(string key)
    {
        _selectedKey = key;
        ApplyColors();
    }

    /// <summary>Re-applies green to the selected side and clears the rest. Safe to call anytime.</summary>
    public void ApplyColors()
    {
        foreach (var kv in _sides)
        {
            bool selected = kv.Key == _selectedKey;
            foreach (var c in kv.Value)
                if (c != null) c.SetSelected(selected);
        }
    }
}
