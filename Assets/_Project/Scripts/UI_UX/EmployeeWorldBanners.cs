using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GameCore.Labor;
using GameCore.Services;

/// <summary>
/// Info banners that float over employees in the world (Tad, 2026-10-10). Same look as the employee info card, scaled with distance, always
/// facing the camera, anchored 0.25 m above the top of the avatar and following it. Several can be up at once.
///
///   • Left-click a player  -> banner appears; left-click the same player again -> it goes away.
///   • Shift-click a player -> banner appears (and EmployeeHighlighter outlines + follows them, see EmployeeClickHandler / MHEOperatorSlot).
///   • Clicking empty world clears the outline / camera follow but NEVER a banner. A banner only goes away by double-clicking the banner
///     itself, the second-click toggle above, or when the employee leaves the company (there is no close button - Tad, 2026-10-10).
///
/// The banner is a placeholder: name, ID, role icon and the four bars are read once when it opens. The ONE live field is the task line at
/// the bottom, refreshed several times a second from what the work queue has assigned to that employee (<see cref="EmployeeTaskText"/>).
///
/// The banners are plain UI Toolkit elements added to the HUD document and re-projected from world to panel space every frame. They sit at the
/// BACK of the HUD's children so the top bar and every modal panel draw over them.
/// </summary>
public class EmployeeWorldBanners : MonoBehaviour
{
    // ── Tunables ─────────────────────────────────────────────────────────────────
    private const float LiftMeters = 0.25f;        // gap between the top of the avatar and the banner's bottom edge
    private const float BannerWidth = 270f;
    private const float RefDistance = 15f;         // camera distance at which the banner is MaxScale
    private const float MaxScale = 0.8f;
    private const float MinScale = 0.42f;
    private const float TaskRefreshSeconds = 0.25f;
    private const float RendererRefreshSeconds = 0.5f;

    // ── Palette (the info card's own colours) ────────────────────────────────────
    private static readonly Color ColBg     = new Color(30 / 255f, 38 / 255f, 46 / 255f, 0.98f);
    private static readonly Color ColBorder = new Color(92 / 255f, 155 / 255f, 196 / 255f, 1f);
    private static readonly Color ColInk    = new Color(22 / 255f, 28 / 255f, 36 / 255f, 1f);
    private static readonly Color ColTitle  = new Color(234 / 255f, 244 / 255f, 255 / 255f, 1f);
    private static readonly Color ColSub    = new Color(150 / 255f, 190 / 255f, 220 / 255f, 1f);
    private static readonly Color ColFatigue = new Color(220 / 255f, 60 / 255f, 60 / 255f, 1f);
    private static readonly Color ColSafety  = new Color(236 / 255f, 200 / 255f, 56 / 255f, 1f);
    private static readonly Color ColMorale  = new Color(76 / 255f, 200 / 255f, 90 / 255f, 1f);
    private static readonly Color ColSkill   = new Color(240 / 255f, 240 / 255f, 240 / 255f, 1f);

    private class Banner
    {
        public EmployeeIdentity Identity;
        public VisualElement Root;
        public Label Status;
        public readonly List<Renderer> Renderers = new();
        public float NextRendererRefresh;
        public float NextTaskRefresh;
        public string LastStatus;
    }

