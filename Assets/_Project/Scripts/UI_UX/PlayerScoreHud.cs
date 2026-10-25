#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;

/// <summary>
/// The score display for a block the PLAYER is playing (HAL 9000 in observe-only mode, started from the Dev Console's HAL tab with
/// "I'LL PLAY (SCORE ME)").
///
///  - While you play: a big live score at the top centre, just under the Day box. It turns gold while you are ahead of HAL's best block.
///  - When the block ends: a results modal with HAL's best score in every category next to yours, and a fanfare if you beat HAL's best overall.
///
/// Self-bootstrapping; draws on the fanfare layer (above every panel and toast). Nothing here changes how anything is scored.
/// </summary>
public static class PlayerScoreHud
{
    const float HudWidth = 260f, HudHeight = 118f;
    static readonly Color Gold = new Color(1f, 0.82f, 0.25f), Green = new Color(0.4f, 0.92f, 0.5f), Ink = new Color(0.93f, 0.96f, 1f),
                          Dim = new Color(0.62f, 0.74f, 0.84f), Orange = new Color(0.71f, 0.45f, 0.23f), Navy = new Color(0.08f, 0.11f, 0.16f, 0.95f);

    static VisualElement _hud, _modal;
    static Label _title, _score, _sub;
    static Label _timeLabel;
    static float _halBest = -1f; static int _halBestBlock; static int _bestForBlockNo = -1;
    static GameObject _driver;

