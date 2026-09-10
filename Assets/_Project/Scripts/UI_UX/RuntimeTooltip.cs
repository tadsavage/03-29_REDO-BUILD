using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared floating hover tooltip for runtime UI Toolkit panels.
///
/// <see cref="VisualElement.tooltip"/> only ever renders through the Editor's own tooltip window
/// (it's wired into EditorWindow/IMGUI) — a runtime UIDocument has no built-in tooltip renderer at
/// all, so every `.tooltip = "..."` set on a game panel element has always silently done nothing in
/// Play Mode or a build. HiringBoardUI already worked around this once with a hand-built hover card;
/// this is the same idea made shared, so every panel gets one call instead of its own copy.
///
/// One host element per panel (per UIDocument root), created lazily on first use and reused for
/// every tooltip shown in that panel — there's only ever one tooltip visible at a time, so a pool of
/// one is enough.
/// </summary>
public static class RuntimeTooltip
{
    private static readonly Color ColBg     = new Color(0.07f, 0.10f, 0.14f, 0.97f);
    private static readonly Color ColBorder = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);

    private static readonly Dictionary<IPanel, VisualElement> _hosts = new();

    /// <summary>Currently-hovered target, so a delayed measure/position pass can bail if the pointer
    /// already moved on before layout caught up.</summary>
    private static VisualElement _activeTarget;

    /// <summary>Wires a floating hover card onto <paramref name="target"/>. Safe to call during panel
    /// construction, before the element is ever added to a document — nothing here touches
    /// <c>target.panel</c> until the pointer actually enters it.</summary>
    public static void Attach(VisualElement target, string text)
    {
        if (target == null || string.IsNullOrEmpty(text)) return;
        target.RegisterCallback<PointerEnterEvent>(evt => Show(target, () => BuildPlainText(text), (Vector2)evt.position));
        target.RegisterCallback<PointerLeaveEvent>(_ => Hide(target));
        // A target that vanishes mid-hover (a panel rebuild while the tooltip is up) gets no
        // PointerLeaveEvent — without this the card would be stuck on screen forever.
        target.RegisterCallback<DetachFromPanelEvent>(_ => Hide(target));
    }

    /// <summary>Same as <see cref="Attach"/>, but the tooltip's body is built fresh on every hover by
    /// <paramref name="buildContent"/> instead of being a single line of text — for a tooltip that
    /// needs structure (a header plus a list of icon+text rows), not just a paragraph.</summary>
    public static void AttachRich(VisualElement target, Func<VisualElement> buildContent)
    {
        if (target == null || buildContent == null) return;
        target.RegisterCallback<PointerEnterEvent>(evt => Show(target, buildContent, (Vector2)evt.position));
        target.RegisterCallback<PointerLeaveEvent>(_ => Hide(target));
        target.RegisterCallback<DetachFromPanelEvent>(_ => Hide(target));
    }

    private static VisualElement BuildPlainText(string text)
    {
        var label = new Label(text) { name = "runtime-tooltip-text" };
        label.pickingMode = PickingMode.Ignore;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.color = new StyleColor(ColText);
        label.style.fontSize = 13;
        return label;
    }

    private static VisualElement GetHost(IPanel panel)
    {
        if (_hosts.TryGetValue(panel, out VisualElement host) && host.panel == panel) return host;

        var body = new VisualElement { name = "runtime-tooltip-body" };
        body.pickingMode = PickingMode.Ignore;
        body.style.flexDirection = FlexDirection.Column;

        host = new VisualElement { name = "runtime-tooltip" };
        // Ignore, not just on the body — a tooltip that could itself be hovered would fight the
        // PointerLeave of whatever it's covering.
        host.pickingMode = PickingMode.Ignore;
        host.style.position = Position.Absolute;
        host.style.maxWidth = 340;
        host.style.paddingTop = 8; host.style.paddingBottom = 8;
        host.style.paddingLeft = 10; host.style.paddingRight = 10;
        host.style.backgroundColor = new StyleColor(ColBg);
        host.style.borderTopWidth = host.style.borderBottomWidth =
            host.style.borderLeftWidth = host.style.borderRightWidth = 1;
        host.style.borderTopColor = host.style.borderBottomColor =
            host.style.borderLeftColor = host.style.borderRightColor = new StyleColor(ColBorder);
        host.style.borderTopLeftRadius = host.style.borderTopRightRadius =
            host.style.borderBottomLeftRadius = host.style.borderBottomRightRadius = 8;
        // A soft drop shadow via a slightly-larger dark box would need a second element; a border
        // this width plus the existing shadow-less flat panels elsewhere in the game already gives
        // enough separation from whatever's behind it, so kept simple.
        // Faded rather than DisplayStyle.None while hidden: hiding via display would skip layout
        // entirely, so the very next Show would have no prior geometry to diff against.
        host.style.opacity = 0f;
        host.Add(body);
        host.userData = body;

        panel.visualTree.Add(host);
        _hosts[panel] = host;
        return host;
    }

    private static void Show(VisualElement target, Func<VisualElement> buildContent, Vector2 pointerPos)
    {
        IPanel panel = target.panel;
        if (panel == null) return;
        _activeTarget = target;

        VisualElement host = GetHost(panel);
        var body = (VisualElement)host.userData;
        body.Clear();
        body.Add(buildContent());
        host.style.left = pointerPos.x;
        host.style.top = pointerPos.y;

        // Deferred one frame so the new content has a real resolvedStyle.width/height to measure —
        // reading it in the same call would still show the PREVIOUS tooltip's size.
        host.schedule.Execute(() =>
        {
            if (_activeTarget != target || host.panel == null) return;

            VisualElement root = panel.visualTree;
            float w = host.resolvedStyle.width;
            float h = host.resolvedStyle.height;
            float maxLeft = Mathf.Max(0f, root.resolvedStyle.width - w - 6f);
            float maxTop = Mathf.Max(0f, root.resolvedStyle.height - h - 6f);
            // Anchored so the tooltip's BOTTOM-RIGHT corner sits at the pointer tip (a small gap short
            // of it) — up and to the left of the cursor, never under it, so the pointer never covers
            // its own tooltip and the tooltip never hides whatever the pointer is actually over.
            host.style.left = Mathf.Clamp(pointerPos.x - w - 10f, 0f, maxLeft);
            host.style.top = Mathf.Clamp(pointerPos.y - h - 10f, 0f, maxTop);
            host.style.opacity = 1f;
            RaiseAboveEverything(host);
        }).ExecuteLater(16);
    }

    private static void Hide(VisualElement target)
    {
        if (_activeTarget != target) return;
        _activeTarget = null;
        if (target.panel != null && _hosts.TryGetValue(target.panel, out VisualElement host))
            host.style.opacity = 0f;
    }

    /// <summary>Same technique as Toast.RaiseAboveEverything: sortingOrder only orders documents
    /// against EACH OTHER, and sibling order inside one document is decided by whoever called
    /// BringToFront last — so getting above every modal in THIS document means walking the whole
    /// ancestor chain, not just reordering the host's immediate siblings.</summary>
    private static void RaiseAboveEverything(VisualElement e)
    {
        while (e?.parent != null)
        {
            e.BringToFront();
            e = e.parent;
        }
    }
}
