using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// When HAL / the playtest bot finds a bug that involves something you can SEE (a worker, a machine, a trailer, a door, a pallet),
    /// pause the game, fly the camera to 3 m from it and put the full description in the toast.
    /// The object is found from the bug text: a world position "(x, y, z)", or an order number "OG1234".
    /// A bug that names neither still gets the toast (no pause: there is nothing to look at).
    /// </summary>
    public static class BugFocus
    {
        public const float ViewDistance = 3f;
        const float ToastSeconds = 20f;          // UIToast doubles this
        const float MinGapSeconds = 8f;          // real seconds between two focuses, so a burst of bugs cannot yank the camera around
        static float _lastFocus = -999f;
        static FreeLookCamera _cam;

        static readonly Regex PosRx = new(@"\((-?\d+(?:\.\d+)?), (-?\d+(?:\.\d+)?), (-?\d+(?:\.\d+)?)\)", RegexOptions.Compiled);
        static readonly Regex OrderRx = new(@"\bOG\d+\b", RegexOptions.Compiled);

        public static void Show(string category, string text, bool pauseAndZoom)
        {
            string body = $"BUG: {category}\n{text}";
            Vector3 focus = default; string what = null;
            bool found = false;
            if (pauseAndZoom && Time.timeScale > 0f && Time.unscaledTime - _lastFocus >= MinGapSeconds)
                found = TryResolve(text, out focus, out what);
            if (!found) { UIToast.Show(body, ToastSeconds); return; }

            _lastFocus = Time.unscaledTime;
            // Quicksave first (same save as F5), so F9 returns to the moment of the bug. The quicksave being replaced is kept as
            // quicksave_before_bug.json first (2026-11-12: a bug during a test run replaced Tad's Day 1 13:19 quicksave and it was lost).
            try
            {
                string qs = System.IO.Path.Combine(SaveSystem.SaveFolder, "quicksave.json");
                if (System.IO.File.Exists(qs))
                    System.IO.File.Copy(qs, System.IO.Path.Combine(SaveSystem.SaveFolder, "quicksave_before_bug.json"), true);
            }
            catch (System.Exception e) { Debug.LogWarning("[BugFocus] could not back up the quicksave: " + e.Message); }
            try { if (SaveLoadSystem.SaveManager.Instance != null) SaveLoadSystem.SaveManager.Instance.SaveToSlot(-1, "quicksave"); }
            catch (System.Exception e) { Debug.LogWarning("[BugFocus] quicksave failed: " + e.Message); }
            Time.timeScale = 0f;
            if (_cam == null) _cam = Object.FindFirstObjectByType<FreeLookCamera>();
            if (_cam != null) _cam.GlideTo(focus, 35f, 35f, ViewDistance, 0.9f, true);
            UIToast.Show($"{body}\n(quick-saved + paused - looking at {what}; F9 reloads this moment)", ToastSeconds);
        }

        static bool TryResolve(string text, out Vector3 focus, out string what)
        {
            focus = default; what = "the spot";

            // 1) an order number -> one of its pallets
            var om = OrderRx.Match(text);
            if (om.Success && ServiceLocator.TryGet<OrderService>(out var os))
            {
                var order = os.ActiveOrders.FirstOrDefault(o => o != null && o.OrderNumber == om.Value);
                if (order != null)
                {
                    var pal = Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None)
                        .FirstOrDefault(p => p != null && p.OrderId == order.OrderId);
                    if (pal != null) { focus = Center(pal.gameObject); what = $"a pallet of {om.Value}"; return true; }
                }
            }

            // 2) a position in the text -> the object standing there, or the spot itself
            var m = PosRx.Match(text);
            if (m.Success
                && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                var p = new Vector3(x, y, z);
                focus = p; what = "the spot";
                var hits = Physics.OverlapSphere(p, 2.5f, ~0, QueryTriggerInteraction.Ignore);
                Transform best = null; float bestD = float.MaxValue;
                foreach (var c in hits)
                {
                    if (c == null) continue;
                    Transform root = Root(c);
                    if (root == null) continue;
                    float d = (c.ClosestPoint(p) - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = root; }
                }
                if (best != null) { focus = Center(best.gameObject); what = best.name; }
                return true;
            }
            return false;
        }

        /// <summary>The gameplay object a collider belongs to (machine, trailer, door, pallet), or null for scenery.</summary>
        static Transform Root(Collider c)
        {
            if (c.GetComponentInParent<MHEOperatorSlot>() is { } s) return s.transform;
            if (c.GetComponentInParent<OutboundPalletBuilder>() is { } pb) return pb.transform;
            if (c.GetComponentInParent<TruckController>() is { } t) return t.transform;
            if (c.GetComponentInParent<DockSlot>() is { } d) return d.transform;
            var anim = c.GetComponentInParent<Animator>();
            if (anim != null) return anim.transform;   // a worker
            return null;
        }

        static Vector3 Center(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return go.transform.position + Vector3.up;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.center;
        }
    }
}
