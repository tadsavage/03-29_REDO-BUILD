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
    public enum Dest { WorkQueue, Purchasing, Orders }

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
            default: // Orders
                fill = new Color(0.95f, 0.78f, 0.20f); hover = new Color(1.00f, 0.88f, 0.38f);
                edge = new Color(0.62f, 0.48f, 0.08f); text = new Color(0.12f, 0.09f, 0.03f); break;
        }

        b.style.color = new StyleColor(text);
        b.style.backgroundColor = new StyleColor(fill);
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(edge);
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(fill));
    }
}
