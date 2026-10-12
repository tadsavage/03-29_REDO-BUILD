using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The contract negotiation table (Tad, 2026-10-11: "I want this to be a spreadsheet game").
///
/// A modal over the Contracts panel. Left to right the sheet reads: the term, THEIR opening offer, the market
/// STANDARD, YOUR ask (stepper), and THEIR counter once they've made one. Under it a live estimate of what the
/// deal is worth per day, and a fuzzy "deal heat" gauge so the player can feel how far they're pushing before they
/// commit. The rules live in NegotiationSession - this class only presents them.
///
/// Built in code in the house style (see ContractsPanel / AODPanel) rather than UXML: every panel in this project
/// is, and mixing the two would put half the palette in a stylesheet nobody else reads.
/// </summary>
public class NegotiationPanel : IUIPanel
{
    // Palette - same values ContractsPanel uses so the modal reads as part of it.
    static readonly Color ColBg = new Color(18f / 255f, 26f / 255f, 36f / 255f, 0.98f);
    static readonly Color ColScrim = new Color(0f, 0f, 0f, 0.62f);
    static readonly Color ColBorder = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    static readonly Color ColTitle = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    static readonly Color ColSubtle = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    static readonly Color ColOrange = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    static readonly Color ColOrangeEdge = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    static readonly Color ColBlue = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    static readonly Color ColBlueEdge = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    static readonly Color ColBlueHover = new Color(0x5C / 255f, 0xA2 / 255f, 0xD2 / 255f, 1f);
    static readonly Color ColGreen = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);
    static readonly Color ColAmber = new Color(0xF2 / 255f, 0xC2 / 255f, 0x5A / 255f, 1f);
    static readonly Color ColRed = new Color(0xE2 / 255f, 0x4B / 255f, 0x4A / 255f, 1f);
    static readonly Color ColRowEven = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    static readonly Color ColRowOdd = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    static readonly Color ColGrid = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 0.22f);
    static readonly Color ColHeaderRow = new Color(28f / 255f, 40f / 255f, 54f / 255f, 1f);

    // Column widths - fixed, so every row lines up like cells in a sheet.
    const float WTerm = 360f, WOpen = 120f, WStd = 120f, WAsk = 230f, WCounter = 170f;

    readonly VisualElement _overlay;
    readonly VisualElement _modal;
    VisualElement _logoBox;
    Label _company, _styleLine, _pitch, _patience, _round, _msg, _heatLabel, _revLabel, _marginLabel, _stdLabel;
    Label _partnerLine, _repLine, _standingNote, _needLabel;
    VisualElement _left, _right, _avatarFrame, _diceLayer;
    Image _avatarImg;
    Label _moodLabel, _rollNote, _diceCaption;
    DieElement _die;
    IVisualElementScheduledItem _diceTimer;
    bool _rolling;
    const float RightW = 330f;
    VisualElement _heatMarker;
    Button _sendBtn, _counterBtn, _standardBtn, _theirsBtn, _walkBtn;
    readonly List<RowUi> _rows = new();

    NegotiationSession _s;
    ContractData _contract;
    ContractTerms _ask;
    ContractTerms _counter;
    Action<bool> _onFinished;
    bool _open;
    bool _over;       // the table is closed out (walked away) - only Close is live

    static Font _lilita, _nunito;

    class RowUi
    {
        public TermSpec Spec;
        public Label Ask, Counter;
        public VisualElement AskCell, Row;
    }

    public NegotiationPanel(VisualElement root)
    {
        _overlay = new VisualElement { name = "NegotiationOverlay" };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = _overlay.style.top = _overlay.style.right = _overlay.style.bottom = 0;
        _overlay.style.backgroundColor = ColScrim;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.justifyContent = Justify.Center;
        _overlay.pickingMode = PickingMode.Position;

        _modal = new VisualElement { name = "NegotiationModal" };
        _modal.style.backgroundColor = ColBg;
        _modal.style.width = WTerm + WOpen + WStd + WAsk + WCounter + 64f + RightW + 22f;
        _modal.style.maxHeight = Length.Percent(94);
        _modal.style.flexDirection = FlexDirection.Column;
        Border(_modal, 3, ColBorder, 10);
        _modal.style.paddingLeft = _modal.style.paddingRight = 24;
        _modal.style.paddingTop = 18; _modal.style.paddingBottom = 18;

        BuildHeader();
        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        _left = new VisualElement(); _left.style.flexGrow = 1; _left.style.flexShrink = 1;
        _right = new VisualElement(); _right.style.width = RightW; _right.style.flexShrink = 0; _right.style.marginLeft = 22;
        body.Add(_left); body.Add(_right);
        _modal.Add(body);
        BuildSheet();
        BuildFooter();
        BuildAvatarWindow();
        _overlay.Add(_modal);
        BuildDiceLayer();
        root.Add(_overlay);
        _overlay.style.display = DisplayStyle.None;
    }

    public bool IsOpen => _open;
    public void Show() { if (_s != null) { _open = true; _overlay.style.display = DisplayStyle.Flex; _overlay.BringToFront(); } }

    public void Hide()
    {
        _open = false;
        _diceTimer?.Pause();
        _rolling = false;
        if (_diceLayer != null) _diceLayer.style.display = DisplayStyle.None;
        NegotiatorStage.Hide();
        _overlay.style.display = DisplayStyle.None;
        if (UIKeyBindingManager.Instance != null) UIKeyBindingManager.Instance.UnregisterAuxiliary(this);
    }

    // ── Opening a negotiation ────────────────────────────────────────────

    /// <summary>Sits the player down with this customer. onFinished(true) when a contract was signed.</summary>
    public void Open(ContractData contract, Action<bool> onFinished)
    {
        var arrivals = ServiceLocator.TryGet<OrderArrivalService>(out var a) ? a : null;
        if (arrivals == null || contract == null) return;
        if (arrivals.IsInWalkAwayCooldown(contract.ContractId))
        {
            UIToast.Show($"{contract.Customer?.CompanyName} won't talk to you for {arrivals.WalkAwayDaysRemaining(contract.ContractId)} more day(s).");
            return;
        }

        _contract = contract;
        _onFinished = onFinished;
        _s = arrivals.GetOrBeginNegotiation(contract);
        _over = false;
        _counter = _s.LastCounter;
        _ask = (_s.LastCounter != null ? _s.LastCounter : _s.Opening).Clone();

        var cust = contract.Customer;
        _company.text = cust != null ? cust.CompanyName : contract.ContractId;
        _pitch.text = string.IsNullOrWhiteSpace(contract.Pitch) ? "" : contract.Pitch;
        _logoBox.style.backgroundImage = cust != null && cust.Icon != null ? new StyleBackground(cust.Icon) : new StyleBackground();
        _msg.text = _s.Round == 0
            ? $"\"Good to meet you, {NegotiationSession.PlayerName}. Here's our opening offer. Tell us what you can do.\""
            : $"\"Back again, {NegotiationSession.PlayerName}? Where were we...\"";

        NegotiatorStage.Show(_company.text);
        _avatarImg.image = NegotiatorStage.Texture;
        _rollNote.text = "";
        _open = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.BringToFront();
        UIToast.KeepOnTop();
        if (UIKeyBindingManager.Instance != null) UIKeyBindingManager.Instance.RegisterAuxiliary(this);
        Refresh();
    }

    // ── Layout ───────────────────────────────────────────────────────────

    void BuildHeader()
    {
        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.marginBottom = 12;

        _logoBox = new VisualElement();
        _logoBox.style.width = 96; _logoBox.style.height = 96;
        _logoBox.style.flexShrink = 0;
        _logoBox.style.marginRight = 18;
        _logoBox.style.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 1f);
        Border(_logoBox, 3, ColOrange, 8);
        head.Add(_logoBox);

        var mid = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
        _company = Text("", 36, ColTitle, lilita: true);
        mid.Add(_company);
        _pitch = Text("", 15, ColSubtle);
        mid.Add(_pitch);
        _styleLine = Text("", 15, ColAmber);
        _styleLine.style.marginTop = 2;
        mid.Add(_styleLine);
        head.Add(mid);

        // Standing block: partnership with THIS customer, our reputation overall, and how both move the deal.
        var standing = new VisualElement();
        standing.style.width = 300; standing.style.flexShrink = 0; standing.style.marginLeft = 12; standing.style.marginRight = 12;
        _partnerLine = Text("", 17, ColTitle, lilita: true);
        _repLine = Text("", 17, ColTitle, lilita: true);
        _standingNote = Text("", 12, ColSubtle);
        _standingNote.style.marginTop = 3;
        standing.Add(_partnerLine); standing.Add(_repLine); standing.Add(_standingNote);
        head.Add(standing);

        var right = new VisualElement();
        right.style.alignItems = Align.FlexEnd;
        right.style.flexShrink = 0;
        _round = Text("ROUND 0", 16, ColSubtle, lilita: true);
        right.Add(_round);
        _patience = Text("PATIENCE", 22, ColGreen, lilita: true);
        right.Add(_patience);
        var close = new Button(() => { Hide(); }) { text = "X" };
        StyleButton(close, ColRed, new Color(0.45f, 0.14f, 0.14f), new Color(1f, 0.35f, 0.35f), Color.white, 20);
        close.style.width = 44; close.style.height = 38; close.style.marginTop = 6;
        right.Add(close);
        head.Add(right);
        _modal.Add(head);
    }

    void BuildSheet()
    {
        var sheet = new VisualElement();
        Border(sheet, 2, ColGrid, 4);

        var header = SheetRow(ColHeaderRow);
        header.Add(Cell("TERM", WTerm, ColSubtle, lilita: true, size: 15, align: TextAnchor.MiddleLeft));
        header.Add(Cell("THEIR OFFER", WOpen, ColSubtle, lilita: true, size: 15));
        header.Add(Cell("STANDARD", WStd, ColSubtle, lilita: true, size: 15));
        header.Add(Cell("YOUR ASK", WAsk, ColOrange, lilita: true, size: 15));
        header.Add(Cell("THEIR COUNTER", WCounter, ColBlue, lilita: true, size: 15));
        sheet.Add(header);

        int i = 0;
        foreach (var spec in TermCatalog.All)
        {
            var row = SheetRow(i++ % 2 == 0 ? ColRowEven : ColRowOdd);
            var ui = new RowUi { Spec = spec, Row = row };

            var termCell = new VisualElement();
            termCell.style.width = WTerm; termCell.style.paddingLeft = 12; termCell.style.justifyContent = Justify.Center;
            termCell.Add(Text(spec.Label, 20, ColTitle, lilita: true));
            var hint = Text(spec.Hint, 12, ColSubtle);
            hint.style.marginTop = -2;
            termCell.Add(hint);
            row.Add(termCell);

            row.Add(Cell(spec.Format(spec.Snap(spec.Opening)), WOpen, ColSubtle, size: 19));
            row.Add(Cell(spec.Format(spec.Snap(spec.Standard)), WStd, ColSubtle, size: 19));

            // Stepper: [-] value [+]
            var askCell = new VisualElement();
            askCell.style.width = WAsk; askCell.style.flexDirection = FlexDirection.Row;
            askCell.style.alignItems = Align.Center; askCell.style.justifyContent = Justify.Center;
            var minus = Stepper("-", () => Nudge(ui, -1));
            ui.Ask = Text("", 24, ColTitle, lilita: true);
            ui.Ask.style.width = 96; ui.Ask.style.unityTextAlign = TextAnchor.MiddleCenter;
            var plus = Stepper("+", () => Nudge(ui, +1));
            askCell.Add(minus); askCell.Add(ui.Ask); askCell.Add(plus);
            ui.AskCell = askCell;
            row.Add(askCell);

            ui.Counter = Cell("-", WCounter, ColBlue, lilita: true, size: 21);
            row.Add(ui.Counter);

            _rows.Add(ui);
            sheet.Add(row);
        }
        _left.Add(sheet);

        // Their reply
        _msg = Text("", 20, ColTitle);
        _msg.style.marginTop = 12;
        _msg.style.paddingLeft = 14; _msg.style.paddingRight = 14; _msg.style.paddingTop = 10; _msg.style.paddingBottom = 10;
        _msg.style.backgroundColor = new Color(0.07f, 0.11f, 0.16f, 1f);
        Border(_msg, 2, ColBlueEdge, 6);
        _left.Add(_msg);
    }

    void BuildFooter()
    {
        // Deal heat gauge: green / amber / red zones with a marker.
        var heatRow = new VisualElement();
        heatRow.style.flexDirection = FlexDirection.Row;
        heatRow.style.alignItems = Align.Center;
        heatRow.style.marginTop = 12;
        heatRow.Add(Text("DEAL HEAT", 15, ColSubtle, lilita: true));
        heatRow.ElementAt(0).style.width = 110;

        var bar = new VisualElement();
        bar.style.flexGrow = 1; bar.style.height = 20; bar.style.flexDirection = FlexDirection.Row;
        Border(bar, 2, ColGrid, 4);
        bar.Add(Zone(ColGreen, 59)); bar.Add(Zone(ColAmber, 25)); bar.Add(Zone(ColRed, 16));
        _heatMarker = new VisualElement();
        _heatMarker.style.position = Position.Absolute;
        _heatMarker.style.top = -4; _heatMarker.style.width = 6; _heatMarker.style.height = 28;
        _heatMarker.style.backgroundColor = Color.white;
        Border(_heatMarker, 1, Color.black, 2);
        bar.Add(_heatMarker);
        heatRow.Add(bar);

        _heatLabel = Text("", 18, ColTitle, lilita: true);
        _heatLabel.style.width = 230; _heatLabel.style.marginLeft = 14;
        heatRow.Add(_heatLabel);
        _left.Add(heatRow);

        _needLabel = Text("", 16, ColAmber, lilita: true);
        _needLabel.style.marginTop = 6;
        _left.Add(_needLabel);

        // Spreadsheet totals
        var totals = new VisualElement();
        totals.style.flexDirection = FlexDirection.Row;
        totals.style.marginTop = 12;
        totals.style.paddingTop = 8;
        totals.style.borderTopWidth = 2; totals.style.borderTopColor = ColGrid;
        _revLabel = Text("", 20, ColTitle, lilita: true); _revLabel.style.flexGrow = 1;
        _marginLabel = Text("", 20, ColGreen, lilita: true); _marginLabel.style.flexGrow = 1;
        _stdLabel = Text("", 14, ColSubtle); _stdLabel.style.flexGrow = 1; _stdLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        totals.Add(_revLabel); totals.Add(_marginLabel); totals.Add(_stdLabel);
        _left.Add(totals);

        // Buttons
        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.marginTop = 14;
        buttons.style.alignItems = Align.Center;

        _standardBtn = new Button(() => { _ask = _s.Standard.Clone(); Refresh(); }) { text = "STANDARD TERMS" };
        StyleButton(_standardBtn, ColBlue, ColBlueEdge, ColBlueHover, Color.black, 18);
        _theirsBtn = new Button(AcceptTheirOffer) { text = "TAKE THEIR OFFER" };
        StyleButton(_theirsBtn, ColBlue, ColBlueEdge, ColBlueHover, Color.black, 18);
        _walkBtn = new Button(WalkAway) { text = "WALK AWAY" };
        StyleButton(_walkBtn, ColRed, new Color(0.45f, 0.14f, 0.14f), new Color(1f, 0.35f, 0.35f), Color.black, 18);
        _counterBtn = new Button(AcceptCounter) { text = "ACCEPT THEIR COUNTER" };
        StyleButton(_counterBtn, ColGreen, new Color(0.2f, 0.45f, 0.25f), new Color(0.6f, 0.95f, 0.65f), Color.black, 18);
        _sendBtn = new Button(SendOffer) { text = "SEND OFFER" };
        StyleButton(_sendBtn, ColOrange, ColOrangeEdge, ColOrangeHover, Color.black, 22);
        _sendBtn.style.height = 52; _sendBtn.style.width = 170;

        var spacer = new VisualElement { style = { flexGrow = 1 } };
        buttons.Add(_standardBtn); buttons.Add(_theirsBtn); buttons.Add(_walkBtn);
        buttons.Add(spacer);
        buttons.Add(_counterBtn); buttons.Add(_sendBtn);
        _left.Add(buttons);
    }

    // ── Behaviour ────────────────────────────────────────────────────────

    void Nudge(RowUi ui, int dir)
    {
        if (_over || _rolling || _s == null) return;
        float v = ui.Spec.Get(_ask);
        // The late fee runs downhill, but the buttons always mean "number goes down / up" - exactly what is printed.
        ui.Spec.Set(_ask, ui.Spec.Snap(v + dir * ui.Spec.Step));
        Refresh();
    }

    void SendOffer()
    {
        if (_over || _rolling || _s == null) return;
        int roll = UnityEngine.Random.Range(1, 21);
        int mod = _s.RollModifier(out string breakdown);
        _rolling = true;
        Refresh();
        BeginRoll(roll, $"d20 {roll}  {breakdown}  = {roll + mod}", () => { _rolling = false; ApplyOffer(roll); });
    }

    void ApplyOffer(int roll)
    {
        var arrivals = Arrivals();
        if (arrivals == null || _s == null) return;

        var result = _s.Submit(_ask, roll);
        _msg.text = $"\"{result.Message}\"" + (string.IsNullOrEmpty(result.RollNote) ? "" : $"\n{result.RollNote}");
        _rollNote.text = result.CriticalSuccess ? "CRITICAL SUCCESS" : result.CriticalFail ? "CRITICAL FAIL" : $"Last roll: {result.RollTotal}";
        _rollNote.style.color = result.CriticalSuccess ? ColGreen : result.CriticalFail ? ColRed : ColSubtle;
        AudioManager.Play("UIClick");

        switch (result.Outcome)
        {
            case NegotiationOutcome.Accepted:
                _over = true;
                _msg.style.color = ColGreen;
                NegotiatorStage.Delighted();
                NegotiationFx.Play(_overlay.parent, true);
                _fxPlayed = true;
                Refresh();
                var terms = result.FinalTerms ?? _ask;
                // Let the customer's wave land before the window closes.
                _overlay.schedule.Execute(() => { _over = false; SignWith(terms, result.CriticalSuccess ? "Natural charm - extra concessions!" : "Deal!"); }).StartingIn(1600);
                return;
            case NegotiationOutcome.Countered:
                _counter = result.Counter;
                _msg.style.color = ColAmber;
                break;
            case NegotiationOutcome.Rejected:
                _counter = null;
                _msg.style.color = ColRed;
                NegotiatorStage.Furious();
                break;
            case NegotiationOutcome.WalkedAway:
                _over = true;
                arrivals.RecordWalkAway(_contract.ContractId);
                arrivals.EndNegotiation(_contract.ContractId);
                _msg.style.color = ColRed;
                NegotiatorStage.Furious();
                NegotiationFx.Play(_overlay.parent, false);
                UIToast.Show($"{_company.text} walked out. They won't talk for {NegotiationService.WalkAwayCooldownDays} days.");
                _onFinished?.Invoke(false);
                break;
        }
        Refresh();
    }

    void AcceptCounter()
    {
        if (_over || _counter == null) return;
        SignWith(_counter, "Deal on their counter!");
    }

    void AcceptTheirOffer()
    {
        if (_over || _s == null) return;
        SignWith(_s.Opening, "Signed on their opening terms.");
    }

    void WalkAway()
    {
        var arrivals = Arrivals();
        if (arrivals != null && _contract != null)
        {
            // Leaving on your own terms costs a day or three, not a lasting grudge - but it does cost.
            arrivals.RecordWalkAway(_contract.ContractId, 1);
            arrivals.EndNegotiation(_contract.ContractId);
        }
        UIToast.Show($"You walked away from {_company.text}. They'll take your call again tomorrow.");
        _over = true;
        Hide();
        _onFinished?.Invoke(false);
    }

    void SignWith(ContractTerms terms, string toast)
    {
        var arrivals = Arrivals();
        if (arrivals == null) return;
        bool ok = arrivals.Sign(_contract.ContractId, terms);
        if (ok && !_fxPlayed) NegotiationFx.Play(_overlay.parent, true);
        _fxPlayed = false;
        arrivals.EndNegotiation(_contract.ContractId);
        _over = true;
        UIToast.Show(ok ? $"{_company.text}: {toast} First delivery tomorrow." : "Couldn't sign that contract - it may already be taken.");
        Hide();
        _onFinished?.Invoke(ok);
    }

    // ── Refresh ──────────────────────────────────────────────────────────

    void Refresh()
    {
        if (_s == null) return;

        _round.text = $"ROUND {_s.Round}";
        _patience.text = "PATIENCE  " + string.Concat(Enumerable.Range(0, NegotiationSession.MaxPatience)
                                                                .Select(i => i < _s.Patience ? "O " : "x "));
        _patience.style.color = _s.Patience >= 3 ? ColGreen : _s.Patience == 2 ? ColAmber : ColRed;
        _styleLine.text = _s.Round >= 1 ? NegotiationService.StyleLabel(_s.Style) : "You don't know how they negotiate yet.";

        string repBand = ReputationService.BandLabel(ReputationService.BandFor(_s.Reputation));
        _partnerLine.text = $"PARTNERSHIP: {NegotiationService.PartnershipLabel(_s.HasHistory, _s.Relationship)}";
        _partnerLine.style.color = !_s.HasHistory ? ColSubtle : _s.Relationship >= 0.7f ? ColGreen : _s.Relationship >= 0.4f ? ColAmber : ColRed;
        _repLine.text = $"YOUR REPUTATION: {repBand} ({_s.Reputation})";
        _repLine.style.color = ColBlue;
        _standingNote.text = $"How much ground they'll give: base {_s.BaseBudget:0.00} {Signed(_s.RepBonus)} reputation {Signed(_s.RelBonus)} partnership " +
                             $"{Signed(_s.DiffBonus)} difficulty {Signed(_s.MoodBonus)} their mood = {_s.Budget:0.00}. Higher means they say yes to more.";

        var std = _s.Standard;
        foreach (var r in _rows)
        {
            float v = r.Spec.Get(_ask);
            r.Ask.text = r.Spec.Format(v);
            float over = r.Spec.Push(v) - r.Spec.Push(r.Spec.Get(std));
            r.Ask.style.color = over <= 0.001f ? ColGreen : over <= 0.25f ? ColAmber : ColOrangeHover;

            if (_counter != null) { r.Counter.text = r.Spec.Format(r.Spec.Get(_counter)); }
            else r.Counter.text = "-";
        }

        // Fuzzy heat: the true ratio plus a fixed per-customer wobble, so the player can feel the edge without reading it off.
        float ratio = _s.Appetite(_ask);
        float wobble = 0f;   // exact now: the roll-needed line below states the same maths outright
        float shown = Mathf.Clamp(ratio * (1f + wobble), 0f, 1.6f);
        _heatMarker.style.left = Length.Percent(Mathf.Clamp01(shown / 1.6f) * 100f - 1f);
        _heatLabel.text = shown < 0.9f ? "They'd likely sign" : shown < 1.35f ? "Could go either way" : "No way";
        _heatLabel.style.color = shown < 0.9f ? ColGreen : shown < 1.35f ? ColAmber : ColRed;
        NegotiatorStage.SetHeat(shown);
        _s.RollNeeded(_ask, out _, out _, out _, out string needText);
        _needLabel.text = "TO CLOSE: " + needText;
        _moodLabel.text = shown < 0.92f ? "PLEASED" : shown < 1.12f ? "THOUGHTFUL" : shown < 1.32f ? "AGITATED" : "FURIOUS";
        _moodLabel.style.color = shown < 0.92f ? ColGreen : shown < 1.12f ? ColAmber : shown < 1.32f ? ColOrangeHover : ColRed;

        var arrivals = Arrivals();
        if (arrivals != null && _contract != null)
        {
            arrivals.AverageSkuEconomics(out float cost, out float cpp);
            NegotiationService.EstimateDaily(_contract, _ask, cost, cpp, out float rev, out float margin);
            NegotiationService.EstimateDaily(_contract, std, cost, cpp, out _, out float stdMargin);
            _revLabel.text = $"Est. revenue / day   ${rev:N0}";
            _marginLabel.text = $"Est. margin / day   ${margin:N0}";
            float delta = margin - stdMargin;
            _stdLabel.text = $"vs standard terms: {(delta >= 0 ? "+" : "-")}${Mathf.Abs(delta):N0}/day";
            _stdLabel.style.color = delta >= 0 ? ColGreen : ColRed;
        }

        _counterBtn.style.display = _counter != null && !_over ? DisplayStyle.Flex : DisplayStyle.None;
        bool live = !_over && !_rolling;
        _sendBtn.SetEnabled(live);
        _standardBtn.SetEnabled(live);
        _theirsBtn.SetEnabled(live);
        _walkBtn.SetEnabled(live);
        _counterBtn.SetEnabled(live);
        if (_over && _s.Patience <= 0) _sendBtn.text = "THEY LEFT";
        else _sendBtn.text = "SEND OFFER";
    }

    // ── Avatar window ────────────────────────────────────────────────────

    void BuildAvatarWindow()
    {
        _avatarFrame = new VisualElement();
        _avatarFrame.style.width = RightW; _avatarFrame.style.height = 412;
        _avatarFrame.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 1f);
        _avatarFrame.style.overflow = Overflow.Hidden;
        Border(_avatarFrame, 3, ColOrange, 8);
        _avatarImg = new Image();
        _avatarImg.style.position = Position.Absolute;
        _avatarImg.style.left = _avatarImg.style.top = _avatarImg.style.right = _avatarImg.style.bottom = 0;
        _avatarImg.scaleMode = ScaleMode.ScaleAndCrop;
        _avatarFrame.Add(_avatarImg);
        _right.Add(_avatarFrame);

        // Mouse wheel over the window zooms between the whole figure and a close-up of the face; double-click resets.
        _avatarFrame.RegisterCallback<WheelEvent>(e => { NegotiatorStage.AddZoom(-e.delta.y * 0.1f); e.StopPropagation(); });
        _avatarFrame.RegisterCallback<ClickEvent>(e => { if (e.clickCount >= 2) NegotiatorStage.ResetZoom(); });
        var zoomHint = Text("scroll to zoom", 12, new Color(0.7f, 0.8f, 0.9f, 0.55f));
        zoomHint.style.position = Position.Absolute;
        zoomHint.style.left = 8; zoomHint.style.bottom = 4;
        zoomHint.pickingMode = PickingMode.Ignore;
        _avatarFrame.Add(zoomHint);

        _moodLabel = Text("", 24, ColGreen, lilita: true);
        _moodLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _moodLabel.style.marginTop = 8;
        _right.Add(_moodLabel);
        _rollNote = Text("", 15, ColSubtle, lilita: true);
        _rollNote.style.unityTextAlign = TextAnchor.MiddleCenter;
        _right.Add(_rollNote);
    }

    // ── The dice ─────────────────────────────────────────────────────────

    /// <summary>A d20 drawn as a hexagon with the usual inner triangle; the number rides on it and tumbles with it.</summary>
    class DieElement : VisualElement
    {
        public readonly Label Number;
        public DieElement()
        {
            style.position = Position.Absolute;
            style.width = 118; style.height = 118;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            Number = new Label("20");
            var f = LilitaFont();
            if (f != null) Number.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
            Number.style.fontSize = 40; Number.style.color = new Color(0.1f, 0.12f, 0.16f);
            Number.style.unityTextAlign = TextAnchor.MiddleCenter;
            Number.style.position = Position.Absolute;
            Number.style.left = Number.style.top = Number.style.right = Number.style.bottom = 0;
            Number.pickingMode = PickingMode.Ignore;
            Add(Number);
        }

        void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            float cx = 59f, cy = 59f, r = 56f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i - 90f);
                var pt = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.fillColor = new Color(0.96f, 0.95f, 0.88f);
            p.Fill();
            p.strokeColor = new Color(0.12f, 0.14f, 0.18f);
            p.lineWidth = 4f;
            p.Stroke();
            p.lineWidth = 2f;
            p.strokeColor = new Color(0.12f, 0.14f, 0.18f, 0.55f);
            p.BeginPath();
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.Deg2Rad * (120f * i - 90f);
                var pt = new Vector2(cx + Mathf.Cos(a) * r * 0.62f, cy + Mathf.Sin(a) * r * 0.62f);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Stroke();
        }
    }

    void BuildDiceLayer()
    {
        _diceLayer = new VisualElement();
        _diceLayer.style.position = Position.Absolute;
        _diceLayer.style.left = _diceLayer.style.top = _diceLayer.style.right = _diceLayer.style.bottom = 0;
        _diceLayer.pickingMode = PickingMode.Ignore;
        _diceLayer.style.display = DisplayStyle.None;
        _die = new DieElement();
        _diceLayer.Add(_die);
        _diceCaption = Text("", 26, Color.white, lilita: true);
        _diceCaption.style.position = Position.Absolute;
        _diceCaption.style.unityTextAlign = TextAnchor.MiddleCenter;
        _diceCaption.style.width = 700;
        _diceCaption.pickingMode = PickingMode.Ignore;
        _diceLayer.Add(_diceCaption);
        _overlay.Add(_diceLayer);
    }

    /// <summary>The thud of the die hitting the table, then a quick rattle of taps - more of them on the big first bounce.</summary>
    void DiceBounceSound(int bounce)
    {
        AudioManager.Play(bounce == 1 ? "DiceDrop" : "DiceDropSoft");
        int taps = bounce == 1 ? 3 : bounce <= 3 ? 2 : 1;
        for (int i = 1; i <= taps; i++)
            _overlay.schedule.Execute(() => AudioManager.Play("DiceTap")).StartingIn(60 * i + i * i * 12);
    }

    /// <summary>Drops the die from the top of the screen, bounces it around the middle, and lands it on the result.
    /// A little hand-rolled physics on unscaled time (the game is paused behind this window). onDone fires once it has settled
    /// and the roll has been on screen a moment.</summary>
    void BeginRoll(int result, string caption, Action onDone)
    {
        _diceTimer?.Pause();
        float W = _overlay.resolvedStyle.width, H = _overlay.resolvedStyle.height;
        if (float.IsNaN(W) || W < 100f) W = 1920f;
        if (float.IsNaN(H) || H < 100f) H = 1080f;

        float floorY = H * 0.5f + 40f;
        float minX = W * 0.5f - 330f, maxX = W * 0.5f + 330f;
        float x = W * 0.5f + UnityEngine.Random.Range(-90f, 90f), y = -140f;
        float vx = UnityEngine.Random.Range(-320f, 320f), vy = 0f;
        float rot = 0f, vr = UnityEngine.Random.Range(620f, 980f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        int bounces = 0; float shuffleT = 0f; bool settled = false; float settleT = 0f;
        const float G = 2500f;

        _diceLayer.style.display = DisplayStyle.Flex;
        _diceCaption.text = "";
        _die.Number.text = UnityEngine.Random.Range(1, 21).ToString();
        _die.style.opacity = 1f;
        _diceCaption.style.opacity = 1f;

        float last = Time.unscaledTime;
        _diceTimer = _overlay.schedule.Execute(() =>
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Min(now - last, 0.05f);
            last = now;

            if (!settled)
            {
                vy += G * dt; y += vy * dt; x += vx * dt; rot += vr * dt;
                if (x < minX) { x = minX; vx = Mathf.Abs(vx) * 0.7f; AudioManager.Play("DiceTap"); }
                if (x > maxX) { x = maxX; vx = -Mathf.Abs(vx) * 0.7f; AudioManager.Play("DiceTap"); }
                if (y >= floorY)
                {
                    y = floorY; vy = -vy * 0.58f; vx *= 0.82f; vr *= 0.72f; bounces++;
                    DiceBounceSound(bounces);
                    if (bounces >= 5 || Mathf.Abs(vy) < 240f) { settled = true; vy = 0f; AudioManager.Play("DiceLand"); }
                }
                shuffleT += dt;
                if (shuffleT > 0.07f && !settled) { shuffleT = 0f; _die.Number.text = UnityEngine.Random.Range(1, 21).ToString(); }
                if (settled)
                {
                    _die.Number.text = result.ToString();
                    rot = Mathf.Repeat(rot, 360f); if (rot > 180f) rot -= 360f;   // ease back upright
                }
            }
            else
            {
                settleT += dt;
                rot = Mathf.Lerp(rot, 0f, 1f - Mathf.Exp(-12f * dt));
                if (settleT > 0.15f && string.IsNullOrEmpty(_diceCaption.text)) _diceCaption.text = caption;
                if (settleT > 1.9f)
                {
                    _diceTimer.Pause();
                    _diceLayer.style.display = DisplayStyle.None;
                    onDone?.Invoke();
                    return;
                }
                if (settleT > 1.5f) { float a = 1f - (settleT - 1.5f) / 0.4f; _die.style.opacity = a; _diceCaption.style.opacity = a; }
            }

            _die.style.left = x - 59f; _die.style.top = y - 59f;
            _die.style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
            _diceCaption.style.left = W * 0.5f - 350f; _diceCaption.style.top = Mathf.Max(y, floorY) + 70f;
        }).Every(16);
    }

    static string Signed(float v) => (v >= 0f ? "+" : "-") + Mathf.Abs(v).ToString("0.00");

    static OrderArrivalService Arrivals() => ServiceLocator.TryGet<OrderArrivalService>(out var a) ? a : null;

    // ── Element helpers ──────────────────────────────────────────────────

    internal static Font FxFont() => LilitaFont();

    bool _fxPlayed;

    static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
        if (guids.Length > 0) _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
        _lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular");
