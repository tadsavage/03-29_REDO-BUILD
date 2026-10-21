using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The "releasing orders to the floor" flourish: a small fake CMD window that spews jibber-jabber for a few
/// seconds, then swells to twice its size and stamps ORDER RELEASED in yellow with the order's numbers, holds
/// there, and closes. Pure UI Toolkit, on the panel's real-time clock (schedule / experimental.animation),
/// so it is unaffected by game speed or pause.
/// </summary>
public static class ReleaseTerminalFx
{
    private const float WindowWidth = 540f;
    private const float WindowHeight = 250f;
    private const int SpewMs = 3000;
    private const int TickMs = 35;
    private const int GrowMs = 350;
    private const int HoldMs = 1500;
    private const int VisibleLines = 11;

    private static readonly Color Yellow = new Color(1f, 0.92f, 0.20f);

    private static readonly string[] Jibber =
    {
        "C:\\fork\\release> sync_pick_faces --all --force ........ OK",
        "resolving reserve slot 01-0{n}-A0 ........ done",
        "allocating pallet pick PLT-{h4} ........ ACK",
        "handshake with reach truck unit {n} ........ ACK",
        "compiling case-pick manifest ........ {p}%",
        "0x{h8} 0x{h8} 0x{h8} 0x{h8}",
        "WARN: forklift horn not found, continuing anyway",
        "reticulating pallet splines ........ done",
        "checking lane clearance 1{c} ........ clear",
        "negotiating with gravity ........ agreed",
        "locking location 02-0{n}-B1 ........ LOCKED",
        "pinging {h4}.{h4}.dock.local ........ 1ms",
        "calibrating shrink wrap ........ tight",
        "uploading task graph to the floor ........ {p}%",
        "verifying ti x hi ........ sufficient",
        "feeding the order selectors ........ fed",
        "ERR 0x{h4}: retrying ........ recovered",
        "dispatching PLT-{h4} -> STAGE {n} ........ sent",
        "defragmenting aisle {n} ........ ok",
    };

    /// <summary>Plays the sequence over <paramref name="root"/>. <paramref name="detail"/> is the second
    /// yellow line; <paramref name="onDone"/> runs once the window has closed.</summary>
    public static void Play(VisualElement root, string headline, string detail, Action onDone)
    {
        if (root == null) { onDone?.Invoke(); return; }

        var window = new VisualElement { name = "release-terminal" };
        window.style.position = Position.Absolute;
        window.style.left = Length.Percent(50);
        window.style.top = Length.Percent(50);
        window.style.width = WindowWidth;
        window.style.height = WindowHeight;
        window.style.marginLeft = -WindowWidth / 2f;
        window.style.marginTop = -WindowHeight / 2f;
        window.style.backgroundColor = Color.black;
        window.style.borderTopWidth = window.style.borderBottomWidth =
            window.style.borderLeftWidth = window.style.borderRightWidth = 2;
        var edge = new Color(0.62f, 0.62f, 0.62f);
        window.style.borderTopColor = window.style.borderBottomColor =
            window.style.borderLeftColor = window.style.borderRightColor = edge;
        window.style.overflow = Overflow.Hidden;
        window.style.scale = new Scale(Vector3.one);

        var titleBar = new VisualElement();
        titleBar.style.height = 22;
        titleBar.style.flexShrink = 0;
        titleBar.style.backgroundColor = new Color(0.78f, 0.78f, 0.78f);
        titleBar.style.justifyContent = Justify.Center;
        titleBar.style.paddingLeft = 6;
        var titleText = new Label("C:\\WINDOWS\\system32\\cmd.exe");
        titleText.style.color = Color.black;
        titleText.style.fontSize = 12;
        titleText.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleText.style.paddingTop = 0; titleText.style.paddingBottom = 0;
        titleText.style.marginTop = 0; titleText.style.marginBottom = 0;
        titleBar.Add(titleText);
        window.Add(titleBar);

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexDirection = FlexDirection.Column;
        body.style.justifyContent = Justify.FlexEnd; // newest line hugs the bottom, like a real console
        body.style.paddingLeft = 8; body.style.paddingRight = 8; body.style.paddingBottom = 6; body.style.paddingTop = 4;
        body.style.overflow = Overflow.Hidden;
        window.Add(body);

        var spew = new Label();
        spew.style.color = Color.white;
        spew.style.fontSize = 13;
        spew.style.whiteSpace = WhiteSpace.NoWrap;
        spew.style.unityTextAlign = TextAnchor.LowerLeft;
        spew.style.paddingTop = 0; spew.style.paddingBottom = 0;
        spew.style.marginTop = 0; spew.style.marginBottom = 0;
        body.Add(spew);

        root.Add(window);
        window.BringToFront();

        var rand = new System.Random();
        var lines = new List<string>();
        bool spewing = true;

        var tick = window.schedule.Execute(() =>
        {
            if (!spewing) return;
            lines.Add(MakeJibber(rand));
            if (lines.Count > VisibleLines) lines.RemoveAt(0);
            spew.text = string.Join("\n", lines);
        }).Every(TickMs);

        window.schedule.Execute(() =>
        {
            spewing = false;
            tick.Pause();

            // Stamp the result, then swell the whole window to twice its size.
            var line1 = new Label(headline);
            line1.style.color = Yellow;
            line1.style.fontSize = 22;
            line1.style.unityFontStyleAndWeight = FontStyle.Bold;
            line1.style.marginTop = 6;
            line1.style.paddingTop = 0; line1.style.paddingBottom = 0;
            body.Add(line1);

            var line2 = new Label(detail);
            line2.style.color = Yellow;
            line2.style.fontSize = 12;
            line2.style.whiteSpace = WhiteSpace.NoWrap;
            line2.style.paddingTop = 0; line2.style.paddingBottom = 0;
            body.Add(line2);

            window.experimental.animation.Start(0f, 1f, GrowMs, (e, t) =>
            {
                float s = Mathf.Lerp(1f, 2f, 1f - (1f - t) * (1f - t)); // ease-out
                e.style.scale = new Scale(new Vector3(s, s, 1f));
            });

            window.schedule.Execute(() =>
            {
                window.RemoveFromHierarchy();
                onDone?.Invoke();
            }).ExecuteLater(GrowMs + HoldMs);
        }).ExecuteLater(SpewMs);
    }

    internal static string MakeJibber(System.Random r)
    {
        string s = Jibber[r.Next(Jibber.Length)];
        var sb = new StringBuilder(s);
        sb.Replace("{n}", r.Next(1, 9).ToString());
        sb.Replace("{c}", ((char)('A' + r.Next(0, 3))).ToString());
        sb.Replace("{p}", r.Next(1, 100).ToString());
        while (true)
        {
            string cur = sb.ToString();
            int i8 = cur.IndexOf("{h8}", StringComparison.Ordinal);
            if (i8 >= 0) { sb.Remove(i8, 4).Insert(i8, r.Next().ToString("X8")); continue; }
            int i4 = cur.IndexOf("{h4}", StringComparison.Ordinal);
            if (i4 >= 0) { sb.Remove(i4, 4).Insert(i4, r.Next(0, 0xFFFF).ToString("X4")); continue; }
            break;
        }
        return sb.ToString();
    }
}
