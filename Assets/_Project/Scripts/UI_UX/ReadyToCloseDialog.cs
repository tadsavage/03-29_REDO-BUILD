using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// "All product loaded" prompt. When a docked outbound trailer holds everything the building could load and the rest of its orders is
/// scratched (OrderService.OnReadyToCloseWithScratches), the game pauses, the camera zooms onto the trailer, the trailer's card is pinned
/// open, and a big banner asks: HOLD the trailer (wait for delayed inbound) or RELEASE it (cancel the scratched cases, ship what is aboard).
/// The penalty for releasing is written under the buttons and on both buttons' tooltips. Hold closes the prompt and changes nothing.
/// </summary>
public static class ReadyToCloseDialog
{
    static readonly Queue<OrderService.ReadyToCloseInfo> _queue = new();
    static VisualElement _card;
    static OrderService.ReadyToCloseInfo _current;
    static float _speedBefore = 1f;
    static Font _lilita;

    static readonly Color Navy = new Color(0.08f, 0.11f, 0.16f, 1f);
    static readonly Color Blue = new Color(0.36f, 0.61f, 0.77f, 1f);
    static readonly Color Ink = new Color(0.90f, 0.95f, 1f, 1f);
    static readonly Color Red = new Color(1f, 0.45f, 0.40f, 1f);

    static Font Lilita => _lilita != null ? _lilita : (_lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        // Domain reload is off: statics survive Play sessions, so drop any stale state and never subscribe twice.
        _queue.Clear(); _card = null; _current = null;
        OrderService.OnReadyToCloseWithScratches -= OnReady;
        OrderService.OnReadyToCloseWithScratches += OnReady;
    }

    static void OnReady(OrderService.ReadyToCloseInfo info)
    {
        if (info == null) return;
        _queue.Enqueue(info);
        if (_current == null) ShowNext();
    }

    static void ShowNext()
    {
        if (_queue.Count == 0) return;
        var info = _queue.Dequeue();
        if (info.Truck == null) { ShowNext(); return; }
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) { ShowNext(); return; }

        _current = info;
        var top = Object.FindAnyObjectByType<TopBarUI>();
        _speedBefore = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        if (top != null) top.SetGameSpeed(0f); else Time.timeScale = 0f;

        // Show the trailer: zoom onto it and open its card.
        var cam = Object.FindAnyObjectByType<FreeLookCamera>();
        if (cam != null) cam.ZoomOnto(info.Truck.transform.position + Vector3.up * 1.5f, 28f);
        Object.FindAnyObjectByType<WorldHoverPopupUI>()?.PinTruck(info.Truck);
        AudioManager.Play("NegotiationFail");

        Build(layer, info);
    }

    static Label L(float size, Color col, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.unityTextAlign = align; l.style.whiteSpace = WhiteSpace.Normal;
        l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        if (Lilita != null) l.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        return l;
    }

    static string PenaltyText(OrderService.ReadyToCloseInfo i)
    {
        string rep = i.ReputationLoss > 0 ? $"-{i.ReputationLoss} reputation" : null;
        string par = i.PartnershipLoss >= 0.5f ? $"-{Mathf.RoundToInt(i.PartnershipLoss)} partnership with {i.Customers}" : null;
        string both = rep != null && par != null ? rep + " and " + par : rep ?? par ?? "no penalty";
        return $"There will be a penalty of {both}.";
    }

    static Button Btn(string text, Color fill, Color edge, System.Action onClick, string tip)
    {
        var b = new Button(onClick) { text = text };
        b.style.width = 200; b.style.height = 56; b.style.marginLeft = b.style.marginRight = 10;
        b.style.fontSize = 24; b.style.color = Color.white;
        if (Lilita != null) b.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        b.style.backgroundColor = fill;
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 3;
        b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = edge;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        RuntimeTooltip.Attach(b, tip, ignoreGlobalToggle: true);
        return b;
    }

    static void Build(VisualElement layer, OrderService.ReadyToCloseInfo info)
    {
        _card?.RemoveFromHierarchy();
        _card = new VisualElement { name = "ReadyToCloseDialog", pickingMode = PickingMode.Position };
        _card.style.position = Position.Absolute;
        _card.style.left = new Length(50, LengthUnit.Percent);
        _card.style.top = 90;
        _card.style.width = 640;
        _card.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        _card.style.backgroundColor = Navy;
        _card.style.paddingLeft = _card.style.paddingRight = 24; _card.style.paddingTop = 18; _card.style.paddingBottom = 20;
        _card.style.borderTopWidth = _card.style.borderBottomWidth = _card.style.borderLeftWidth = _card.style.borderRightWidth = 4;
        _card.style.borderTopColor = _card.style.borderBottomColor = _card.style.borderLeftColor = _card.style.borderRightColor = Blue;
        _card.style.borderTopLeftRadius = _card.style.borderTopRightRadius = _card.style.borderBottomLeftRadius = _card.style.borderBottomRightRadius = 14;

        var title = L(30, new Color(1f, 0.85f, 0.25f)); title.text = $"DOOR {info.Door}: ALL PRODUCT LOADED";
        _card.Add(title);

        string orders = string.Join(", ", info.Orders.ConvertAll(o => o.OrderNumber ?? o.OrderId));
        var body = L(18, Ink);
        body.text = $"Everything the warehouse had is on the trailer. {info.CasesShort} case(s) are scratched for {info.Customers} (order {orders}).\n" +
                    "Release the trailer and close the order, or hold it for delayed inbound?";
        body.style.marginTop = 10;
        _card.Add(body);

        string penalty = PenaltyText(info);
        string releaseTip = "Release: the scratched cases are cancelled and the customer is not billed for them. " + penalty;
        string holdTip = "Hold: nothing changes and the clock keeps running while you wait for the missing product. Releasing later costs the same. " + penalty;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center; row.style.marginTop = 16;
        row.Add(Btn("HOLD", new Color(0.78f, 0.45f, 0.18f), new Color(0.5f, 0.28f, 0.1f), () => Finish(false), holdTip));
        row.Add(Btn("RELEASE", new Color(0.20f, 0.62f, 0.28f), new Color(0.10f, 0.38f, 0.15f), () => Finish(true), releaseTip));
        _card.Add(row);

        var pen = L(17, Red); pen.text = penalty; pen.style.marginTop = 12;
        _card.Add(pen);

        layer.Add(_card);
        _card.BringToFront();
    }

    static void Finish(bool release)
    {
        var info = _current;
        _current = null;
        _card?.RemoveFromHierarchy(); _card = null;

        // Resume first, so the release (and its fanfare) plays at the player's own speed.
        var top = Object.FindAnyObjectByType<TopBarUI>();
        if (top != null) top.SetGameSpeed(_speedBefore); else Time.timeScale = _speedBefore;

        if (release && info != null && ServiceLocator.TryGet(out OrderService orders) && orders != null)
        {
            if (!orders.ReleaseScratchedTrailer(info.Door))
                UIToast.Show("That trailer can't be released right now - something changed.", 3f);
        }
        ShowNext();
    }
}
