// METADATA file_path: Assets/10. Editor/SaveLoadE2ETest.cs
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// TEMPORARY end-to-end save/load harness. Drives the exact F5/F9 chain
/// (PlacementSystem.SaveGame("quicksave") -> LoadGame()) WITHOUT keyboard input,
/// then validates that the hardened EmployeeListPanelController subscription
/// rebuilt the UI correctly from the respawned EmployeeRegistry.
///
/// Writes a JSON verdict to Assets/_Saves/e2e_test_result.json because the
/// MCP console tool is currently broken (reflection error) and cannot read logs.
///
/// Auto-runs ~2s after play mode starts. Delete this file after the test.
/// </summary>
public class SaveLoadE2ETest : MonoBehaviour
{
    private const string ResultPath = "Assets/_Saves/e2e_test_result.json";

    private void Start()
    {
        StartCoroutine(RunTestGuarded());
    }

    // Wrapper that guarantees a result file is written even if RunTest throws.
    // Coroutines can't try/catch across yields, so we drive RunTest manually and
    // catch on each MoveNext. Any exception -> write FAIL_EXCEPTION with details.
    private IEnumerator RunTestGuarded()
    {
        var log = new StringBuilder();
        var result = new TestResult();
        IEnumerator inner = RunTest(result, log);

        while (true)
        {
            object current;
            try
            {
                if (!inner.MoveNext())
                    yield break; // RunTest finished and already called Finish()
                current = inner.Current;
            }
            catch (System.Exception ex)
            {
                result.verdict = "FAIL_EXCEPTION";
                log.AppendLine($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                log.AppendLine(ex.StackTrace);
                Finish(result, log);
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator RunTest(TestResult result, StringBuilder log)
    {

        // ── Let the scene fully boot ───────────────────────────────────────────
        yield return new WaitForSeconds(2f);

        var placement = Object.FindAnyObjectByType<PlacementSystem>();
        var panel = EmployeeListPanelController.Instance;
        var registry = EmployeeRegistry.Instance;

        result.placementFound = placement != null;
        result.panelFound = panel != null;
        result.registryFound = registry != null;

        if (placement == null || panel == null || registry == null)
        {
            result.verdict = "FAIL_SETUP";
            log.AppendLine($"Setup missing: placement={result.placementFound} panel={result.panelFound} registry={result.registryFound}");
            Finish(result, log);
            yield break;
        }

        // ── (1) Snapshot the PRE-SAVE employee roster ──────────────────────────
        var preSave = registry.All
            .Where(i => i?.Record != null)
            .Select(i => Snapshot(i.Record))
            .OrderBy(s => s.guid)
            .ToList();

        result.preSaveCount = preSave.Count;
        log.AppendLine($"Pre-save roster: {preSave.Count} employees");
        foreach (var s in preSave)
            log.AppendLine($"  {s.name} | guid={Short(s.guid)} | morale={s.morale:F1} safety={s.safety:F1} fatigue={s.fatigue:F1} skill={s.skill:F1} | avatar={Trunc(s.avatarResourceKey)}");

        if (preSave.Count != 4)
            log.AppendLine($"WARNING: expected 4 employees pre-save, found {preSave.Count}.");

        // ── (2) F5 quicksave (direct call — same path the keybind triggers) ────
        placement.SaveGame("quicksave");
        log.AppendLine("F5 -> SaveGame(\"quicksave\") invoked.");
        yield return null; // let disk write settle

        // ── INSTRUMENTATION: reproduce LoadGame's restore steps with logging ───
        // Verify what SaveSystem.Load actually returns.
        var loaded = SaveSystem.Load("quicksave");
        log.AppendLine(loaded == null
            ? "SaveSystem.Load returned NULL"
            : $"SaveSystem.Load returned {loaded.employeeRecords?.Count ?? -1} employee records");

        var spawner = Object.FindAnyObjectByType<EmployeeSpawner>();
        log.AppendLine($"EmployeeSpawner found: {spawner != null}");
        log.AppendLine($"Registry.Instance == harness registry: {ReferenceEquals(EmployeeRegistry.Instance, registry)}");
        log.AppendLine($"Registry count BEFORE destroy: {registry.Count}");

        // ── (3) F9 quickload (direct call — destroy + respawn + events) ────────
        placement.LoadGame();
        log.AppendLine("F9 -> LoadGame() invoked.");
        log.AppendLine($"Registry count IMMEDIATELY after LoadGame (same frame): {registry.Count}");

        // ApplySaveData uses Object.Destroy (deferred to end of frame) then
        // spawns synchronously. Wait several frames so destroy flushes, the
        // retry-loop subscription settles, and OnEmployeeAdded rebuilds fire.
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            log.AppendLine($"  frame {i}: registry count = {registry.Count}");
        }
        yield return new WaitForSeconds(1f);
        log.AppendLine($"Registry count after 1s settle: {registry.Count}");

        // ── (4) Open the panel — forces TrySubscribe + RebuildList ─────────────
        panel.Open();
        yield return null;
        yield return null;

        // ── (5a) Validate the REGISTRY post-load ───────────────────────────────
        var postLoad = registry.All
            .Where(i => i?.Record != null)
            .Select(i => Snapshot(i.Record))
            .OrderBy(s => s.guid)
            .ToList();

        result.postLoadRegistryCount = postLoad.Count;
        log.AppendLine($"Post-load registry roster: {postLoad.Count} employees");
        foreach (var s in postLoad)
            log.AppendLine($"  {s.name} | guid={Short(s.guid)} | morale={s.morale:F1} safety={s.safety:F1} fatigue={s.fatigue:F1} skill={s.skill:F1} | avatar={Trunc(s.avatarResourceKey)}");

        // ── (5b) Validate the UI DOM rebuilt by RebuildList ────────────────────
        // Read the actual rebuilt rows straight from the UIDocument tree — this
        // proves the subscription chain refreshed the visible UI, not just data.
        var doc = panel.GetComponent<UIDocument>();
        var root = doc != null ? doc.rootVisualElement : null;
        var listContainer = root?.Q<VisualElement>("employee-list-container");
        var countLabel = root?.Q<Label>("employee-count");

        var uiRowNames = new List<string>();
        if (listContainer != null)
        {
            foreach (var child in listContainer.Children())
            {
                var nameLabel = child.Q<Label>("row-name");
                if (nameLabel != null) uiRowNames.Add(nameLabel.text);
            }
        }
        result.uiRowCount = uiRowNames.Count;
        result.uiCountLabel = countLabel?.text ?? "(null)";
        log.AppendLine($"UI rows rendered: {uiRowNames.Count} | count-label='{result.uiCountLabel}'");
        foreach (var n in uiRowNames) log.AppendLine($"  row: {n}");

        // ── Comparisons ────────────────────────────────────────────────────────
        // Names must match pre-save set (order-independent).
        var preNames = preSave.Select(s => s.name).OrderBy(n => n).ToList();
        var postNames = postLoad.Select(s => s.name).OrderBy(n => n).ToList();
        var uiNamesSorted = uiRowNames.OrderBy(n => n).ToList();

        result.namesMatchRegistry = preNames.SequenceEqual(postNames);
        result.namesMatchUI = preNames.SequenceEqual(uiNamesSorted);

        // Stats must match by name (GUIDs are regenerated on respawn, so match on name).
        result.statsMatch = true;
        foreach (var pre in preSave)
        {
            var post = postLoad.FirstOrDefault(p => p.name == pre.name);
            if (post == null)
            {
                result.statsMatch = false;
                log.AppendLine($"MISMATCH: '{pre.name}' missing post-load.");
                continue;
            }
            if (!Approximately(pre, post, out string diff))
            {
                result.statsMatch = false;
                log.AppendLine($"STAT MISMATCH for '{pre.name}': {diff}");
            }
        }

        // Avatar keys must survive round-trip.
        result.avatarsMatch = true;
        foreach (var pre in preSave)
        {
            var post = postLoad.FirstOrDefault(p => p.name == pre.name);
            if (post != null && pre.avatarResourceKey != post.avatarResourceKey)
            {
                result.avatarsMatch = false;
                log.AppendLine($"AVATAR MISMATCH for '{pre.name}': '{Trunc(pre.avatarResourceKey)}' -> '{Trunc(post.avatarResourceKey)}'");
            }
        }

        bool pass =
            result.preSaveCount == 4 &&
            result.postLoadRegistryCount == 4 &&
            result.uiRowCount == 4 &&
            result.namesMatchRegistry &&
            result.namesMatchUI &&
            result.statsMatch &&
            result.avatarsMatch;

        result.verdict = pass ? "PASS" : "FAIL_VALIDATION";
        log.AppendLine($"VERDICT: {result.verdict}");

        Finish(result, log);
    }

    private void Finish(TestResult result, StringBuilder log)
    {
        result.logText = log.ToString();
        var dir = Path.GetDirectoryName(ResultPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
        Debug.Log($"[SaveLoadE2ETest] {result.verdict} — result written to {ResultPath}\n{result.logText}");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static EmpSnapshot Snapshot(EmployeeRecord r) => new EmpSnapshot
    {
        guid = r.employeeGuid ?? "",
        name = r.employeeName ?? "",
        morale = r.morale,
        safety = r.safety,
        fatigue = r.fatigue,
        skill = r.skill,
        avatarResourceKey = r.avatarResourceKey ?? ""
    };

    private static bool Approximately(EmpSnapshot a, EmpSnapshot b, out string diff)
    {
        diff = "";
        if (Mathf.Abs(a.morale - b.morale) > 0.05f) diff += $"morale {a.morale:F2}->{b.morale:F2} ";
        if (Mathf.Abs(a.safety - b.safety) > 0.05f) diff += $"safety {a.safety:F2}->{b.safety:F2} ";
        if (Mathf.Abs(a.fatigue - b.fatigue) > 0.05f) diff += $"fatigue {a.fatigue:F2}->{b.fatigue:F2} ";
        if (Mathf.Abs(a.skill - b.skill) > 0.05f) diff += $"skill {a.skill:F2}->{b.skill:F2} ";
        return diff.Length == 0;
    }

    private static string Trunc(string s) =>
        string.IsNullOrEmpty(s) ? "(empty)" : (s.Length > 36 ? s.Substring(0, 36) : s);

    private static string Short(string s) =>
        string.IsNullOrEmpty(s) ? "(none)" : (s.Length >= 8 ? s.Substring(0, 8) : s);

    private class EmpSnapshot
    {
        public string guid, name, avatarResourceKey;
        public float morale, safety, fatigue, skill;
    }

    [System.Serializable]
    private class TestResult
    {
        public string verdict = "NOT_RUN";
        public bool placementFound, panelFound, registryFound;
        public int preSaveCount, postLoadRegistryCount, uiRowCount;
        public string uiCountLabel;
        public bool namesMatchRegistry, namesMatchUI, statsMatch, avatarsMatch;
        public string logText;
    }
}
#endif
