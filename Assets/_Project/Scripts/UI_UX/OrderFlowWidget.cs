using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// "OPEN ORDERS" widget for the bottom tray, plus the fly-in effects that go with it (Tad, 2026-11-08).
///
/// <para><b>What it shows.</b> Every OPEN, UNACCEPTED offer on the Contracts table
/// (<see cref="OrderArrivalService.AvailableOffers"/>: Bulk orders with an ACCEPT ORDER button, and Recurring
/// contracts you can NEGOTIATE), newest first, as small tiles. A tile says who, what kind, the estimated PROFIT, and
/// whether the stock is there (green dot = on the shelf, orange = only once an inbound PO lands, red = you would
/// have to buy). Hover a tile for the full picture: customer icon and name, an offer number, and a line per item with
/// BOH (balance on hand), PENDING (already promised to open orders), INBOUND (on open POs), what this offer ORDERS, and
/// the FINAL balance, then the cases you would still have to buy and revenue / cost / profit. It is a read-only
/// decision aid: it never accepts, signs or buys anything.</para>
///
/// <para><b>Fly-in.</b> A freshly posted offer (<see cref="OrderArrivalService.OnOfferPosted"/>, never a save restore)
/// drops in as a tile that flies down from the TOP of the screen and lands in this widget; the older tiles slide
/// right. Every purchase order raised (<see cref="ShipmentService.OnPurchaseOrderCreated"/>) flies down the same way
/// and drops into the mini dock schedule widget, ringing it on arrival (if that widget is not on screen it lands here).
/// Several at once are staggered.</para>
///
/// <para>The card is a <see cref="DockableHudCard"/> like the log and the dev HUD: it starts docked in the RIGHT zone
/// of the tray, left of the FPS card, and can be dragged to any zone or floated (PlayerPrefs "HudCard_NewOrders").
/// Flying tiles live on a click-through layer in this component's own UIDocument, sorted above the HUD, so they pass
/// over everything including the top bar. Motion runs on UI Toolkit's real-time scheduler, so it plays while paused.
/// Self-bootstrapping like <see cref="SystemsLogWindow"/>.</para>
/// </summary>
public class OrderFlowWidget : MonoBehaviour
{
    // ── Tuning ───────────────────────────────────────────────────────
    private const float CardW = 362f, CardH = 110f;
    private const float TileW = 100f, TileH = 72f, TileGap = 4f;
    private const float ChipW = 30f;
    private const int MaxTiles = 3;
    private const int OfferFlightMs = 1100;
    private const int PoFlightMs = 1250;
    private const float StaggerSeconds = 0.45f;
    private const float PollSeconds = 0.75f;
    private const int MaxQueued = 40;
    private const float IntroStaggerSeconds = 0.35f;   // faster than a live drop: several tiles arrive at once
    private const int IntroMaxPos = 6;                 // pending POs flown in at the start (the rest are on the Scheduler anyway)
    private const string PrefKey = "HudCard_NewOrders";

    private static readonly Color RecurringColor = new Color(0.36f, 0.62f, 0.90f, 1f);
    private static readonly Color BulkColor      = new Color(0.35f, 0.80f, 0.45f, 1f);
    private static readonly Color RushColor      = new Color(1.00f, 0.82f, 0.25f, 1f);
    private static readonly Color InboundColor   = new Color(0.96f, 0.62f, 0.25f, 1f);
    private static readonly Color OkColor        = new Color(0.36f, 0.86f, 0.42f, 1f);
    private static readonly Color WarnColor      = new Color(0.96f, 0.62f, 0.25f, 1f);
    private static readonly Color BadColor       = new Color(1.00f, 0.40f, 0.35f, 1f);
    private static readonly Color NameColor      = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextColor      = new Color(0.81f, 0.89f, 0.94f, 1f);
    private static readonly Color DimColor       = new Color(0.58f, 0.69f, 0.81f, 1f);

    /// <summary>What a tile shows. Offer tiles and PO flyers share one builder so a flyer and the tile it lands as match.</summary>
    private struct TileModel
    {
        public string title, tag, line1, sub;
        public Color accent, line1Color, subColor, dot;
        public bool showDot;
        public Sprite icon;
    }

    /// <summary>One item line of an offer measured against the warehouse.</summary>
    private sealed class StockLine
    {
        public string skuId;
        public SkuData sku;
        public int onHand, committed, inbound, qty;
        public int Final => onHand - committed + inbound - qty;
    }

    private sealed class OfferStats
    {
        public readonly List<StockLine> lines = new List<StockLine>();
        public int cases, pallets, revenue, rushRevenue, cost, shortCases;
        public int status;   // 0 = on the shelf, 1 = only with inbound, 2 = short
        public int Profit => revenue - cost;
        public int RushProfit => rushRevenue - cost;
    }

    private sealed class Slot
    {
        public string id;
        public VisualElement root, tile, dot;
        public Label line1, sub;
    }

    private static OrderFlowWidget _instance;

    private readonly Queue<string> _offerFlyQueue = new Queue<string>();
    private readonly Queue<TileModel> _poQueue = new Queue<TileModel>();
    private readonly List<string> _order = new List<string>();                         // offer ids, newest first
    private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();
    private readonly HashSet<string> _flying = new HashSet<string>();
    private float _nextOfferAt, _nextPoAt, _nextTry, _nextPoll;
    private bool _synced;

    // Start-of-session intro (Tad, 2026-11-08): on a new game or a load, once the loading screen is gone, every open offer (the visible
    // ones) and pending inbound PO flies in the way a freshly posted one does, instead of the row simply being there.
    private bool _introRequested, _introActive;
    private float _introRequestedAt = -100f, _lastIntroAt = -100f;

    private UIDocument _doc, _fxDoc;
    private GameObject _fxGo;
    private VisualElement _docRoot, _fxRoot, _card, _row, _fxLayer;
    private Label _countLabel, _emptyLabel, _moreChip;
    private DockableHudCard _dock;
    private bool _built;

