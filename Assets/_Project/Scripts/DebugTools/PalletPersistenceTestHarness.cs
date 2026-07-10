using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using GameCore.Services;
using GameCore.Inventory;

namespace GameCore.DebugTools
{
    /// <summary>
    /// TEMPORARY test-only harness for verifying pallet save/load persistence end-to-end without
    /// requiring keyboard input simulation. Self-bootstraps like the other dock services (hidden
    /// DontDestroyOnLoad object). Polls a trigger file on disk every 0.5s; when found, reads a
    /// command ("SAVE:<name>" or "LOAD:<name>"), runs it against the live PlacementSystem, then
    /// dumps InventoryService + scene pallet-visual state to a result file for external inspection.
    ///
    /// DELETE THIS FILE after persistence verification is complete — it is not part of the shipping
    /// game and exists purely to drive PlacementSystem.SaveGame/LoadGame from outside Play Mode
    /// input (the MCP tooling available this session has no keyboard-simulation action).
    /// </summary>
    public class PalletPersistenceTestHarness : MonoBehaviour
    {
        private static PalletPersistenceTestHarness _instance;

        private static readonly string TriggerPath = Path.Combine(
            @"C:\Users\tadsa\AppData\Local\Temp\claude\C--Users-tadsa-Documents-GitHub-03-29-REDO-BUILD\b6c6c399-45af-4193-85fe-cc64d5c2e2e3\scratchpad",
            "pallet_test_trigger.txt");

        private static readonly string ResultPath = Path.Combine(
            @"C:\Users\tadsa\AppData\Local\Temp\claude\C--Users-tadsa-Documents-GitHub-03-29-REDO-BUILD\b6c6c399-45af-4193-85fe-cc64d5c2e2e3\scratchpad",
            "pallet_test_result.txt");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[PalletPersistenceTestHarness]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PalletPersistenceTestHarness>();
            Debug.Log("[PalletTestHarness] Bootstrapped, polling for trigger file: " + TriggerPath);
        }

        private float _nextPoll;
        private bool _busy;

        private void Update()
        {
            if (_busy) return;
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;

            if (!File.Exists(TriggerPath)) return;

            string cmd;
            try { cmd = File.ReadAllText(TriggerPath).Trim(); }
            catch { return; }

            try { File.Delete(TriggerPath); } catch { /* ignore */ }

            if (string.IsNullOrEmpty(cmd)) return;

            _busy = true;
            StartCoroutine(RunCommand(cmd));
        }

        private IEnumerator RunCommand(string cmd)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[PalletTestHarness] Command: {cmd}  (t={DateTime.Now:HH:mm:ss})");

            var ps = FindAnyObjectByType<PlacementSystem>();
            if (ps == null)
            {
                sb.AppendLine("ERROR: PlacementSystem not found in scene.");
                WriteResult(sb.ToString());
                _busy = false;
                yield break;
            }

            if (cmd.StartsWith("LOAD:", StringComparison.OrdinalIgnoreCase))
            {
                string name = cmd.Substring(5);
                sb.AppendLine($"Calling ps.LoadGame(\"{name}\")...");
                bool threw = false;
                try { ps.LoadGame(name); }
                catch (Exception e) { sb.AppendLine("EXCEPTION during LoadGame: " + e); threw = true; }

                if (!threw)
                {
                    // Give the load's deferred coroutines (destroy flush, navmesh bake, pallet
                    // visual instantiation) time to finish before we inspect state.
                    yield return new WaitForSecondsRealtime(6f);
                    DumpState(sb, $"AFTER LOAD '{name}'");
                }
            }
            else if (cmd.StartsWith("SAVE:", StringComparison.OrdinalIgnoreCase))
            {
                string name = cmd.Substring(5);
                sb.AppendLine($"Calling ps.SaveGame(\"{name}\")...");
                try { ps.SaveGame(name); }
                catch (Exception e) { sb.AppendLine("EXCEPTION during SaveGame: " + e); }
                yield return null;
                DumpState(sb, $"AFTER SAVE '{name}'");
            }
            else if (cmd.Equals("DUMP", StringComparison.OrdinalIgnoreCase))
            {
                DumpState(sb, "DUMP (no action taken)");
            }
            else
            {
                sb.AppendLine("Unrecognized command. Use LOAD:<name>, SAVE:<name>, or DUMP.");
            }

            WriteResult(sb.ToString());
            _busy = false;
        }

        private void DumpState(StringBuilder sb, string label)
        {
            sb.AppendLine($"=== {label} ===");

            if (ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
            {
                var pallets = inv.GetAllPallets();
                sb.AppendLine($"InventoryService.GetAllPallets() count: {pallets.Count}");
                int shown = 0;
                foreach (var p in pallets)
                {
                    if (p == null) continue;
                    string idPrefix = !string.IsNullOrEmpty(p.PalletId) && p.PalletId.Length >= 5 ? p.PalletId.Substring(0, 5) : p.PalletId;
                    sb.AppendLine($"  record id={idPrefix} sku={p.SkuId} loadId={p.LoadId} cell=({p.CurrentLocation.x},{p.CurrentLocation.y}) worldY={p.WorldHeightY:F3} lane={p.StagingLaneId}");
                    shown++;
                    if (shown >= 60) { sb.AppendLine("  ... (truncated)"); break; }
                }
            }
            else
            {
                sb.AppendLine("InventoryService not found via ServiceLocator.");
            }

            var container = GameObject.Find("PlacedObjectsContainer");
            int visualCount = 0;
            if (container != null)
            {
                foreach (Transform t in container.transform)
                {
                    bool isPalletVisual = t.name.StartsWith("RestoredPallet", StringComparison.OrdinalIgnoreCase)
                        || t.GetComponent<PalletBuilder>() != null
                        || t.GetComponent<PalletMasterLink>() != null;
                    if (!isPalletVisual) continue;
                    visualCount++;
                    if (visualCount <= 30)
                        sb.AppendLine($"  visual: {t.name} pos={t.position:F3}");
                }
                sb.AppendLine($"Pallet visual GameObjects under PlacedObjectsContainer: {visualCount}");
            }
            else
            {
                sb.AppendLine("PlacedObjectsContainer not found.");
            }
        }

        private void WriteResult(string content)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, content);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PalletTestHarness] Failed writing result file: {e}");
            }
            Debug.Log("[PalletTestHarness] " + content.Replace("\n", " | "));
        }
    }
}