#endif
        return _lilita;
    }

    static Font NunitoFont()
    {
        if (_nunito != null) return _nunito;
#if UNITY_EDITOR
        foreach (var g in UnityEditor.AssetDatabase.FindAssets("NunitoSans t:Font"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            if (path.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            _nunito = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(path);
            break;
        }
#else
        _nunito = Resources.Load<Font>("Resource_Fonts/NunitoSans-VariableFont_YTLC,opsz,wdth,wght");
#endif
        return _nunito;
    }

    static Label Text(string text, int size, Color color, bool lilita = false)
    {
        var l = new Label(text);
        var f = lilita ? LilitaFont() : NunitoFont();
        if (f != null) l.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        l.style.fontSize = size;
        l.style.color = color;
        l.style.whiteSpace = WhiteSpace.Normal;
        return l;
    }

    static Label Cell(string text, float width, Color color, bool lilita = false, int size = 18, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var l = Text(text, size, color, lilita);
        l.style.width = width;
        l.style.flexShrink = 0;
        l.style.unityTextAlign = align;
        if (align == TextAnchor.MiddleLeft) l.style.paddingLeft = 12;
        return l;
    }

    static VisualElement SheetRow(Color bg)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems = Align.Center;
        r.style.backgroundColor = bg;
        r.style.minHeight = 58;
        r.style.borderBottomWidth = 1; r.style.borderBottomColor = ColGrid;
        return r;
    }

    static Button Stepper(string label, Action onClick)
    {
        var b = new Button(onClick) { text = label };
        StyleButton(b, ColBlue, ColBlueEdge, ColBlueHover, Color.black, 26);
        b.style.width = 40; b.style.height = 40; b.style.paddingLeft = b.style.paddingRight = 0;
        b.style.marginLeft = b.style.marginRight = 4;
        return b;
    }

    static VisualElement Zone(Color c, float widthPercent)
    {
        var z = new VisualElement();
        z.style.width = Length.Percent(widthPercent);
        z.style.backgroundColor = new Color(c.r, c.g, c.b, 0.55f);
        return z;
    }

    static void Border(VisualElement e, float w, Color c, float radius)
    {
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
    }

    static void StyleButton(Button b, Color fill, Color edge, Color hover, Color text, int size)
    {
        var f = LilitaFont();
        if (f != null) b.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.fontSize = size;
        b.style.color = text;
        b.style.backgroundColor = fill;
        b.style.height = 44;
        b.style.paddingLeft = b.style.paddingRight = 16;
        b.style.marginLeft = b.style.marginRight = 5;
        b.style.unityTextAlign = TextAnchor.MiddleCenter;
        Border(b, 2, edge, 6);
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = hover);
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = fill);
    }
}
