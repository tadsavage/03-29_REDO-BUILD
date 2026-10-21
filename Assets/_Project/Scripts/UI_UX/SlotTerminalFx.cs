using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The little fake console that sits at the upper-left corner of the slotting window during Batch Auto Slot:
/// data scrolls by constantly, each slotted item bulges up in yellow ("Graham Crackers slotted in 01-03-00"),
/// and at the end a bigger yellow "All items slotted successfully!" holds 1.5 s before the window closes.
/// Real-time (UI Toolkit scheduler), so it ignores game speed and pause.
/// </summary>
public sealed class SlotTerminalFx
{
    private const float W = 480f, H = 240f;
    private const float Overlap = 56f;      // how far it tucks under the corner of the anchor window
    private const int TickMs = 30;
    private const int VisibleLines = 8;
    private const int FinalHoldMs = 1500;
    private static readonly Color Yellow = new Color(1f, 0.92f, 0.20f);
    private const string YellowHex = "#FFEB33";

    private readonly VisualElement _window, _announceHost;
    private readonly Label _spew;
    private readonly List<string> _lines = new();
    private readonly System.Random _rand = new();
    private IVisualElementScheduledItem _tick;
    private bool _closed;

    private SlotTerminalFx(VisualElement overlay, VisualElement anchor)
    {
        _window = new VisualElement { name = "slot-terminal", pickingMode = PickingMode.Ignore };
        _window.style.position = Position.Absolute;
        _window.style.width = W; _window.style.height = H;
        _window.style.backgroundColor = Color.black;
        _window.style.borderTopWidth = _window.style.borderBottomWidth =
            _window.style.borderLeftWidth = _window.style.borderRightWidth = 2;
        var edge = new Color(0.62f, 0.62f, 0.62f);
        _window.style.borderTopColor = _window.style.borderBottomColor =
            _window.style.borderLeftColor = _window.style.borderRightColor = edge;
        _window.style.overflow = Overflow.Hidden;

        // Dead centre of the screen.
        _window.style.left = Length.Percent(50);
        _window.style.top = Length.Percent(50);
        _window.style.marginLeft = -W / 2f;
        _window.style.marginTop = -H / 2f;

        var title = new VisualElement { pickingMode = PickingMode.Ignore };
        title.style.height = 20; title.style.flexShrink = 0;
        title.style.backgroundColor = new Color(0.78f, 0.78f, 0.78f);
        title.style.justifyContent = Justify.Center; title.style.paddingLeft = 6;
        var tl = new Label(@"C:\WINDOWS\system32\slotter.exe") { pickingMode = PickingMode.Ignore };
        tl.style.color = Color.black; tl.style.fontSize = 11;
        tl.style.unityFontStyleAndWeight = FontStyle.Bold;
        tl.style.marginTop = tl.style.marginBottom = tl.style.paddingTop = tl.style.paddingBottom = 0;
        title.Add(tl);
        _window.Add(title);

        var body = new VisualElement { pickingMode = PickingMode.Ignore };
        body.style.flexGrow = 1; body.style.justifyContent = Justify.FlexEnd;
        body.style.paddingLeft = 8; body.style.paddingRight = 8; body.style.paddingBottom = 5; body.style.paddingTop = 3;
        body.style.overflow = Overflow.Hidden;
        _window.Add(body);

        _spew = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
        _spew.style.color = Color.white; _spew.style.fontSize = 12;
        _spew.style.whiteSpace = WhiteSpace.NoWrap;
        _spew.style.unityTextAlign = TextAnchor.LowerLeft;
        _spew.style.marginTop = _spew.style.marginBottom = _spew.style.paddingTop = _spew.style.paddingBottom = 0;
        body.Add(_spew);

        // Where the bulging yellow announcements are drawn, centred over the stream.
        _announceHost = new VisualElement { pickingMode = PickingMode.Ignore };
        _announceHost.style.position = Position.Absolute;
        _announceHost.style.left = 0; _announceHost.style.right = 0; _announceHost.style.top = 20; _announceHost.style.bottom = 0;
        _announceHost.style.alignItems = Align.Center; _announceHost.style.justifyContent = Justify.Center;
        _window.Add(_announceHost);

        overlay.Add(_window);
        _window.BringToFront();

        _tick = _window.schedule.Execute(() => Push(ReleaseTerminalFx.MakeJibber(_rand))).Every(TickMs);
    }

    /// <summary>Opens the console beside <paramref name="anchor"/> (the slotting window), parented to <paramref name="overlay"/>.</summary>
    public static SlotTerminalFx Open(VisualElement overlay, VisualElement anchor) => new SlotTerminalFx(overlay, anchor);

    private void Push(string line)
    {
        _lines.Add(line);
        if (_lines.Count > VisibleLines) _lines.RemoveAt(0);
        _spew.text = string.Join("\n", _lines);
    }

    /// <summary>One item slotted: a yellow line in the stream plus a short yellow bulge-up pop.</summary>
    public void Announce(string text)
    {
        if (_closed) return;
        Push($"<color={YellowHex}>> {text}</color>");
        Pop(text, 15, 1.0f, 1.28f, 520);
    }

    /// <summary>Everything slotted: a bigger yellow message, held 1.5 s, then the window closes.</summary>
    public void Finish(string text, Action onClosed = null)
    {
        if (_closed) return;
        _tick.Pause();
        Push($"<color={YellowHex}>> {text}</color>");
        Pop(text, 22, 0.9f, 1.15f, 420, hold: FinalHoldMs);
        _window.schedule.Execute(() =>
        {
            Close();
            onClosed?.Invoke();
        }).StartingIn(420 + FinalHoldMs);
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _tick.Pause();
        _window.RemoveFromHierarchy();
    }

    private void Pop(string text, int fontSize, float fromScale, float peakScale, int ms, int hold = 0)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.color = Yellow; l.style.fontSize = fontSize;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.whiteSpace = WhiteSpace.Normal; l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.style.maxWidth = W - 24;
        l.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        l.style.paddingLeft = l.style.paddingRight = 8; l.style.paddingTop = l.style.paddingBottom = 3;
        l.style.opacity = 0f;
        l.style.scale = new Scale(new Vector3(fromScale, fromScale, 1f));
        _announceHost.Add(l);

        // Bulge up to the peak, settle, then (unless held) fade out.
        l.experimental.animation.Start(0f, 1f, ms, (e, t) =>
        {
            float up = t < 0.45f ? t / 0.45f : 1f;
            float s = t < 0.45f ? Mathf.Lerp(fromScale, peakScale, 1f - (1f - up) * (1f - up))
                                : Mathf.Lerp(peakScale, 1f, (t - 0.45f) / 0.55f);
            e.style.scale = new Scale(new Vector3(s, s, 1f));
            e.style.opacity = hold > 0 ? Mathf.Min(1f, t * 3f) : (t < 0.7f ? Mathf.Min(1f, t * 4f) : 1f - (t - 0.7f) / 0.3f);
        }).OnCompleted(() =>
        {
            if (hold > 0) { l.style.opacity = 1f; l.schedule.Execute(() => l.RemoveFromHierarchy()).StartingIn(hold); }
            else l.RemoveFromHierarchy();
        });
    }
}
