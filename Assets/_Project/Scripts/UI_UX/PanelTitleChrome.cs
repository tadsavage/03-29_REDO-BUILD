using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The standard pair of window buttons in the upper-right corner of a panel's title bar:
/// a <b>resize/maximize</b> button on the LEFT and a <b>close</b> button on the RIGHT.
///
/// Extracted from ContractsPanel (key 8), which is the reference look, so that panels 1/2/4/5 get
/// the identical corner rather than four hand-copied near-misses that drift apart. Anything that
/// wants that corner should call <see cref="Attach"/> instead of building its own buttons.
///
/// The two buttons are deliberately built as a pair by one method: they must match each other in
/// size and sit flush on the same row, and every previous attempt at that was a pair of separate
/// blocks whose numbers slowly diverged.
///
/// Behaviour:
///  • LEFT  — cycles the panel between normal size and fill-screen via <see cref="ResizableWindow"/>,
///            swapping its glyph to match the current state (resize-handle when normal, the two
///            overlapping "restore" squares when maximized).
///  • RIGHT — invokes the caller's close action.
///
/// Both are plain <see cref="Button"/>s, so a title bar that implements drag-by-pointer must keep
/// ignoring events whose target is a Button (the reference panel already does: `if (evt.target is
/// Button) return;`). Without that, dragging the bar would swallow these clicks.
/// </summary>
public static class PanelTitleChrome
{
    /// <summary>Edge length of both buttons — 1.5x the 32px base square used elsewhere.</summary>
    public const float ButtonSize = 48f;

    /// <summary>Gap between the resize button and the close button.</summary>
    private const float ButtonGap = 6f;

    // Palette copied from ContractsPanel so the corner matches the house style without those
    // constants having to be made public on that class.
    private static readonly Color TitleText = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color BlueEdge  = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color FaceBg    = new Color(0.16f, 0.22f, 0.29f, 1f);
    private static readonly Color ScaleHot  = new Color(0.35f, 0.55f, 0.95f, 0.35f);
    private static readonly Color CloseHot  = new Color(0.80f, 0.30f, 0.20f, 1f);
    private static readonly Color ClosePress= new Color(0.60f, 0.16f, 0.12f, 1f);

    private static Font _lilita;

    /// <summary>
    /// Appends the resize and close buttons to <paramref name="titleBar"/> and wires them.
    /// </summary>
    /// <param name="titleBar">Row the buttons are appended to. Should be a flex row; the title label
    /// before them normally has flexGrow 1 so the pair is pushed into the corner.</param>
    /// <param name="resizer">Drives the left button. If null, the left button is not created at all —
    /// a maximize control that can't maximize is worse than no control.</param>
    /// <param name="onClose">Invoked by the right button.</param>
    /// <returns>The two buttons, so callers can keep a reference (e.g. to re-sync the glyph).</returns>
    public static (Button scale, Button close) Attach(VisualElement titleBar, ResizableWindow resizer,
                                                      System.Action onClose)
    {
        if (titleBar == null) return (null, null);

        Button scale = null;
        if (resizer != null)
        {
            scale = new Button { text = string.Empty, tooltip = "Resize window (normal / fill screen)" };
            StyleSquare(scale);
            scale.style.marginRight = ButtonGap;
            ResizableWindow.AddStackedSquaresGlyph(scale, ButtonSize, TitleText, isFilled: false);
            scale.RegisterCallback<PointerEnterEvent>(_ => scale.style.backgroundColor = new StyleColor(ScaleHot));
            scale.RegisterCallback<PointerLeaveEvent>(_ => scale.style.backgroundColor = new StyleColor(FaceBg));
            scale.clicked += () =>
            {
                resizer.CycleScale();
                resizer.UpdateScaleButtonIcon(scale, ButtonSize, TitleText);
            };
            titleBar.Add(scale);
        }

        var close = new Button(() => onClose?.Invoke()) { text = "✕", tooltip = "Close" };
        StyleSquare(close);
        ApplyFont(close, bold: true, size: 22);
        close.RegisterCallback<PointerEnterEvent>(_ => close.style.backgroundColor = new StyleColor(CloseHot));
        close.RegisterCallback<PointerLeaveEvent>(_ => close.style.backgroundColor = new StyleColor(FaceBg));
        close.RegisterCallback<PointerDownEvent>(_ => close.style.backgroundColor = new StyleColor(ClosePress));
        close.RegisterCallback<PointerUpEvent>(_ => close.style.backgroundColor = new StyleColor(CloseHot));
        titleBar.Add(close);

        return (scale, close);
    }

