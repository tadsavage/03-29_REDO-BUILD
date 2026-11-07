using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// The body of the MiniDockTimeline trailer hover tooltip: how far along the freight for that trailer is
/// BEFORE it reaches the door. Pallet picks (whole pallets pulled by a reach truck) and case picks
/// (mixed pallets built by a selector) are counted separately, then together, then per SKU.
///
/// "Staged" = the pallet is completely set down in its staging lane, i.e. a dockstocker could grab and
/// load it right now. Pallets in transit (on a jack/forks, being moved) or already on the trailer don't count.
///
/// Built fresh on every hover. Styled inline because the tooltip host lives at the panel root, outside the
/// BuildMenu stylesheet's reach.
/// </summary>
public static class LoadProgressCard
{
    public const float Width = 430f;

    private static readonly Color ColText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColMuted  = new Color(158 / 255f, 184 / 255f, 204 / 255f, 1f);
    private static readonly Color ColTrack  = new Color(92 / 255f, 155 / 255f, 196 / 255f, 0.25f);
    private static readonly Color ColFill   = new Color(115 / 255f, 184 / 255f, 224 / 255f, 1f);
    private static readonly Color ColDone   = new Color(51 / 255f, 173 / 255f, 97 / 255f, 1f);
    private static readonly Color ColPending = new Color(1f, 200 / 255f, 80 / 255f, 1f);

    private class SkuRow
    {
        public SkuData Sku;
        public int NeedCases, FullCases, NeedPlt, NeedCs, StagedPlt, StagedCsCases;
        public string SkuId;
        public bool NoStock;
    }

