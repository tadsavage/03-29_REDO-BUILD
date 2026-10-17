using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCore.Labor;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// ORDER SELECTOR ON A PALLET JACK (Tad, 2026-10-16).
    ///
    /// When a selector claims an OrderSelect task and a free pallet jack exists:
    ///   1. walk to the jack and mount it (hands on the handlebars = the Drive_PalletJack pose),
    ///   2. an empty CHEP sits on the forks,
    ///   3. per pick: drive down the aisle and park beside the pick slot, step off, walk to the slot, take a
    ///      case in hand (CarryCase pose + a real case model), turn round, set it on the pallet, repeat,
    ///      then step back on,
    ///   4. when done: drive to the staging lane's open end, turn to face away from the door, back in
    ///      forks-first, lower and leave the pallet(s) (unparented, Staged), drive out, step off and leave
    ///      the jack parked at the lane mouth.
    ///
    /// Same order logic as the on-foot path (same slot choice, short-pick and pallet-pick rules, same staging
    /// lane rules, same PlacePalletsAtStagingSlot). With NO free jack, or if the jack can't path somewhere, the
    /// selector falls back to the old on-foot behaviour (pallets ride the selector) so the order never stalls.
    ///
    /// The jack is driven the same hybrid way as the Reach Truck: its own NavMeshAgent for the open-floor legs,
    /// scripted transform moves for the precise parking/backing. While mounted, the selector's own navigation is
    /// off (MHEOperatorSlot), and the short walks off the jack are scripted too, so their positions are exact.
    /// Geometry constants below were measured on PltJack_FULL (forks run 0.2–2.54 m behind the operator anchor).
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        // ── Tunables ────────────────────────────────────────────────────────────────────────────────
        private const float JackDriveSpeed   = 1.8f;   // m/s, scripted forward legs
        private const float JackReverseSpeed = 0.9f;   // m/s, backing into the lane
        private const float JackTurnSpeed    = 110f;   // deg/s, turning in place
        private const float FootWalkSpeed    = 1.4f;   // matches the staff walk speed
        private const float FootTurnSpeed    = 480f;   // deg/s
        private const float GrabSeconds      = 0.35f;  // reaching into the slot
        private const float PlaceSeconds     = 0.30f;  // setting the case down on the pallet
        private const float ParkOffset       = 2.1f;   // jack centreline distance from the pick face (into the aisle)
        private const float PickFaceStandOff = 0.65f;  // selector stops this far out from the slot pivot (≈ the pallet's front edge + a step) so they don't walk into the cases
        private const float PalletSideOff    = 1.20f;  // where the selector stands to load the pallet
        private const float ForkLift         = 0.25f;  // pallet raised this high while it rides the jack (Tad, 2026-10-16)
        private const float ForkMoveSeconds  = 0.6f;   // time to raise / lower
        private const float LaneEntryMargin  = 1.0f;   // LaneLineUp stops this far outside the lane's last slot edge before reversing in
        private const float MountWalkTimeout = 60f;
        private const float StagingCellPitch = 1.33f;

        /// <summary>Pallet positions on the jack's double-length forks, local Z behind the operator anchor:
        /// first pallet at the fork TIPS (so it goes deepest into a lane), second next to the operator.
        /// A pallet's 40" side runs along the forks, so its rotation matches the jack's.</summary>
        private static readonly float[] ForkPalletZ = { -2.00f, -0.95f };
        /// <summary>Hands in the Carry_Case pose sit ~1.16 m up and 0.40 m in front of the feet (measured on the
        /// male rig), so a held case is centred there; its pivot is at its base.</summary>
        private const float HeldHandsHeight = 1.16f, HeldHandsForward = 0.40f;

        // ── Claims: one selector per jack ─────────────────────────────────────────────────────────────
        private static readonly Dictionary<MHEOperatorSlot, OrderSelectionTaskDriver> s_jackClaims = new();
        private static bool s_warnedNoJack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetJackStatics() { s_jackClaims.Clear(); s_warnedNoJack = false; s_warnedNoParking = false; }

        private MHEOperatorSlot _jack;
        private AiNavigation _jackNav;
        private NavMeshAgent _jackAgent;
        private Coroutine _jackRun;
        private GameObject _heldCase;
        private (int Door, string Lane)? _jackLaneLock;
        private bool _jackLegOk;
        private float _forkY;          // current pallet height on the forks (0 = lowered)
        private bool _footScripted;    // own navigation switched off so script can walk the body precisely

        private bool OnJack => _jack != null;

        /// <summary>Called right after a task is claimed. True = the jack run owns this order now.</summary>
        private bool TryStartJackRun()
        {
            // Nothing a case picker can take right now (no stock, or the rest is Reach Truck pallets): don't fetch
            // a jack for it. The on-foot path makes the exact same out-of-stock decisions, with no walk.
            if (!TryFindBestPickLocation(_currentOrder, out _, out _, out _)) return false;

            var jack = FindFreeJack();
            if (jack == null)
            {
                if (!s_warnedNoJack)
                {
                    s_warnedNoJack = true;
                    Debug.LogWarning("[OrderSelectionTaskDriver] No free pallet jack — order selector is picking by hand. Place a Pallet Jack (Vehicles) for them.");
                    UIToast.Show("Order Selectors need a Pallet Jack - picking by hand until one is placed (Vehicles).");
                }
                return false;
            }

            _jack = jack;
            _jackNav = jack.GetComponent<AiNavigation>();
            _jackAgent = jack.GetComponent<NavMeshAgent>();
            s_jackClaims[jack] = this;
            PalletJackParkingSpot.ReleaseJack(jack);   // leaving its bay (if it was parked in one)
            _jackRun = StartCoroutine(JackOrderRoutine());
            return true;
        }

        private MHEOperatorSlot FindFreeJack()
        {
            MHEOperatorSlot best = null;
            float bestD = float.MaxValue;
            foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
            {
                if (slot == null || !slot.isActiveAndEnabled || !slot.IsPalletJack || slot.IsOccupied) continue;
                if (slot.GetComponent<AiNavigation>() == null || slot.GetComponent<PlacedObject>() == null) continue; // build-menu ghost
                if (s_jackClaims.TryGetValue(slot, out var owner) && owner != null && owner != this) continue;
                float d = PlanarDist(slot.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = slot; }
            }
            return best;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // The run
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private IEnumerator JackOrderRoutine()
        {
            var identity = GetComponent<EmployeeIdentity>();

            // 1. Walk to the jack's nearer mount point (MountLeft / MountRight), then step in.
            bool reached = false;
            _nav.CancelSeekPosition();
            _nav.SeekPosition(MountPointNear(transform.position), () => reached = true);
            float waited = 0f;
            while (!reached && waited < MountWalkTimeout)
            {
                if (_jack == null || _jack.IsOccupied) break;
                waited += Time.deltaTime;
                yield return null;
            }
            if (!reached || _jack == null || _jack.IsOccupied)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' could not get to pallet jack — picking order {_currentOrder.OrderId} by hand.");
                ReleaseJack();
                AdvanceToNextPick();
                yield break;
            }
            _nav.CancelSeekPosition();

            // Scripted from here so the step-in is exact: mount point → turn to the operator spot → step in.
            BeginScriptedFoot();
            yield return StepIn();
            if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
            _jack.AssignOperator(identity);
            _footScripted = false;   // the MHE slot owns the body's navigation state while riding
            SetJackDrive(false, "selector mounted");
            EnsurePallet();
            yield return MoveForks(ForkLift);

            // 2. Pick, slot by slot.
            OrderData.OrderStatus completion = OrderData.OrderStatus.Staged;
            while (true)
            {
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }

                if (_currentOrder.IsFullyPicked)
                {
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) fully picked on the pallet jack — {TotalCasesOnPallets()} case(s). Driving to staging.");
                    _orderService.MarkOrderFulfilled(_currentOrder.OrderId);
                    break;
                }

                if (!TryFindBestPickLocation(_currentOrder, out var location, out var lineItem, out int takeQty))
                {
                    // Same exits as AdvanceToNextPick, in the same order.
                    if (_orderService.RemainingIsAllPalletPick(_currentOrder))
                    {
                        completion = OrderData.OrderStatus.PartiallyPicked;
                        break;
                    }
                    if (TotalCasesOnPallets() > 0)
                    {
                        Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no reachable pick location for its remaining line item(s) — staging short at {_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits}.");
                        break;
                    }
                    if (_orderService.HasLivePalletPicks(_currentOrder))
                    {
                        completion = OrderData.OrderStatus.PartiallyPicked;
                        break;
                    }
                    // Nothing picked at all: put the jack back and hand the order back to the player.
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no reachable pick location for any line item and nothing was picked — returning it to Open.");
                    DestroyEmptyPallets();
                    ReleaseJack();
                    ReturnOrderToOpen();
                    yield break;
                }

                // Drive down the aisle and park alongside the slot, active pallet level with it.
                int palletIndex = Mathf.Clamp(_pallets.Count - 1, 0, ForkPalletZ.Length - 1);
                if (!TryGetPickGeometry(location, out Vector3 slotFlat, out Vector3 toAisle))
                {
                    yield return AbandonJackOnFoot($"pick slot {location.Address} has no usable position");
                    yield break;
                }
                Vector3 heading = PickHeading(toAisle);
                Vector3 park = slotFlat + toAisle * ParkOffset - heading * ForkPalletZ[palletIndex];
                yield return DriveJackTo(park, heading, $"pick slot {location.Address}");
                if (!_jackLegOk)
                {
                    yield return AbandonJackOnFoot($"could not drive the jack to pick slot {location.Address}");
                    yield break;
                }

                // Off the jack on the rack side (via that side's mount point), case by case.
                yield return StepOut(slotFlat);
                yield return PickCasesFromSlot(location, lineItem, takeQty, slotFlat, toAisle);
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }

                // Back on: nearer mount point → turn → step in.
                yield return StepIn();
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                _jack.StepOn();

                if (_capReached)
                {
                    _capReached = false;
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) — both pallets on the jack are full ({_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits}). Driving to staging.");
                    _currentOrder.Status = OrderData.OrderStatus.PartiallyPicked;
                    completion = OrderData.OrderStatus.Staged;
                    _cubingCapHit = true;
                    break;
                }
            }

            // 3. Stage.
            yield return JackStageRoutine(completion);
        }

        private bool _capReached;

        /// <summary>The round trips between the slot and the pallet. Stock and pick counts change at the moment
        /// each case lands on the pallet, so an interrupted trip never counts a case that isn't there.</summary>
        private IEnumerator PickCasesFromSlot(LocationData location, OrderLineItem lineItem, int takeQty, Vector3 slotFlat, Vector3 toAisle)
        {
            var sku = _inventoryService.GetSkuData(lineItem.SkuId);
            if (sku == null || sku.Prefab == null)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] SKU {lineItem.SkuId} missing SkuData/prefab — skipping this pick at {location.Address}.");
                yield break;
            }
            Vector3 caseDim = new Vector3(sku.CaseWidth, sku.CaseHeight, sku.CaseLength);
            int ti = Mathf.Max(1, sku.Ti);
            int hi = Mathf.Max(1, sku.Hi);

            float y = transform.position.y;
            Vector3 faceSpot = Flat(slotFlat + toAisle * PickFaceStandOff, y);

            for (int i = 0; i < takeQty; i++)
            {
                if (_jack == null) yield break;

                // To the slot, reach in, take a case.
                yield return WalkTo(faceSpot);
                yield return FaceDir(-toAisle);
                // Cases in the back half of the slot: lean in to reach them.
                bool reach = NextPickIsDeep(location, slotFlat, toAisle);
                if (reach) SetLean(1f);
                yield return new WaitForSeconds(GrabSeconds + (reach ? 0.3f : 0f));
                // The case leaves the slot (top layer first) the moment it appears in their hands.
                string facePalletId = location.PalletId;
                if (!string.IsNullOrEmpty(facePalletId))
                    _inventoryService.PickFromPallet(facePalletId, 1);
                location.Pick(1);
                RemoveCaseFromPickFace(location, facePalletId);
                SpawnHeldCase(sku.Prefab, caseDim.y);
                SetLean(0f);
                SetAnimBool("CarryCase", true);

                // Turn round, carry it to the pallet, set it down.
                var activePallet = _pallets[_pallets.Count - 1];
                if (activePallet == null) { DestroyHeldCase(); yield break; }
                Vector3 palletPos = activePallet.transform.position;
                Vector3 palletSide = Flat(palletPos - toAisle * (ParkOffset - PalletSideOff), y);
                yield return WalkTo(palletSide);
                yield return FaceDir(toAisle);
                yield return new WaitForSeconds(PlaceSeconds);

                DestroyHeldCase();
                SetAnimBool("CarryCase", false);

                lineItem.QuantityPicked++;
                activePallet.AddCase(lineItem.SkuId, sku.Prefab, caseDim, ti, hi);

                if (_currentOrder.IsFullyPicked) yield break;

                if (activePallet.IsCubedOut)
                {
                    if (_pallets.Count < MaxPalletsPerOrder)
                    {
                        StartNextPallet();
                        // The next pallet sits elsewhere on the forks — finish this line from the next visit,
                        // when the jack re-parks with the new pallet level with the slot.
                        yield break;
                    }
                    _capReached = true;
                    yield break;
                }
            }
        }

        /// <summary>Lane resolve, drive to the lane's open end, back in, leave the pallets, drive out.</summary>
        private IEnumerator JackStageRoutine(OrderData.OrderStatus completion)
        {
            var order = _currentOrder;
            var task = _currentTask;

            DestroyEmptyPallets();
            if (LivePalletCount() == 0)
            {
                // Nothing physical to stage (the rest of the order is pallet picks): just finish.
                order.Status = completion;
                ReleaseJack();
                FinishOrderCleanup(task);
                yield break;
            }

            // Lane + slot, retrying while every lane is full (the selector waits on the jack).
            string resolvedLane = null;
            LaneNamingService.LaneSlot slot = default;
            Vector3 slotPos = default, depthAxis = Vector3.forward;
            bool warned = false;
            while (true)
            {
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared", completion); yield break; }
                if (TryResolveStagingTarget(order, out resolvedLane, out slot, out slotPos, out depthAxis, out string fail))
                    break;
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) can't stage yet — {fail}. Waiting on the pallet jack.");
                    UIToast.Show($"Order Selector can't stage order for {order.CustomerName} - no free staging lane. Waiting to retry.");
                }
                yield return new WaitForSeconds(StagingRetryInterval);
            }

            int door = slot.DoorNumber;   // may be a neighbouring door's lane (overflow)
            if (!LaneNamingService.TryGetLaneGeometry(door, resolvedLane, out var geo))
            {
                yield return AbandonJackOnFoot($"lane {door}{resolvedLane} has no geometry", completion);
                yield break;
            }
            Vector3 axis = geo.DepthAxis.sqrMagnitude > 0.0001f ? geo.DepthAxis.normalized : depthAxis;

            // Normal approach: from the far (open-floor) end, facing AWAY from the door, forks toward it, backing
            // toward the door. Never drive through freight: if anything stands between the open end and the target
            // slot, come in from the door end instead (facing the door, backing away from it); if both ends are
            // blocked, don't force it — the old placement takes over.
            float driveY = _jack.transform.position.y;
            var tipGo = _pallets.FirstOrDefault(p => p != null)?.gameObject;
            bool farBlocked  = LaneSlotsBlocked(door, resolvedLane, s => s > slot.Slot, tipGo);
            bool nearBlocked = LaneSlotsBlocked(door, resolvedLane, s => s < slot.Slot, tipGo);
            if (farBlocked && nearBlocked)
            {
                yield return AbandonJackOnFoot($"lane {door}{resolvedLane} has freight on both sides of slot {slot.Name}", completion);
                yield break;
            }
            Vector3 forward = farBlocked ? -axis : axis;

            // LINE UP (Tad, 2026-10-16): the jack's LaneLineUp transform (rear, at the fork tips) is placed on the
            // lane's centre line just outside its open end, jack facing away from the lane — THEN it reverses
            // straight in. So the jack sits at  lineUp + forward * (anchor→LaneLineUp distance). Turning in place
            // there sweeps the forks round a circle that stays LaneEntryMargin clear of the lane ends, so it never
            // swings over neighbouring lanes, and the reverse starts at the lane's actual entry.
            var laneSlots = LaneNamingService.GetLane(door, resolvedLane);
            Vector3 endSlot = Vector3.zero;
            bool haveEnd = laneSlots.Count > 0 &&
                           LaneNamingService.TryGetSlotWorldPos((farBlocked ? laneSlots[0] : laneSlots[laneSlots.Count - 1]).Cell, out endSlot);
            if (!haveEnd) endSlot = farBlocked ? geo.EntryPoint : geo.ExitPoint - axis * 2f;
            Vector3 lineUpPt = Flat(endSlot + forward * (StagingCellPitch * 0.5f + LaneEntryMargin), driveY);
            Vector3 mouth = lineUpPt + forward * LineUpDistance();
            yield return DriveJackTo(mouth, forward, $"staging lane {door}{resolvedLane} line-up");
            if (!_jackLegOk)
            {
                yield return AbandonJackOnFoot($"could not drive the jack to staging lane {door}{resolvedLane}", completion);
                yield break;
            }

            // One machine in a lane at a time (same lock as the reach trucks and dock stockers).
            while (!_inventoryService.TryEnterLaneForDelivery(door, resolvedLane))
                yield return null;
            _jackLaneLock = (door, resolvedLane);

            // Back in until the tip pallet is over the target slot. If that slot already holds a pallet (the order
            // is stacking onto it), stop one cell short — a pallet jack can't lift onto a stack, the drop snaps it.
            Vector3 dropAt = Flat(slotPos, driveY);
            if (CellHasFreight(door, resolvedLane, slot.Cell, tipGo)) dropAt += forward * StagingCellPitch;
            Vector3 backTo = dropAt - forward * ForkPalletZ[0];
            yield return JackMoveStraight(backTo, JackReverseSpeed);

            // Lower and leave them.
            yield return MoveForks(0f);
            PlacePalletsAtStagingSlot(order, door, resolvedLane, slotPos, axis);
            order.Status = completion;
            _pallets.Clear();

            // Drive back out to the line-up spot, release the lane, step off (via a mount point) and leave the jack.
            yield return JackMoveStraight(mouth, JackDriveSpeed);
            ReleaseJackLaneLock();

            Debug.Log($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) staged in {door}{resolvedLane} by pallet jack.");

            // Park it in a Pallet Jack Parking bay instead of leaving it across the lane mouth.
            yield return ParkJack();
            if (_jack != null) yield return StepOut(_jack.transform.position - _jack.transform.right);
            ReleaseJack();
            FinishOrderCleanup(task);
        }

        private static bool s_warnedNoParking;

        /// <summary>Drives the (empty) jack to the nearest free Pallet Jack Parking bay. With no bay placed it stays
        /// at the lane's line-up spot and the player is told once.</summary>
        private IEnumerator ParkJack()
        {
            if (_jack == null) yield break;
            var spot = PalletJackParkingSpot.FindNearestFree(_jack.transform.position, _jack);
            if (spot == null)
            {
                if (!s_warnedNoParking)
                {
                    s_warnedNoParking = true;
                    UIToast.Show("Pallet jacks have nowhere to park - place Pallet Jack Parking (Vehicles) so they stop blocking the lanes.");
                }
                yield break;
            }
            spot.Reserve(_jack);
            spot.ParkPose(_jack.transform.forward, out Vector3 origin, out Vector3 forward);
            yield return DriveJackTo(origin, forward, "pallet jack parking");
            if (!_jackLegOk)
            {
                if (_jack != null) spot.Release(_jack);
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' couldn't drive the pallet jack to its parking bay — leaving it where it is.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Fallback + release
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Gives the jack back and carries on ON FOOT with whatever is on the forks (the old
        /// placeholder: pallets ride the selector). The order is never lost to a jack problem.</summary>
        private IEnumerator AbandonJackOnFoot(string reason, OrderData.OrderStatus? stagingWith = null)
        {
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' leaving the pallet jack ({reason}) — carrying on by hand with order {_currentOrder?.OrderId}.");
            DestroyHeldCase();
            ReleaseJack();
            for (int i = 0; i < _pallets.Count; i++)
            {
                var p = _pallets[i];
                if (p == null) continue;
                p.transform.SetParent(transform, false);
                p.transform.localPosition = PalletOffsets[Mathf.Min(i, PalletOffsets.Length - 1)];
                p.transform.localRotation = Quaternion.identity;
            }
            yield return null; // let the selector's agent register again after re-enabling
            if (_currentOrder == null) yield break;
            if (stagingWith.HasValue) FinishOrder(stagingWith.Value);
            else AdvanceToNextPick();
        }

        /// <summary>Gets off (if still on), hands the jack back to its parked idle state and drops the claim.</summary>
        private void ReleaseJack()
        {
            ReleaseJackLaneLock();
            DestroyHeldCase();
            SetLean(0f);
            SetAnimBool("CarryCase", false);
            SetAnimBool("IsWalking", false);

            var identity = GetComponent<EmployeeIdentity>();
            if (_jack != null)
            {
                if (_jack.CurrentOperator == identity) { _jack.VacateOperator(); _footScripted = false; }
                s_jackClaims.Remove(_jack);
            }
            EndScriptedFoot();   // walked by script but never mounted (e.g. the jack vanished mid step-in)
            _jack = null;
            _jackNav = null;
            _jackAgent = null;
            _jackRun = null;
            _forkY = 0f;

            // Own navigation is back on: resync the agent to where the script left the body.
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(transform.position);
        }

        private void ReleaseJackLaneLock()
        {
            if (_jackLaneLock == null) return;
            _inventoryService?.ReleaseLaneEntry(_jackLaneLock.Value.Door, _jackLaneLock.Value.Lane);
            _jackLaneLock = null;
        }

        private void OnDisable()
        {
            // Assignment changed / fired / destroyed mid-run: never leave a lane locked or a jack claimed.
            ReleaseJackLaneLock();
            DestroyHeldCase();
            if (_jack != null)
            {
                var identity = GetComponent<EmployeeIdentity>();
                if (gameObject.activeInHierarchy && _jack.CurrentOperator == identity) { _jack.VacateOperator(); _footScripted = false; }
                s_jackClaims.Remove(_jack);
                _jack = null;
            }
            if (gameObject.activeInHierarchy) EndScriptedFoot();
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Pallets on the forks
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Puts a freshly spawned pallet on the jack's forks (called from StartNextPallet).</summary>
        private void SeatPalletOnJack(OutboundPalletBuilder pallet, int index)
        {
            pallet.transform.SetParent(_jack.transform, false);
            pallet.transform.localPosition = new Vector3(0f, _forkY, ForkPalletZ[Mathf.Min(index, ForkPalletZ.Length - 1)]);
            pallet.transform.localRotation = Quaternion.identity;
            pallet.SetNavObstacleActive(false);
        }

        /// <summary>Raises / lowers every pallet riding the forks to <paramref name="targetY"/> over ForkMoveSeconds.
        /// Up while it carries a pallet and picks; down to set it in the lane.</summary>
        private IEnumerator MoveForks(float targetY)
        {
            float start = _forkY;
            for (float t = 0f; t < ForkMoveSeconds; t += Time.deltaTime)
            {
                ApplyForkY(Mathf.Lerp(start, targetY, Mathf.SmoothStep(0f, 1f, t / ForkMoveSeconds)));
                yield return null;
            }
            ApplyForkY(targetY);
        }

        private void ApplyForkY(float y)
        {
            _forkY = y;
            if (_jack == null) return;
            foreach (var p in _pallets)
            {
                if (p == null || p.transform.parent != _jack.transform) continue;
                var lp = p.transform.localPosition;
                p.transform.localPosition = new Vector3(lp.x, y, lp.z);
            }
        }

        private void DestroyEmptyPallets()
        {
            for (int i = _pallets.Count - 1; i >= 0; i--)
            {
                var p = _pallets[i];
                if (p != null && p.TotalCases > 0) continue;
                if (p != null) Destroy(p.gameObject);
                _pallets.RemoveAt(i);
            }
        }

        private int TotalCasesOnPallets() => _pallets.Sum(p => p != null ? p.TotalCases : 0);

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Held case + animator
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private void SpawnHeldCase(GameObject prefab, float caseHeight)
        {
            DestroyHeldCase();
            var go = Instantiate(prefab, transform);
            go.name = "HeldCase";
            go.transform.localPosition = new Vector3(0f, HeldHandsHeight - caseHeight * 0.5f, HeldHandsForward);
            go.transform.localRotation = Quaternion.identity;
            // Same stripping OutboundPalletBuilder.AddCase does: a build-menu prefab must not self-register.
            var po = go.GetComponent<PlacedObject>();
            if (po != null) { po.enabled = false; Destroy(po); }
            var bd = go.GetComponent<BuildingData>();
            if (bd != null) Destroy(bd);
            var bh = go.GetComponent<BuildingHighlighter>();
            if (bh != null) Destroy(bh);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            _heldCase = go;
        }

        private void DestroyHeldCase()
        {
            if (_heldCase != null) Destroy(_heldCase);
            _heldCase = null;
        }

        /// <summary>Sets a bool on every Animator in the body (the base rig and the modular avatar), if it exists.</summary>
        private void SetAnimBool(string param, bool value)
        {
            foreach (var anim in GetComponentsInChildren<Animator>(true))
            {
                if (anim == null || anim.runtimeAnimatorController == null) continue;
                foreach (var p in anim.parameters)
                    if (p.name == param && p.type == AnimatorControllerParameterType.Bool) { anim.SetBool(param, value); break; }

                // The Carry layer sits at weight 0 and is only switched on while a case is held. On a humanoid
                // rig an empty state on a full-weight override layer still forces the arms into a reference pose,
                // which froze every walking employee's arms (2026-10-17).
                if (param == "CarryCase")
                {
                    int layer = anim.GetLayerIndex("Carry");
                    if (layer >= 0) anim.SetLayerWeight(layer, value ? 1f : 0f);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Scripted movement — selector on foot
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        // ── Mounting: MountLeft / MountRight on the jack prefab ─────────────────────────────────────────
        // The operator never cuts across the machine: off = anchor → a mount point → wherever; on = nearer
        // mount point → turn to face the operator spot → step in.

        private Vector3 MountPointNear(Vector3 near)
        {
            if (_jack == null) return near;
            Transform jt = _jack.transform;
            Transform l = jt.Find("MountLeft"), r = jt.Find("MountRight");
            Vector3 lp = l != null ? l.position : _jack.OperatorAnchor.position - jt.right;
            Vector3 rp = r != null ? r.position : _jack.OperatorAnchor.position + jt.right;
            return PlanarDist(lp, near) <= PlanarDist(rp, near) ? lp : rp;
        }

        /// <summary>From wherever the selector stands (their navigation already off): nearer mount point, turn to
        /// face the operator spot, step onto it, face the way the jack points. Caller then mounts/StepOn()s.</summary>
        private IEnumerator StepIn()
        {
            if (_jack == null) yield break;
            yield return WalkTo(MountPointNear(transform.position));
            if (_jack == null) yield break;
            Vector3 anchor = _jack.OperatorAnchor.position;
            yield return FaceDir(anchor - transform.position);
            yield return WalkTo(anchor);
            if (_jack == null) yield break;
            yield return FaceDir(_jack.transform.forward);
        }

        /// <summary>Off the jack on the side nearest <paramref name="toward"/>: anchor → that mount point.</summary>
        private IEnumerator StepOut(Vector3 toward)
        {
            if (_jack == null) yield break;
            _jack.StepOff();
            yield return WalkTo(MountPointNear(toward));
        }

        /// <summary>Own navigation off (as if riding) so the script can walk the body to exact points.</summary>
        private void BeginScriptedFoot()
        {
            if (_footScripted) return;
            _nav.CancelSeekPosition();
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }
            _nav.enabled = false;
            GetComponent<AgentAnimation>()?.SetRidingMHE(true);
            _footScripted = true;
        }

        private void EndScriptedFoot()
        {
            if (!_footScripted) return;
            _footScripted = false;
            SetAnimBool("IsWalking", false);
            GetComponent<AgentAnimation>()?.SetRidingMHE(false);
            if (_nav != null) _nav.enabled = true;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(transform.position);
            }
        }

        /// <summary>Distance from the operator anchor back to the jack's LaneLineUp transform (rear, fork tips).</summary>
        private float LineUpDistance()
        {
            Transform lu = _jack != null ? _jack.transform.Find("LaneLineUp") : null;
            return lu != null ? Mathf.Abs(_jack.transform.InverseTransformPoint(lu.position).z) : 2.68f;
        }

        // ── Pick face: cases disappear as they're picked ────────────────────────────────────────────────

        /// <summary>The pick slot's pallet shows exactly as many cases as it holds: picked cases vanish from the TOP
        /// layer down. When it's empty the empty pallet goes too, which frees the slot (LocationData.Pick already
        /// released it) for ReplenishmentService to file a reach-truck replenishment through the Work Queue.
        /// An empty pallet left in the rack would sit where the replenishment drops, and on reload the tracker
        /// would rebuild it from its PalletData as full phantom stock.</summary>
        private void RemoveCaseFromPickFace(LocationData location, string palletId)
        {
            GameObject palletGo = FindPickFacePallet(location, palletId);
            if (palletGo == null) return;

            // Remaining cases: the pallet's record when it has one (gone = empty); without a record, the slot's own count.
            int remaining = string.IsNullOrEmpty(palletId)
                ? location.Quantity
                : (_inventoryService.GetPallet(palletId)?.Quantity ?? 0);
            palletGo.GetComponent<PalletData>()?.ReduceQuantity(1);

            if (remaining <= 0)
            {
                Destroy(palletGo);
                return;
            }

            var visible = PickOrder(palletGo);
            for (int i = 0; i < visible.Count - remaining; i++) visible[i].gameObject.SetActive(false);
        }

        /// <summary>The visible cases on a pick-face pallet in the order a selector takes them: top layer first, and
        /// within a layer the ones nearest the selector first (front half, then reaching for the back half).</summary>
        private List<Transform> PickOrder(GameObject palletGo)
        {
            var visible = new List<Transform>();
            Transform load = palletGo != null ? palletGo.transform.Find("PalletLoad") : null;
            if (load == null) return visible;
            foreach (Transform c in load) if (c.gameObject.activeSelf) visible.Add(c);
            Vector3 me = transform.position;
            visible.Sort((a, b) =>
            {
                int layer = Mathf.RoundToInt(b.localPosition.y * 100f).CompareTo(Mathf.RoundToInt(a.localPosition.y * 100f));
                return layer != 0 ? layer : PlanarDist(a.position, me).CompareTo(PlanarDist(b.position, me));
            });
            return visible;
        }

        /// <summary>True when the next case to come off this pick face is in the back half (past the slot pivot,
        /// away from the selector) — they lean in to reach it.</summary>
        private bool NextPickIsDeep(LocationData location, Vector3 slotFlat, Vector3 toAisle)
        {
            var order = PickOrder(FindPickFacePallet(location, location.PalletId));
            if (order.Count == 0) return false;
            return Vector3.Dot(Flat(order[0].position - slotFlat, 0f), toAisle) < 0f;
        }

        private void SetLean(float weight)
        {
            var lean = GetComponent<BodyLean>();
            if (lean == null) { if (weight <= 0f) return; lean = gameObject.AddComponent<BodyLean>(); }
            lean.SetLean(weight);
        }

        private static GameObject FindPickFacePallet(LocationData location, string palletId)
        {
            var link = PalletMasterLink.Find(palletId);
            if (link != null) return link.gameObject;

            // Fallback: the pallet standing on this slot (same tolerance the occupancy reconcile uses).
            Vector3 at = location.transform.position;
            GameObject best = null;
            float bestD = 0.9f;
            foreach (var pd in FindObjectsByType<PalletData>())
            {
                if (pd == null || pd.GetComponent<OutboundPalletBuilder>() != null) continue;
                Vector3 p = pd.transform.position;
                if (Mathf.Abs(p.y - at.y) > 0.6f) continue;
                float d = PlanarDist(p, at);
                if (d < bestD) { bestD = d; best = pd.gameObject; }
            }
            return best;
        }

        private IEnumerator WalkTo(Vector3 target)
        {
            Transform t = transform;
            Vector3 flat = Flat(target, t.position.y);
            Vector3 to = flat - t.position;
            if (to.magnitude < 0.05f) yield break;
            yield return FaceDir(to);
            SetAnimBool("IsWalking", true);
            while (PlanarDist(t.position, flat) > 0.03f)
            {
                t.position = Vector3.MoveTowards(t.position, flat, FootWalkSpeed * Time.deltaTime);
                Vector3 d = flat - t.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0004f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(d.normalized), FootTurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = flat;
            SetAnimBool("IsWalking", false);
        }

        private IEnumerator FaceDir(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) yield break;
            Transform t = transform;
            Quaternion goal = Quaternion.LookRotation(dir.normalized);
            while (Quaternion.Angle(t.rotation, goal) > 1f)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, goal, FootTurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = goal;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Driving the jack
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>NavMesh leg to near <paramref name="target"/>, then a precise scripted finish onto it,
        /// ending with the jack's forward along <paramref name="finalForward"/>. Sets _jackLegOk.</summary>
        private IEnumerator DriveJackTo(Vector3 target, Vector3 finalForward, string phase)
        {
            _jackLegOk = false;
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            target = Flat(target, jt.position.y);

            yield return JackNavLeg(target, phase);
            if (!_jackLegOk || _jack == null) yield break;

            // Close the last metre exactly, then square up.
            Vector3 to = target - jt.position; to.y = 0f;
            if (to.magnitude > 0.05f)
            {
                yield return JackFace(to);
                yield return JackMoveStraight(target, JackDriveSpeed);
            }
            yield return JackFace(finalForward);
            _jackLegOk = _jack != null;
        }

        private IEnumerator JackFace(Vector3 dir)
        {
            dir.y = 0f;
            if (_jack == null || dir.sqrMagnitude < 0.0001f) yield break;
            Transform jt = _jack.transform;
            Quaternion goal = Quaternion.LookRotation(dir.normalized);
            while (_jack != null && Quaternion.Angle(jt.rotation, goal) > 0.5f)
            {
                jt.rotation = Quaternion.RotateTowards(jt.rotation, goal, JackTurnSpeed * Time.deltaTime);
                yield return null;
            }
            if (_jack != null) jt.rotation = goal;
        }

        /// <summary>Slides the jack in a straight line without turning (forward or backward).</summary>
        private IEnumerator JackMoveStraight(Vector3 target, float speed)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            Vector3 flat = Flat(target, jt.position.y);
            while (_jack != null && PlanarDist(jt.position, flat) > 0.02f)
            {
                jt.position = Vector3.MoveTowards(jt.position, Flat(flat, jt.position.y), speed * Time.deltaTime);
                yield return null;
            }
            if (_jack != null) jt.position = Flat(flat, jt.position.y);
        }

        /// <summary>Script owns the jack (manual) vs its own NavMeshAgent drives it. Same rules as
        /// ReachTruckOperator.SetDriveMode: always reassert, always Warp when handing back to the agent.</summary>
        private void SetJackDrive(bool ai, string phase)
        {
            if (_jack == null) return;
            if (!ai)
            {
                if (_jackNav != null) { _jackNav.CancelSeekPosition(); _jackNav.enabled = false; }
                if (_jackAgent != null)
                {
                    if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh) _jackAgent.isStopped = true;
                    _jackAgent.enabled = false;
                }
            }
            else
            {
                if (_jackAgent != null)
                {
                    _jackAgent.enabled = true;
                    _jackAgent.Warp(_jack.transform.position);
                    if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh) _jackAgent.isStopped = false;
                }
                if (_jackNav != null) _jackNav.enabled = true;
            }
        }

        /// <summary>The jack's own NavMeshAgent carries it across open floor (avoids racks). Mirrors
        /// ReachTruckOperator.SeekViaNavMesh, including its hard-won guards. Leaves the jack in manual mode.</summary>
        private IEnumerator JackNavLeg(Vector3 target, string phase)
        {
            _jackLegOk = false;
            if (_jack == null || _jackNav == null || _jackAgent == null) yield break;
            Transform jt = _jack.transform;

            // A short hop gains nothing from the mesh and is where it fails (carved slivers). Scripted finish does it.
            if (PlanarDist(jt.position, target) < 2f) { _jackLegOk = true; yield break; }

            SetJackDrive(true, phase);
            float reg = 0f;
            while (_jack != null && !_jackAgent.isOnNavMesh && reg < 1f)
            {
                _jackAgent.Warp(jt.position);
                reg += Time.deltaTime;
                yield return null;
            }
            if (_jack == null || !_jackAgent.isOnNavMesh)
            {
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack could not register on the NavMesh for '{phase}'.");
                SetJackDrive(false, phase);
                yield break;
            }

            Vector3 driveTarget = Flat(target, jt.position.y);
            if (NavMesh.SamplePosition(driveTarget, out var hit, 4f, _jackAgent.areaMask)) driveTarget = hit.position;

            _jackNav.CancelSeekPosition();
            _jackAgent.Warp(jt.position);
            _jackNav.SetTaskBusy(true);

            bool arrived = false;
            _jackNav.SeekPosition(driveTarget, () => arrived = true);
            yield return null;
            if (_jack != null && !arrived && !_jackAgent.pathPending && !_jackAgent.hasPath)
            {
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack: no path to {driveTarget} for '{phase}'.");
                _jackNav.CancelSeekPosition();
                _jackNav.SetTaskBusy(false);
                SetJackDrive(false, phase);
                yield break;
            }

            float elapsed = 0f, sinceRecheck = 0f;
            int reissues = 0;
            while (_jack != null && !arrived && _jackNav.IsSeekingTask && elapsed < 45f)
            {
                elapsed += Time.deltaTime;
                sinceRecheck += Time.deltaTime;
                if (sinceRecheck >= 0.5f)
                {
                    sinceRecheck = 0f;
                    if (!_jackAgent.isOnNavMesh) _jackAgent.Warp(jt.position);
                    else if (!_jackAgent.pathPending && !_jackAgent.hasPath && reissues < 10)
                    {
                        reissues++;
                        _jackNav.CancelSeekPosition();
                        _jackNav.SeekPosition(driveTarget, () => arrived = true);
                    }
                }
                yield return null;
            }

            if (_jack == null) yield break;
            _jackNav.SetTaskBusy(false);
            SetJackDrive(false, phase);
            if (!arrived)
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack did not reach '{phase}' ({driveTarget}) after {elapsed:F1}s.");
            _jackLegOk = arrived;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Geometry helpers
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Slot position on the floor and the unit direction from the rack face into the aisle
        /// (from the slot's LocApproachAnchor, which every rack prefab carries).</summary>
        private static bool TryGetPickGeometry(LocationData location, out Vector3 slotFlat, out Vector3 toAisle)
        {
            slotFlat = default; toAisle = default;
            if (location == null) return false;
            Vector3 slot = location.transform.position;
            Transform anchor = location.transform.Find("LocApproachAnchor");
            Vector3 dir = anchor != null ? anchor.position - slot : location.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = location.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            toAisle = dir.normalized;
            slotFlat = slot;
            return true;
        }

        /// <summary>Aisle direction to park along: whichever way along the aisle is closer to the jack's heading now.</summary>
        private Vector3 PickHeading(Vector3 toAisle)
        {
            Vector3 along = Vector3.Cross(Vector3.up, toAisle).normalized;
            Vector3 fwd = _jack != null ? _jack.transform.forward : transform.forward;
            return Vector3.Dot(along, fwd) >= 0f ? along : -along;
        }

        /// <summary>Resolves the lane + slot this order stages into. Shared by both paths (same rules as FinishOrder).</summary>
        private bool TryResolveStagingTarget(OrderData order, out string resolvedLane, out LaneNamingService.LaneSlot slot,
                                             out Vector3 pos, out Vector3 depthAxis, out string failReason)
        {
            resolvedLane = null; slot = default; pos = default; depthAxis = Vector3.forward; failReason = null;
            if (order.EffectiveStagingDoor <= 0 || string.IsNullOrEmpty(order.AssignedLane))
            { failReason = "order was never released to a staging lane"; return false; }
            if (!StagingLaneAssignmentService.TryFindTripLane(_orderService, _inventoryService, order, LivePalletCount(), out int door, out resolvedLane))
            { failReason = $"no lane at Stage {order.EffectiveStagingDoor} or any nearby door has room for {LivePalletCount()} pallet(s)"; return false; }
            if (!_inventoryService.TryFindStagingSlotInLane(door, resolvedLane, out slot))
            { failReason = $"lane {door}{resolvedLane} reported room but handed back no slot"; return false; }
            if (!LaneNamingService.TryGetSlotWorldPos(slot.Cell, out pos))
            { failReason = $"slot {slot.Name} has no world position yet"; return false; }
            if (LaneNamingService.TryGetLaneGeometry(slot.DoorNumber, slot.Lane, out var geo)) depthAxis = geo.DepthAxis;
            if (!string.IsNullOrEmpty(resolvedLane) && !(door == order.EffectiveStagingDoor && resolvedLane == order.AssignedLane))
                order.AddOverflowLane(door, resolvedLane);
            return true;
        }

        /// <summary>Anything standing in this lane cell (inbound stock or staged outbound)? Same measurement the
        /// stacking code uses: the top of what's there sits above the bare lane surface (1.15).</summary>
        private static bool CellHasFreight(int door, string lane, Vector2Int cell, GameObject self)
            => TrailerOffloadController.StagingDropBaseY(door, lane, cell, self) > 1.15f + 0.05f;

        private static bool LaneSlotsBlocked(int door, string lane, System.Func<int, bool> slotFilter, GameObject self)
        {
            foreach (var s in LaneNamingService.GetLane(door, lane))
                if (slotFilter(s.Slot) && CellHasFreight(door, lane, s.Cell, self)) return true;
            return false;
        }

        private static Vector3 Flat(Vector3 p, float y) => new Vector3(p.x, y, p.z);
        private static float PlanarDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
