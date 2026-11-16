using UnityEngine;
using System.Collections;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// EMPTY PALLETS FROM THE CHEP STACKS (Tad, 2026-11-15): "I no longer want selectors to get their empty cheps from the MHE battery
    /// station. I want them to pull up to or alongside of the chep pallet stacks ... I also want 2 pallets on each pallet jack ... when the
    /// selector pulls up to the chep stacks and stops they will step off the jack and walk back to the forks and as they walk by the pallet
    /// jack's forks the pallets will instantiate on the forks ... then the selector will walk back."
    ///
    /// Right after mounting (the jack is collected from its charger), the jack drives to the nearest chep stack (StackPlts, ChepStack.prefab)
    /// and parks ALONGSIDE it, forks beside the stack with a walking gap between. The selector steps off on the stack side, walks down the
    /// forks - the near pallet appears as they pass the near fork position, the order's pallet as they pass the tip - and walks back on.
    /// The near one is a SPARE: when the order needs its second pallet (StartNextPallet), it becomes that pallet in place.
    /// No stack, or the jack can't get to one: the pallets appear at the charger as before.
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        private const int   ChepStackObjId        = 64;     // ObjData "StackPlts" (Prefabs/Inventory/ChepStack.prefab)
        private const float ChepStackSideGap      = 1.95f;  // fork centre line this far from the stack centre (pallet + stack half widths + a walking gap)
        private const float ChepWalkInset         = 0.98f;  // the selector walks this far from the fork centre line, on the stack side
        private const float ChepForkCentreBack    = 1.5f;   // the fork load centre is this far behind the jack's root

        private GameObject _spareChep;   // the empty pallet riding the near fork position until the order needs a second pallet
        private static PlacementGrid s_chepGrid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetChepStatics() => s_chepGrid = null;

        /// <summary>Drive to the nearest chep stack, step off, walk the forks (pallets appear), step back on. False = not done: the caller
        /// spawns the pallets where the jack is (the old behaviour).</summary>
        private IEnumerator FetchEmptiesFromChepStack(System.Action<bool> done)
        {
            if (_jack == null || !TryChooseChepStackPark(out Transform stack, out Vector3 park, out Vector3 heading, out Vector3 side))
            {
                done?.Invoke(false);
                yield break;
            }
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': collecting empty pallets from the chep stack at {stack.position.x:0.0}, {stack.position.z:0.0}.");
            yield return DriveJackTo(park, heading, "chep stack (empty pallets)");
            if (_jack == null || !_jackLegOk || PlanarDist(_jack.transform.position, park) > 1.0f)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': could not park at the chep stack - taking the empty pallets on here.");
                done?.Invoke(false);
                yield break;
            }

            // Off on the stack side, along the forks (near position first, then the tip), pallets appearing as the selector passes them.
            Transform jt = _jack.transform;
            Vector3 toStack = Flat(stack.position - jt.position, 0f);
            float s = Vector3.Dot(toStack, jt.right) >= 0f ? 1f : -1f;
            Vector3 Beside(float z) => Flat(jt.TransformPoint(new Vector3(s * ChepWalkInset, 0f, z)), transform.position.y);

            yield return StepOut(stack.position);
            if (_jack == null) { done?.Invoke(false); yield break; }
            _offJack = true;
            yield return WalkTo(Beside(ForkPalletZ[1]));
            if (_jack == null) { done?.Invoke(false); yield break; }
            yield return FaceDir(stack.position - transform.position);
            SpawnSpareChep();
            yield return WalkTo(Beside(ForkPalletZ[0]));
            if (_jack == null) { done?.Invoke(false); yield break; }
            yield return FaceDir(stack.position - transform.position);
            EnsurePallet();
            yield return new WaitForSeconds(0.2f);

            // Back on.
            yield return WalkTo(Beside(ForkPalletZ[1]));
            yield return StepIn();
            if (_jack == null) { done?.Invoke(false); yield break; }
            _jack.StepOn();
            _offJack = false;
            done?.Invoke(true);
        }

        /// <summary>The nearest chep stack and where the jack parks beside it: forks beside the stack on its most open side, the jack's nose
        /// toward whichever end is free (preferring the end nearer the jack, so it drives straight in).</summary>
        private bool TryChooseChepStackPark(out Transform stack, out Vector3 park, out Vector3 heading, out Vector3 side)
        {
            stack = null; park = heading = side = default;
            Vector3 from = _jack.transform.position;
            float best = float.MaxValue;
            foreach (var po in PlacedObjectRegistry.All)
            {
                if (po == null || po.data == null || !po.isActiveAndEnabled) continue;
                if (po.data.id != ChepStackObjId && (po.data.prefab == null || po.data.prefab.name != "ChepStack")) continue;
                float d = PlanarDist(po.transform.position, from);
                if (d < best && TryParkBeside(po.transform, from, out var p, out var h, out var sd))
                {
                    best = d; stack = po.transform; park = p; heading = h; side = sd;
                }
            }
            return stack != null;
        }

        private static readonly Vector3[] Axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        private bool TryParkBeside(Transform stack, Vector3 from, out Vector3 park, out Vector3 heading, out Vector3 side)
        {
            park = heading = side = default;
            Vector3 s = Flat(stack.position, from.y);
            float bestScore = float.MinValue;
            bool found = false;
            foreach (var d in Axes)
            {
                Vector3 forkCentre = s + d * ChepStackSideGap;
                if (!FloorClear(forkCentre) || !FloorClear(s + d * (ChepStackSideGap * 0.5f))) continue;
                foreach (var h in new[] { new Vector3(-d.z, 0f, d.x), new Vector3(d.z, 0f, -d.x) })
                {
                    Vector3 root = forkCentre + h * ChepForkCentreBack;
                    Vector3 tips = forkCentre - h * 1.2f;
                    if (!FloorClear(root) || !FloorClear(root + h * 1.0f) || !FloorClear(tips)) continue;
                    // Closer to the jack is better; facing so it drives in nose-first from where it is.
                    float score = -PlanarDist(root, from) + Vector3.Dot(Flat(root - from, 0f).normalized, h) * 2f;
                    if (score > bestScore) { bestScore = score; park = root; heading = h; side = d; found = true; }
                }
            }
            return found;
        }

        /// <summary>No rack, wall or other placed solid, and no other chep stack or floor pallet, at this point.</summary>
        private static bool FloorClear(Vector3 p)
        {
            if (MachineClearance.SolidAt(p, out _)) return false;
            if (s_chepGrid == null) s_chepGrid = FindAnyObjectByType<PlacementGrid>();
            var grid = s_chepGrid;
            if (grid == null) return true;
            var list = grid.GetObjectsInCell(grid.WorldToCell(p));
            if (list == null) return true;
            foreach (var e in list)
                if (e.instance != null && e.data != null && !e.data.isFloor && e.data.category == "Inventory") return false;
            return true;
        }

        /// <summary>The spare empty pallet on the near fork position (a plain chep, not yet an order pallet).</summary>
        private void SpawnSpareChep()
        {
            if (_jack == null || _spareChep != null) return;
            var prefab = ResolveChepEmptyPrefab();
            if (prefab == null) return;
            var go = Instantiate(prefab);
            go.name = "SpareChep";
            foreach (var c in go.GetComponents<MonoBehaviour>())
                if (c is PalletBuilder || c is PlacedObject || c is BuildingData || c is BuildingHighlighter) { c.enabled = false; Destroy(c); }
            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            go.transform.SetParent(_jack.transform, false);
            go.transform.localPosition = new Vector3(0f, _forkY, ForkPalletZ[1]);
            go.transform.localRotation = Quaternion.identity;
            _spareChep = go;
        }

        /// <summary>The spare is used (it becomes the order's second pallet) or the trip is over.</summary>
        private void DropSpareChep()
        {
            if (_spareChep != null) Destroy(_spareChep);
            _spareChep = null;
        }
    }
}