    /// <summary>The hover body for <paramref name="appt"/>; <paramref name="header"/> is the existing one-line summary.</summary>
    public static VisualElement Build(DockAppointment appt, string header)
    {
        var root = new VisualElement { pickingMode = PickingMode.Ignore };
        root.style.width = Width;
        var headerRow = new VisualElement { pickingMode = PickingMode.Ignore };
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.FlexStart;
        var headerLabel = MakeLabel(header, 13, ColText, true);
        headerLabel.style.flexShrink = 1;
        headerRow.Add(headerLabel);
        root.Add(headerRow);

        ServiceLocator.TryGet<OrderService>(out var orders);
        ServiceLocator.TryGet<InventoryService>(out var inv);
        if (orders == null || appt.Kind == AppointmentKind.Inbound || appt.OrderIds == null || appt.OrderIds.Count == 0)
        {
            root.Add(MakeLabel(appt.Kind == AppointmentKind.Inbound
                ? "Inbound truck - no outbound freight to stage."
                : "No order raised for this trailer yet, so nothing is being prepared.", 13, ColMuted, false, 6));
            return root;
        }

        var all = orders.ActiveOrders.Concat(orders.OrderHistory).ToList();
        var list = appt.OrderIds.Select(id => all.FirstOrDefault(o => o.OrderId == id)).Where(o => o != null).ToList();
        if (list.Count == 0)
        {
            root.Add(MakeLabel("The order for this trailer is no longer on file.", 13, ColMuted, false, 6));
            return root;
        }

        bool holding = list.Any(o => o.Status != OrderData.OrderStatus.Cancelled && o.Status != OrderData.OrderStatus.Shipped
                                     && orders.IsAwaitingRelease(o));
        headerRow.Add(ReleaseBadge(!holding));

        var rows = new Dictionary<string, SkuRow>();
        var ids = new HashSet<string>();
        var shippedIds = new HashSet<string>();
        int pltTotal = 0, caseTotal = 0, pltStaged = 0, caseStaged = 0;

        foreach (var order in list)
        {
            ids.Add(order.OrderId);
            if (order.Status == OrderData.OrderStatus.Shipped) shippedIds.Add(order.OrderId);

            int whole = 0;
            foreach (var li in order.LineItems)
            {
                if (!rows.TryGetValue(li.SkuId, out var row))
                    rows[li.SkuId] = row = new SkuRow { SkuId = li.SkuId, Sku = inv?.GetSkuData(li.SkuId), FullCases = orders.FullPalletCases(li.SkuId) };
                row.NeedCases += li.QuantityNeeded;
                int nPlt = row.FullCases > 0 ? li.QuantityNeeded / row.FullCases : 0;
                row.NeedPlt += nPlt;
                row.NeedCs += li.QuantityNeeded - nPlt * row.FullCases;
                whole += nPlt;
            }

            int orderTotal = order.PlannedPallets > 0 ? order.PlannedPallets : orders.PalletsNeeded(order);
            pltTotal += whole;
            caseTotal += Mathf.Max(0, orderTotal - whole);
        }

        foreach (var ob in Object.FindObjectsByType<OutboundPalletBuilder>())
        {
            if (ob == null || string.IsNullOrEmpty(ob.OrderId) || !ids.Contains(ob.OrderId) || shippedIds.Contains(ob.OrderId)) continue;
            // Only a pallet set down in its lane counts: something a dockstocker could pick up and load right now.
            if (ob.Stage != OutboundPalletBuilder.PalletStage.Staged || !ob.IsOnFloor) continue;

            if (ob.CaseCountsBySku.Count > 0)
            {
                caseStaged++;
                foreach (var kv in ob.CaseCountsBySku)
                    if (rows.TryGetValue(kv.Key, out var r)) r.StagedCsCases += kv.Value;
            }
            else
            {
                pltStaged++;
                var pd = ob.GetComponent<PalletData>();
                if (pd != null && !string.IsNullOrEmpty(pd.ItemNumber) && rows.TryGetValue(pd.ItemNumber, out var r)) r.StagedPlt++;
            }
        }

        // The totals are estimates until every pallet exists; never show more staged than the estimate.
        pltTotal = Mathf.Max(pltTotal, pltStaged);
        caseTotal = Mathf.Max(caseTotal, caseStaged);
        int total = pltTotal + caseTotal;

        // The headline is case-weighted, not a pallet count: a case-pick pallet that exists but is missing a
        // SKU's cases (stock-out, still being picked) must not read as fully staged.
        int needCasesAll = 0, stagedCasesAll = 0;
        ServiceLocator.TryGet(out GameCore.Labor.WorkQueueSystem wq);
        foreach (var r in rows.Values)
        {
            needCasesAll += r.NeedCases;
            int stagedCs = StagedCases(r);
            stagedCasesAll += stagedCs;
            // Same stock test OrderService uses when it cuts a line ("not in stock - cut from the order").
            int missing = r.NeedCases - stagedCs;
            r.NoStock = missing > 0 && !string.IsNullOrEmpty(r.SkuId)
                        && OrderPickPath.ChooseSlot(r.SkuId, missing, out _) == null
                        && !(inv != null && GameCore.Labor.ReplenishmentService.HasStockAnywhere(wq, inv, r.SkuId));
        }
        float stagedFraction = needCasesAll > 0 ? stagedCasesAll / (float)needCasesAll : (total > 0 ? 1f : 0f);
        // Floor to 0.1 so a card that is short never rounds up to "N out of N".
        float stagedPlts = stagedFraction >= 1f ? total : Mathf.Floor(stagedFraction * total * 10f) / 10f;

        root.Add(Divider());
        if (pltTotal > 0) root.Add(MakeLabel($"{pltStaged} out of {pltTotal} Plt. Picks Staged", 13, ColMuted));
        if (caseTotal > 0) root.Add(MakeLabel($"{caseStaged} out of {caseTotal} plts. of case pick have been Staged", 13, ColMuted));
        // Pallets on the floor are not cases on the floor: say so when the staged pallets are missing stock (Tad, 2026-11-05 -
        // "5 out of 5 staged" read as done while 7 SKUs were out of stock and the truck shipped 25 cases short).
        int noStockCases = 0, noStockLines = 0;
        foreach (var r in rows.Values)
            if (r.NoStock) { noStockLines++; noStockCases += Mathf.Max(0, r.NeedCases - StagedCases(r)); }
        if (noStockCases > 0)
            root.Add(MakeLabel($"SHORT: {noStockCases} case(s) across {noStockLines} item(s) are out of stock - this load will ship short unless stock arrives.",
                13, new Color(0.95f, 0.35f, 0.3f, 1f), true, 2));

        var totalRow = new VisualElement { pickingMode = PickingMode.Ignore };
        totalRow.style.flexDirection = FlexDirection.Row;
        totalRow.style.alignItems = Align.Center;
        totalRow.style.marginTop = 6;
        totalRow.Add(MakeBar(total > 0 ? stagedFraction : 0f, 110, 18, 10));
        totalRow.Add(MakeLabel($"{Fmt(stagedPlts)} out of {total} plts. Total Staged", 19, Color.white, true));
        root.Add(totalRow);

        root.Add(Divider());
        foreach (var r in rows.Values.OrderBy(r => r.Sku != null ? r.Sku.ItemNumber : int.MaxValue))
            root.Add(BuildRow(r));
        return root;
    }