    private static EmployeeWorldBanners _instance;
    public static EmployeeWorldBanners Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EmployeeWorldBanners>();
                if (_instance == null) _instance = new GameObject("EmployeeWorldBanners").AddComponent<EmployeeWorldBanners>();
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; }

    private readonly Dictionary<EmployeeIdentity, Banner> _banners = new();
    private readonly List<EmployeeIdentity> _scratch = new();
    private EmployeeInfoUI _infoUI;
    private Camera _cam;

    public bool IsShown(EmployeeIdentity id) => id != null && _banners.ContainsKey(id);

    /// <summary>Plain left-click: open the banner, or close it if it is already open.</summary>
    public void Toggle(EmployeeIdentity id)
    {
        if (id == null) return;
        if (_banners.ContainsKey(id)) Hide(id); else Show(id);
    }

    /// <summary>Open the banner if it is not already up (shift-click never closes one).</summary>
    public void Show(EmployeeIdentity id)
    {
        if (id == null || id.Record == null || _banners.ContainsKey(id)) return;
        var host = HudRoot();
        if (host == null) return;

        var b = new Banner { Identity = id };
        b.Root = BuildCard(id, b);
        host.Add(b.Root);
        b.Root.SendToBack();            // top bar + modal panels must draw over the banners
        _banners[id] = b;
        RefreshRenderers(b);
        RefreshTask(b, force: true);
        PositionBanner(b);              // so it never flashes at the top-left corner for a frame
    }

    public void Hide(EmployeeIdentity id)
    {
        if (id == null || !_banners.TryGetValue(id, out var b)) return;
        b.Root?.RemoveFromHierarchy();
        _banners.Remove(id);
    }

    public void HideAll()
    {
        foreach (var b in _banners.Values) b.Root?.RemoveFromHierarchy();
        _banners.Clear();
    }

    private void OnDestroy() { HideAll(); if (_instance == this) _instance = null; }

    // ── Per frame ────────────────────────────────────────────────────────────────
    private void Update()
    {
        HandleWorldClick();

        if (_banners.Count == 0) return;
        _scratch.Clear();
        _scratch.AddRange(_banners.Keys);
        foreach (var id in _scratch)
        {
            var b = _banners[id];
            // Gone (destroyed, fired, resigned): drop the banner with them.
            if (id == null || id.Record == null || id.Record.status == EmploymentStatus.Terminated || id.Record.status == EmploymentStatus.Resigned)
            { Hide(id); continue; }

            if (Time.unscaledTime >= b.NextRendererRefresh) RefreshRenderers(b);
            RefreshTask(b, force: false);
            PositionBanner(b);
        }
    }

    /// <summary>Clicking empty world ends the outline + camera follow, but never touches a banner (Tad's rule).</summary>
    private void HandleWorldClick()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (!EmployeeHighlighter.HasInstance || !EmployeeHighlighter.Instance.HasHighlight) return;
        if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || UIInputGuard.IsPointerOverUIToolkit()) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        bool onPlayer = false;
        if (Physics.Raycast(ray, out var hit))
            onPlayer = hit.collider.GetComponentInParent<EmployeeIdentity>() != null || hit.collider.GetComponentInParent<MHEOperatorSlot>() != null;
        if (!onPlayer) EmployeeHighlighter.Instance.Clear();
    }

    private void PositionBanner(Banner b)
    {
        if (_cam == null) _cam = Camera.main;
        var panel = b.Root.panel;
        if (_cam == null || panel == null) return;

        var anchor = AnchorWorld(b);
        var vp = _cam.WorldToViewportPoint(anchor);
        bool visible = vp.z > 0.2f && vp.x > -0.15f && vp.x < 1.15f && vp.y > -0.3f && vp.y < 1.3f;
        b.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;

        var p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, anchor, _cam);
        float s = Mathf.Clamp(RefDistance / Mathf.Max(vp.z, 0.01f), MinScale, MaxScale);
        float h = b.Root.resolvedStyle.height; if (float.IsNaN(h) || h < 1f) h = 250f;
        b.Root.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
        b.Root.style.left = p.x - BannerWidth * 0.5f;    // origin is the bottom-centre, so scaling keeps the bottom edge pinned to the anchor
        b.Root.style.top = p.y - h;
    }

    /// <summary>Top of what is actually drawn for this employee (hard hat included), lifted by LiftMeters; XZ from the employee.</summary>
    private Vector3 AnchorWorld(Banner b)
    {
        var t = b.Identity.transform;
        float top = t.position.y + 1.8f;
        bool any = false;
        foreach (var r in b.Renderers)
        {
            if (r == null || !r.enabled) continue;
            float y = r.bounds.max.y;
            if (!any || y > top) { top = y; any = true; }
        }
        return new Vector3(t.position.x, top + LiftMeters, t.position.z);
    }

    private void RefreshRenderers(Banner b)
    {
        b.NextRendererRefresh = Time.unscaledTime + RendererRefreshSeconds;
        b.Renderers.Clear();
        foreach (var r in b.Identity.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled || r is SpriteRenderer || r is LineRenderer || r is ParticleSystemRenderer) continue;
            if (r.name == "__EmployeeOutlineClone" || r.name.StartsWith("ContactShadow")) continue;
            b.Renderers.Add(r);
        }
    }

    private void RefreshTask(Banner b, bool force)
    {
        if (!force && Time.unscaledTime < b.NextTaskRefresh) return;
        b.NextTaskRefresh = Time.unscaledTime + TaskRefreshSeconds;
        string text = EmployeeTaskText.For(b.Identity);
        if (text == b.LastStatus) return;       // only touch the label when the words change
        b.LastStatus = text;
        if (b.Status != null) b.Status.text = text;
    }

    // ── The card ─────────────────────────────────────────────────────────────────
    private VisualElement HudRoot()
    {
        if (_infoUI == null) _infoUI = FindAnyObjectByType<EmployeeInfoUI>();
        var root = _infoUI != null ? _infoUI.HudRoot : null;
        if (root != null) return root;

        // The info card has not (or no longer) cached its panel - find the HUD document the same way it does: the one that holds "employee-info-panel".
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            var r = doc != null ? doc.rootVisualElement : null;
            var card = r?.Q<VisualElement>("employee-info-panel");
            if (card != null) return card.parent ?? r;
        }
        return null;
    }

    private VisualElement BuildCard(EmployeeIdentity id, Banner b)
    {
        var rec = id.Record;
        var root = new VisualElement { name = "employee-world-banner" };
        root.pickingMode = PickingMode.Position;           // the card swallows clicks, so a click on it never reaches the world
        root.style.position = Position.Absolute;
        root.style.width = BannerWidth;
        root.style.backgroundColor = ColBg;
        Border(root, 3f, ColBorder, 16f);
        root.style.paddingLeft = root.style.paddingRight = 12f;
        root.style.paddingTop = 12f; root.style.paddingBottom = 10f;
        root.style.flexDirection = FlexDirection.Column;
        root.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
        root.style.display = DisplayStyle.None;            // shown by the first PositionBanner

        // header: role icon + name / id
        var head = new VisualElement(); head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center; head.style.marginBottom = 10f;
        var icon = new VisualElement { name = "banner-job-icon" };
        icon.style.width = 48f; icon.style.height = 48f; icon.style.flexShrink = 0f; icon.style.marginRight = 10f;
        icon.style.backgroundColor = ColInk; Border(icon, 2f, ColBorder, 8f);
        icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        if (_infoUI == null) _infoUI = FindAnyObjectByType<EmployeeInfoUI>();
        var sprite = _infoUI != null ? _infoUI.GetRoleIcon(rec.role) : null;
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
        head.Add(icon);
        var names = new VisualElement(); names.style.flexDirection = FlexDirection.Column; names.style.flexGrow = 1f; names.style.flexShrink = 1f;
        names.Add(MakeLabel((rec.employeeName ?? "").ToUpper(), 17, ColTitle, TextAnchor.UpperLeft, wrap: false));
        names.Add(MakeLabel($"EMPLOYEE ID: {rec.employeeId}", 11, ColSub, TextAnchor.UpperLeft, wrap: false));
        head.Add(names);
        root.Add(head);

        // stats (read once: placeholder)
        root.Add(StatRow("FATIGUE", ColFatigue, rec.fatigue, "~{0:F0}%"));
        root.Add(StatRow("SAFETY", ColSafety, rec.safety, "~{0:F0}%"));
        root.Add(StatRow("MORALE", ColMorale, rec.morale, "~{0:F0}%"));
        root.Add(StatRow("SKILL", ColSkill, rec.skill, "{0:F0}%"));

        var lvl = MakeLabel($"LVL {rec.skillLevel}", 21, new Color(130 / 255f, 175 / 255f, 205 / 255f, 1f), TextAnchor.MiddleCenter, wrap: false);
        lvl.style.marginTop = 6f; root.Add(lvl);

        // the one LIVE line: what the work queue has them doing
        var status = MakeLabel("", 14, new Color(160 / 255f, 180 / 255f, 200 / 255f, 1f), TextAnchor.MiddleCenter, wrap: false);
        status.style.marginTop = 2f; status.style.overflow = Overflow.Hidden; status.style.textOverflow = TextOverflow.Ellipsis;
        root.Add(status);
        b.Status = status;

        // Double-click the banner to close it (no close button: clicking the player again closes it too).
        root.RegisterCallback<ClickEvent>(evt => { if (evt.clickCount >= 2) { Hide(id); evt.StopPropagation(); } });
        return root;
    }

    private VisualElement StatRow(string label, Color col, float pct, string format)
    {
        var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginBottom = 6f;
        var sq = new VisualElement(); sq.style.width = 18f; sq.style.height = 18f; sq.style.backgroundColor = col; sq.style.borderTopLeftRadius = sq.style.borderTopRightRadius = sq.style.borderBottomLeftRadius = sq.style.borderBottomRightRadius = 4f; sq.style.marginRight = 8f; sq.style.flexShrink = 0f;
        row.Add(sq);
        var l = MakeLabel(label, 12, col, TextAnchor.MiddleLeft, wrap: false); l.style.width = 62f; l.style.flexShrink = 0f; row.Add(l);
        var track = new VisualElement(); track.style.flexGrow = 1f; track.style.height = 12f; track.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f); track.style.overflow = Overflow.Hidden; track.style.marginRight = 6f;
        track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 6f;
        var fill = new VisualElement(); fill.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f)); fill.style.height = Length.Percent(100); fill.style.backgroundColor = col;
        fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 6f;
        track.Add(fill); row.Add(track);
        var v = MakeLabel(string.Format(format, pct), 12, col, TextAnchor.MiddleRight, wrap: false); v.style.width = 40f; v.style.flexShrink = 0f; row.Add(v);
        return row;
    }

    private static Label MakeLabel(string text, int size, Color col, TextAnchor align, bool wrap)
    {
        var l = new Label(text);
        ApplyLilita(l, size);
        l.style.color = col; l.style.unityTextAlign = align;
        l.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
        l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0f;
        l.pickingMode = PickingMode.Ignore;
        return l;
    }

    private static void Border(VisualElement e, float w, Color c, float radius)
    {
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
    }

    // Lilita One, the same face as the info card and the AOD.
    private static Font _lilita;
    private static void ApplyLilita(VisualElement el, int size)
    {
        if (_lilita == null)
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0) _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular");
#endif
        }
        if (_lilita != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_lilita));
        el.style.fontSize = size;
    }
}