    // ── bootstrap ─────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        _hud = null; _modal = null; _timeLabel = null; _bestForBlockNo = -1;   // statics outlive Play sessions (domain reload is off)
        PlaytestBot.BlockFinished -= OnBlockFinished;
        PlaytestBot.BlockFinished += OnBlockFinished;
        if (_driver != null) UnityEngine.Object.Destroy(_driver);
        _driver = new GameObject("[PlayerScoreHud]") { hideFlags = HideFlags.HideInHierarchy };
        _driver.AddComponent<Ticker>();
        UnityEngine.Object.DontDestroyOnLoad(_driver);
    }

    class Ticker : MonoBehaviour
    {
        float _next;
        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            try { Tick(); } catch (Exception e) { Debug.LogWarning("[PlayerScoreHud] " + e.Message); _next = Time.unscaledTime + 5f; }
        }
    }

    // ── the live score ────────────────────────────────────────────────────────

    static void Tick()
    {
        var bot = PlaytestBot.Instance;
        bool show = bot != null && bot.Running && bot.ObserveOnly;
        if (!show) { if (_hud != null) _hud.style.display = DisplayStyle.None; return; }

        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        if (_hud == null || _hud.panel == null) BuildHud(layer);

        if (_bestForBlockNo != bot.BlockNo)
        {
            _bestForBlockNo = bot.BlockNo;
            var hal = bot.LoadHalHistory();
            var best = hal.Count > 0 ? hal.OrderByDescending(b => b.score).First() : null;
            _halBest = best != null ? best.score : -1f; _halBestBlock = best != null ? best.block : 0;
        }

        _hud.style.display = DisplayStyle.Flex;
        float s = bot.LiveScore;
        _score.text = s.ToString("0.0");
        bool ahead = _halBest >= 0f && s > _halBest;
        _score.style.color = ahead ? Gold : Color.white;
        _title.text = ahead ? "YOUR SCORE  -  BEATING HAL!" : "YOUR SCORE";
        _title.style.color = ahead ? Gold : Dim;
        _sub.text = _halBest >= 0f ? $"HAL's best: {_halBest:0.0}  (block {_halBestBlock})" : "HAL has no recorded block yet";
        _hud.style.borderTopColor = _hud.style.borderBottomColor = _hud.style.borderLeftColor = _hud.style.borderRightColor = ahead ? Gold : Orange;
        Place(layer);
    }

    // Centred under the Day box: the box's own bounds drive it, so it follows the top bar if that moves or rescales.
    static void Place(VisualElement layer)
    {
        if (_timeLabel == null || _timeLabel.panel == null)
        {
            var tb = UnityEngine.Object.FindAnyObjectByType<TopBarUI>();
            var doc = tb != null ? tb.GetComponent<UIDocument>() : null;
            _timeLabel = doc != null && doc.rootVisualElement != null ? doc.rootVisualElement.Q<Label>("TimeLabel") : null;
        }
        float left, top;
        if (_timeLabel != null && _timeLabel.worldBound.width > 1f)
        {
            var box = _timeLabel.parent != null && _timeLabel.parent.worldBound.width > 1f ? _timeLabel.parent : _timeLabel;
            var wb = box.worldBound;
            left = wb.center.x - HudWidth * 0.5f; top = wb.yMax + 8f;
        }
        else { left = (layer.resolvedStyle.width - HudWidth) * 0.5f; top = 74f; }
        _hud.style.left = Mathf.Max(0f, left);
        _hud.style.top = Mathf.Max(0f, top);
    }

    static Label L(int size, Color col, bool bold)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.flexShrink = 0;
        l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        l.style.paddingTop = l.style.paddingBottom = l.style.paddingLeft = l.style.paddingRight = 0;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (HalDevTabs.HalFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(HalDevTabs.HalFont);
        return l;
    }

    static void BuildHud(VisualElement layer)
    {
        _hud = new VisualElement { name = "PlayerScoreHud", pickingMode = PickingMode.Ignore };
        _hud.style.position = Position.Absolute;
        _hud.style.width = HudWidth; _hud.style.height = HudHeight;
        _hud.style.backgroundColor = Navy;
        _hud.style.borderTopWidth = _hud.style.borderBottomWidth = _hud.style.borderLeftWidth = _hud.style.borderRightWidth = 3;
        _hud.style.borderTopLeftRadius = _hud.style.borderTopRightRadius = _hud.style.borderBottomLeftRadius = _hud.style.borderBottomRightRadius = 10;
        _hud.style.alignItems = Align.Center; _hud.style.justifyContent = Justify.Center;
        _title = L(13, Dim, true); _title.style.height = 20;
        _score = L(62, Color.white, true); _score.style.height = 70; _score.style.width = HudWidth - 16;
        _sub = L(12, Dim, false); _sub.style.height = 18;
        _hud.Add(_title); _hud.Add(_score); _hud.Add(_sub);
        layer.Add(_hud);
        _hud.BringToFront();
    }

    // ── the results ───────────────────────────────────────────────────────────

    struct Cat { public string name; public Func<PlaytestBlockResult, float> get; }
    static readonly Cat[] Cats =
    {
        new Cat { name = "On-time delivery", get = r => r.onTime },
        new Cat { name = "Fill rate",        get = r => r.fill },
        new Cat { name = "Throughput",       get = r => r.throughput },
        new Cat { name = "Revenue",          get = r => r.revenue },
        new Cat { name = "Profit",           get = r => r.profit },
        new Cat { name = "Cash health",      get = r => r.cash },
        new Cat { name = "Reliability",      get = r => r.reliability },
        new Cat { name = "Reputation",       get = r => r.reputation },
    };

    static void OnBlockFinished(PlaytestBlockResult mine, bool observeOnly)
    {
        if (!observeOnly || mine == null) return;
        try { ShowResults(mine); } catch (Exception e) { Debug.LogWarning("[PlayerScoreHud] results: " + e.Message); }
    }

    static void ShowResults(PlaytestBlockResult mine)
    {
        var layer = ShippedFanfare.LayerRoot();
        var bot = PlaytestBot.Instance;
        if (layer == null || bot == null) { UIToast.Show($"Your block is done: {mine.score:0.0}"); return; }
        if (_hud != null) _hud.style.display = DisplayStyle.None;
        _modal?.RemoveFromHierarchy();

        var hal = bot.LoadHalHistory();
        PlaytestBlockResult Best(Func<PlaytestBlockResult, float> f) => hal.Count > 0 ? hal.OrderByDescending(f).First() : null;
        var bestOverall = Best(r => r.score);
        bool beatOverall = bestOverall != null && mine.score > bestOverall.score;
        int beatenCats = 0;

        _modal = new VisualElement { name = "PlayerResultsModal", pickingMode = PickingMode.Position };
        _modal.style.position = Position.Absolute;
        _modal.style.left = 0; _modal.style.right = 0; _modal.style.top = 0; _modal.style.bottom = 0;
        _modal.style.backgroundColor = new Color(0f, 0f, 0f, 0.62f);
        _modal.style.alignItems = Align.Center; _modal.style.justifyContent = Justify.Center;

        var card = new VisualElement();
        card.style.width = 820;
        card.style.backgroundColor = Navy;
        card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 4;
        card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = beatOverall ? Gold : Orange;
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 14;
        card.style.paddingTop = 22; card.style.paddingBottom = 22; card.style.paddingLeft = 30; card.style.paddingRight = 30;
        _modal.Add(card);

        var title = L(34, beatOverall ? Gold : Ink, true); title.text = beatOverall ? "YOU BEAT HAL 9000!" : "BLOCK COMPLETE"; title.style.height = 46; card.Add(title);
        var big = L(76, beatOverall ? Gold : Color.white, true); big.text = mine.score.ToString("0.0"); big.style.height = 90; card.Add(big);
        var vs = L(15, Dim, false);
        vs.text = bestOverall != null ? $"HAL 9000's best: {bestOverall.score:0.0}  (block {bestOverall.block})     -     your margin: {mine.score - bestOverall.score:+0.0;-0.0;0.0}" : "HAL 9000 has no recorded block to compare with";
        vs.style.height = 24; vs.style.marginBottom = 12; card.Add(vs);

        card.Add(Row("CATEGORY", "HAL BEST", "YOU", "", Dim, true));
        foreach (var c in Cats)
        {
            var b = Best(c.get);
            float halV = b != null ? c.get(b) : -1f, youV = c.get(mine);
            bool win = b != null && youV > halV;
            if (win) beatenCats++;
            card.Add(Row(c.name, b != null ? $"{halV:0.0}  (blk {b.block})" : "-", $"{youV:0.0}", win ? "BEAT HAL" : "", win ? Green : Ink, false));
        }
        var line = new VisualElement(); line.style.height = 2; line.style.backgroundColor = Orange; line.style.marginTop = 6; line.style.marginBottom = 6; card.Add(line);
        card.Add(Row("OVERALL SCORE", bestOverall != null ? $"{bestOverall.score:0.0}  (blk {bestOverall.block})" : "-", $"{mine.score:0.0}", beatOverall ? "BEAT HAL" : "", beatOverall ? Gold : Ink, true));

        var foot = L(14, Dim, false);
        foot.text = $"Orders shipped {mine.ordersShipped}   Late {mine.ordersLate}   Pallets {mine.palletsShipped}   Revenue ${mine.revenueTotal:N0}   Bug incidents {mine.bugIncidents}" +
                    (beatenCats > 0 ? $"   -   you beat HAL in {beatenCats} of {Cats.Length} categories" : "");
        foot.style.height = 26; foot.style.marginTop = 10; foot.style.whiteSpace = WhiteSpace.Normal; card.Add(foot);

        var btn = new Button(() => { _modal?.RemoveFromHierarchy(); _modal = null; Time.timeScale = 1f; }) { text = "CONTINUE" };
        btn.style.height = 48; btn.style.marginTop = 14; btn.style.fontSize = 22; btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.backgroundColor = Orange; btn.style.color = Color.white;
        btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 8;
        if (HalDevTabs.HalFont != null) btn.style.unityFontDefinition = FontDefinition.FromFont(HalDevTabs.HalFont);
        card.Add(btn);

        layer.Add(_modal);
        _modal.BringToFront();

        // The fanfare comes a beat after the card so it lands ON the screen, not behind the fade-in.
        if (beatOverall)
            _modal.schedule.Execute(() => ShippedFanfare.ShowBanner("YOU BEAT HAL 9000!", Gold)).ExecuteLater(350);
        else if (beatenCats > 0)
            UIToast.Show($"You beat HAL 9000 in {beatenCats} categories!", 4f);
    }

    static VisualElement Row(string cat, string hal, string you, string tag, Color col, bool bold)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.height = 32; row.style.flexShrink = 0;
        VisualElement Cell(string t, float w, TextAnchor a)
        {
            var l = L(bold ? 18 : 17, col, bold); l.text = t; l.style.width = w; l.style.height = 32; l.style.unityTextAlign = a; return l;
        }
        row.Add(Cell(cat, 270, TextAnchor.MiddleLeft));
        row.Add(Cell(hal, 210, TextAnchor.MiddleRight));
        row.Add(Cell(you, 120, TextAnchor.MiddleRight));
        row.Add(Cell(tag, 150, TextAnchor.MiddleRight));
        return row;
    }
}
#endif
