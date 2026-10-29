using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Services;
using GameCore.Inventory;

/// <summary>
/// "Release trailer" confirmation, opened from the button on an outbound truck's card. States the exact fine and the
/// reputation / partnership hit (OrderService.PreviewReleaseTrailer) before OrderService.ReleaseTrailer is allowed to run.
/// </summary>
public static class ReleaseTrailerDialog
{
    static VisualElement _card;
    static Font _lilita;
    static float _speedBefore = 1f;

    static Font Lilita => _lilita != null ? _lilita : (_lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular"));

    static Label L(float size, Color col)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.whiteSpace = WhiteSpace.Normal;
        l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        if (Lilita != null) l.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        return l;
    }

    static Button Btn(string text, Color fill, Color edge, System.Action onClick)
    {
        var b = new Button(onClick) { text = text };
        b.style.width = 200; b.style.height = 52; b.style.marginLeft = b.style.marginRight = 10;
        b.style.fontSize = 24; b.style.color = Color.white;
        if (Lilita != null) b.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        b.style.backgroundColor = fill;
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 3;
        b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = edge;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        return b;
    }

    public static void Show(TruckController truck)
    {
        if (truck == null || _card != null) return;
        if (!ServiceLocator.TryGet(out OrderService orders) || orders == null) return;
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;

        var impact = orders.PreviewReleaseTrailer(truck);
        if (!impact.Allowed)
        {
            UIToast.Show($"This trailer can't be released: {impact.WhyNot}.", 3.5f);
            return;
        }

        var top = Object.FindAnyObjectByType<TopBarUI>();
        _speedBefore = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        if (top != null) top.SetGameSpeed(0f); else Time.timeScale = 0f;

        _card = new VisualElement { name = "ReleaseTrailerDialog", pickingMode = PickingMode.Position };
        _card.style.position = Position.Absolute;
        _card.style.left = new Length(50, LengthUnit.Percent);
        _card.style.top = 120;
        _card.style.width = 560;
        _card.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        _card.style.backgroundColor = new Color(0.08f, 0.11f, 0.16f, 1f);
        _card.style.paddingLeft = _card.style.paddingRight = 24; _card.style.paddingTop = 18; _card.style.paddingBottom = 20;
        _card.style.borderTopWidth = _card.style.borderBottomWidth = _card.style.borderLeftWidth = _card.style.borderRightWidth = 4;
        _card.style.borderTopColor = _card.style.borderBottomColor = _card.style.borderLeftColor = _card.style.borderRightColor = new Color(0.82f, 0.14f, 0.14f, 1f);
        _card.style.borderTopLeftRadius = _card.style.borderTopRightRadius = _card.style.borderBottomLeftRadius = _card.style.borderBottomRightRadius = 14;

        var title = L(32, new Color(1f, 0.85f, 0.25f)); title.text = "Are you sure?";
        _card.Add(title);

        string rep = impact.ReputationLoss > 0 ? $"-{impact.ReputationLoss} to your reputation" : null;
        string par = impact.PartnershipLoss >= 0.5f ? $"-{Mathf.RoundToInt(impact.PartnershipLoss)} to partnership" : null;
        string hits = rep != null && par != null ? $"take a {par} and a {rep}" : rep != null ? $"take a {rep}" : par != null ? $"take a {par}" : null;
        string body = "Fines and penalties will occur.\n" +
                      (impact.Fine > 0 ? $"You will be fined ${impact.Fine:N0}" : "There is no fine") +
                      (hits != null ? $", {hits}." : ".");
        if (impact.CasesShort > 0) body += $"\n{impact.CasesShort} case(s) not yet loaded will be cancelled and go back to stock.";
        var text = L(20, new Color(0.90f, 0.95f, 1f)); text.text = body; text.style.marginTop = 12;
        _card.Add(text);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center; row.style.marginTop = 18;
        row.Add(Btn("CANCEL", new Color(0.35f, 0.40f, 0.48f), new Color(0.2f, 0.24f, 0.3f), () => Close()));
        row.Add(Btn("RELEASE", new Color(0.82f, 0.14f, 0.14f), new Color(0.45f, 0.05f, 0.05f), () =>
        {
            Close();
            if (truck != null && !orders.ReleaseTrailer(truck, out _))
                UIToast.Show("That trailer can't be released right now - something changed.", 3f);
        }));
        _card.Add(row);

        layer.Add(_card);
        _card.BringToFront();
    }

    static void Close()
    {
        _card?.RemoveFromHierarchy(); _card = null;
        var top = Object.FindAnyObjectByType<TopBarUI>();
        if (top != null) top.SetGameSpeed(_speedBefore); else Time.timeScale = _speedBefore;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _card = null; }
}