    /// <summary>Green RELEASED / red HOLDING pill, same test the Scheduler tile's release button uses.</summary>
    private static Label ReleaseBadge(bool released)
    {
        var l = new Label(released ? "RELEASED" : "HOLDING") { pickingMode = PickingMode.Ignore };
        l.style.fontSize = 18;
        l.style.color = Color.white;
        ApplyLilita(l);
        l.style.backgroundColor = released ? ColDone : new Color(0.78f, 0.16f, 0.16f, 1f);
        l.style.paddingTop = 2; l.style.paddingBottom = 2; l.style.paddingLeft = 12; l.style.paddingRight = 12;
        l.style.marginLeft = 10;
        l.style.flexShrink = 0;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.style.borderTopLeftRadius = l.style.borderTopRightRadius =
            l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 6;
        return l;
    }

    private static Font _lilita;
    private static void ApplyLilita(VisualElement el)
    {
        if (_lilita == null)
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0) _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular");
#endif
        }
        if (_lilita != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_lilita));
    }

    private static int StagedCases(SkuRow r) =>
        Mathf.Min(r.NeedCases, r.StagedPlt * Mathf.Max(0, r.FullCases) + r.StagedCsCases);

    private static VisualElement BuildRow(SkuRow r)
    {
        int stagedCases = StagedCases(r);
        float fraction = r.NeedCases > 0 ? stagedCases / (float)r.NeedCases : 0f;

        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 3;

        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = 22; icon.style.height = 22; icon.style.marginRight = 6;
        icon.style.flexShrink = 0;
        if (r.Sku?.Icon != null) icon.style.backgroundImage = new StyleBackground(r.Sku.Icon);
        row.Add(icon);
        row.Add(MakeBar(fraction, 70, 10, 8));

        string name = r.Sku != null ? r.Sku.ItemDescription : "Unknown item";
        string text = r.FullCases > 0
            ? $"{name} ({Fmt(stagedCases / (float)r.FullCases)}/{Fmt(r.NeedCases / (float)r.FullCases)}) plts. staged"
            : $"{name} ({stagedCases}/{r.NeedCases}) cs. staged";
        var label = MakeLabel(text, 13, ColText);
        label.style.flexShrink = 1;
        row.Add(label);

        if (r.NoStock)
        {
            var shortTag = MakeLabel("[Short - no stock]", 12, new Color(0.95f, 0.35f, 0.3f, 1f), true);
            shortTag.style.marginLeft = 6;
            shortTag.style.marginRight = 4;
            row.Add(shortTag);
        }

        bool hasPlt = r.NeedPlt > 0, hasCs = r.NeedCs > 0;
        if (hasCs) row.Add(Tag("[Cs.Pick]", r.StagedCsCases >= r.NeedCs, 6));
        if (hasCs && hasPlt) row.Add(MakeLabel("and", 12, ColMuted));
        if (hasPlt) row.Add(Tag("[Plt. Pick]", r.StagedPlt >= r.NeedPlt, hasCs ? 4 : 6));
        return row;
    }

    private static Label Tag(string text, bool done, float leftMargin)
    {
        var l = MakeLabel(text, 12, done ? ColDone : ColPending, true);
        l.style.marginLeft = leftMargin;
        l.style.marginRight = 4;
        return l;
    }

    private static Label MakeLabel(string text, int size, Color color, bool bold = false, float marginTop = 0)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size;
        l.style.color = color;
        l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.marginTop = marginTop;
        l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        l.style.paddingTop = l.style.paddingBottom = l.style.paddingLeft = l.style.paddingRight = 0;
        return l;
    }

    private static VisualElement MakeBar(float fraction, float width, float height, float rightMargin)
    {
        fraction = Mathf.Clamp01(fraction);
        var bar = new VisualElement { pickingMode = PickingMode.Ignore };
        bar.style.width = width; bar.style.height = height;
        bar.style.marginRight = rightMargin;
        bar.style.flexShrink = 0;
        bar.style.backgroundColor = ColTrack;
        bar.style.overflow = Overflow.Hidden;
        bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius =
            bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = height / 2f;

        var fill = new VisualElement { pickingMode = PickingMode.Ignore };
        fill.style.height = Length.Percent(100f);
        fill.style.width = Length.Percent(fraction * 100f);
        fill.style.backgroundColor = fraction >= 0.999f ? ColDone : ColFill;
        bar.Add(fill);
        return bar;
    }

    private static VisualElement Divider()
    {
        var d = new VisualElement { pickingMode = PickingMode.Ignore };
        d.style.height = 1;
        d.style.marginTop = 6; d.style.marginBottom = 6;
        d.style.backgroundColor = ColTrack;
        return d;
    }

    /// <summary>Whole numbers stay whole; a part-pallet shows one decimal ("2.5").</summary>
    private static string Fmt(float v) => (Mathf.Round(v * 10f) / 10f).ToString("0.#");
}
