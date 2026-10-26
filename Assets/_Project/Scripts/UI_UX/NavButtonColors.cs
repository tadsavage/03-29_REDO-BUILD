using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One colour per destination for the cross-panel navigation buttons (WORK Q, PURCHASING, ORDERS),
/// so a button looks the same on every panel it appears on. SCHEDULER keeps its original orange.
/// Call <see cref="Apply"/> AFTER a button's own styling: it registers its own hover handlers last,
/// so its hover colour wins over the orange ones the panel helpers register.
/// </summary>
public static class NavButtonColors
{
    public enum Dest { WorkQueue, Purchasing, Orders, RushOrders, ItemListing, Scheduler }

    public static void Apply(Button b, Dest dest)
    {
        Color fill, hover, edge, text;
        switch (dest)
        {
            case Dest.WorkQueue:
                fill = new Color(0.78f, 0.20f, 0.20f); hover = new Color(0.90f, 0.30f, 0.30f);
                edge = new Color(0.50f, 0.10f, 0.10f); text = Color.white; break;
            case Dest.Purchasing:
                fill = new Color(0.22f, 0.48f, 0.74f); hover = new Color(0.32f, 0.60f, 0.88f);
                edge = new Color(0.12f, 0.28f, 0.48f); text = Color.white; break;
            case Dest.Scheduler: // the original nav orange
                fill = new Color(0.78f, 0.45f, 0.18f); hover = new Color(0.90f, 0.56f, 0.26f);
                edge = new Color(0.50f, 0.28f, 0.10f); text = Color.white; break;
            case Dest.ItemListing:
            case Dest.RushOrders: // baby blue, white text
                fill = new Color(0.45f, 0.74f, 0.94f); hover = new Color(0.58f, 0.82f, 0.98f);
                edge = new Color(0.25f, 0.50f, 0.72f); text = Color.white; break;
            default: // Orders: green with white text (was yellow/black - it sat next to two orange buttons)
                fill = new Color(0.20f, 0.62f, 0.28f); hover = new Color(0.28f, 0.74f, 0.37f);
                edge = new Color(0.10f, 0.38f, 0.15f); text = Color.white; break;
        }

        b.style.color = new StyleColor(text);
        b.style.backgroundColor = new StyleColor(fill);
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(edge);
        // MouseEnter/MouseLeave, not Pointer*: the panel helpers (StyleOrangeButton) repaint the fill orange on MouseLeaveEvent, which
        // is dispatched AFTER the pointer event and so used to win, leaving only the border in the nav colour. Same event type,
        // registered later, runs later and wins.
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(fill));
    }
}