    // ── Lifecycle ────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[OrderFlowWidget]");
        DontDestroyOnLoad(go);
        go.AddComponent<OrderFlowWidget>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void OnEnable()
    {
        OrderArrivalService.OnOfferPosted -= HandleOffer;
        OrderArrivalService.OnOfferPosted += HandleOffer;
        ShipmentService.OnPurchaseOrderCreated -= HandlePo;
        ShipmentService.OnPurchaseOrderCreated += HandlePo;
        GameContext.OnWorldReady -= RequestIntro;
        GameContext.OnWorldReady += RequestIntro;
    }

    private void OnDisable()
    {
        OrderArrivalService.OnOfferPosted -= HandleOffer;
        ShipmentService.OnPurchaseOrderCreated -= HandlePo;
        GameContext.OnWorldReady -= RequestIntro;
    }

    private void OnDestroy()
    {
        BuildMenuUI.OnActiveBarChanged -= FollowActiveBar;
        if (_fxGo != null) Destroy(_fxGo);
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!_built)
        {
            // Throttled: the lookups walk the scene, and the tray only exists once the Main scene's HUD is up.
            if (Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 0.5f;
            TryBuild();
            return;
        }

        float now = Time.unscaledTime;

        // The intro waits for the loading screen to be gone (so it plays on the screen the player actually sees) and for the row to be
        // laid out (the flights aim at its real position).
        if (_introRequested && LoadingScreenManager.Instance == null && now >= _introRequestedAt + 0.6f && _row != null && _row.worldBound.width > 1f)
        {
            _introRequested = false;
            StartIntro();
        }

        if (now >= _nextPoll) { _nextPoll = now + PollSeconds; Refresh(); }

        float stagger = _introActive ? IntroStaggerSeconds : StaggerSeconds;
        if (_offerFlyQueue.Count > 0 && now >= _nextOfferAt)
        {
            SpawnOfferFlight(_offerFlyQueue.Dequeue());
            _nextOfferAt = now + stagger;
        }
        if (_poQueue.Count > 0 && now >= _nextPoAt)
        {
            SpawnInbound(_poQueue.Dequeue());
            _nextPoAt = now + stagger;
        }

        // The last intro tile has landed: let the normal sync take over (adds any offers beyond the visible ones, the "+N" chip, the count).
        if (_introActive && !_introRequested && _offerFlyQueue.Count == 0 && _flying.Count == 0)
        {
            _introActive = false;
            Refresh();
        }
    }

    // ── Start-of-session intro ───────────────────────────────────────

    /// <summary>GameContext.OnWorldReady: a new game was set up or a save restored. Debounced (a boot-load raises it twice).</summary>
    private void RequestIntro()
    {
        if (Time.unscaledTime - _lastIntroAt < 2f) return;
        _introRequested = true;
        _introRequestedAt = Time.unscaledTime;
        _introActive = true;   // from now until the flights are done Refresh() leaves the row alone, so no tile shows before it has flown in
        ClearRowForIntro();
    }

    /// <summary>Empties the row (tiles left over from before a load describe a world that is gone) and hides the empty-text and "+N"
    /// chip, so the row stays blank behind the loading screen until the first tile flies in.</summary>
    private void ClearRowForIntro()
    {
        foreach (var s in _slots.Values) s.root.RemoveFromHierarchy();
        _slots.Clear(); _order.Clear(); _flying.Clear();
        _offerFlyQueue.Clear(); _poQueue.Clear();
        if (_emptyLabel != null) _emptyLabel.style.display = DisplayStyle.None;
        if (_moreChip != null) _moreChip.style.display = DisplayStyle.None;
        if (_countLabel != null) _countLabel.text = "";
    }

    private void StartIntro()
    {
        _lastIntroAt = Time.unscaledTime;
        ClearRowForIntro();

        var offers = ServiceLocator.TryGet(out OrderArrivalService arrivals) && arrivals != null
            ? arrivals.AvailableOffers.Where(o => o != null).ToList()
            : new List<ContractData>();

        // Each flight lands at the FRONT of the row, so the oldest visible offer flies first and the newest lands last, front and centre.
        foreach (var id in offers.Take(MaxTiles).Select(o => o.ContractId).Reverse()) _offerFlyQueue.Enqueue(id);

        // Inbound POs still on their way (in transit or delayed) fly into the dock schedule widget, soonest arrival first.
        if (ServiceLocator.TryGet(out ShipmentService shipments) && shipments != null)
        {
            foreach (var s in shipments.PendingShipments
                         .Where(x => x != null && (x.Status == ShipmentData.ShipmentStatus.InTransit || x.Status == ShipmentData.ShipmentStatus.Delayed))
                         .OrderBy(x => x.ArrivalDayNumber).Take(IntroMaxPos))
                _poQueue.Enqueue(PoModel(s));
        }

        _countLabel.text = $"{offers.Count} available";
        _emptyLabel.style.display = offers.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        _nextOfferAt = _nextPoAt = Time.unscaledTime;
        _introActive = true;   // Refresh() stays out of the way until the last tile lands
    }

    // ── Events ───────────────────────────────────────────────────────

    private void HandleOffer(ContractData c)
    {
        if (c == null) return;
        _offerFlyQueue.Enqueue(c.ContractId);
        while (_offerFlyQueue.Count > MaxQueued) _offerFlyQueue.Dequeue();
    }

    private void HandlePo(ShipmentData s)
    {
        if (s == null) return;
        _poQueue.Enqueue(PoModel(s));
        while (_poQueue.Count > MaxQueued) _poQueue.Dequeue();
    }

    /// <summary>The flyer for one purchase order (a new PO, or a pending one replayed by the start-of-session intro).</summary>
    private static TileModel PoModel(ShipmentData s)
    {
        Sprite icon = null;
        if (!string.IsNullOrEmpty(s.SupplierId)) icon = VendorRegistry.Load()?.GetById(s.SupplierId)?.Icon;
        return new TileModel
        {
            title = string.IsNullOrEmpty(s.SupplierName) ? "Vendor" : s.SupplierName,
            tag = "INBOUND PO",
            accent = InboundColor,
            line1 = $"{s.TotalUnits:N0} cases", line1Color = InboundColor,
            sub = $"PO {s.PONumber} · Day {s.ArrivalDayNumber}", subColor = DimColor,
            icon = icon,
        };
    }

