using System;
using System.Collections.Generic;

/// <summary>
/// Reusable, generic-free header sort/filter state machine shared by both the VENDORS grid and the
/// Purchasing Tab's item list, so the "click header → Ascending → Descending → Base" cycle and the
/// inline search box behavior are implemented and debugged in exactly one place.
///
/// Deliberately holds no UI references — the owning panel (VendorsTabView / PurchasingPanel) wires
/// header Labels/TextFields to this controller's methods and reacts to OnStateChanged by re-running
/// its own row filtering/sorting, matching the code-only styling convention already used across
/// ContractsPanel/PurchasingPanel (no data binding, no USS).
/// </summary>
public class ExcelHeaderSortController
{
    public enum SortDirection { None, Ascending, Descending }

    private readonly Dictionary<string, SortDirection> _directionByColumn = new();
    private readonly Dictionary<string, string> _searchByColumn = new();

    /// <summary>Raised on any header click or search text change so the owning panel knows to
    /// rebuild just its rows.</summary>
    public event Action OnStateChanged;

    /// <summary>Idempotent — calling twice on the same column id is a no-op, so a panel can register
    /// its full column set once per Build() without guarding every call site itself.</summary>
    public void RegisterColumn(string columnId)
    {
        if (string.IsNullOrEmpty(columnId)) throw new ArgumentException("columnId must not be empty.");
        if (!_directionByColumn.ContainsKey(columnId))
        {
            _directionByColumn[columnId] = SortDirection.None;
            _searchByColumn[columnId] = string.Empty;
        }
    }

    /// <summary>Cycles this column's direction (None → Ascending → Descending → None) and clears
    /// every other column's direction — a spreadsheet has one active sort column at a time.</summary>
    public void OnHeaderClicked(string columnId)
    {
        RequireRegistered(columnId);

        var current = _directionByColumn[columnId];
        var next = current switch
        {
            SortDirection.None => SortDirection.Ascending,
            SortDirection.Ascending => SortDirection.Descending,
            _ => SortDirection.None
        };

        foreach (var key in new List<string>(_directionByColumn.Keys))
            _directionByColumn[key] = SortDirection.None;
        _directionByColumn[columnId] = next;

        OnStateChanged?.Invoke();
    }

    public SortDirection GetDirection(string columnId)
    {
        RequireRegistered(columnId);
        return _directionByColumn[columnId];
    }

    public void SetSearchText(string columnId, string text)
    {
        RequireRegistered(columnId);
        _searchByColumn[columnId] = text ?? string.Empty;
        OnStateChanged?.Invoke();
    }

    public string GetSearchText(string columnId)
    {
        RequireRegistered(columnId);
        return _searchByColumn[columnId];
    }

    /// <summary>The one column currently driving sort, or null if every column is at rest (Base
    /// order — whatever order the caller's own list already holds).</summary>
    public string ActiveSortColumn
    {
        get
        {
            foreach (var kv in _directionByColumn)
                if (kv.Value != SortDirection.None) return kv.Key;
            return null;
        }
    }

    private void RequireRegistered(string columnId)
    {
        if (string.IsNullOrEmpty(columnId) || !_directionByColumn.ContainsKey(columnId))
            throw new ArgumentException($"Column '{columnId}' was never registered via RegisterColumn.");
    }
}