    /// <summary>
    /// Restyles a close button that already exists (from UXML or earlier code) and inserts a matching
    /// resize button immediately to its LEFT, in the same parent.
    ///
    /// This is the path for panels whose title bar is authored in UXML: it keeps the existing element
    /// — and therefore any USS hooks, tooltips or callbacks already attached to it — rather than
    /// deleting it and rebuilding the row.
    /// </summary>
    /// <param name="close">The panel's existing close button. Must already be parented.</param>
    /// <param name="resizer">Drives the new resize button; null skips creating one.</param>
    /// <param name="onClose">Wired to the close button's `clicked`. Pass null when the caller has
    /// ALREADY wired its own close handler — double-wiring would toggle the panel shut and open again
    /// on a single click.</param>
    public static (Button scale, Button close) Adopt(Button close, ResizableWindow resizer,
                                                     System.Action onClose = null)
    {
        if (close == null) return (null, null);

        // Inline styles beat the USS class the UXML applied, so the adopted button matches the
        // reference corner without having to edit each panel's stylesheet.
        StyleSquare(close);
        ApplyFont(close, bold: true, size: 22);
        if (string.IsNullOrEmpty(close.text)) close.text = "✕";
        if (string.IsNullOrEmpty(close.tooltip)) close.tooltip = "Close";
        close.RegisterCallback<PointerEnterEvent>(_ => close.style.backgroundColor = new StyleColor(CloseHot));
        close.RegisterCallback<PointerLeaveEvent>(_ => close.style.backgroundColor = new StyleColor(FaceBg));
        close.RegisterCallback<PointerDownEvent>(_ => close.style.backgroundColor = new StyleColor(ClosePress));
        close.RegisterCallback<PointerUpEvent>(_ => close.style.backgroundColor = new StyleColor(CloseHot));
        if (onClose != null) close.clicked += () => onClose();

        Button scale = null;
        var row = close.parent;
        if (resizer != null && row != null)
        {
            scale = new Button { text = string.Empty, tooltip = "Resize window (normal / fill screen)" };
            StyleSquare(scale);
            scale.style.marginRight = ButtonGap;
            ResizableWindow.AddStackedSquaresGlyph(scale, ButtonSize, TitleText, isFilled: false);
            scale.RegisterCallback<PointerEnterEvent>(_ => scale.style.backgroundColor = new StyleColor(ScaleHot));
            scale.RegisterCallback<PointerLeaveEvent>(_ => scale.style.backgroundColor = new StyleColor(FaceBg));
            scale.clicked += () =>
            {
                resizer.CycleScale();
                resizer.UpdateScaleButtonIcon(scale, ButtonSize, TitleText);
            };
            row.Insert(row.IndexOf(close), scale); // left of close, matching the reference corner
            Arrange(scale, close);
        }

        return (scale, close);
    }

    /// <summary>
    /// Decides — once, after the first layout, when resolvedStyle is finally meaningful — how to keep
    /// the two buttons flush, because host title bars come in two incompatible shapes:
    ///
    ///  • close is <c>position: absolute</c> (HiringBoard pins it top/right in USS). It's out of flow,
    ///    so a preceding sibling lands wherever the row ends — next to the centred title. The resize
    ///    button has to be pinned alongside it. See <see cref="FollowIfAbsolute"/>.
    ///  • close is in normal flow on a row using <c>justify-content: space-between</c> (EmployeeList).
    ///    Free space is distributed BETWEEN children, so a third child doesn't sit next to close — it
    ///    gets pushed to the far side (measured: a 315px gap). They have to become one child.
    ///    ToolsWindow's own USS already solves it this way with `.tools-titlebar-buttons`.
    /// </summary>
    private static void Arrange(Button scale, Button close)
    {
        void Once(GeometryChangedEvent _)
        {
            close.UnregisterCallback<GeometryChangedEvent>(Once); // decide once, never re-enter

            if (close.resolvedStyle.position == Position.Absolute) FollowIfAbsolute(scale, close);
            else GroupAsOneChild(scale, close);
        }

        close.RegisterCallback<GeometryChangedEvent>(Once);
    }

    /// <summary>Reparents the pair into a single flex-row child so the host row's justification treats
    /// them as one unit.</summary>
    private static void GroupAsOneChild(Button scale, Button close)
    {
        var row = close.parent;
        if (row == null) return;

        int index = row.IndexOf(scale);
        if (index < 0) index = row.IndexOf(close);
        if (index < 0) return;

        var group = new VisualElement { name = "panel-title-chrome" };
        group.style.flexDirection = FlexDirection.Row;
        group.style.alignItems = Align.Center;
        group.style.flexShrink = 0;

        row.Insert(index, group);
        scale.RemoveFromHierarchy();
        close.RemoveFromHierarchy();
        group.Add(scale);
        group.Add(close);
    }

