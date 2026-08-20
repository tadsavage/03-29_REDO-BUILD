using GameCore.Inventory;
using GameCore.Services;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Reputation status — triggered by the "Reputation" label in TopBarUI (replaced the old Headcount
/// tile/StaffingPanel). Shows the current score, standing (band), and how far to the next band.
/// ReputationService.Score is the one number that gates both which vendors will sell to the player
/// and which customer contracts are on offer — this is the single place in the HUD that reads it.
public class ReputationPanel : ITopBarPanel
{
    readonly VisualElement _panel;
    Label _bandLabel;
    Label _scoreLabel;
    Label _nextBandLabel;
    bool _visible;

    public ReputationPanel(VisualElement root)
    {
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
        panel.Add(SectionHeader("Reputation", ColBlueDark, ColBlueTint));

        _bandLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(DataRow("Standing", _bandLabel, ColRowA, false));

        _scoreLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(DataRow("Score", _scoreLabel, ColRowB, false));

        _nextBandLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(DataRow("To Next Band", _nextBandLabel, ColRowA, false));

        return panel;
    }

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
        _bandLabel.text = ReputationService.BandLabel(band);
        _scoreLabel.text = $"{score} / {ReputationService.MaxScore}";

        int next = ReputationService.NextBandThreshold(score);
        _nextBandLabel.text = next < 0
            ? "Max standing"
            : $"{next - score} to {ReputationService.BandLabel(ReputationService.BandFor(next))}";
    }
}
