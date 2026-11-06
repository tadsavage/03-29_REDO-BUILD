using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// THE BANK (Tad, 2026-11-05), opened from the Capital panel's BANK button. Styled like the negotiation window: dark navy
/// card, blue/orange accents, Lilita headings. Two sections:
///   YOUR LOANS   - each loan's balance, rate, weekly payment and weeks left, with Pay $5k / Pay Off / Refinance.
///   LOAN OFFERS  - Starter / Equipment / Expansion / Line of Credit. Offers above your reputation band are greyed out with
///                  the requirement written on the button.
/// Programmatic UI Toolkit with inline styles (same as the other finance panels). Rebuilt on every loan or money change.
/// </summary>
public static class BankWindow
{
    private static VisualElement _overlay;
    private static VisualElement _body;
    private static bool _subscribed;

    private static readonly Color ColBg      = new Color(0x14 / 255f, 0x1C / 255f, 0x26 / 255f, 0.98f);
    private static readonly Color ColRow     = new Color(0x1B / 255f, 0x26 / 255f, 0x33 / 255f, 1f);
    private static readonly Color ColBlue    = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColOrange  = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColText    = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColMuted   = new Color(0.55f, 0.62f, 0.70f, 1f);
    private static readonly Color ColGood    = new Color(0.20f, 0.68f, 0.38f, 1f);
    private static readonly Color ColBad     = new Color(0.85f, 0.30f, 0.28f, 1f);

    public static bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

    public static void Toggle(VisualElement anyElementOnPanel)
    {
        if (IsOpen) { Close(); return; }
        Open(anyElementOnPanel);
    }

    public static void Open(VisualElement anyElementOnPanel)
    {
        var root = anyElementOnPanel?.panel?.visualTree;
        if (root == null) return;
        if (_overlay == null || _overlay.panel == null)
        {
            _overlay = BuildShell();
            root.Add(_overlay);
        }
        if (!_subscribed)
        {
            _subscribed = true;
            // Loan changes only: rebuilding on every money tick (payroll, every game minute) would swap the buttons out from under
            // a click. Every button here changes a loan, so the cash figure is refreshed with it.
            LoanService.OnLoansChanged += Refresh;
        }
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.BringToFront();
        Refresh();
    }

    public static void Close()
    {
        if (_overlay != null) _overlay.style.display = DisplayStyle.None;
    }

