using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// World-space battery read-outs for MHE (Tad, 2026-10-17), modelled on his reference image: a small glassy card
/// floating over the machine with BATTERY bars + %, hours remaining (or hours to full while charging), a status
/// line and a fill bar.
///
///   • Click a machine -> its card appears; click it again -> it goes away.
///   • A machine on a charger shows its card by itself while it charges (and for a few seconds once full).
///   • A red lightning bolt floats over any machine within one game hour of a flat battery.
///
/// Same technique as EmployeeWorldBanners: UI Toolkit elements in the HUD document, re-projected from world to
/// panel space every frame and sent to the back so the top bar and modal panels draw over them.
/// </summary>
public class EquipmentBatteryPanels : MonoBehaviour
{
    private const float CardWidth = 236f;
    private const float LiftMeters = 0.35f;
    private const float RefDistance = 15f;
    private const float MaxScale = 0.85f;
    private const float MinScale = 0.45f;
    private const float FullLingerSeconds = 4f;
    private const float BoltSize = 70f;

    private static readonly Color ColGlass   = new Color(0.86f, 0.90f, 0.93f, 0.80f);
    private static readonly Color ColEdge    = new Color(1f, 1f, 1f, 0.90f);
    private static readonly Color ColInk     = new Color(0.14f, 0.19f, 0.24f, 1f);
    private static readonly Color ColSub     = new Color(0.30f, 0.37f, 0.44f, 1f);
    private static readonly Color ColTrack   = new Color(0.62f, 0.68f, 0.73f, 0.55f);
    private static readonly Color ColGood    = new Color(0.27f, 0.70f, 0.38f, 1f);
    private static readonly Color ColMid     = new Color(0.93f, 0.68f, 0.18f, 1f);
    private static readonly Color ColLow     = new Color(0.86f, 0.22f, 0.20f, 1f);
    private static readonly Color ColCharge  = new Color(0.25f, 0.62f, 0.86f, 1f);
    private static readonly Color ColBolt    = new Color(0.90f, 0.13f, 0.12f, 1f);

    private class Card
    {
        public EquipmentBattery Battery;
        public VisualElement Root, Fill, HeadBolt;
        public Label Title, Station, Percent, Hours, HoursCaption, Status;
        public readonly VisualElement[] Bars = new VisualElement[4];
        public bool Clicked;
        public float FullAt = -1f;
    }

