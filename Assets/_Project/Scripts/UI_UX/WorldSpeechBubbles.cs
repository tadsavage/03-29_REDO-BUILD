using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Small text speech bubbles that float over a person or machine in the world (Tad, 2026-10-18): "There's no available
/// equipment", "I guess I can do it...". Lilita One, white bubble with a tail, re-projected from world to HUD space every
/// frame (the same technique as <see cref="EquipmentBatteryPanels"/> / <see cref="EmployeeWorldBanners"/>), and sent to the
/// back of the HUD so panels draw over it. One bubble per anchor; showing another replaces the first.
/// </summary>
public class WorldSpeechBubbles : MonoBehaviour
{
    private const float LiftMeters = 0.45f;
    private const float RefDistance = 15f;
    private const float MaxScale = 0.9f;
    private const float MinScale = 0.5f;
    private const float DefaultSeconds = 3.5f;

    private class Bubble
    {
        public Transform Anchor;
        public VisualElement Root;
        public float ExpiresAt;
    }

    private static WorldSpeechBubbles _instance;
    public static WorldSpeechBubbles Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<WorldSpeechBubbles>();
                if (_instance == null) _instance = new GameObject("WorldSpeechBubbles").AddComponent<WorldSpeechBubbles>();
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; _lilita = null; }

    private readonly List<Bubble> _bubbles = new();
    private EmployeeInfoUI _infoUI;
    private Camera _cam;

    /// <summary>Pops a bubble over <paramref name="anchor"/> (a person or machine root). Uses unscaled time so it also reads while paused.</summary>
    public static void Show(Transform anchor, string text, float seconds = DefaultSeconds)
    {
        if (anchor == null || string.IsNullOrEmpty(text)) return;
        Instance.Add(anchor, text, seconds);
    }

    private void Add(Transform anchor, string text, float seconds)
    {
        var host = HudRoot();
        if (host == null) return;

        // One bubble per anchor.
        for (int i = _bubbles.Count - 1; i >= 0; i--)
            if (_bubbles[i].Anchor == anchor) { _bubbles[i].Root?.RemoveFromHierarchy(); _bubbles.RemoveAt(i); }

        var root = new VisualElement { pickingMode = PickingMode.Ignore };
        root.style.position = Position.Absolute;
        root.style.alignItems = Align.Center;
        root.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(50), Length.Percent(100), 0f));
        root.style.display = DisplayStyle.None;   // shown by the first Place()

        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.backgroundColor = Color.white;
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 14;
        box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 2;
        box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = new Color(0.14f, 0.19f, 0.24f, 1f);
        box.style.paddingLeft = box.style.paddingRight = 14;
        box.style.paddingTop = 7; box.style.paddingBottom = 8;
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.style.color = new Color(0.14f, 0.19f, 0.24f, 1f);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
        ApplyLilita(label, 22);
        box.Add(label);
        root.Add(box);

        // Tail: a small white square turned 45 degrees, tucked under the box.
        var tail = new VisualElement { pickingMode = PickingMode.Ignore };
        tail.style.width = 14; tail.style.height = 14;
        tail.style.backgroundColor = Color.white;
        tail.style.marginTop = -9;
        tail.style.rotate = new StyleRotate(new Rotate(45f));
        tail.style.borderRightWidth = tail.style.borderBottomWidth = 2;
        tail.style.borderRightColor = tail.style.borderBottomColor = new Color(0.14f, 0.19f, 0.24f, 1f);
        root.Add(tail);

        host.Add(root);
        root.SendToBack();
        var b = new Bubble { Anchor = anchor, Root = root, ExpiresAt = Time.unscaledTime + seconds };
        _bubbles.Add(b);
        Place(b);
    }

    private void Update()
    {
        for (int i = _bubbles.Count - 1; i >= 0; i--)
        {
            var b = _bubbles[i];
            if (b.Anchor == null || Time.unscaledTime >= b.ExpiresAt)
            {
                b.Root?.RemoveFromHierarchy();
                _bubbles.RemoveAt(i);
                continue;
            }
            Place(b);
        }
    }

    private void OnDestroy()
    {
        foreach (var b in _bubbles) b.Root?.RemoveFromHierarchy();
        _bubbles.Clear();
        if (_instance == this) _instance = null;
    }

    private void Place(Bubble b)
    {
        if (_cam == null) _cam = Camera.main;
        var panel = b.Root.panel;
        if (_cam == null || panel == null) return;

        // Over the top of whatever is drawn for this anchor (hard hat, mast...), not its pivot.
        float top = b.Anchor.position.y + 1.8f;
        foreach (var r in b.Anchor.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r.name.StartsWith("ContactShadow")) continue;
            top = Mathf.Max(top, r.bounds.max.y);
        }
        Vector3 world = new Vector3(b.Anchor.position.x, top + LiftMeters, b.Anchor.position.z);

        var vp = _cam.WorldToViewportPoint(world);
        bool visible = vp.z > 0.2f && vp.x > -0.15f && vp.x < 1.15f && vp.y > -0.3f && vp.y < 1.3f;
        b.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;

        var p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, _cam);
        float s = Mathf.Clamp(RefDistance / Mathf.Max(vp.z, 0.01f), MinScale, MaxScale);
        float w = b.Root.resolvedStyle.width; if (float.IsNaN(w) || w < 1f) w = 260f;
        float h = b.Root.resolvedStyle.height; if (float.IsNaN(h) || h < 1f) h = 60f;
        b.Root.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
        b.Root.style.left = p.x - w * 0.5f;
        b.Root.style.top = p.y - h;
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