    // ── Build ────────────────────────────────────────────────────────

    private void TryBuild()
    {
        var hud = BuildMenuUI.Instance;
        if (hud == null || hud.WidgetTray == null || hud.TrayZone(DockableHudCard.ZoneRight) == null) return;

        if (_doc == null)
        {
            var ps = SharedPanelSettings.Find();
            if (ps == null) return;
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = ps;
            _doc.sortingOrder = 100;   // same layer as the systems log: this document only holds the card while it is floating

            // The flying tiles get their OWN document, above every window: the Contracts panel and the other windows are built
            // into the HUD document (sorting 999999 / UILayers.WindowAboveHud), so a layer at the card's own sorting order
            // would have the tile slide UNDER an open window. Still below confirmation modals (+10) and toasts.
            // A ROOT object, deliberately not a child of this one: Unity nests a UIDocument that sits on a child GameObject INSIDE its
            // parent's document and ignores its sorting order, which drew the flyers under every window (found 2026-11-08).
            // Built the way UIToast builds its layer: INACTIVE first, panelSettings assigned, THEN activated. A UIDocument builds its tree
            // on enable, and one enabled with no panel settings and configured afterwards made UI Toolkit assert in RepaintPanels the next
            // time another window opened ("VisualElements cannot change their render data...", found 2026-11-08).
            var fxGo = new GameObject("[OrderFlowFx]");
            fxGo.SetActive(false);
            DontDestroyOnLoad(fxGo);
            _fxGo = fxGo;
            _fxDoc = fxGo.AddComponent<UIDocument>();
            _fxDoc.panelSettings = ps;
            _fxDoc.sortingOrder = UILayers.WindowAboveHud + 5f;
            fxGo.SetActive(true);
        }
        _docRoot = _doc.rootVisualElement;
        _fxRoot = _fxDoc != null ? _fxDoc.rootVisualElement : null;
        if (_docRoot == null || _fxRoot == null) return;
        _docRoot.pickingMode = PickingMode.Ignore;
        _fxRoot.pickingMode = PickingMode.Ignore;

        BuildUI();

        BuildMenuUI.OnActiveBarChanged -= FollowActiveBar;
        BuildMenuUI.OnActiveBarChanged += FollowActiveBar;

        // First run: dock just LEFT of whatever is already in the right zone (the FPS card), not at its far edge.
        var zone = hud.TrayZone(DockableHudCard.ZoneRight);
        if (!PlayerPrefs.HasKey(PrefKey + "_Floating") && zone != null) _dock.DockInto(zone, 0);
        else _dock.Restore();

        _built = true;
        Refresh();
    }

    private void FollowActiveBar(VisualElement bar) => _dock?.EnsureInTray();

    private float _lastPressAt = -10f;
    private Vector2 _lastPressPos;
    private const float DoubleClickSeconds = 0.45f, DoubleClickSlop = 14f;

    private void OnCardPointerDown(PointerDownEvent e)
    {
        if (e.button != 0) return;

        // Own timing rather than trusting PointerDownEvent.clickCount alone: through the Input System UI module it was not reliably 2
        // for a quick double press (found 2026-11-08), so two presses close in time and place count too.
        float now = Time.unscaledTime;
        Vector2 pos = e.position;
        bool dbl = e.clickCount >= 2 || (now - _lastPressAt <= DoubleClickSeconds && (pos - _lastPressPos).sqrMagnitude <= DoubleClickSlop * DoubleClickSlop);
        _lastPressAt = dbl ? -10f : now;
        _lastPressPos = pos;
        if (!dbl) return;

        string offerId = null;
        if (e.target is VisualElement hit)
        {
            foreach (var kv in _slots)
            {
                var tile = kv.Value.tile;
                if (tile != null && (tile == hit || tile.Contains(hit))) { offerId = kv.Key; break; }
            }
        }
        OpenOutboundOrders(offerId);
    }

    /// <summary>Opens the Outbound Orders screen (the Contracts panel, key 8): on the New Contracts board with that offer outlined when a
    /// tile was double-clicked, otherwise just open. Same route a Systems Log contract link takes.</summary>
    private static void OpenOutboundOrders(string offerId)
    {
        var topBar = FindAnyObjectByType<TopBarUI>();
        var panel = topBar != null ? topBar.ContractsPanel : null;
        if (panel == null) { UIToast.Show("Couldn't open Outbound Orders — it isn't loaded."); return; }
        UIKeyBindingManager.Instance?.CloseAll();
        if (!string.IsNullOrEmpty(offerId)) panel.ShowOffer(offerId); else panel.Show();
    }

    private void BuildUI()
    {
        _card = new VisualElement { name = "OrderFlowWidget" };
        _card.style.flexShrink = 0;
        _card.style.flexDirection = FlexDirection.Column;
        _card.style.alignSelf = Align.Center;
        _card.style.overflow = Overflow.Hidden;
        _card.style.paddingLeft = 8; _card.style.paddingRight = 8;
        _card.style.paddingTop = 5; _card.style.paddingBottom = 5;
        _card.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.92f);
        SetRadius(_card, 8f);

        var head = new VisualElement { pickingMode = PickingMode.Ignore };
        head.style.flexDirection = FlexDirection.Row;
        head.style.justifyContent = Justify.SpaceBetween;
        head.style.alignItems = Align.Center;
        head.style.height = 16;
        head.style.flexShrink = 0;
        head.Add(L("OPEN ORDERS", 10, new Color(0.60f, 0.70f, 0.85f, 1f), true));
        _countLabel = L("0 available", 10, DimColor, false);
        _countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        head.Add(_countLabel);
        _card.Add(head);

