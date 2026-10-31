using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GameCore.Economy;
using GameCore.Services;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// A short trail of what happened to each pallet (Tad, 2026-10-31): received, registered, moved, picked, destroyed, its tasks
    /// created / claimed / completed / CANCELLED and by which method. The health check appends the trail to every NO-TASK /
    /// NO-RECORD / FLOATING / OVERLAP line, so a bad pallet explains itself instead of being guessed at from totals.
    ///
    /// Keyed by the inventory pallet id (or "ord:&lt;orderId&gt;" for a case-picked outbound pallet that has no record). Memory is
    /// bounded: the last <see cref="PerPallet"/> entries per pallet and the <see cref="MaxPallets"/> most recently touched pallets.
    /// Entries are plain strings made only when something changes (never per frame), so the cost is negligible.
    /// </summary>
    public static class PalletHistory
    {
        private const int PerPallet = 12;
        private const int MaxPallets = 600;

        private static readonly Dictionary<string, LinkedList<string>> _trail = new Dictionary<string, LinkedList<string>>();
        private static readonly LinkedList<string> _recent = new LinkedList<string>();

        /// <summary>Statics survive Play sessions here (Domain Reload is off), so start every session empty.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() { _trail.Clear(); _recent.Clear(); }

        public static string OutboundKey(string orderId) => "ord:" + orderId;

        /// <summary>Records one event for a pallet. <paramref name="callerSkip"/> frames are skipped when naming the calling method.</summary>
        public static void Note(string palletKey, string what, bool nameCaller = false, int callerSkip = 2)
        {
            if (string.IsNullOrEmpty(palletKey) || string.IsNullOrEmpty(what)) return;

            if (!_trail.TryGetValue(palletKey, out var list))
            {
                if (_trail.Count >= MaxPallets && _recent.First != null)
                {
                    _trail.Remove(_recent.First.Value);
                    _recent.RemoveFirst();
                }
                list = new LinkedList<string>();
                _trail[palletKey] = list;
            }
            _recent.Remove(palletKey);
            _recent.AddLast(palletKey);

            string caller = nameCaller ? " by " + CallerName(callerSkip) : "";
            list.AddLast($"{Clock()} {what}{caller}");
            while (list.Count > PerPallet) list.RemoveFirst();
        }

        /// <summary>The trail for one pallet, oldest first, as " | " separated text. Empty when nothing was recorded.</summary>
        public static string Describe(string palletKey, int last = 6)
        {
            if (string.IsNullOrEmpty(palletKey) || !_trail.TryGetValue(palletKey, out var list) || list.Count == 0) return "";
            var all = new List<string>(list);
            int from = Mathf.Max(0, all.Count - last);
            var sb = new StringBuilder();
            for (int i = from; i < all.Count; i++) { if (sb.Length > 0) sb.Append(" | "); sb.Append(all[i]); }
            return sb.ToString();
        }

        private static string Clock()
        {
            if (ServiceLocator.TryGet<SimulationTimeService>(out var t) && t != null) return $"d{t.Day} {t.Hour:00}:{t.Minute:00}";
            return $"t{Time.time:F0}";
        }

        /// <summary>Name of the method that made the change (the code that cancelled a task, moved a pallet...).</summary>
        private static string CallerName(int skip)
        {
            try
            {
                var frames = new System.Diagnostics.StackTrace(skip, false).GetFrames();
                if (frames == null) return "?";
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    string type = m.DeclaringType.Name;
                    if (type == nameof(PalletHistory) || type == "WorkTask" || type == "WorkQueueSystem" && m.Name == "CreateTask") continue;
                    // Coroutine state machines are named <Routine>d__12; show the routine.
                    string owner = m.DeclaringType.DeclaringType != null ? m.DeclaringType.DeclaringType.Name : type;
                    string method = m.Name == "MoveNext" ? type.Trim('<', '>').Split('>')[0] : m.Name;
                    return owner + "." + method;
                }
            }
            catch { /* diagnostics must never throw into game code */ }
            return "?";
        }
    }
}
