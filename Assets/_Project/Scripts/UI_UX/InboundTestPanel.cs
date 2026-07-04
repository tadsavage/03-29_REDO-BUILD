using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DEBUG-ONLY on-screen panel to manually trigger CHUNK 1 (Inbound) for visual testing: spawns a
/// truck carrying a 12-pallet test PO so you can watch it queue at the gate, get inspected (trailer
/// doors open — GuardController's existing behavior), drive to a dock, sit docked with its
/// pre-built cargo, and depart. Also shows live shipment/work-queue state so receiving (Load IDs +
/// Putaway tasks) is visible without digging through the console.
///
/// Self-bootstrapping (like PalletInventoryTracker) — no scene setup needed. Press P or click the
/// button. This is a prototype-testing tool only; remove or gate behind a build flag before shipping.
/// </summary>
public class InboundTestPanel : MonoBehaviour
{
    private static InboundTestPanel _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[InboundTestPanel]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InboundTestPanel>();
    }

    private const string TestSkuId = "035-12345";
    private int _testCounter;
    private GUIStyle _bigLabel;
    private GUIStyle _bigButton;

    // Half the previous size (was 36); ~18 reads as roughly 1.5x the default IMGUI font.
    private const int BigFontSize = 18;

    // Draggable window state — position persists across frames as the user drags it.
    private const int WindowId = 761234;
    private Rect _windowRect = new Rect(10, 120, 460, 520);

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame && !UIModalGuard.IsCapturing)
            SpawnTestTruck();
    }

    private void EnsureStyles()
    {
        if (_bigLabel != null) return;
        _bigLabel  = new GUIStyle(GUI.skin.label)  { richText = true, fontSize = BigFontSize, wordWrap = true };
        _bigButton = new GUIStyle(GUI.skin.button) { richText = true, fontSize = BigFontSize, wordWrap = true };
    }

    private void OnGUI()
    {
        EnsureStyles();
        _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "INBOUND TEST — drag me");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Space(4);
        GUILayout.Label("<b>Chunk 1 debug</b>  (or press P)", _bigLabel);

        if (GUILayout.Button($"Spawn Test PO Truck (12 pallets, SKU {TestSkuId})", _bigButton, GUILayout.Height(44)))
            SpawnTestTruck();

        GUILayout.Space(8);
        DrawShipments();
        GUILayout.Space(8);
        DrawWorkQueue();

        // The whole title bar (top ~22px) is the drag handle.
        GUI.DragWindow(new Rect(0, 0, _windowRect.width, 22));
    }

    private void SpawnTestTruck()
    {
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipmentService) || shipmentService == null)
        {
            Debug.LogError("[InboundTestPanel] ShipmentService not available.");
            return;
        }

        _testCounter++;
        var items = new List<ShipmentLineItem>();
        for (int i = 0; i < 12; i++)
            items.Add(new ShipmentLineItem(TestSkuId, quantity: 24, unitCost: 5, shelfLifeDays: -1));

        shipmentService.CreatePurchaseOrder($"SUPP_TEST{_testCounter}", "Test Supplier", items);
    }

    private void DrawShipments()
    {
        GUILayout.Label("<b>Shipments</b>", _bigLabel);
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipmentService) || shipmentService == null) return;

        if (shipmentService.PendingShipments.Count == 0)
        {
            GUILayout.Label("(none)", _bigLabel);
            return;
        }

        foreach (var s in shipmentService.PendingShipments)
            GUILayout.Label($"{s.ShipmentId.Substring(0, 8)}  {s.SupplierName}  [{s.Status}]  {s.LineItems.Count} pallets", _bigLabel);
    }

    private void DrawWorkQueue()
    {
        GUILayout.Label("<b>Work Queue</b>", _bigLabel);
        if (!ServiceLocator.TryGet<WorkQueueSystem>(out var queue) || queue == null) return;

        if (queue.Tasks.Count == 0)
        {
            GUILayout.Label("(no pending tasks)", _bigLabel);
            return;
        }

        foreach (var t in queue.Tasks.Take(10))
            GUILayout.Label($"[{t.Status}] {t.Description}", _bigLabel);
    }
}