/// <summary>
/// The words for the banner's live task line: what the work queue has assigned to this employee right now, falling back to their standing
/// assignment when the queue has nothing for them (patrolling, driving a machine between jobs, ...).
/// </summary>
public static class EmployeeTaskText
{
    public static string For(EmployeeIdentity id)
    {
        var rec = id != null ? id.Record : null;
        if (rec == null) return "";
        if (rec.status == EmploymentStatus.Suspended || rec.status == EmploymentStatus.Terminated) return "Being walked out";

        if (ServiceLocator.TryGet(out WorkQueueSystem queue) && queue != null && !string.IsNullOrEmpty(rec.employeeGuid))
        {
            foreach (var t in queue.GetAllTasks())
                if (t != null && t.Status == WorkTaskStatus.Assigned && t.AssignedToEmployeeGuid == rec.employeeGuid)
                    return Describe(t);
        }

        return rec.currentAssignment switch
        {
            EmployeeAssignment.ReceiveInbound   => "Receiving inbound",
            EmployeeAssignment.DriveDockstalker => "Operating DS",
            EmployeeAssignment.DriveReach       => "Operating RT",
            EmployeeAssignment.Patrol           => "Patrolling",
            EmployeeAssignment.OrderSelection   => "Selecting orders",
            _                                   => rec.currentAssignment.ToString()
        };
    }

    private static string Describe(WorkTask t)
    {
        string order = string.IsNullOrEmpty(t.OrderId) ? "" : " " + t.OrderId;
        switch (t.Type)
        {
            case WorkTaskType.Putaway:    return string.IsNullOrEmpty(t.ToLocation) ? "Putaway" : "Putaway to " + t.ToLocation;
            case WorkTaskType.Replenish:  return string.IsNullOrEmpty(t.ToLocation) ? "Replenishing" : "Replenishing " + t.ToLocation;
            case WorkTaskType.OrderSelect: return "Picking order" + order;
            case WorkTaskType.Load:       return "Loading" + (string.IsNullOrEmpty(order) ? " trailer" : order);
            case WorkTaskType.PalletPick: return "Pulling pallet" + order;
            case WorkTaskType.Receive:    return "Receiving pallet";
            default:                      return string.IsNullOrEmpty(t.Description) ? t.Type.ToString() : t.Description;
        }
    }
}
