using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCore.Labor;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// RETURNS - REVERSE PICKING (Tad, 2026-10-17).
    ///
    /// A case-picked pallet left in a staging lane after its order shipped (it didn't fit, it never got loaded) was not
    /// billed to the customer (OrderService.ShipOrder pays SHIPPED pallets only). OrderService.ReclaimOrphanedFreight files
    /// a Return task for it. A selector takes a pallet jack, backs into the lane, lifts the pallet, and then visits every
    /// pick slot the cases came from in the EXACT REVERSE of the order they were picked (OutboundPalletBuilder records
    /// each case's source slot), carrying each case off the pallet and back into its slot. The empty pallet is then
    /// removed and the jack parked. Without a free jack the selector walks it instead: same slots, same order, by hand.
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        private const float ReturnCaseSeconds = 0.3f;

        private void StartReturnRun()
        {
            var task = _currentTask;
            var pallet = FindObjectsByType<OutboundPalletBuilder>().FirstOrDefault(p => p != null && p.ReturnId == task.PalletId);
            if (pallet == null || pallet.TotalCases == 0)
            {
                if (pallet != null) Destroy(pallet.gameObject);
                Debug.LogWarning($"[OrderSelectionTaskDriver] Return task {task.TaskId}: its pallet is gone or empty - closing it.");
                FinishReturn(task);
                return;
            }

            var jack = FindFreeJack();
            if (jack != null)
            {
                _jack = jack;
                _jackNav = jack.GetComponent<AiNavigation>();
                _jackAgent = jack.GetComponent<UnityEngine.AI.NavMeshAgent>();
                s_jackClaims[jack] = this;
                PalletJackParkingSpot.ReleaseJack(jack);
                _jackRun = StartCoroutine(JackReturnRoutine(task, pallet));
            }
            else
            {
                StartCoroutine(FootReturnRoutine(task, pallet));
            }
        }

        private IEnumerator JackReturnRoutine(WorkTask task, OutboundPalletBuilder pallet)
        {
            var identity = GetComponent<EmployeeIdentity>();

            // 1. Mount the jack.
            bool reached = false;
            _nav.CancelSeekPosition();
            _nav.SeekPosition(MountPointNear(transform.position), () => reached = true);
            float waited = 0f;
            while (!reached && waited < MountWalkTimeout && _jack != null && !_jack.IsOccupied) { waited += Time.deltaTime; yield return null; }
            if (!reached || _jack == null || _jack.IsOccupied)
            {
                ReleaseJack();
                yield return FootReturnRoutine(task, pallet);
                yield break;
            }
            _nav.CancelSeekPosition();
            BeginScriptedFoot();
            yield return StepIn();
            if (_jack == null) { ReleaseJack(); yield return FootReturnRoutine(task, pallet); yield break; }
            _jack.AssignOperator(identity);
            _footScripted = false;
            SetJackDrive(false, "selector mounted for a return");
            yield return MoveForks(0f);

            // 2. Back into the lane and pick the pallet up.
            if (!TryLanePose(pallet.transform.position, out int door, out string lane, out Vector3 forward, out Vector3 mouth))
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] Return: pallet isn't in a staging lane - lifting it where it stands.");
                forward = (_jack.transform.position - pallet.transform.position); forward.y = 0f;
                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
                mouth = Flat(pallet.transform.position + forward * (Mathf.Abs(ForkPalletZ[0]) + 2.5f), _jack.transform.position.y);
            }
            float driveY = _jack.transform.position.y;
            yield return DriveJackTo(mouth, forward, "return pallet pickup");
            if (!_jackLegOk || _jack == null) { ReleaseJack(); yield return FootReturnRoutine(task, pallet); yield break; }
            if (!string.IsNullOrEmpty(lane))
            {
                while (!_inventoryService.TryEnterLaneForDelivery(door, lane)) yield return null;
                _jackLaneLock = (door, lane);
            }
            Vector3 under = Flat(pallet.transform.position, driveY) - forward * ForkPalletZ[0];
            yield return JackMoveStraight(under, JackReverseSpeed);
            if (_jack == null || pallet == null) { ReleaseJack(); if (pallet != null) yield return FootReturnRoutine(task, pallet); else FinishReturn(task); yield break; }

            pallet.SetNavObstacleActive(false);
            pallet.Stage = OutboundPalletBuilder.PalletStage.Moving;
            _pallets.Clear();
            _pallets.Add(pallet);
            SeatPalletOnJack(pallet, 0);
            yield return MoveForks(ForkLift);
            yield return JackMoveStraight(mouth, JackDriveSpeed);
            ReleaseJackLaneLock();

            // 3. Reverse pick: last case picked goes back first, slot by slot.
            while (pallet != null && pallet.TotalCases > 0 && _jack != null)
            {
                string source = pallet.CaseSources.Count > 0 ? pallet.CaseSources[pallet.CaseSources.Count - 1] : "";
                LocationData loc = null;
                if (!string.IsNullOrEmpty(source)) LocationRegistry.TryGet(source, out loc);
                if (loc == null || !TryGetPickGeometry(loc, out Vector3 slotFlat, out Vector3 toAisle))
                {
                    // Source unknown: the case still goes back into stock (best available slot), no trip needed.
                    if (pallet.RemoveLastCase(out string sku0, out _)) PutCaseBack(null, sku0);
                    continue;
                }

                // A return walks the pick route BACKWARDS, so it faces against the aisle's pick direction.
                Vector3 heading = -PickHeading(toAisle, loc);
                Vector3 park = slotFlat + toAisle * ParkOffset - heading * ForkPalletZ[0];
                yield return DriveJackTo(park, heading, $"return to {loc.Address}");
                if (!_jackLegOk || _jack == null) break;

                yield return StepOut(slotFlat);
                float y = transform.position.y;
                Vector3 faceSpot = Flat(slotFlat + toAisle * PickFaceStandOff, y);
                // Every consecutive case that came from this slot goes back on this visit.
                while (pallet != null && pallet.TotalCases > 0 &&
                       pallet.CaseSources.Count > 0 && pallet.CaseSources[pallet.CaseSources.Count - 1] == source)
                {
                    Vector3 palletSide = Flat(pallet.transform.position - toAisle * (ParkOffset - PalletSideOff), y);
                    yield return WalkTo(palletSide);
                    yield return FaceDir(toAisle);
                    yield return new WaitForSeconds(ReturnCaseSeconds);
                    if (!pallet.RemoveLastCase(out string sku, out _)) break;
                    var skuData = _inventoryService.GetSkuData(sku);
                    if (skuData != null && skuData.Prefab != null) SpawnHeldCase(skuData.Prefab, skuData.CaseHeight);
                    SetAnimBool("CarryCase", true);
                    yield return WalkTo(faceSpot);
                    yield return FaceDir(-toAisle);
                    yield return new WaitForSeconds(GrabSeconds);
                    DestroyHeldCase();
                    SetAnimBool("CarryCase", false);
                    PutCaseBack(source, sku);
                }
                yield return StepIn();
                if (_jack == null) break;
                _jack.StepOn();
            }

            // 4. Done (or the jack went away): anything still on the pallet goes back without a trip, then tidy up.
            if (pallet != null)
            {
                while (pallet.RemoveLastCase(out string skuLeft, out string srcLeft)) PutCaseBack(srcLeft, skuLeft);
                _pallets.Remove(pallet);
                Destroy(pallet.gameObject);
            }
            yield return MoveForks(0f);
            yield return ParkJack();
            if (_jack != null) yield return StepOut(_jack.transform.position - _jack.transform.right);
            ReleaseJack();
            FinishReturn(task);
        }

        /// <summary>No jack: the selector walks to each source slot in reverse picking order and puts the cases back.</summary>
        private IEnumerator FootReturnRoutine(WorkTask task, OutboundPalletBuilder pallet)
        {
            EndScriptedFoot();
            while (pallet != null && pallet.TotalCases > 0)
            {
                string source = pallet.CaseSources.Count > 0 ? pallet.CaseSources[pallet.CaseSources.Count - 1] : "";
                LocationData loc = null;
                if (!string.IsNullOrEmpty(source)) LocationRegistry.TryGet(source, out loc);
                if (loc != null)
                {
                    bool arrived = false;
                    _nav.CancelSeekPosition();
                    _nav.SeekPosition(loc.WorldPosition, () => arrived = true);
                    float t = 0f;
                    while (!arrived && t < 45f) { t += Time.deltaTime; yield return null; }
                }
                while (pallet != null && pallet.TotalCases > 0 &&
                       (pallet.CaseSources.Count == 0 || pallet.CaseSources[pallet.CaseSources.Count - 1] == source))
                {
                    if (!pallet.RemoveLastCase(out string sku, out _)) break;
                    PutCaseBack(source, sku);
                    yield return new WaitForSeconds(ReturnCaseSeconds);
                }
            }
            if (pallet != null) Destroy(pallet.gameObject);
            FinishReturn(task);
        }

        /// <summary>
        /// One case back into stock: into the pallet in its source pick slot (count + the hidden case model reappears).
        /// If that slot is empty now or holds another SKU, into another pick face of the same SKU, then any pallet of it.
        /// </summary>
        private bool PutCaseBack(string address, string skuId)
        {
            if (string.IsNullOrEmpty(skuId)) return false;
            LocationData loc = null;
            if (!string.IsNullOrEmpty(address) && LocationRegistry.TryGet(address, out var src) &&
                src.SkuId == skuId && !string.IsNullOrEmpty(src.PalletId))
                loc = src;
            if (loc == null)
                foreach (var a in SlotAssignmentService.GetSlotsForSku(skuId))
                    if (LocationRegistry.TryGet(a, out var l) && l.SkuId == skuId && !string.IsNullOrEmpty(l.PalletId)) { loc = l; break; }

            if (loc != null)
            {
                var rec = _inventoryService.GetPallet(loc.PalletId);
                if (rec != null) rec.Quantity += 1;
                loc.AddCases(1);
                var faceGo = FindPickFacePallet(loc, loc.PalletId);
                if (faceGo != null)
                {
                    faceGo.GetComponent<PalletData>()?.ReduceQuantity(-1);
                    RevealOneCase(faceGo);
                }
                return true;
            }

            var any = _inventoryService.GetPalletsBySku(skuId)?.FirstOrDefault();
            if (any != null)
            {
                any.Quantity += 1;
                PalletMasterLink.Find(any.PalletId)?.GetComponent<PalletData>()?.ReduceQuantity(-1);
                return true;
            }
            Debug.LogWarning($"[OrderSelectionTaskDriver] Return: no slot or pallet of {skuId} left to put a case back into - the case is written off.");
            return false;
        }

        /// <summary>Shows one case that picking hid on a pick-face pallet: the lowest hidden one (picking hides top-down,
        /// so the lowest hidden case was the last to go).</summary>
        private static void RevealOneCase(GameObject palletGo)
        {
            var load = palletGo.transform.Find("PalletLoad");
            if (load == null) return;
            Transform best = null;
            foreach (Transform c in load)
                if (!c.gameObject.activeSelf && (best == null || c.localPosition.y < best.localPosition.y)) best = c;
            if (best != null) best.gameObject.SetActive(true);
        }

        /// <summary>The lane a pallet stands in, the direction the jack must face to back into it (away from the lane, from its
        /// open end), and the line-up point - the same approach JackStageRoutine uses.</summary>
        private bool TryLanePose(Vector3 palletPos, out int door, out string lane, out Vector3 forward, out Vector3 mouth)
        {
            door = 0; lane = null; forward = Vector3.forward; mouth = palletPos;
            var grid = FindAnyObjectByType<PlacementGrid>();
            if (grid == null || !LaneNamingService.TryGetSlot(grid.WorldToCell(palletPos), out var slot)) return false;
            if (!LaneNamingService.TryGetLaneGeometry(slot.DoorNumber, slot.Lane, out var geo)) return false;
            door = slot.DoorNumber; lane = slot.Lane;
            Vector3 axis = geo.DepthAxis.sqrMagnitude > 0.0001f ? geo.DepthAxis.normalized : Vector3.forward;
            var laneSlots = LaneNamingService.GetLane(door, lane);
            bool farBlocked = LaneSlotsBlocked(door, lane, s => s > slot.Slot, null);
            forward = farBlocked ? -axis : axis;
            Vector3 endSlot = Vector3.zero;
            bool haveEnd = laneSlots.Count > 0 &&
                           LaneNamingService.TryGetSlotWorldPos((farBlocked ? laneSlots[0] : laneSlots[laneSlots.Count - 1]).Cell, out endSlot);
            if (!haveEnd) endSlot = farBlocked ? geo.EntryPoint : geo.ExitPoint - axis * 2f;
            float y = _jack != null ? _jack.transform.position.y : palletPos.y;
            Vector3 lineUpPt = Flat(endSlot + forward * (StagingCellPitch * 0.5f + LaneEntryMargin), y);
            mouth = lineUpPt + forward * LineUpDistance();
            return true;
        }

        private void FinishReturn(WorkTask task)
        {
            if (task != null) _workQueue.CompleteTask(task.TaskId);
            Debug.Log($"[OrderSelectionTaskDriver] '{name}' finished a return - the cases are back in their pick slots.");
            _currentTask = null;
            _currentOrder = null;
            _pallets.Clear();
            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }
    }
}