    /// <summary>
    /// Keeps the resize button glued to the left of a close button that is <c>position: absolute</c>.
    ///
    /// A close button pinned by USS (HiringBoard's `.hb-close` is top:12/right:14, for instance) is out
    /// of normal flow, so inserting the resize button as its preceding sibling puts it wherever the
    /// flex row happens to end — next to the centred title, not in the corner. When that's the case the
    /// resize button has to be pinned too, one button-width further right.
    ///
    /// Measured after layout rather than read from style: the offsets live in USS, so resolvedStyle is
    /// NaN at the moment Adopt runs and the only honest source is the laid-out rect.
    /// </summary>
    private static void FollowIfAbsolute(Button scale, Button close)
    {
        bool seeded = false;

        void Place(GeometryChangedEvent _ = null)
        {
            if (close.resolvedStyle.position != Position.Absolute) return;

            var parent = close.parent;
            if (parent == null || !(parent.resolvedStyle.width > 0f)) return;
            if (!(close.layout.width > 0f)) return; // close hasn't been laid out yet

            scale.style.position = Position.Absolute;
            scale.style.marginRight = 0; // margin means nothing once absolutely positioned

            if (!seeded)
            {
                // First pass: a best guess from the laid-out rect. `resolvedStyle.top/right` are NOT
                // usable for this — measured on HiringBoard's close button, resolvedStyle.right reads
                // -14 for an element genuinely inset 14px, and a `top` of 14 lands the element at
                // y=16. Rather than encode either quirk, seed roughly and correct below.
                scale.style.top = close.layout.yMin;
                scale.style.right = (parent.resolvedStyle.width - close.layout.xMax) + ButtonSize + ButtonGap;
                seeded = true;
                return; // corrected on the relayout this triggers
            }

            // Self-correcting pass: nudge by however far off the last layout actually landed, so the
            // pair ends up flush regardless of the parent's box model. Converges in one step and the
            // 0.5px deadband stops it from ping-ponging relayouts forever.
            float dy = scale.layout.yMin - close.layout.yMin;
            float dx = scale.layout.xMax - (close.layout.xMin - ButtonGap);

            if (Mathf.Abs(dy) > 0.5f)
                scale.style.top = scale.style.top.value.value - dy;
            if (Mathf.Abs(dx) > 0.5f)
                scale.style.right = scale.style.right.value.value + dx;
        }

        // Watch BOTH: the close button moving (panel resized) and the scale button's own relayout,
        // which is what delivers the correction pass after the seed.
        close.RegisterCallback<GeometryChangedEvent>(evt => Place(evt));
        scale.RegisterCallback<GeometryChangedEvent>(evt => Place(evt));
        Place();
    }

    /// <summary>
    /// Re-syncs the resize button's glyph to the panel's current state. Call after
    /// <see cref="ResizableWindow.ResetToNormal"/> on show — otherwise a panel closed while maximized
    /// reopens at normal size still wearing the "restore" glyph, and the first click then maximizes
    /// when the icon says it should restore.
    /// </summary>
    public static void SyncScaleGlyph(Button scale, ResizableWindow resizer)
    {
        if (scale == null || resizer == null) return;
        resizer.UpdateScaleButtonIcon(scale, ButtonSize, TitleText);
    }

    /// <summary>Applies the shared square-button face. Mirrors ContractsPanel.StyleSquareButton at
    /// <see cref="ButtonSize"/> rather than its 32px base.</summary>
    private static void StyleSquare(Button b)
    {
        ApplyFont(b, bold: true, size: 15);
        b.style.width = ButtonSize;
        b.style.height = ButtonSize;
        b.style.flexShrink = 0;
        b.style.backgroundColor = new StyleColor(FaceBg);
        b.style.color = new StyleColor(TitleText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(BlueEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
        b.style.marginLeft = 0;
        b.style.marginRight = 0;
        b.style.paddingLeft = 0; b.style.paddingRight = 0;
        b.style.paddingTop = 0; b.style.paddingBottom = 0;
    }

    private static void ApplyFont(VisualElement el, bool bold = false, int size = -1)
    {
        if (_lilita == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0)
                _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _lilita = Resources.Load<Font>("LilitaOne-Regular");
#endif
        }
        if (_lilita != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_lilita));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (size > 0) el.style.fontSize = size;
    }
}
