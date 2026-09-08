using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Mints player-facing order/PO numbers in the house format {Kind}{Area}{Day}{Seq:D3} -- e.g.
    /// "OG5001" (Outbound, Grocery, due/ship day 5, 1st of its kind ever) or "IG2001" (Inbound,
    /// Grocery, arrival day 2, 1st PO ever). Kind and Area are single letters supplied by the caller
    /// (see OrderService.DominantOrderNumberPrefix for the area scheme); Day is whatever day the
    /// order/PO is scheduled against -- the day it's due to ship for an outbound order, the day it's
    /// expected to arrive for an inbound PO -- NOT the day it was created/accepted, which can differ
    /// (e.g. accepted day 2, due day 5 -> the "5" is what appears in the number).
    ///
    /// The 3-digit sequence is a running lifetime counter PER KIND LETTER -- Outbound and Inbound
    /// count independently, since "the first PO ever" is a PO-specific milestone, not a combined one.
    /// It deliberately never resets at day rollover; per Tad, running the counter out over time is
    /// part of the point (a milestone worth noticing, maybe a future Steam achievement), so it wraps
    /// 999 back to 001 instead of growing unboundedly or resetting daily. Persisted via PlayerPrefs,
    /// the same mechanism PONumberGenerator already used for its own (now-retired) counter.
    /// </summary>
    public static class OrderNumberGenerator
    {
        private const int MaxSequence = 999;
        private const string PrefKeyPrefix = "OrderNumberSeq_";

        public static string GetNext(char kindLetter, char areaLetter, int day)
        {
            string prefKey = PrefKeyPrefix + kindLetter;
            int seq = PlayerPrefs.GetInt(prefKey, 1);
            string number = $"{kindLetter}{areaLetter}{day}{seq:D3}";
            PlayerPrefs.SetInt(prefKey, seq >= MaxSequence ? 1 : seq + 1);
            PlayerPrefs.Save();
            return number;
        }
    }
}