    private static VisualElement BuildShell()
    {
        var overlay = new VisualElement { name = "BankOverlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.right = 0; overlay.style.top = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.RegisterCallback<PointerDownEvent>(e => { if (e.target == overlay) Close(); });

        var card = new VisualElement();
        card.style.width = 820;
        card.style.maxHeight = Length.Percent(88);
        card.style.backgroundColor = ColBg;
        SetBorder(card, ColBlue, 3, 10);
        card.style.paddingLeft = card.style.paddingRight = 16;
        card.style.paddingTop = card.style.paddingBottom = 12;

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        var title = Text("FIRST NATIONAL WAREHOUSE BANK", 30, ColOrange, true);
        title.style.flexGrow = 1;
        head.Add(title);
        var close = new Button(Close) { text = "X" };
        close.style.width = 34; close.style.height = 34; close.style.fontSize = 18;
        close.style.color = Color.white; close.style.backgroundColor = new Color(0.75f, 0.18f, 0.18f, 1f);
        SetBorder(close, new Color(0.4f, 0.05f, 0.05f, 1f), 2, 6);
        head.Add(close);
        card.Add(head);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexShrink = 1;
        _body = new VisualElement();
        scroll.Add(_body);
        card.Add(scroll);
        overlay.Add(card);
        return overlay;
    }

    private static void Refresh()
    {
        if (_body == null || !IsOpen) return;
        _body.Clear();
        if (!ServiceLocator.TryGet(out LoanService bank) || bank == null)
        {
            _body.Add(Text("The bank is closed (no LoanService).", 16, ColBad));
            return;
        }
        ServiceLocator.TryGet(out MoneyService money);
        int cash = money != null ? money.CurrentCapital : 0;
        var band = bank.CurrentBand;

        // Summary strip
        var sum = Row(ColRow);
        sum.Add(Stat("CASH", $"${cash:N0}", ColText));
        sum.Add(Stat("DEBT", $"${bank.TotalDebt:N0}", ColBad));
        int net = cash - bank.TotalDebt;
        sum.Add(Stat("NET WORTH", $"{(net < 0 ? "-" : "")}${Mathf.Abs(net):N0}", net >= 0 ? ColGood : ColBad));
        sum.Add(Stat("WEEKLY PAYMENTS", $"${bank.WeeklyPayments:N0}", ColOrange));
        sum.Add(Stat("YOUR RATE", $"{LoanService.AprFor(band):P0} ({ReputationService.BandLabel(band)})", ColBlue));
        _body.Add(sum);

        // Your loans
        _body.Add(Section("YOUR LOANS"));
        if (bank.Loans.Count == 0) _body.Add(Text("No loans. Debt free!", 16, ColMuted));
        foreach (var loan in bank.Loans)
        {
            var r = Row(ColRow);
            var info = new VisualElement(); info.style.flexGrow = 1;
            info.Add(Text(loan.name, 20, ColText, true));
            int weeksLeft = loan.WeeksLeft(LoanService.WeeklyRate(loan.apr));
            info.Add(Text($"Owes ${Mathf.CeilToInt(loan.balance):N0} of ${loan.amount:N0}  |  {loan.apr:P0} APR  |  ${loan.weeklyPayment:N0}/week  |  ~{weeksLeft} weeks left  |  next due day {loan.nextDueDay}" +
                          (loan.missedPayments > 0 ? $"  |  {loan.missedPayments} missed" : ""), 13, ColMuted));
            r.Add(info);
            var l = loan;
            int extra = Mathf.Min(5000, Mathf.CeilToInt(loan.balance));
            r.Add(Btn($"Pay ${extra:N0}", cash >= extra, ColBlue, () => bank.PayExtra(l, 5000)));
            r.Add(Btn("Pay Off", cash >= Mathf.CeilToInt(loan.balance), ColGood, () => bank.PayOff(l)));
            string why = bank.WhyNoRefinance(loan);
            r.Add(Btn(why == null ? $"Refinance to {LoanService.AprFor(band):P0}" : "Refinance", why == null, ColOrange, () => bank.Refinance(l), why));
            _body.Add(r);
        }

        // Offers
        _body.Add(Section("LOAN OFFERS"));
        foreach (var offer in LoanService.Offers)
        {
            string why = bank.WhyNot(offer);
            bool locked = band < offer.Requires;
            var r = Row(ColRow);
            r.style.opacity = locked ? 0.45f : 1f;
            var info = new VisualElement(); info.style.flexGrow = 1;
            info.Add(Text($"{offer.Name} - ${offer.Amount:N0}", 20, locked ? ColMuted : ColText, true));
            float apr = LoanService.AprFor(band);
            info.Add(Text($"{offer.Blurb}  {offer.TermWeeks} weeks at {apr:P0} = ${LoanService.PaymentFor(offer.Amount, apr, offer.TermWeeks):N0}/week" +
                          $"  |  needs {ReputationService.BandLabel(offer.Requires)}", 13, ColMuted));
            r.Add(info);
            var o = offer;
            r.Add(Btn(why == null ? "Take Loan" : why, why == null, ColGood, () => bank.TakeLoan(o), why));
            _body.Add(r);
        }

        _body.Add(Text("Interest is an expense (Lease / Mortgage > Loan Interest). Borrowed cash and principal repayments are not income or expenses. " +
                       "A missed payment adds a 5% fee to the balance and costs 10 reputation. Refinance needs Respected reputation, once every 4 weeks.",
                       12, ColMuted));
    }

    // ── small builders ──
    private static Label Text(string s, int size, Color c, bool bold = false)
    {
        var l = new Label(s);
        l.style.fontSize = size; l.style.color = c;
        l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.marginTop = 2; l.style.marginBottom = 2;
        return l;
    }

    private static VisualElement Section(string title)
    {
        var l = Text(title, 22, ColBlue, true);
        l.style.marginTop = 12;
        l.style.borderBottomWidth = 2; l.style.borderBottomColor = ColBlue;
        return l;
    }

    private static VisualElement Row(Color bg)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems = Align.Center;
        r.style.backgroundColor = bg;
        r.style.marginTop = 6;
        r.style.paddingLeft = r.style.paddingRight = 10;
        r.style.paddingTop = r.style.paddingBottom = 6;
        r.style.flexShrink = 0;
        SetBorder(r, new Color(1f, 1f, 1f, 0.08f), 1, 6);
        return r;
    }

    private static VisualElement Stat(string key, string value, Color c)
    {
        var v = new VisualElement();
        v.style.flexGrow = 1; v.style.flexBasis = 0;
        v.style.alignItems = Align.Center;
        v.Add(Text(key, 12, ColMuted, true));
        v.Add(Text(value, 18, c, true));
        return v;
    }

    private static Button Btn(string label, bool enabled, Color c, System.Action onClick, string tooltip = null)
    {
        var b = new Button(() => { if (onClick != null) onClick(); }) { text = label };
        b.SetEnabled(enabled);
        b.style.minWidth = 110; b.style.height = 34; b.style.marginLeft = 6;
        b.style.fontSize = 14; b.style.color = Color.white;
        b.style.backgroundColor = enabled ? c : new Color(0.25f, 0.28f, 0.32f, 1f);
        b.style.whiteSpace = WhiteSpace.Normal;
        b.style.flexShrink = 0;
        SetBorder(b, new Color(0f, 0f, 0f, 0.35f), 2, 6);
        if (!string.IsNullOrEmpty(tooltip)) b.tooltip = tooltip;
        return b;
    }

    private static void SetBorder(VisualElement e, Color c, float w, float radius)
    {
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
    }
}
