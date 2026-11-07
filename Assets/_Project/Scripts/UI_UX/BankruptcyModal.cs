using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using GameCore.Diagnostics;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// OUT OF CASH (Tad, 2026-11-06): cash on hand should never be negative. The moment it drops below zero the game pauses and asks:
///   TAKE LOAN         - opens the Bank window (taking a loan that lifts cash back above zero closes this prompt and resumes play).
///   CONTINUE PLAYING  - dismisses the prompt. It will not come back until cash has been zero or better and then falls below zero again.
///   I'M BANKRUPT      - shows the lifetime recap (days, income, expenses, debt, reputation), then GAME OVER, then the Main Menu.
/// Watches MoneyService from a tiny scene-scoped driver instead of the OnBankrupt event: the event fires at "&lt;= 0", only on the way
/// down, and MoneyService does not exist yet when scenes finish loading. Programmatic UI Toolkit, on the shared fanfare layer.
/// Not shown while HAL 9000 is driving (an unattended bot must not be stopped by a modal); a human player's observed block still gets it.
/// </summary>
public class BankruptcyModal : MonoBehaviour
{
    static BankruptcyModal _instance;

    VisualElement _card;
    bool _prompted;        // this "episode" (cash below zero) has already been shown to the player
    float _speedBefore = 1f;
    float _nextPoll;
    static Font _lilita;

    static readonly Color Navy = new Color(0.08f, 0.11f, 0.16f, 0.98f);
    static readonly Color Ink = new Color(0.90f, 0.95f, 1f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.70f, 0.78f, 1f);
    static readonly Color Red = new Color(1f, 0.45f, 0.40f, 1f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.25f, 1f);
    static readonly Color Green = new Color(0.20f, 0.68f, 0.38f, 1f);
    static readonly Color Orange = new Color(0.78f, 0.45f, 0.18f, 1f);
    static readonly Color Blue = new Color(0.36f, 0.61f, 0.77f, 1f);

    static Font Lilita => _lilita != null ? _lilita : (_lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // Domain reload is off: a stale (destroyed) instance reads as null, so a fresh scene always gets exactly one watcher.
        if (_instance != null) return;
        var go = new GameObject("BankruptcyModal");
        _instance = go.AddComponent<BankruptcyModal>();
    }

    void Update()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + 0.4f;

        if (SceneManager.GetActiveScene().name == "MainMenu") return;
        if (!ServiceLocator.TryGet<MoneyService>(out var money) || money == null) return;

        // A closed episode re-arms as soon as cash is back to zero or better.
        if (money.CurrentCapital >= 0)
        {
            _prompted = false;
            if (_card != null && _card.name == "OutOfCashCard") CloseCard(resume: true);   // a loan (or a sale) fixed it while the prompt was up
            return;
        }

