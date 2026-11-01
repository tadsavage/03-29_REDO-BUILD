using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Explains a left click that should have opened an employee / machine card and did nothing (Tad, 2026-10-31: clicking avatars and
/// equipment stopped showing their info, but nothing in the code could be seen to fail).
///
/// On every left click in the Main scene that is NOT over a UI element, if the pointer is within a few dozen pixels of an employee
/// or a machine on screen, it writes ONE warning (so it shows with info logs muted) listing what the click picker returned and the
/// first raw physics hits in front of it. Read it from Logs/Editor.log or Player.log: it names exactly what stood in the way, or
/// shows that nothing did, which means the handler itself never ran. At most one line every 5 s.
/// </summary>
public class ClickProbe : MonoBehaviour
{
    private const float NearPixels = 70f;
    private const float MinSecondsBetweenLogs = 5f;
    private static ClickProbe _instance;
    private float _lastLog = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("ClickProbe");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ClickProbe>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (Time.unscaledTime - _lastLog < MinSecondsBetweenLogs) return;
        if (SceneManager.GetActiveScene().name != "Main") return;
        if (UIInputGuard.IsPointerOverUIToolkit()) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector2 mp = mouse.position.ReadValue();
        string nearName = null;
        float nearDist = NearPixels;
        foreach (var h in FindObjectsByType<EmployeeClickHandler>())
        {
            if (h == null || h.name.StartsWith("LiveFeed_")) continue;
            Vector3 s = cam.WorldToScreenPoint(h.transform.position + Vector3.up);
            if (s.z <= 0f) continue;
            float d = Vector2.Distance(mp, new Vector2(s.x, s.y));
            if (d < nearDist) { nearDist = d; nearName = h.name; }
        }
        foreach (var b in FindObjectsByType<EquipmentBattery>())
        {
            if (b == null) continue;
            Vector3 s = cam.WorldToScreenPoint(b.transform.position + Vector3.up);
            if (s.z <= 0f) continue;
            float d = Vector2.Distance(mp, new Vector2(s.x, s.y));
            if (d < nearDist) { nearDist = d; nearName = b.name; }
        }
        if (nearName == null) return;   // not aimed at a person or a machine

        _lastLog = Time.unscaledTime;
        Ray ray = cam.ScreenPointToRay(mp);
        var sb = new StringBuilder();
        sb.Append($"[ClickProbe] Click {nearDist:0}px from '{nearName}'. ");
        if (WorldClickPick.Raycast(ray, out var best))
            sb.Append($"Picker returned '{best.collider.name}' (root '{best.collider.transform.root.name}', trigger={best.collider.isTrigger}, layer={LayerMask.LayerToName(best.collider.gameObject.layer)}, {best.distance:0.0} m). ");
        else
            sb.Append("Picker returned NOTHING. ");

        var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
        sb.Append($"Raw hits ({hits.Length}): ");
        for (int i = 0; i < hits.Length && i < 6; i++)
        {
            var c = hits[i].collider;
            sb.Append($"{c.name}{(c.isTrigger ? "[T]" : "")}@{hits[i].distance:0.0}; ");
        }
        sb.Append($"FSM modal: '{UIModalGuard.Describe()}'. Camera: {cam.name}.");
        Debug.LogWarning(sb.ToString());
    }
}