        // Tile row. Slots go in at index 0 (newest first); clipped so a 4th slot mid-flight never spills out of the card.
        _row = new VisualElement { pickingMode = PickingMode.Ignore };
        _row.style.flexDirection = FlexDirection.Row;
        _row.style.alignItems = Align.FlexStart;
        _row.style.overflow = Overflow.Hidden;
        _row.style.height = TileH;
        _row.style.marginTop = 2;
        _row.style.flexShrink = 0;
        _emptyLabel = L("No open orders right now", 11, DimColor, false);
        _emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        _emptyLabel.style.alignSelf = Align.Center;
        _emptyLabel.style.marginLeft = 4;
        _emptyLabel.style.marginTop = 24;
        _row.Add(_emptyLabel);
        _moreChip = L("+0", 12, DimColor, true);
        _moreChip.style.width = ChipW; _moreChip.style.height = TileH;
        _moreChip.style.unityTextAlign = TextAnchor.MiddleCenter;
        _moreChip.style.display = DisplayStyle.None;
        _row.Add(_moreChip);
        _card.Add(_row);

        // Double-click (the card or a tile) opens the Outbound Orders screen (Tad, 2026-11-08); on a tile it opens on that offer.
        // Trickle-down so it still sees the press when the dock's drag handling captures the pointer.
        _card.RegisterCallback<PointerDownEvent>(OnCardPointerDown, TrickleDown.TrickleDown);

        // Fly layer: the whole panel, click-through, in the fly document (above every window).
        _fxLayer = new VisualElement { name = "OrderFlowFx", pickingMode = PickingMode.Ignore };
        _fxLayer.style.position = Position.Absolute;
        _fxLayer.style.left = 0; _fxLayer.style.top = 0; _fxLayer.style.right = 0; _fxLayer.style.bottom = 0;
        _fxRoot.Add(_fxLayer);

