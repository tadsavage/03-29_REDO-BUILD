using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent record of everyone who has left the company (terminated / resigned). Their full
/// EmployeeRecord is kept here so it survives the avatar despawning — available later for
/// rehire, union reinstatement, or HR history.
///
/// Auto-captures on EmployeeLifecycleService.OnFired / OnResigned (so every separation path is
/// covered), and is also written to explicitly by EmployeeTerminationService. Archive() dedups
/// by GUID, so double-capture is harmless.
///
/// Persisted by the save system via SaveData.formerEmployees (see PlacementSystem
/// Build/ApplySaveData). Self-contained singleton — lazily created, no scene wiring required.
/// </summary>
public class FormerEmployeeArchive : MonoBehaviour
{
    private static FormerEmployeeArchive _instance;
    public static FormerEmployeeArchive Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<FormerEmployeeArchive>();
                if (_instance == null)
                {
                    var go = new GameObject("FormerEmployeeArchive");
                    _instance = go.AddComponent<FormerEmployeeArchive>();
                }
            }
            return _instance;
        }
    }

    /// <summary>True if an archive instance already exists (so callers can avoid creating one).</summary>
    public static bool HasInstance => _instance != null;

    private readonly List<EmployeeRecord> _former = new();
    private bool _subscribed;
    private Coroutine _retry;

    public IReadOnlyList<EmployeeRecord> All => _former;
    public int Count => _former.Count;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        TrySubscribe();
    }

    private void OnDestroy()
    {
        if (_subscribed && EmployeeLifecycleService.Instance != null)
        {
            EmployeeLifecycleService.Instance.OnFired    -= OnFired;
            EmployeeLifecycleService.Instance.OnResigned -= OnResigned;
        }
    }

    // The lifecycle service may not exist yet when we wake — retry until it does.
    private void TrySubscribe()
    {
        if (_subscribed) return;
        var svc = EmployeeLifecycleService.Instance;
        if (svc != null)
        {
            svc.OnFired    += OnFired;
            svc.OnResigned += OnResigned;
            _subscribed = true;
            if (_retry != null) { StopCoroutine(_retry); _retry = null; }
            return;
        }
        if (_retry == null && isActiveAndEnabled)
            _retry = StartCoroutine(RetryLoop());
    }

    private IEnumerator RetryLoop()
    {
        var wait = new WaitForSeconds(0.25f);
        while (!_subscribed) { yield return wait; TrySubscribe(); }
    }

    private void OnFired(EmployeeRecord r)    => Archive(r, EmploymentStatus.Terminated);
    private void OnResigned(EmployeeRecord r) => Archive(r, EmploymentStatus.Resigned);

    /// <summary>Store a separated employee's record (replacing any existing entry for the same GUID).</summary>
    public void Archive(EmployeeRecord record, EmploymentStatus separationStatus)
    {
        if (record == null || string.IsNullOrEmpty(record.employeeGuid)) return;

        var clone = record.Clone();
        clone.status = separationStatus;
        if (string.IsNullOrEmpty(clone.separationDateIso))
            clone.separationDateIso = DateTime.UtcNow.ToString("yyyy-MM-dd");

        _former.RemoveAll(e => e.employeeGuid == clone.employeeGuid);
        _former.Add(clone);

        Debug.Log($"[FormerEmployeeArchive] Archived {clone.employeeName} ({clone.employeeId}) — " +
                  $"{separationStatus}. Total on file: {_former.Count}");
    }

    /// <summary>Look up an archived record by GUID (e.g. to rehire). Null if not on file.</summary>
    public EmployeeRecord Get(string guid) => string.IsNullOrEmpty(guid) ? null : _former.Find(e => e.employeeGuid == guid);

    /// <summary>Remove an archived record (e.g. when the person is rehired). True if one was removed.</summary>
    public bool Remove(string guid) => !string.IsNullOrEmpty(guid) && _former.RemoveAll(e => e.employeeGuid == guid) > 0;

    // ── Save/load integration ────────────────────────────────────────────────────
    public List<EmployeeRecord> Snapshot()
    {
        var list = new List<EmployeeRecord>(_former.Count);
        foreach (var r in _former) list.Add(r.Clone());
        return list;
    }

    public void LoadFrom(List<EmployeeRecord> records)
    {
        _former.Clear();
        if (records == null) return;
        foreach (var r in records)
            if (r != null) _former.Add(r.Clone());
    }
}
