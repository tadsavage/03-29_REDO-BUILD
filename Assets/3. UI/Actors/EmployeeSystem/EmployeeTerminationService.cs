using UnityEngine;

/// <summary>
/// The single authority for the full "terminate an employee" process, shared by every UI entry
/// point (roster card, info card, employee list panel) so termination behaves identically
/// everywhere:
///   1. HR / model — EmployeeLifecycleService.Fire() sets status = Terminated, stamps the
///      separation date, and broadcasts OnFired (the hook any future HR/notification system
///      listens to).
///   2. Record-keeping — the full record is archived (FormerEmployeeArchive) so it survives the
///      avatar despawning and can be used for rehire / union reinstatement / history.
///   3. Roster — the employee is unregistered immediately, so every live list drops them.
///   4. Selection — if they were the outlined/followed employee, the highlight is cleared.
///   5. World — they storm off to the yard exit and despawn (EmployeeTerminationWalk).
///
/// NOTE on "no more paychecks / off the schedule": there is no payroll or attendance system yet
/// (totalWagesPaid is never incremented), so a terminated, unregistered, despawned employee
/// can't be paid or scheduled. When payroll/scheduling are built they must gate on
/// status == Active (and the registry), which already excludes terminated staff.
/// </summary>
public static class EmployeeTerminationService
{
    public static void Terminate(EmployeeIdentity identity, Sprite angryEmote = null, float waveSeconds = 3f)
    {
        if (identity == null || identity.Record == null) return;
        if (identity.Record.status == EmploymentStatus.Terminated) return;

        AudioManager.Play("ButtonClick");

        // 1) HR / model change (also broadcasts OnFired). Guarantee the status even if the
        //    lifecycle service happens to be missing.
        EmployeeLifecycleService.Instance?.Fire(identity.Record.employeeGuid);
        identity.Record.status = EmploymentStatus.Terminated;

        // 2) Keep the full record on file for rehire / union / history (dedup'd by GUID).
        FormerEmployeeArchive.Instance?.Archive(identity.Record, EmploymentStatus.Terminated);

        // 3) Drop them off the live roster now; the avatar keeps walking off.
        EmployeeRegistry.Instance?.Unregister(identity);

        // 4) Stop outlining/following them if they were the selected employee.
        if (EmployeeHighlighter.HasInstance && EmployeeHighlighter.Instance.IsHighlighted(identity))
            EmployeeHighlighter.Instance.Clear();

        // 5) Storm-off walk → yard exit → despawn. Anchors come from the guard shack; fall back
        //    to a straight march if the yard manager isn't present.
        var yard = Object.FindAnyObjectByType<TruckYardManager>();
        Vector3  stop = (yard != null && yard.GuardExitPost.HasValue)
            ? yard.GuardExitPost.Value : identity.transform.position;
        Vector3? face = yard != null ? yard.GuardShackMain : null;
        Vector3  exit = (yard != null && yard.YardExit.HasValue)
            ? yard.YardExit.Value : identity.transform.position + identity.transform.forward * 20f;

        var walk = identity.gameObject.AddComponent<EmployeeTerminationWalk>();
        walk.Begin(stop, face, exit, angryEmote, waveSeconds);
    }
}
