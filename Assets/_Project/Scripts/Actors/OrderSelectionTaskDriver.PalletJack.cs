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
        private const float BaseJackDriveSpeed   = 1.8f;   // m/s, scripted forward legs
        private const float BaseJackReverseSpeed = 0.9f;   // m/s, backing into the lane
        private const float BaseJackTurnSpeed    = 110f;   // deg/s, turning in place
        // Doubled while the jack finishes delivering driverless after its battery died (EquipmentBattery).
        private float JackSpeedMul => _jack != null ? EquipmentBattery.SpeedMultiplierFor(_jack) : 1f;
        private float JackDriveSpeed   => BaseJackDriveSpeed * JackSpeedMul;
        private float JackReverseSpeed => BaseJackReverseSpeed * JackSpeedMul;
        private float JackTurnSpeed    => BaseJackTurnSpeed * JackSpeedMul;

        /// <summary>A selector currently holds this jack (EquipmentBattery waits for this before driving it to a charger).</summary>
        public static bool IsJackClaimed(MHEOperatorSlot jack)
            => jack != null && s_jackClaims.TryGetValue(jack, out var owner) && owner != null;

        /// <summary>The jack's battery went flat mid-run (EquipmentBattery.NeedsCharge).</summary>
        private bool JackOutOfPower => _jack != null && _jack.GetComponent<EquipmentBattery>() is EquipmentBattery b && b.NeedsCharge;

        /// <summary>This selector climbed off a flat jack: the jack finishes the delivery on its own, the selector patrols.</summary>
        private bool _ejectedFromJack;

        /// <summary>The battery went flat (or Change Battery was chosen) mid-run: the selector STAYS aboard, stops picking, stages what
        /// is on the pallet, then parks the jack in a charging bay and steps off (Tad, 2026-10-18). Still owes the order a trip.</summary>
        private bool _flatBatteryStop;

        /// <summary>Flat battery (Tad, 2026-10-17): the selector climbs off and goes back to patrolling; the jack carries
        /// on alone at double speed to stage what is on its forks, then EquipmentBattery takes it to a charger.</summary>
        private void EjectFromJack()
        {
            if (_ejectedFromJack || _jack == null) return;
            _ejectedFromJack = true;
            DestroyHeldCase();
            SetLean(0f);
            SetAnimBool("CarryCase", false);
            var identity = GetComponent<EmployeeIdentity>();
            if (_jack.CurrentOperator == identity) _jack.EjectRider();
            EndScriptedFoot();   // if they were off the jack walking by script, hand their body back to their own navigation
            _jack.GetComponent<EquipmentBattery>()?.BeginDriverlessRun();
            _nav.SetTaskBusy(false);
            _nav.Patrol();
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': pallet jack battery died on order {_currentOrder?.OrderNumber} - climbed off; the jack stages what's aboard by itself.");
        }
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
        private const float ReverseMaxDistance = 8f;   // ~3 bays (a bay is two 1.33 m cells): shorter backward moves reverse instead of turning

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
            _ejectedFromJack = false;
            _flatBatteryStop = false;
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
                // Battery: a dead jack stays on its charger; a low one (under an hour left) only if nothing better is free.
                var batt = slot.GetComponent<EquipmentBattery>();
                if (batt != null && (batt.IsDead || batt.NeedsCharge)) continue;   // flat, or on its way to / sitting on a charger
                float d = PlanarDist(slot.transform.position, transform.position);
                if (batt != null && batt.HoursLeft <= EquipmentBattery.LowHours) d += 1000f;
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
            _offJack = false; _parkedBay = null; _parkedPalletIndex = -1;
            yield return MoveForks(ForkLift);

            // 2. Pick, slot by slot.
            OrderData.OrderStatus completion = OrderData.OrderStatus.Staged;
            while (true)
            {
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }

                // Flat battery: climb off; the jack stages what it already has by itself, the rest of the order
                // becomes another trip (FinishOrderCleanup files it) for a selector with a charged jack.
                if (JackOutOfPower)
                {
                    _flatBatteryStop = true;   // stay aboard: stage, park in a charging bay, step off
                    if (!_currentOrder.IsFullyPicked) { MarkPartialUnlessLoading(_currentOrder); _cubingCapHit = true; }
                    completion = OrderData.OrderStatus.Staged;
                    break;
                }

                if (_currentOrder.IsFullyPicked)
                {
                    if (HelperNotNeeded()) break;   // nothing of ours aboard: JackStageRoutine just parks + finishes
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) fully picked on the pallet jack — {TotalCasesOnPallets()} case(s). Driving to staging.");
                    _orderService.MarkOrderFulfilled(_currentOrder.OrderId);
                    break;
                }

                if (!TryFindBestPickLocation(_currentOrder, out var location, out var lineItem, out int takeQty))
                {
                    // Everything left is being fetched by other selectors on this order.
                    if (HelperNotNeeded()) break;
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

                int palletIndex = Mathf.Clamp(_pallets.Count - 1, 0, ForkPalletZ.Length - 1);
                if (!TryGetPickGeometry(location, out Vector3 slotFlat, out Vector3 toAisle))
                {
                    yield return AbandonJackOnFoot($"pick slot {location.Address} has no usable position");
                    yield break;
                }

                // BAY STOPS (Tad, 2026-10-17): the jack parks ONCE per bay, at the bay's first position, and the selector
                // walks to every slot they need in that bay before the jack moves on. Same bay + same pallet = stay off.
                string bayKey = BayKey(location.Address);
                if (!(_offJack && bayKey == _parkedBay && palletIndex == _parkedPalletIndex))
                {
                    if (_offJack)
                    {
                        yield return StepIn();
                        if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                        _jack.StepOn();
                        _offJack = false;
                    }
                    Vector3 bayFlat = BayFirstPositionFlat(location, slotFlat);
                    Vector3 heading = PickHeading(toAisle, location);
                    Vector3 park = bayFlat + toAisle * ParkOffset - heading * ForkPalletZ[palletIndex];

                    // End of the aisle: the jack would be left nose-first into the wall. Turn it ~45 degrees out toward the open
                    // aisle instead (Tad, 2026-10-18: "turning towards the direction of travel, not driving into the wall").
                    Vector3 parkHeading = heading;
                    if (UnityEngine.AI.NavMesh.Raycast(park + Vector3.up * 0.05f, park + heading * 2.5f, out _, UnityEngine.AI.NavMesh.AllAreas))
                        parkHeading = (heading + toAisle).normalized;
                    park = KeepJackBodyOffWalls(park, parkHeading);
                    yield return DriveJackTo(park, parkHeading, $"bay {bayKey}");
                    if (!_jackLegOk)
                    {
                        yield return AbandonJackOnFoot($"could not drive the jack to bay {bayKey}");
                        yield break;
                    }
                    if (JackOutOfPower) continue;   // died on the way here: handled at the top of the loop
                    // Off the jack on the rack side (via that side's mount point).
                    yield return StepOut(slotFlat);
                    _offJack = true;
                    _parkedBay = bayKey;
                    _parkedPalletIndex = palletIndex;
                }

                yield return PickCasesFromSlot(location, lineItem, takeQty, slotFlat, toAisle);
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }

                if (_capReached)
                {
                    _capReached = false;
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) — both pallets on the jack are full ({_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits}). Driving to staging.");
                    MarkPartialUnlessLoading(_currentOrder);
                    completion = OrderData.OrderStatus.Staged;
                    _cubingCapHit = true;
                    break;
                }
            }

            // Back on the jack before driving to staging (picking leaves the selector off it, at the last bay).
            // Not after a flat battery: the jack drives to staging alone.
            if (_offJack && _jack != null && !_ejectedFromJack)
            {
                yield return StepIn();
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                _jack.StepOn();
                _offJack = false;
            }

            // 3. Stage.
            yield return JackStageRoutine(completion);
        }

        /// <summary>The NavMeshAgent only plans the open-floor legs and stops short of the target; the last metres of a
        /// bay stop are scripted straight onto the park pose, and the agent's radius covers its centre, not the 2.5 m of
        /// body + forks. So a pick at the end of an aisle drove the jack's body through the wall. Pull the park pose
        /// away from any navmesh edge that the body's rear / sides would touch.</summary>
        private static Vector3 KeepJackBodyOffWalls(Vector3 park, Vector3 heading)
        {
            const float Margin = 0.5f;
            Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
            for (int pass = 0; pass < 4; pass++)
            {
                Vector3 push = Vector3.zero;
                foreach (Vector3 probe in new[] { park - heading * 1.4f, park - heading * 0.7f, park - heading * 1.0f + right * 0.45f, park - heading * 1.0f - right * 0.45f })
                {
                    if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out var onMesh, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    if (UnityEngine.AI.NavMesh.FindClosestEdge(onMesh.position, out var edge, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        float off = Vector3.Distance(probe, onMesh.position);   // probe already outside the mesh
                        float need = Margin - edge.distance + off;
                        if (need > 0.02f) { Vector3 n = edge.normal; n.y = 0f; push += n.normalized * need; }
                    }
                }
                if (push.sqrMagnitude < 0.0004f) break;
                park += push * 0.6f;
            }
            return park;
        }

        private bool _capReached;
        private bool _offJack;            // selector standing at a bay, jack parked
        private string _parkedBay;        // "AA-BB" of the bay the jack is parked at
        private int _parkedPalletIndex = -1;

        /// <summary>"AA-BB" part of a slot address: every slot in one bay shares it.</summary>
        /// <summary>Where the jack stops (Tad, 2026-10-18): one stop per PAIR of bays facing each other across the aisle
        /// (01 and 02, 03 and 04...). The selector picks every needed slot of both bays on foot, stepping across the
        /// aisle, then the jack pulls FORWARD to the next pair that has a needed slot. Odd bays are one side, even bays the other.</summary>
        private static string BayKey(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 5) return address;
            if (!int.TryParse(address.Substring(0, 2), out int aisle) || !int.TryParse(address.Substring(3, 2), out int bay)) return address.Substring(0, 5);
            return $"{aisle:00}-{(bay + 1) / 2:00}";
        }

        /// <summary>Where the bay starts in walking direction (position 0 in an odd aisle, position 1 in an even one, the
        /// serpentine sequence's first slot of the bay), at the same level as the pick. Falls back to the pick slot.</summary>
        private static Vector3 BayFirstPositionFlat(LocationData pick, Vector3 pickFlat)
        {
            var a = pick != null ? pick.Address : null;
            if (string.IsNullOrEmpty(a) || a.Length < 7 || !int.TryParse(a.Substring(0, 2), out int aisle)) return pickFlat;
            char firstPos = '0';   // every aisle ascends: the bay's first slot in walking direction is always position 0
            string first = a.Substring(0, 7) + firstPos;
            if (first == a || !LocationRegistry.TryGet(first, out var loc)) return pickFlat;
            return TryGetPickGeometry(loc, out var f, out _) ? f : pickFlat;
        }

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
                _orderService.NotePickedCase(_currentTask?.TaskId);
                activePallet.AddCase(lineItem.SkuId, sku.Prefab, caseDim, ti, hi, location.Address);

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
                // Nothing physical to stage (the rest of the order is pallet picks, or this was a helper trip the
                // other selectors finished first): park the jack and finish. A helper leaves the order's status alone.
                if (_orderService.OtherSelectorsOnOrder(order, task) == 0)
                    order.Status = TripEndStatus(order, completion, task);
                if (!_ejectedFromJack && !_flatBatteryStop) _cubingCapHit = false;   // a flat-battery stop still owes the rest of the order a trip
                if (_jack != null && !_ejectedFromJack)
                {
                    yield return ParkJack();
                    yield return StayAboardWhileCharging();
                    if (_jack != null) yield return StepOut(_jack.transform.position - _jack.transform.right);
                }
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
            order.Status = TripEndStatus(order, completion, task);
            _pallets.Clear();

            // Drive back out to the line-up spot, release the lane, step off (via a mount point) and leave the jack.
            yield return JackMoveStraight(mouth, JackDriveSpeed);
            ReleaseJackLaneLock();

            Debug.Log($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) staged in {door}{resolvedLane} by pallet jack.");

            // Park it in a Pallet Jack Parking bay instead of leaving it across the lane mouth. A flat jack running
            // driverless skips this: once released, EquipmentBattery drives it to a charging station itself.
            if (!_ejectedFromJack)
            {
                yield return ParkJack();
                yield return StayAboardWhileCharging();
                if (_jack != null) yield return StepOut(_jack.transform.position - _jack.transform.right);
            }
            ReleaseJack();
            FinishOrderCleanup(task);
        }

        private static bool s_warnedNoParking;

        /// <summary>Parked on a charger and charging: the selector stays on the jack until it is full (Tad, 2026-10-18), then steps off.
        /// Gives up after 90 real seconds so a stuck charge can never hold the selector forever.</summary>
        private IEnumerator StayAboardWhileCharging()
        {
            if (_jack == null) yield break;
            var b = _jack.GetComponent<EquipmentBattery>();
            if (b == null) yield break;
            yield return null;   // let the battery notice it is on the pad
            float waited = 0f;
            while (_jack != null && b != null && b.IsCharging && !b.IsFull && waited < 90f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

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

            // SHORT MOVE ALONG THE AISLE (Tad, 2026-10-17): no turning round to go back a bay or two. Face the aisle
            // heading and drive straight there - forward, or in REVERSE when the target is behind. Only a longer
            // trip (more than ~3 bays) goes through the normal drive-and-turn below.
            Vector3 toT = target - jt.position; toT.y = 0f;
            Vector3 headF = finalForward; headF.y = 0f;
            if (headF.sqrMagnitude > 0.0001f && toT.magnitude <= ReverseMaxDistance &&
                (toT.magnitude < 0.05f || Mathf.Abs(Vector3.Dot(toT.normalized, headF.normalized)) > 0.8f))
            {
                // NEVER BACK UP (Tad, 2026-10-18): a stop behind the jack is not driven to - the selector just walks there
                // on foot. The jack only ever moves forward through an aisle.
                if (Vector3.Dot(toT, headF) < 0f && toT.magnitude > 0.3f)
                {
                    _jackLegOk = _jack != null;
                    yield break;
                }
                yield return JackFace(headF);
                yield return JackMoveStraight(target, JackDriveSpeed);
                _jackLegOk = _jack != null;
                yield break;
            }

            yield return JackNavLeg(target, phase);
            if (!_jackLegOk || _jack == null) yield break;

            // Close the last metre exactly, then square up. Square up FIRST and slide the remainder (forward or back),
            // so the jack never spins round on the spot just to cover a few centimetres.
            Vector3 to = target - jt.position; to.y = 0f;
            yield return JackFace(finalForward);
            if (to.magnitude > 0.05f && Vector3.Dot(to, finalForward) >= 0f)   // forward only: never reverse the last bit
                yield return JackMoveStraight(target, JackDriveSpeed);
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
            float yielded = 0f;
            while (_jack != null && PlanarDist(jt.position, flat) > 0.02f)
            {
                // Another jack in the way: wait behind it (capped, so a head-on meeting can't freeze both).
                if (yielded < JackYieldMaxSeconds && JackPathBlocked(flat - jt.position))
                {
                    yielded += Time.deltaTime;
                    yield return null;
                    continue;
                }
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

            float elapsed = 0f, sinceRecheck = 0f, navYielded = 0f;
            int reissues = 0;
            while (_jack != null && !arrived && _jackNav.IsSeekingTask && elapsed < 45f)
            {
                elapsed += Time.deltaTime;
                sinceRecheck += Time.deltaTime;

                // Another jack in the way: hold the agent still behind it (capped, see JackYieldMaxSeconds).
                if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh)
                {
                    Vector3 heading = _jackAgent.desiredVelocity.sqrMagnitude > 0.01f ? _jackAgent.desiredVelocity : jt.forward;
                    bool hold = navYielded < JackYieldMaxSeconds && JackPathBlocked(heading);
                    if (hold) navYielded += Time.deltaTime;
                    if (_jackAgent.isStopped != hold) _jackAgent.isStopped = hold;
                }

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

        /// <summary>Aisle direction to park along: the way the pick route travels through this aisle (bays going UP
        /// in an odd aisle, DOWN in an even one), so the jack drives forward bay to bay and only reverses for a short
        /// backtrack. It used to keep whatever heading it arrived with, so a jack that entered an aisle backwards
        /// reversed the whole length of it (Tad, 2026-10-17). Falls back to the current heading.</summary>
        private Vector3 PickHeading(Vector3 toAisle, LocationData pick)
        {
            Vector3 along = Vector3.Cross(Vector3.up, toAisle).normalized;
            Vector3 fwd = TryAisleTravelDir(pick, out var travel) ? travel
                        : (_jack != null ? _jack.transform.forward : transform.forward);
            return Vector3.Dot(along, fwd) >= 0f ? along : -along;
        }

        /// <summary>World direction of travel through the pick's aisle: toward higher bay numbers in an odd aisle,
        /// lower in an even one (the serpentine sequence, OrderPickPath.SequenceKey).</summary>
        private static bool TryAisleTravelDir(LocationData pick, out Vector3 dir)
        {
            dir = Vector3.zero;
            string a = pick != null ? pick.Address : null;
            if (string.IsNullOrEmpty(a) || a.Length < 8 ||
                !int.TryParse(a.Substring(0, 2), out int aisle) || !int.TryParse(a.Substring(3, 2), out int bay)) return false;
            string prefix = a.Substring(0, 3), suffix = a.Substring(5);
            for (int d = 1; d <= 4; d++)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    int other = bay + sign * d;
                    if (other < 1 || other > 99) continue;
                    if (!LocationRegistry.TryGet($"{prefix}{other:00}{suffix}", out var loc) || loc == null) continue;
                    Vector3 v = loc.WorldPosition - pick.WorldPosition; v.y = 0f;
                    if (v.sqrMagnitude < 0.01f) continue;
                    dir = v.normalized * sign;                 // toward higher bay numbers, in EVERY aisle
                    return true;
                }
            }
            return false;
        }

        // ── Keeping pallet jacks apart (Tad, 2026-10-17: two selectors on one order drove over each other) ──
        private const float JackCentreBack = 1.0f;     // the forks trail behind the body: the jack's middle is ~1 m back
        private const float JackClearAhead = 3.6f;     // centre-to-centre gap along the path that counts as "in the way"
        private const float JackClearSide = 1.2f;      // ...and side to side
        private const float JackYieldMaxSeconds = 6f;  // then go anyway, so a head-on meeting can't freeze both forever
        private static readonly List<MHEOperatorSlot> s_allJacks = new List<MHEOperatorSlot>();
        private static float s_allJacksAt = -10f;

        private static Vector3 JackCentre(Transform t) => t.position - t.forward * JackCentreBack;

        /// <summary>Another pallet jack (working or parked) sits in this jack's path for a move in <paramref name="moveDir"/>.</summary>
        private bool JackPathBlocked(Vector3 moveDir)
        {
            if (_jack == null) return false;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 0.0001f) return false;
            moveDir.Normalize();
            if (Time.unscaledTime - s_allJacksAt > 1f)
            {
                s_allJacksAt = Time.unscaledTime;
                s_allJacks.Clear();
                foreach (var s in FindObjectsByType<MHEOperatorSlot>())
                    if (s != null && s.isActiveAndEnabled && s.IsPalletJack && s.GetComponent<PlacedObject>() != null) s_allJacks.Add(s);
            }
            Vector3 me = JackCentre(_jack.transform);
            foreach (var other in s_allJacks)
            {
                if (other == null || other == _jack) continue;
                Vector3 rel = JackCentre(other.transform) - me; rel.y = 0f;
                float ahead = Vector3.Dot(rel, moveDir);
                if (ahead <= 0f || ahead > JackClearAhead) continue;
                if ((rel - moveDir * ahead).magnitude < JackClearSide) return true;
            }
            return false;
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