        _dock = new DockableHudCard(_card, _card, () => _docRoot, PrefKey,
            DockableHudCard.ZoneRight, defaultDocked: true, defaultFloatPos: new Vector2(24f, 150f),
            applyLayout: ApplyLayout, pickableWhenFloating: true);
        ApplyLayout(false);
    }

    private void ApplyLayout(bool docked)
    {
        if (_card == null) return;
        _card.style.width = CardW;
        _card.style.height = CardH;
        _card.style.marginLeft = docked ? 8 : 0;
        _card.style.marginRight = docked ? 8 : 0;
        _card.style.marginTop = docked ? 4 : 0;
        _card.style.marginBottom = docked ? 4 : 0;
        SetBorder(_card, docked ? new Color(1f, 1f, 1f, 0.14f) : new Color(0.22f, 0.30f, 0.45f, 1f), 1f);
    }

    // ── Keeping the row in step with the board ───────────────────────

    /// <summary>Polls the board (cheap: a few offers), keeps <see cref="_order"/> newest-first, and refreshes the tile text in
    /// place. Tiles are only added/removed when the set changes, so a tooltip is never torn down under the pointer.</summary>
    private void Refresh()
    {
        if (!_built || _row == null || _introActive) return;   // during the intro the flights build the row; Update() calls this again when they are done
        if (!ServiceLocator.TryGet(out OrderArrivalService arrivals) || arrivals == null) return;

        var available = arrivals.AvailableOffers.Where(o => o != null).ToList();
        var ids = new HashSet<string>(available.Select(o => o.ContractId));

        _order.RemoveAll(id => !ids.Contains(id) && !_flying.Contains(id));
        foreach (var o in available)
            if (!_order.Contains(o.ContractId)) _order.Add(o.ContractId);   // not announced (first sync / unlocked quietly): catalog order
        _synced = true;

        SyncRow(available);

        _countLabel.text = $"{available.Count} available";
        _emptyLabel.style.display = _order.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        int extra = _order.Count - MaxTiles;
        _moreChip.style.display = extra > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (extra > 0) _moreChip.text = $"+{extra}";
    }

    private void SyncRow(List<ContractData> available)
    {
        var byId = available.ToDictionary(o => o.ContractId, o => o);
        BuildMaps(out var committed, out var inbound);
        ServiceLocator.TryGet(out InventoryService inv);

        int wanted = MaxTiles + _flying.Count;
        var visible = _order.Take(wanted).ToList();

        // Drop slots that fell off the end or whose offer was accepted / expired.
        foreach (var id in _slots.Keys.ToList())
        {
            if (visible.Contains(id)) continue;
            if (_flying.Contains(id)) continue;
            _slots[id].root.RemoveFromHierarchy();
            _slots.Remove(id);
        }

        for (int i = 0; i < visible.Count; i++)
        {
            string id = visible[i];
            if (!byId.TryGetValue(id, out var offer)) continue;   // flying but no longer offered: lands, then goes next poll
            var model = ModelFor(offer, ComputeStats(offer, inv, committed, inbound));
            if (!_slots.TryGetValue(id, out var slot))
            {
                slot = MakeSlot(id, model, TileW + TileGap);
                _slots[id] = slot;
            }
            else UpdateSlot(slot, model);
            if (_row.IndexOf(slot.root) != i) _row.Insert(i, slot.root);
        }
    }

    private Slot MakeSlot(string id, TileModel model, float width)
    {
        var s = new Slot { id = id };
        s.root = new VisualElement { pickingMode = PickingMode.Ignore };
        s.root.style.height = TileH;
        s.root.style.width = width;
        s.root.style.flexShrink = 0;
        s.root.style.overflow = Overflow.Hidden;
        s.tile = MakeTile(model, true, out s.dot, out s.line1, out s.sub);
        s.tile.pickingMode = PickingMode.Position;   // hoverable: the stock / profit tooltip
        RuntimeTooltip.AttachRich(s.tile, () => BuildOfferTooltip(id), 480f, ignoreGlobalToggle: true);
        s.root.Add(s.tile);
        return s;
    }

    private static void UpdateSlot(Slot s, TileModel m)
    {
        if (s.line1 != null) { s.line1.text = m.line1; s.line1.style.color = m.line1Color; }
        if (s.sub != null) { s.sub.text = m.sub; s.sub.style.color = m.subColor; }
        if (s.dot != null) s.dot.style.backgroundColor = m.dot;
    }

    // ── Measuring an offer against the warehouse ─────────────────────

    /// <summary>committed = cases still owed to open customer orders; inbound = cases still to arrive on open POs
    /// (same rule as the Scheduler's item table: in transit / receiving / delayed, minus what is already received).</summary>
    private static void BuildMaps(out Dictionary<string, int> committed, out Dictionary<string, int> inbound)
    {
        committed = new Dictionary<string, int>();
        inbound = new Dictionary<string, int>();

        if (ServiceLocator.TryGet(out OrderService orders) && orders != null)
        {
            foreach (var o in orders.ActiveOrders)
            {
                if (o == null || o.Status == OrderData.OrderStatus.Cancelled || o.Status == OrderData.OrderStatus.Shipped) continue;
                foreach (var li in o.LineItems)
                {
                    if (li == null || string.IsNullOrEmpty(li.SkuId)) continue;
                    int rem = li.QuantityRemaining;
                    if (rem > 0) Add(committed, li.SkuId, rem);
                }
            }
        }

        if (ServiceLocator.TryGet(out ShipmentService shipments) && shipments != null)
        {
            foreach (var s in shipments.PendingShipments)
            {
                if (s == null) continue;
                if (s.Status != ShipmentData.ShipmentStatus.InTransit && s.Status != ShipmentData.ShipmentStatus.Receiving &&
                    s.Status != ShipmentData.ShipmentStatus.Delayed) continue;
                foreach (var li in s.LineItems)
                {
                    if (li == null || li.Dropped || string.IsNullOrEmpty(li.SkuId)) continue;
                    int rem = li.Quantity - li.ReceivedQuantity;
                    if (rem > 0) Add(inbound, li.SkuId, rem);
                }
            }
        }
    }

    private static void Add(Dictionary<string, int> d, string key, int v)
    {
        d.TryGetValue(key, out int cur);
        d[key] = cur + v;
    }

    private static int Get(Dictionary<string, int> d, string key) => d.TryGetValue(key, out int v) ? v : 0;

    /// <summary>Bulk offers only: the rolled preview lines, priced exactly as <c>OrderArrivalService.GenerateBulk</c> will build the
    /// order (cost of goods + 10%, goods revenue x the pallet-pick dial, doubled for a same-day rush). Handling fees are not included.</summary>
    private static OfferStats ComputeStats(ContractData c, InventoryService inv, Dictionary<string, int> committed, Dictionary<string, int> inbound)
    {
        var st = new OfferStats();
        var lines = c.BulkPreviewLines;
        if (!c.IsBulk || lines == null || lines.Count == 0) return st;

        float rev = 0f;
        foreach (var line in lines)
        {
            if (line == null || string.IsNullOrEmpty(line.SkuId)) continue;
            var sku = inv?.GetSkuData(line.SkuId);
            var sl = new StockLine
            {
                skuId = line.SkuId, sku = sku, qty = line.Quantity,
                onHand = inv != null ? inv.TotalOnHand(line.SkuId) : 0,
                committed = Get(committed, line.SkuId),
                inbound = Get(inbound, line.SkuId),
            };
            st.lines.Add(sl);
            st.cases += line.Quantity;
            int per = sku != null && sku.Ti > 0 && sku.Hi > 0 ? sku.Ti * sku.Hi : 0;
            if (per > 0) st.pallets += Mathf.CeilToInt(line.Quantity / (float)per);
            if (sku != null)
            {
                int unitCost = Mathf.RoundToInt(sku.BuyValue);
                int unitSell = Mathf.RoundToInt(sku.BuyValue * OrderArrivalService.BulkSurchargeMultiplier);
                st.cost += line.Quantity * unitCost;
                rev += line.Quantity * unitSell * OrderData.PalletPickRevenueMultiplier;
            }
            if (sl.Final < 0) st.shortCases += -sl.Final;
            int status = sl.onHand - sl.committed >= sl.qty ? 0 : (sl.Final >= 0 ? 1 : 2);
            if (status > st.status) st.status = status;
        }
        st.revenue = Mathf.RoundToInt(rev);
        st.rushRevenue = Mathf.RoundToInt(rev * OrderData.SameDayRushRevenueMultiplier);
        return st;
    }

    private static Color StatusColor(int status) => status == 0 ? OkColor : status == 1 ? WarnColor : BadColor;

    private static TileModel ModelFor(ContractData c, OfferStats st)
    {
        string name = c.Customer != null ? c.Customer.CompanyName : "Customer";
        var m = new TileModel
        {
            title = name,
            icon = c.Customer != null ? c.Customer.Icon : null,
            showDot = c.IsBulk && st.lines.Count > 0,
        };

        if (c.IsBulk)
        {
            m.tag = c.IsSameDayRush ? "BULK · RUSH" : "BULK";
            m.accent = c.IsSameDayRush ? RushColor : BulkColor;
            if (st.lines.Count > 0)
            {
                m.line1 = $"{(st.Profit >= 0 ? "+" : "-")}${Mathf.Abs(st.Profit):N0}";
                m.line1Color = st.Profit >= 0 ? OkColor : BadColor;
                m.dot = StatusColor(st.status);
                m.sub = st.status == 0 ? $"{st.cases:N0} cs · in stock"
                      : st.status == 1 ? $"{st.cases:N0} cs · inbound"
                      : $"BUY {st.shortCases:N0} cs";
                m.subColor = StatusColor(st.status);
            }
            else
            {
                m.line1 = "Bulk order"; m.line1Color = BulkColor;
                m.sub = "items not rolled"; m.subColor = DimColor;
            }
        }
        else
        {
            m.tag = "RECURRING";
            m.accent = RecurringColor;
            m.line1 = $"~{c.EstimatedCasesPerDay:N0} cs/day"; m.line1Color = RecurringColor;
            m.sub = "negotiate"; m.subColor = DimColor;
        }
        return m;
    }

    /// <summary>The offer's own number. A real O-number is only issued when an order is created (on Accept), so until then this is a
    /// short stable code derived from the contract id.</summary>
    private static string OfferCode(ContractData c)
    {
        int h = 17;
        foreach (char ch in c.ContractId ?? "") h = h * 31 + ch;
        return (c.IsBulk ? "BO-" : "RC-") + (Mathf.Abs(h) % 10000).ToString("0000");
    }

    // ── Tooltip ──────────────────────────────────────────────────────

    private VisualElement BuildOfferTooltip(string id)
    {
        var body = new VisualElement { pickingMode = PickingMode.Ignore };
        body.style.minWidth = 440;

        ServiceLocator.TryGet(out OrderArrivalService arrivals);
        var c = arrivals != null ? arrivals.GetContract(id) : null;
        if (c == null)
        {
            body.Add(Wrap("This offer is no longer on the table.", 13, DimColor, false));
            return body;
        }

        ServiceLocator.TryGet(out InventoryService inv);
        BuildMaps(out var committed, out var inbound);
        var st = ComputeStats(c, inv, committed, inbound);

        // Header: customer icon, name, what it is.
        var head = new VisualElement { pickingMode = PickingMode.Ignore };
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.paddingBottom = 6;
        head.style.borderBottomWidth = 1;
        head.style.borderBottomColor = new Color(0.36f, 0.61f, 0.77f, 0.6f);
        head.Add(IconBox(c.Customer != null ? c.Customer.Icon : null, c.Customer != null ? c.Customer.CompanyName : "?", 40, 8));
        var hc = new VisualElement { pickingMode = PickingMode.Ignore };
        hc.style.flexGrow = 1; hc.style.flexShrink = 1;
        hc.Add(Wrap(c.Customer != null ? c.Customer.CompanyName : "(no customer)", 19, NameColor, true));
        string kind = c.IsBulk
            ? (c.IsSameDayRush ? "BULK ORDER · SAME-DAY RUSH (2x pay if shipped on time)" : $"BULK ORDER · ship within {c.LeadTimeDays} day(s)")
            : "RECURRING CONTRACT · negotiate to take it";
        hc.Add(Wrap(kind, 12, c.IsBulk ? (c.IsSameDayRush ? RushColor : BulkColor) : RecurringColor, true));
        hc.Add(Wrap($"Offer {OfferCode(c)}  (order number is issued when you accept)", 11, DimColor, false));
        head.Add(hc);
        body.Add(head);

        if (!c.IsBulk || st.lines.Count == 0)
        {
            body.Add(Wrap(c.IsBulk
                ? "The items for this order have not been rolled yet."
                : $"A standing account: ~{c.OrdersPerDayMin}-{c.OrdersPerDayMax} orders and ~{c.EstimatedCasesPerDay:N0} cases a day, " +
                  "rolled fresh every day, so there is no fixed item list to check against stock yet.", 12, TextColor, false));
            if (!c.IsBulk)
                body.Add(Wrap($"Late fee {c.LateFeePercent:P0}. Profit depends on what you stock and on the terms you negotiate.", 11, DimColor, false));
            return body;
        }

        // Column header.
        body.Add(Row(null, "ITEM", "BOH", "PENDING", "INBOUND", "ORDER", "FINAL", DimColor, true, 11));

        foreach (var l in st.lines)
        {
            string name = l.sku != null ? l.sku.ItemDescription : l.skuId;
            Color finalInk = l.Final < 0 ? BadColor : (l.onHand - l.committed >= l.qty ? OkColor : WarnColor);
            body.Add(Row(l.sku != null ? l.sku.Icon : null, name,
                         l.onHand.ToString("N0"), l.committed > 0 ? "-" + l.committed.ToString("N0") : "0",
                         l.inbound > 0 ? "+" + l.inbound.ToString("N0") : "0", l.qty.ToString("N0"),
                         l.Final.ToString("N0"), TextColor, false, 14, finalInk));
        }

        // What to buy.
        var buy = Wrap(st.shortCases > 0
                ? $"YOU WOULD NEED TO BUY {st.shortCases:N0} CASES ({BuyLine(st)})"
                : (st.status == 1 ? "COVERED, BUT ONLY ONCE THE INBOUND POs ARRIVE" : "ALL ON THE SHELF - NOTHING TO BUY"),
            13, StatusColor(st.status), true);
        buy.style.marginTop = 6;
        body.Add(buy);

        // Money.
        var money = new VisualElement { pickingMode = PickingMode.Ignore };
        money.style.marginTop = 6; money.style.paddingTop = 5;
        money.style.borderTopWidth = 1; money.style.borderTopColor = new Color(0.36f, 0.61f, 0.77f, 0.6f);
        money.Add(MoneyRow("Revenue (goods)", $"${st.revenue:N0}", TextColor, 14, false));
        money.Add(MoneyRow("Cost of goods", $"-${st.cost:N0}", TextColor, 14, false));
        money.Add(MoneyRow("PROFIT", $"{(st.Profit >= 0 ? "+" : "-")}${Mathf.Abs(st.Profit):N0}", st.Profit >= 0 ? OkColor : BadColor, 19, true));
        if (c.IsSameDayRush)
            money.Add(MoneyRow("If shipped same day (2x)", $"+${st.RushProfit:N0}", RushColor, 14, true));
        body.Add(money);
        body.Add(Wrap("Estimate: goods only. Pick and pallet handling fees and wages are not included.", 11, DimColor, false));
        return body;
    }

    private static string BuyLine(OfferStats st)
    {
        var parts = st.lines.Where(l => l.Final < 0)
            .Select(l => $"{(l.sku != null ? l.sku.ItemDescription : l.skuId)}: {-l.Final:N0}");
        return string.Join(", ", parts);
    }

    private static VisualElement Row(Sprite icon, string name, string boh, string pending, string inbound, string order, string final,
                                     Color ink, bool header, int size, Color? finalInk = null)
    {
        var r = new VisualElement { pickingMode = PickingMode.Ignore };
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems = Align.Center;
        r.style.marginTop = header ? 6 : 3;

        var left = new VisualElement { pickingMode = PickingMode.Ignore };
        left.style.flexDirection = FlexDirection.Row;
        left.style.alignItems = Align.Center;
        left.style.flexGrow = 1; left.style.flexShrink = 1;
        if (!header) left.Add(IconBox(icon, name, 22, 4));
        var n = Wrap(name, size, header ? ink : NameColor, !header);
        n.style.marginLeft = header ? 0 : 6;
        left.Add(n);
        r.Add(left);

        r.Add(Num(boh, ink, header, size));
        r.Add(Num(pending, header ? ink : DimColor, header, size));
        r.Add(Num(inbound, header ? ink : (inbound == "0" ? DimColor : WarnColor), header, size));
        r.Add(Num(order, ink, header, size));
        r.Add(Num(final, finalInk ?? ink, true, size));
        return r;
    }

    private static Label Num(string text, Color c, bool bold, int size)
    {
        var l = L(text, size, c, bold);
        l.style.width = 62;
        l.style.unityTextAlign = TextAnchor.MiddleRight;
        return l;
    }

    private static VisualElement MoneyRow(string label, string value, Color c, int size, bool bold)
    {
        var r = new VisualElement { pickingMode = PickingMode.Ignore };
        r.style.flexDirection = FlexDirection.Row;
        r.style.justifyContent = Justify.SpaceBetween;
        r.style.marginTop = 2;
        r.Add(Wrap(label, size, bold ? c : DimColor, bold));
        var v = Wrap(value, size, c, bold);
        v.style.unityTextAlign = TextAnchor.MiddleRight;
        r.Add(v);
        return r;
    }

    private static VisualElement IconBox(Sprite icon, string fallbackName, float size, float radius)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.width = size; e.style.height = size; e.style.flexShrink = 0;
        e.style.marginRight = size >= 30 ? 10 : 0;
        SetRadius(e, radius);
        e.style.backgroundColor = new Color(0.20f, 0.26f, 0.36f, 1f);
        if (icon != null)
        {
            e.style.backgroundImage = new StyleBackground(icon);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }
        else
        {
            var initial = L(string.IsNullOrEmpty(fallbackName) ? "?" : fallbackName.Substring(0, 1).ToUpperInvariant(),
                            Mathf.RoundToInt(size * 0.55f), DimColor, true);
            initial.style.unityTextAlign = TextAnchor.MiddleCenter;
            initial.style.width = size; initial.style.height = size;
            e.Add(initial);
        }
        return e;
    }

    /// <summary>A label that wraps (tooltip text), unlike <see cref="L"/> which is a one-line clipped label.</summary>
    private static Label Wrap(string text, int size, Color c, bool bold)
    {
        var l = L(text, size, c, bold);
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.overflow = Overflow.Visible;
        l.style.textOverflow = TextOverflow.Clip;
        return l;
    }

    // ── Tiles ────────────────────────────────────────────────────────

    private static VisualElement MakeTile(TileModel m, bool inSlot, out VisualElement dot, out Label line1, out Label sub)
    {
        var t = new VisualElement { pickingMode = PickingMode.Ignore };
        t.style.width = TileW;
        t.style.height = TileH;
        t.style.flexShrink = 0;
        t.style.marginRight = inSlot ? TileGap : 0;
        t.style.paddingLeft = 7; t.style.paddingRight = 4; t.style.paddingTop = 4; t.style.paddingBottom = 3;
        t.style.backgroundColor = new Color(0.11f, 0.15f, 0.23f, 1f);
        SetBorder(t, new Color(1f, 1f, 1f, 0.10f), 1f);
        t.style.borderLeftWidth = 4;
        t.style.borderLeftColor = m.accent;
        SetRadius(t, 6f);
        t.style.flexDirection = FlexDirection.Column;

        // Row 1: customer / vendor icon, what kind it is, and a stock-status dot.
        var top = new VisualElement { pickingMode = PickingMode.Ignore };
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        top.style.height = 20;
        top.style.marginBottom = 2;
        top.style.flexShrink = 0;
        var icon = IconBox(m.icon, m.title, 20, 4);
        icon.style.marginRight = 5;
        top.Add(icon);
        var tag = L(m.tag, 9, m.accent, true);
        tag.style.flexShrink = 1; tag.style.flexGrow = 1;
        top.Add(tag);
        dot = null;
        if (m.showDot)
        {
            dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.style.width = 8; dot.style.height = 8; dot.style.flexShrink = 0; dot.style.marginRight = 2;
            SetRadius(dot, 4f);
            dot.style.backgroundColor = m.dot;
            top.Add(dot);
        }
        t.Add(top);

        var name = L(m.title, 11, NameColor, true); name.style.height = 14; t.Add(name);
        line1 = L(m.line1, 13, m.line1Color, true); line1.style.height = 16; t.Add(line1);
        sub = L(m.sub, 9, m.subColor, false); sub.style.height = 11; t.Add(sub);
        return t;
    }

    private static Label L(string text, int size, Color c, bool bold)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size;
        l.style.color = c;
        l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.overflow = Overflow.Hidden;
        l.style.textOverflow = TextOverflow.Ellipsis;
        l.style.flexShrink = 0;
        l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
        l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        return l;
    }

    // ── Outbound: a new offer flies down into this widget ────────────

    private void SpawnOfferFlight(string id)
    {
        if (_row == null || _fxLayer == null) return;
        if (!ServiceLocator.TryGet(out OrderArrivalService arrivals) || arrivals == null) return;
        var offer = arrivals.AvailableOffers.FirstOrDefault(o => o != null && o.ContractId == id);
        if (offer == null || _flying.Contains(id) || _slots.ContainsKey(id)) return;   // not takeable right now, or already shown

        ServiceLocator.TryGet(out InventoryService inv);
        BuildMaps(out var committed, out var inbound);
        var model = ModelFor(offer, ComputeStats(offer, inv, committed, inbound));

        // The real tile sits in a slot that opens while the flyer is in the air, so the older tiles slide right smoothly instead
        // of jumping when it lands. The tile stays invisible until then.
        _order.Remove(id);
        _order.Insert(0, id);
        _flying.Add(id);
        var slot = MakeSlot(id, model, 0f);
        slot.tile.style.opacity = 0f;
        _slots[id] = slot;
        _row.Insert(0, slot.root);
        _emptyLabel.style.display = DisplayStyle.None;

        Rect rr = _row.worldBound, fr = _fxLayer.worldBound;
        if (rr.width < 1f || fr.width < 1f)
        {
            // Widget not on screen (hidden / not laid out): no flight, just show it.
            Land(id, slot, model.accent);
            return;
        }

        _fxLayer.BringToFront();
        var fly = MakeTile(model, false, out _, out _, out _);
        fly.style.position = Position.Absolute;
        fly.style.opacity = 0f;
        _fxLayer.Add(fly);

        float endX = rr.xMin - fr.xMin, endY = rr.yMin - fr.yMin;
        float startX = endX + 80f, startY = -TileH - 60f;
        fly.experimental.animation.Start(0f, 1f, OfferFlightMs, (e, t) =>
        {
            // A small hop as it lands, so it reads as "arriving", not just "stopping".
            float hop = Mathf.Sin(Mathf.Clamp01((t - 0.78f) / 0.22f) * Mathf.PI) * 7f;
            e.style.left = Mathf.Lerp(startX, endX, EaseOutCubic(t));
            e.style.top = Mathf.Lerp(startY, endY, EaseOutCubic(t)) - hop;
            e.style.opacity = Mathf.Clamp01(t * 7f);
            float sc = Mathf.Lerp(1.35f, 1f, EaseOutCubic(t));
            e.transform.scale = new Vector3(sc, sc, 1f);
            e.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(9f, 0f, EaseOutCubic(t)));
            slot.root.style.width = (TileW + TileGap) * EaseOutCubic(Mathf.Clamp01(t * 1.15f));
        });

        _fxLayer.schedule.Execute(() =>
        {
            fly.RemoveFromHierarchy();
            Land(id, slot, model.accent);
        }).ExecuteLater(OfferFlightMs + 20);
    }

    private void Land(string id, Slot slot, Color accent)
    {
        _flying.Remove(id);
        if (slot.root == null || slot.root.parent == null) return;
        slot.root.style.width = TileW + TileGap;
        slot.tile.style.opacity = 1f;
        Ring(slot.tile, accent);
        Refresh();   // trims the oldest tile off the end now the flight is over
    }

    // ── Inbound: a new PO flies down into the dock schedule widget ───

    private void SpawnInbound(TileModel m)
    {
        if (_fxLayer == null) return;

        var target = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.MiniTimelineRoot : null;
        Rect tr = target != null ? target.worldBound : default;
        if (target == null || tr.width < 1f) { target = _card; tr = _card != null ? _card.worldBound : default; }   // no schedule widget on screen
        Rect fr = _fxLayer.worldBound;
        if (target == null || tr.width < 1f || fr.width < 1f) return;

        _fxLayer.BringToFront();
        var fly = MakeTile(m, false, out _, out _, out _);
        fly.style.position = Position.Absolute;
        fly.style.opacity = 0f;
        _fxLayer.Add(fly);

        float endX = tr.center.x - fr.xMin - TileW * 0.5f;
        float endY = tr.center.y - fr.yMin - TileH * 0.5f;
        float startX = endX - 90f, startY = -TileH - 60f;
        const float swoopShare = 0.72f;

        fly.experimental.animation.Start(0f, 1f, PoFlightMs, (e, t) =>
        {
            float p = Mathf.Clamp01(t / swoopShare);                        // swoop in above the widget
            float u = Mathf.Clamp01((t - swoopShare) / (1f - swoopShare));  // then drop in and shrink into it
            float uu = u * u;
            float y = Mathf.Lerp(Mathf.Lerp(startY, endY - 70f, EaseOutCubic(p)), endY, uu);
            e.style.left = Mathf.Lerp(startX, endX, EaseOutCubic(Mathf.Clamp01(t / 0.85f)));
            e.style.top = y;
            e.style.opacity = Mathf.Clamp01(t * 7f) * (1f - uu);
            float sc = Mathf.Lerp(1.3f, 1f, EaseOutCubic(p)) * Mathf.Lerp(1f, 0.35f, uu);
            e.transform.scale = new Vector3(sc, sc, 1f);
            e.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 0f, EaseOutCubic(p)));
        });

        _fxLayer.schedule.Execute(() =>
        {
            fly.RemoveFromHierarchy();
            if (target != null && target.panel != null)
            {
                Pulse(target, 1.05f, 320);
                Ring(target, m.accent);
            }
        }).ExecuteLater(PoFlightMs + 20);
    }

    // ── Effects ──────────────────────────────────────────────────────

    /// <summary>A bordered, tinted overlay that fades out over <paramref name="target"/>: "something just landed here".</summary>
    private static void Ring(VisualElement target, Color c)
    {
        if (target == null) return;
        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.style.position = Position.Absolute;
        ring.style.left = 0; ring.style.top = 0; ring.style.right = 0; ring.style.bottom = 0;
        SetBorder(ring, c, 2f);
        SetRadius(ring, 7f);
        ring.style.backgroundColor = new Color(c.r, c.g, c.b, 0.20f);
        target.Add(ring);
        ring.experimental.animation.Start(0f, 1f, 700, (e, t) => e.style.opacity = 1f - t);
        ring.schedule.Execute(() => ring.RemoveFromHierarchy()).ExecuteLater(720);
    }

    /// <summary>Brief scale bump; restores the element's own scale rather than assuming identity.</summary>
    private static void Pulse(VisualElement e, float peak, int ms)
    {
        Vector3 baseScale = e.transform.scale;
        e.experimental.animation.Start(0f, 1f, ms, (el, t) =>
        {
            float s = Mathf.Lerp(1f, peak, Mathf.Sin(t * Mathf.PI));
            el.transform.scale = new Vector3(baseScale.x * s, baseScale.y * s, 1f);
        });
        e.schedule.Execute(() => e.transform.scale = baseScale).ExecuteLater(ms + 16);
    }

    private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
    private static float EaseOutQuart(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 4f); }

    private static void SetRadius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    private static void SetBorder(VisualElement e, Color c, float w)
    {
        e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
    }
}
