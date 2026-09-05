using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Reputation status — triggered by the "Reputation" label in TopBarUI (replaced the old Headcount
/// tile/StaffingPanel). Shows the current score, standing (band), and how far to the next band.
/// ReputationService.Score is the one number that gates both which vendors will sell to the player
/// and which customer contracts are on offer — this is the single place in the HUD that reads it.
public class ReputationPanel : ITopBarPanel
{
    // 2x the shared FinanceUIKit row/header text — this panel alone reads noticeably larger than
    // the other TopBar dropdowns, by request, so sizes are hand-rolled here rather than through
    // DataRow/SectionHeader's shared (and un-parameterized) 13f/14f.
    const float KeySize = 26f;
    const float ValueSize = 26f;
    const float HeaderSize = 28f;
    const float RowH = 52f;
    const float HeaderH = 44f;

    readonly VisualElement _panel;
    readonly VisualElement _trigger; // the TopBar Reputation box — panel opens flush below its left edge
    Label _bandLabel;
    Label _scoreLabel;
    Label _nextBandLabel;
    bool _visible;

    public ReputationPanel(VisualElement root, VisualElement trigger)
    {
        _trigger = trigger;
        _panel = Build();
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);
        Refresh();
    }

    public bool IsVisible => _visible;
    public VisualElement Root => _panel;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        PositionUnderTrigger(_panel, _trigger);
        _panel.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        _visible = false;
        _panel.style.display = DisplayStyle.None;
    }

    /// <summary>Called by TopBarUI's own refresh whenever the score changes — only does work while
    /// open, same pattern as ShiftStatusPanel.RefreshIfVisible.</summary>
    public void RefreshIfVisible()
    {
        if (_visible) Refresh();
    }

    public void Dispose()
    {
        if (_panel.parent != null) _panel.RemoveFromHierarchy();
    }

    VisualElement Build()
    {
        var panel = Panel();
        panel.style.width = Width * 1.3f; // the doubled font needs more room than the 360px default

        var header = SectionHeader("Reputation", ColBlueDark, ColBlueTint);
        var headerLbl = header.Q<Label>();
        if (headerLbl != null) headerLbl.style.fontSize = HeaderSize;
        header.style.height = HeaderH;
        panel.Add(header);

        _bandLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(BigRow("Standing:", _bandLabel, ColRowA));

        _scoreLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(BigRow("Score:", _scoreLabel, ColRowB));

        _nextBandLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(BigRow("To Next Band:", _nextBandLabel, ColRowA));

        return panel;
    }

    /// <summary>Same look as FinanceUIKit.DataRow, just built locally so its key/value font sizes and
    /// row height can be doubled without changing the shared helper every other TopBar dropdown uses.</summary>
    static VisualElement BigRow(string key, Label val, Color bg)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = RowH;
        row.style.alignItems      = Align.Center;

        var keyLbl = Lbl(key, size: KeySize);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 10f;
        keyLbl.style.color       = new StyleColor(ColLabelNormal);

        val.style.fontSize        = ValueSize;
        val.style.width           = ValueWidth * 1.4f;
        val.style.unityTextAlign  = TextAnchor.MiddleRight;
        val.style.paddingRight    = 10f;
        val.style.backgroundColor = new StyleColor(ColValueBg);

        row.Add(keyLbl);
        row.Add(val);
        return row;
    }

    /// <summary>Fixed color per band name for the "To Next Band" line's colored span — a level name,
    /// not the current score, so it doesn't track ColorFor's live gradient. Known is "yellow with a
    /// hint of orange" per Tad's call-out; the bands above it step further toward green, matching
    /// the same read-at-a-glance progression as the Standing gradient.</summary>
    static Color BandNameColor(ReputationBand band) => band switch
    {
        ReputationBand.Known       => new Color(0.95f, 0.75f, 0.25f), // yellow, hint of orange
        ReputationBand.Respected   => new Color(0.80f, 0.85f, 0.30f), // yellow-green
        ReputationBand.Preferred   => new Color(0.55f, 0.85f, 0.35f), // green-leaning
        ReputationBand.Untouchable => new Color(0.35f, 0.85f, 0.45f), // full green
        _                          => Color.white,
    };

    void Refresh()
    {
        // Resolved lazily on every refresh, not cached — GameContext constructs TopBarUI before
        // ReputationService in some orderings (OrderArrivalService has the identical documented
        // caveat), and a field captured once at construction would read as permanently unavailable
        // if that ordering ever lands this way.
        if (!ServiceLocator.TryGet(out ReputationService reputation) || reputation == null)
        {
            _bandLabel.text = "—";
            _scoreLabel.text = "—";
            _nextBandLabel.text = "—";
            return;
        }

        int score = reputation.Score;
        var band = reputation.Band;

        // Standing reflects the player's current level, so it tracks the red->yellow->green gradient.
        _bandLabel.text = ReputationService.BandLabel(band);
        _bandLabel.style.color = new StyleColor(ReputationService.ColorFor(score));

        _scoreLabel.text = $"{score} / {ReputationService.MaxScore}";
        _scoreLabel.style.color = new StyleColor(Color.white);

        // "100 to Known" — the "100 to" descriptor is plain white; the band name itself is colored
        // by that band's own level color (rich-text span), since unlike Score this line names an
        // actual reputation level.
        int next = ReputationService.NextBandThreshold(score);
        _nextBandLabel.style.color = new StyleColor(Color.white);
        if (next < 0)
        {
            _nextBandLabel.text = "Max standing";
        }
        else
        {
            var targetBand = ReputationService.BandFor(next);
            string hex = ColorUtility.ToHtmlStringRGB(BandNameColor(targetBand));
            _nextBandLabel.text = $"{next - score} to <color=#{hex}>{ReputationService.BandLabel(targetBand)}</color>";
        }
    }
}