    private static EquipmentBatteryPanels _instance;
    public static EquipmentBatteryPanels Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EquipmentBatteryPanels>();
                if (_instance == null) _instance = new GameObject("EquipmentBatteryPanels").AddComponent<EquipmentBatteryPanels>();
            }
            return _instance;
        }
    }
    public static void Ensure() { var _ = Instance; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; _lilita = null; }

    private readonly Dictionary<EquipmentBattery, Card> _cards = new();
    private readonly Dictionary<EquipmentBattery, VisualElement> _bolts = new();
    private readonly List<EquipmentBattery> _scratch = new();
    private EmployeeInfoUI _infoUI;
    private Camera _cam;

    public void Toggle(EquipmentBattery b)
    {
        if (b == null) return;
        if (_cards.TryGetValue(b, out var c) && c.Clicked) { c.Clicked = false; if (!WantsAutoCard(c)) Remove(b); }
        else { var card = GetOrCreate(b); if (card != null) card.Clicked = true; }
        AudioManager.Play("UIClick");
    }

    private void OnDestroy()
    {
        foreach (var c in _cards.Values) c.Root?.RemoveFromHierarchy();
        foreach (var v in _bolts.Values) v?.RemoveFromHierarchy();
        _cards.Clear(); _bolts.Clear();
        if (_instance == this) _instance = null;
    }

    // ── Right-click menu: "Charge Battery" (Tad, 2026-10-17) ─────────────────────
    private VisualElement _menu;
    private Vector2 _rmbDownPos;
    private float _rmbDownTime = -10f;

    /// <summary>A quick right-click (not a right-drag, which turns the camera) on a machine opens its menu.</summary>
    private void HandleRightClick()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        // Close the menu on any left click outside it, or Escape.
        if (_menu != null && (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                              (mouse.leftButton.wasPressedThisFrame && !UIInputGuard.IsPointerOverUIToolkit())))
            CloseMenu();

        if (mouse.rightButton.wasPressedThisFrame) { _rmbDownPos = mouse.position.ReadValue(); _rmbDownTime = Time.unscaledTime; }
        if (!mouse.rightButton.wasReleasedThisFrame) return;
        if (Time.unscaledTime - _rmbDownTime > 0.4f || (mouse.position.ReadValue() - _rmbDownPos).magnitude > 8f) return;
        if (UIInputGuard.IsPointerOverUIToolkit()) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        if (!Physics.Raycast(_cam.ScreenPointToRay(mouse.position.ReadValue()), out var hit)) { CloseMenu(); return; }
        var b = hit.collider.GetComponentInParent<EquipmentBattery>();
        if (b == null) { CloseMenu(); return; }
        OpenMenu(b, mouse.position.ReadValue());
    }

    private void OpenMenu(EquipmentBattery b, Vector2 screen)
    {
        CloseMenu();
        var host = HudRoot();
        if (host == null || host.panel == null) return;

        _menu = new VisualElement();
        _menu.style.position = UnityEngine.UIElements.Position.Absolute;
        _menu.style.width = 190;
        _menu.style.backgroundColor = new Color(0.07f, 0.10f, 0.14f, 0.97f);
        _menu.style.borderTopWidth = _menu.style.borderBottomWidth = _menu.style.borderLeftWidth = _menu.style.borderRightWidth = 2;
        _menu.style.borderTopColor = _menu.style.borderBottomColor = _menu.style.borderLeftColor = _menu.style.borderRightColor = new Color(0.36f, 0.61f, 0.77f, 1f);
        _menu.style.borderTopLeftRadius = _menu.style.borderTopRightRadius = _menu.style.borderBottomLeftRadius = _menu.style.borderBottomRightRadius = 6;
        _menu.style.paddingLeft = _menu.style.paddingRight = 8;
        _menu.style.paddingTop = _menu.style.paddingBottom = 6;

        var title = Text($"{b.KindName}  {Mathf.FloorToInt(b.Charge01 * 100f)}%", 13, new Color(0.81f, 0.89f, 0.94f, 1f));
        title.style.marginBottom = 5;
        _menu.Add(title);

        bool can = b.CanRequestCharge;
        var btn = new Button(() => { b.RequestCharge(); AudioManager.Play("UIClick"); UIToast.Show($"{b.KindName.Substring(0, 1)}{b.KindName.Substring(1).ToLowerInvariant()} is driving to a charging station."); CloseMenu(); })
        { text = "Charge Battery" };
        ApplyLilita(btn, 15);
        btn.style.height = 32;
        btn.style.marginLeft = btn.style.marginRight = 0;
        btn.style.backgroundColor = can ? new Color(0.71f, 0.45f, 0.23f, 1f) : new Color(0.25f, 0.28f, 0.32f, 1f);
        btn.style.color = can ? Color.white : new Color(0.6f, 0.63f, 0.66f, 1f);
        btn.SetEnabled(can);
        _menu.Add(btn);

        if (!can)
        {
            string why = b.OnChargeTrip || b.NeedsCharge ? "Already going to charge."
                       : b.IsFull ? "Battery is already full."
                       : b.Slot != null && b.Slot.IsOccupied ? "Someone is driving it."
                       : "Busy with a job.";
            var note = Text(why, 11, new Color(0.6f, 0.68f, 0.75f, 1f));
            note.style.marginTop = 4;
            note.style.whiteSpace = WhiteSpace.Normal;
            _menu.Add(note);
        }

        host.Add(_menu);
        _menu.BringToFront();
        Vector2 p = RuntimePanelUtils.ScreenToPanel(host.panel, new Vector2(screen.x, Screen.height - screen.y));
        _menu.style.left = p.x + 6;
        _menu.style.top = p.y + 6;
    }

    private void CloseMenu()
    {
        _menu?.RemoveFromHierarchy();
        _menu = null;
    }

    private void Update()
    {
        HandleRightClick();

        // Cards: clicked ones, plus every machine charging on a station.
        foreach (var b in EquipmentBattery.All)
        {
            if (b == null) continue;
            if (b.IsCharging && !_cards.ContainsKey(b)) GetOrCreate(b);
            SetBolt(b, b.IsLow);
        }

        _scratch.Clear();
        _scratch.AddRange(_cards.Keys);
        foreach (var b in _scratch)
        {
            var c = _cards[b];
            if (b == null || !b.isActiveAndEnabled) { Remove(b); continue; }
            if (!c.Clicked && !WantsAutoCard(c)) { Remove(b); continue; }
            Refresh(c);
            Position(c.Root, Anchor(b), CardWidth, 150f);
        }

        _scratch.Clear();
        _scratch.AddRange(_bolts.Keys);
        foreach (var b in _scratch)
        {
            var v = _bolts[b];
            if (b == null || !b.isActiveAndEnabled) { v?.RemoveFromHierarchy(); _bolts.Remove(b); continue; }
            // While the card is up the bolt shows in the card's header instead (above it, it went off-screen).
            if (_cards.ContainsKey(b)) { v.style.display = DisplayStyle.None; continue; }
            Position(v, Anchor(b), BoltSize, BoltSize);
            v.style.opacity = 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));   // pulse to catch the eye
        }
    }

    /// <summary>A charging card stays up while charging and for a few seconds after reaching full.</summary>
    private bool WantsAutoCard(Card c)
    {
        var b = c.Battery;
        if (b.IsCharging) { c.FullAt = -1f; return true; }
        if (b.Station != null && b.IsFull)
        {
            if (c.FullAt < 0f) c.FullAt = Time.unscaledTime;
            return Time.unscaledTime - c.FullAt < FullLingerSeconds;
        }
        return false;
    }

    private void Remove(EquipmentBattery b)
    {
        if (b != null && _cards.TryGetValue(b, out var c)) { c.Root?.RemoveFromHierarchy(); _cards.Remove(b); }
        else
        {
            // Destroyed machine: drop any card whose key is now null.
            var dead = new List<EquipmentBattery>();
            foreach (var kv in _cards) if (kv.Key == null) { kv.Value.Root?.RemoveFromHierarchy(); dead.Add(kv.Key); }
            foreach (var k in dead) _cards.Remove(k);
        }
    }

    private void SetBolt(EquipmentBattery b, bool show)
    {
        bool has = _bolts.TryGetValue(b, out var v);
        if (show && !has)
        {
            var host = HudRoot();
            if (host == null) return;
            v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = UnityEngine.UIElements.Position.Absolute;
            v.style.width = BoltSize; v.style.height = BoltSize;
            v.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(50), Length.Percent(100), 0f));
            v.generateVisualContent += DrawBolt;
            host.Add(v);
            v.SendToBack();
            _bolts[b] = v;
        }
        else if (!show && has)
        {
            v?.RemoveFromHierarchy();
            _bolts.Remove(b);
        }
    }

    /// <summary>The lightning bolt from Tad's icon (Lightning.svg, 16x16 view box), filled solid red with a dark rim.</summary>
    private static void DrawBolt(MeshGenerationContext ctx)
    {
        var r = ctx.visualElement.contentRect;
        float s = Mathf.Min(r.width, r.height) / 16f;
        Vector2 P(float x, float y) => new Vector2(x * s, y * s);
        var p = ctx.painter2D;
        p.lineJoin = LineJoin.Round;
        p.BeginPath();
        p.MoveTo(P(10.3f, 0.6f));
        p.LineTo(P(2.6f, 8.5f));
        p.LineTo(P(6.4f, 10.2f));
        p.LineTo(P(5.6f, 15.4f));
        p.LineTo(P(13.4f, 7.5f));
        p.LineTo(P(9.6f, 5.8f));
        p.ClosePath();
        p.fillColor = ColBolt;
        p.Fill();
        p.strokeColor = new Color(0.35f, 0.04f, 0.04f, 1f);
        p.lineWidth = 1.6f;
        p.Stroke();
    }

    private Vector3 Anchor(EquipmentBattery b)
    {
        float top = b.transform.position.y + 1.5f;
        foreach (var r in b.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r.name.StartsWith("ContactShadow")) continue;
            if (r.GetComponentInParent<EmployeeIdentity>() != null) continue;   // not the rider's hard hat
            top = Mathf.Max(top, r.bounds.max.y);
        }
        Vector3 c = b.Slot != null && b.Slot.IsPalletJack ? PalletJackParkingSpot.JackBodyCentre(b.transform) : b.transform.position;
        return new Vector3(c.x, top + LiftMeters, c.z);
    }

    /// <summary>Projects a bottom-centre anchored element onto the HUD, scaled with distance.</summary>
    private void Position(VisualElement el, Vector3 world, float width, float fallbackHeight)
    {
        if (_cam == null) _cam = Camera.main;
        var panel = el.panel;
        if (_cam == null || panel == null) return;
        var vp = _cam.WorldToViewportPoint(world);
        bool visible = vp.z > 0.2f && vp.x > -0.15f && vp.x < 1.15f && vp.y > -0.3f && vp.y < 1.3f;
        el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;
        var p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, _cam);
        float s = Mathf.Clamp(RefDistance / Mathf.Max(vp.z, 0.01f), MinScale, MaxScale);
        float h = el.resolvedStyle.height; if (float.IsNaN(h) || h < 1f) h = fallbackHeight;
        el.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
        el.style.left = p.x - width * 0.5f;
        el.style.top = p.y - h;
    }

    // ── The card ─────────────────────────────────────────────────────────────────
    private Card GetOrCreate(EquipmentBattery b)
    {
        if (_cards.TryGetValue(b, out var existing)) return existing;
        var host = HudRoot();
        if (host == null) return null;
        var c = new Card { Battery = b };
        c.Root = BuildCard(c);
        host.Add(c.Root);
        c.Root.SendToBack();
        _cards[b] = c;
        Refresh(c);
        Position(c.Root, Anchor(b), CardWidth, 150f);
        return c;
    }

    private VisualElement BuildCard(Card c)
    {
        var root = new VisualElement { pickingMode = PickingMode.Ignore };
        root.style.position = UnityEngine.UIElements.Position.Absolute;
        root.style.width = CardWidth;
        root.style.backgroundColor = ColGlass;
        root.style.borderTopLeftRadius = root.style.borderTopRightRadius = 10;
        root.style.borderBottomLeftRadius = root.style.borderBottomRightRadius = 10;
        root.style.borderTopWidth = root.style.borderBottomWidth = root.style.borderLeftWidth = root.style.borderRightWidth = 2;
        root.style.borderTopColor = root.style.borderBottomColor = root.style.borderLeftColor = root.style.borderRightColor = ColEdge;
        root.style.paddingLeft = root.style.paddingRight = 10;
        root.style.paddingTop = 7; root.style.paddingBottom = 8;
        root.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(50), Length.Percent(100), 0f));

        // Header: machine kind + station letter.
        var head = Row(); head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 4;
        var titleRow = Row(); titleRow.style.alignItems = Align.Center;
        c.HeadBolt = new VisualElement { pickingMode = PickingMode.Ignore };
        c.HeadBolt.style.width = 20; c.HeadBolt.style.height = 20; c.HeadBolt.style.marginRight = 3;
        c.HeadBolt.generateVisualContent += DrawBolt;
        titleRow.Add(c.HeadBolt);
        c.Title = Text("", 15, ColInk); titleRow.Add(c.Title);
        head.Add(titleRow);
        c.Station = Text("", 12, ColSub); head.Add(c.Station);
        root.Add(head);

        // Body: battery bars + % on the left, hours on the right, split by a thin divider.
        var body = Row(); body.style.alignItems = Align.FlexEnd;
        var left = new VisualElement { pickingMode = PickingMode.Ignore }; left.style.width = 104;
        left.Add(Text("BATTERY", 12, ColSub));
        var bars = Row(); bars.style.height = 30; bars.style.alignItems = Align.FlexEnd; bars.style.marginTop = 2; bars.style.marginBottom = 2;
        for (int i = 0; i < 4; i++)
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.width = 14; bar.style.height = 12 + i * 6; bar.style.marginRight = 4;
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = 2;
            bar.style.backgroundColor = ColTrack;
            c.Bars[i] = bar; bars.Add(bar);
        }
        left.Add(bars);
        c.Percent = Text("", 24, ColInk); left.Add(c.Percent);
        body.Add(left);

        var div = new VisualElement { pickingMode = PickingMode.Ignore };
        div.style.width = 1; div.style.height = 62; div.style.backgroundColor = ColTrack; div.style.marginRight = 10;
        body.Add(div);

        var right = new VisualElement { pickingMode = PickingMode.Ignore }; right.style.flexGrow = 1;
        c.Hours = Text("", 26, ColInk); right.Add(c.Hours);
        c.HoursCaption = Text("", 11, ColSub); c.HoursCaption.style.whiteSpace = WhiteSpace.Normal; right.Add(c.HoursCaption);
        body.Add(right);
        root.Add(body);

        // Status line + fill bar.
        c.Status = Text("", 12, ColInk); c.Status.style.marginTop = 6; root.Add(c.Status);
        var track = new VisualElement { pickingMode = PickingMode.Ignore };
        track.style.height = 9; track.style.marginTop = 3; track.style.backgroundColor = ColTrack;
        track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 4;
        track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 4;
        track.style.overflow = Overflow.Hidden;
        c.Fill = new VisualElement { pickingMode = PickingMode.Ignore };
        c.Fill.style.height = Length.Percent(100);
        c.Fill.style.borderTopLeftRadius = c.Fill.style.borderBottomLeftRadius = 4;
        track.Add(c.Fill);
        root.Add(track);
        return root;
    }

    private static void Refresh(Card c)
    {
        var b = c.Battery;
        float pct = b.Charge01 * 100f;
        Color level = b.HoursLeft <= EquipmentBattery.LowHours ? ColLow : (b.Charge01 < 0.5f ? ColMid : ColGood);

        c.Title.text = b.KindName;
        c.HeadBolt.style.display = b.IsLow || b.IsDead ? DisplayStyle.Flex : DisplayStyle.None;
        string letter = b.Station != null ? b.Station.GetComponent<MHEStationLetter>()?.Letter : null;
        c.Station.text = b.Station != null ? (string.IsNullOrEmpty(letter) ? "ON CHARGER" : $"STATION {letter}") : "";
        c.Percent.text = $"{Mathf.FloorToInt(pct + 0.0001f)}%";

        int lit = Mathf.Clamp(Mathf.CeilToInt(b.Charge01 * 4f - 0.0001f), 0, 4);
        for (int i = 0; i < 4; i++) c.Bars[i].style.backgroundColor = i < lit ? level : ColTrack;

        Color fill;
        if (b.IsCharging)
        {
            c.Hours.text = FormatHours(b.HoursToFullCharge);
            c.HoursCaption.text = "HRS TO FULL";
            c.Status.text = "Charging" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
            fill = Color.Lerp(ColCharge, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)));
        }
        else
        {
            c.Hours.text = FormatHours(b.HoursLeft);
            c.HoursCaption.text = "HRS REMAINING";
            c.Status.text = b.DriverlessRun ? "Finishing its job - no driver"
                          : b.WaitingForCharger ? "Waiting for a free charger"
                          : b.OnChargeTrip ? "Driving to a charger"
                          : b.IsDead ? "DEAD - needs a charger"
                          : b.Station != null && b.IsFull ? "Fully charged"
                          : b.IsLow ? "LOW - send to a charger"
                          : b.IsMoving ? "In use" : "Parked";
            fill = level;
        }
        c.Status.style.color = b.IsLow || b.IsDead ? ColLow : ColInk;
        c.Fill.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f));
        c.Fill.style.backgroundColor = fill;
    }

    private static string FormatHours(float h) => h >= 9.95f ? h.ToString("0") : h.ToString("0.0");

    private static VisualElement Row()
    {
        var r = new VisualElement { pickingMode = PickingMode.Ignore };
        r.style.flexDirection = FlexDirection.Row;
        return r;
    }

    private static Label Text(string s, int size, Color color)
    {
        var l = new Label(s) { pickingMode = PickingMode.Ignore };
        l.style.color = color;
        l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
        l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
        ApplyLilita(l, size);
        return l;
    }

    private VisualElement HudRoot()
    {
        if (_infoUI == null) _infoUI = FindAnyObjectByType<EmployeeInfoUI>();
        var root = _infoUI != null ? _infoUI.HudRoot : null;
        if (root != null) return root;
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            var r = doc != null ? doc.rootVisualElement : null;
            var card = r?.Q<VisualElement>("employee-info-panel");
            if (card != null) return card.parent ?? r;
        }
        return null;
    }

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