        if (_prompted || _card != null) return;
        var bot = PlaytestBot.Instance;
        if (bot != null && bot.Running && !bot.ObserveOnly) return;   // HAL is playing: never stop it with a modal
        ShowOutOfCash(money);
    }

    // ── cards ──────────────────────────────────────────────────────────────

    void ShowOutOfCash(MoneyService money)
    {
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        _prompted = true;
        Pause();
        if (AudioManager.Has("NegotiationFail")) AudioManager.Play("NegotiationFail");

        var card = NewCard(layer, "OutOfCashCard", Red);
        card.Add(Title("OUT OF CASH", Red));
        var body = Text(20, Ink);
        body.text = $"Cash on hand is -${Mathf.Abs(money.CurrentCapital):N0}.\nThe bank may lend you more, you can carry on and try to dig out, or you can call it.";
        body.style.marginTop = 10;
        card.Add(body);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center; row.style.marginTop = 18; row.style.flexWrap = Wrap.Wrap;
        row.Add(Btn("TAKE LOAN", Green, () => BankWindow.Open(_card)));
        row.Add(Btn("CONTINUE PLAYING", Orange, () => CloseCard(resume: true)));
        row.Add(Btn("I'M BANKRUPT", Red, ShowRecap));
        card.Add(row);
    }

    void ShowRecap()
    {
        BankWindow.Close();
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        CloseCard(resume: false);   // stay paused

        var card = NewCard(layer, "BankruptRecapCard", Gold);
        card.Add(Title("THE BOOKS ARE CLOSED", Gold));
        var sub = Text(16, Muted); sub.text = "Your warehouse, start to finish."; sub.style.marginBottom = 10; card.Add(sub);

        int days = ServiceLocator.TryGet<SimulationTimeService>(out var time) && time != null ? time.Day : 0;
        int income = 0, expenses = 0;
        if (ServiceLocator.TryGet<MoneyService>(out var money) && money != null)
        {
            income = money.LifetimeIncome.Values.Sum();
            money.LifetimeExpenses.TryGetValue("Loan Principal", out int principal);
            expenses = money.LifetimeExpenses.Values.Sum() - principal;   // debt repaid is not a cost of doing business
        }
        int owed = ServiceLocator.TryGet<LoanService>(out var loans) && loans != null ? loans.TotalDebt : 0;
        string rep = ServiceLocator.TryGet<ReputationService>(out var rs) && rs != null
            ? $"{rs.Score}  ({ReputationService.BandLabel(rs.Band)})" : "n/a";
        int cash = money != null ? money.CurrentCapital : 0;

        card.Add(StatRow("Days in business", days.ToString(), Ink));
        card.Add(StatRow("Lifetime income", "$" + income.ToString("N0"), Green));
        card.Add(StatRow("Lifetime expenses", "$" + expenses.ToString("N0"), Red));
        card.Add(StatRow("Net", (income - expenses >= 0 ? "+$" : "-$") + Mathf.Abs(income - expenses).ToString("N0"), income - expenses >= 0 ? Green : Red));
        card.Add(StatRow("Owed to the bank", "$" + owed.ToString("N0"), owed > 0 ? Red : Ink));
        card.Add(StatRow("Cash on hand", (cash < 0 ? "-$" : "$") + Mathf.Abs(cash).ToString("N0"), cash < 0 ? Red : Ink));
        card.Add(StatRow("Reputation", rep, Ink));

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center; row.style.marginTop = 18;
        row.Add(Btn("CONTINUE", Orange, ShowGameOver));
        card.Add(row);
    }

    void ShowGameOver()
    {
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) { GoToMainMenu(); return; }
        CloseCard(resume: false);

        var card = NewCard(layer, "GameOverCard", Red);
        var big = Title("GAME OVER", Red); big.style.fontSize = 64; big.style.height = 80; card.Add(big);
        var body = Text(20, Ink); body.text = "The bank has taken the building."; body.style.marginTop = 6; card.Add(body);
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center; row.style.marginTop = 20;
        row.Add(Btn("MAIN MENU", Blue, GoToMainMenu));
        card.Add(row);
    }

    static void GoToMainMenu()
    {
        Time.timeScale = 1f;   // the menu must not inherit the pause
        MainMenuManager.SkipIntro = true;
        SceneManager.LoadScene("MainMenu");
    }

    // ── pause / resume ─────────────────────────────────────────────────────

    void Pause()
    {
        var top = FindAnyObjectByType<TopBarUI>();
        _speedBefore = Time.timeScale > 0.01f ? Time.timeScale : _speedBefore;
        if (top != null) top.SetGameSpeed(0f); else Time.timeScale = 0f;
    }

    void CloseCard(bool resume)
    {
        _card?.RemoveFromHierarchy(); _card = null;
        if (!resume) return;
        var top = FindAnyObjectByType<TopBarUI>();
        if (top != null) top.SetGameSpeed(_speedBefore); else Time.timeScale = _speedBefore;
    }

    // ── small UI helpers ───────────────────────────────────────────────────

    VisualElement NewCard(VisualElement layer, string name, Color edge)
    {
        _card?.RemoveFromHierarchy();
        _card = new VisualElement { name = name, pickingMode = PickingMode.Position };
        _card.style.position = Position.Absolute;
        _card.style.left = new Length(50, LengthUnit.Percent);
        _card.style.top = new Length(26, LengthUnit.Percent);
        _card.style.width = 640;
        _card.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        _card.style.backgroundColor = Navy;
        _card.style.paddingLeft = _card.style.paddingRight = 26; _card.style.paddingTop = 18; _card.style.paddingBottom = 22;
        _card.style.alignItems = Align.Center;
        _card.style.borderTopWidth = _card.style.borderBottomWidth = _card.style.borderLeftWidth = _card.style.borderRightWidth = 4;
        _card.style.borderTopColor = _card.style.borderBottomColor = _card.style.borderLeftColor = _card.style.borderRightColor = edge;
        _card.style.borderTopLeftRadius = _card.style.borderTopRightRadius = _card.style.borderBottomLeftRadius = _card.style.borderBottomRightRadius = 14;
        layer.Add(_card);
        _card.BringToFront();
        return _card;
    }

    static Label Text(float size, Color col)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.whiteSpace = WhiteSpace.Normal;
        l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        if (Lilita != null) l.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        return l;
    }

    static Label Title(string text, Color col)
    {
        var l = Text(36, col); l.text = text; l.style.height = 46; l.style.flexShrink = 0;
        return l;
    }

    static VisualElement StatRow(string label, string value, Color valueColor)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.width = 460; row.style.flexShrink = 0; row.style.height = 30;
        row.style.alignItems = Align.Center; row.style.justifyContent = Justify.SpaceBetween;
        var a = Text(20, Muted); a.text = label; a.style.unityTextAlign = TextAnchor.MiddleLeft;
        var b = Text(22, valueColor); b.text = value; b.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(a); row.Add(b);
        return row;
    }

    static Button Btn(string text, Color fill, System.Action onClick)
    {
        var b = new Button(onClick) { text = text };
        b.style.minWidth = 190; b.style.height = 54; b.style.marginLeft = b.style.marginRight = 8; b.style.marginTop = 4;
        b.style.paddingLeft = b.style.paddingRight = 12;
        b.style.fontSize = 22; b.style.color = Color.white;
        if (Lilita != null) b.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        b.style.backgroundColor = fill;
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 3;
        b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new Color(fill.r * 0.55f, fill.g * 0.55f, fill.b * 0.55f, 1f);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        return b;
    }
}
