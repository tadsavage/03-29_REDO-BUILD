using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace NavTest
{
    /// <summary>
    /// One graybox machine driven by a bare NavMeshAgent: SetDestination, wait to arrive, dwell, repeat. Nothing else steers it.
    /// It only MEASURES: time stuck (has a path, not moving), give-ups, time its box overlaps another machine, and time it clips a
    /// rack or wall (the agent is a 0.5-0.6 m circle, the machine is a 1 x 2 m box, so corners can swing into racks on turns).
    ///   Jack : a little picker rides it. It drives to 2-4 bays in one aisle; at each one it either pulls up at the slot or stops a few
    ///          metres short (random, NavTestSim.stopShortChance), the picker steps off, walks to the rack face, carries 1-3 cases back
    ///          to the jack one at a time, and gets back on. Then it stages at a lane's exit end. Sometimes parks.
    ///   Reach: pull a pallet from a lane's exit end, drive to a bay on the aisle centre line, turn to the rack, lift.
    /// Speeds and radii follow the settings panel live.
    /// </summary>
    public class NavTestMachine : MonoBehaviour
    {
        public enum Role { Jack, ReachTruck }

        public Role MachineRole { get; private set; }
        public string State { get; private set; } = "starting";
        public int Trips { get; private set; }
        public int Picks { get; private set; }
        public int Walks { get; private set; }
        public float WalkMeters { get; private set; }
        public float StuckSeconds { get; private set; }
        public int StuckEpisodes { get; private set; }
        public int GiveUps { get; private set; }
        public float LongestStuck { get; private set; }
        public int Unreachable { get; private set; }
        public float MachineOverlapSeconds { get; private set; }
        public float RackClipSeconds { get; private set; }

        private const float StuckAfter = 2f;      // not moving this long with a path = stuck
        private const float MovingSpeed = 0.1f;

        private NavTestSim _sim;
        private NavMeshAgent _agent;
        private BoxCollider _body;
        private int _index;
        private float _stuckFor;
        private bool _inStuck, _clipping, _lastArrived;
        private readonly Collider[] _hits = new Collider[16];

        // The picker (jacks only)
        private GameObject _picker;
        private NavMeshAgent _pickerAgent;
        private Vector3 _rideLocal;

        public void Init(NavTestSim sim, Role role, NavMeshAgent agent, BoxCollider body, int index)
        {
            _sim = sim; MachineRole = role; _agent = agent; _body = body; _index = index;
            if (role == Role.Jack)
            {
                // Rides standing on the back of the jack.
                _picker = sim.MakePerson($"{name} picker");
                _rideLocal = new Vector3(0f, 0.12f, -sim.jackSize.z * 0.5f + 0.35f);   // standing on the platform at the back
                Mount();
            }
            else
            {
                // A seated driver on the reach truck, for looks only.
                var driver = sim.MakePerson($"{name} driver");
                driver.transform.SetParent(transform, false);
                driver.transform.localPosition = new Vector3(0f, 0.75f, -sim.reachSize.z * 0.5f + 0.4f);
                driver.transform.localScale = Vector3.one * 0.85f;
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            while (true)
            {
                if (MachineRole == Role.Jack) yield return JackTrip();
                else yield return ReachTrip();
                yield return null;
            }
        }

        // Reach truck fork carriage (slides up the mast) and the cases on the forks.
        private Transform _carriage;
        private GameObject _load;
        private float _forkTarget = NavTestSim.ForkEmptyY;
        private const float ForkSpeed = 0.8f;   // m/s up and down

        public void SetReachParts(Transform carriage, GameObject load) { _carriage = carriage; _load = load; }

        private void Update()
        {
            if (_carriage != null)
            {
                var p = _carriage.localPosition;
                p.y = Mathf.MoveTowards(p.y, _forkTarget, ForkSpeed * Time.deltaTime);
                _carriage.localPosition = p;
            }
            bool jack = MachineRole == Role.Jack;
            _agent.speed = jack ? _sim.jackSpeed : _sim.reachSpeed;
            _agent.radius = jack ? _sim.jackRadius : _sim.reachRadius;
            if (_pickerAgent != null && _pickerAgent.enabled) _pickerAgent.speed = _sim.walkSpeed;
        }

        private void OnDestroy()
        {
            if (_picker != null) Destroy(_picker);
        }

        // ── Jobs ──

        private IEnumerator JackTrip()
        {
            var rng = _sim.Rng;
            int aisle = rng.Next(_sim.aisles.Length);
            var spots = Enumerable.Range(0, _sim.jackPicks.Length).Where(i => _sim.jackPickAisle[i] == aisle)
                                  .OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).Select(i => _sim.jackPicks[i]).ToList();
            var a = _sim.aisles[aisle];
            bool eastbound = aisle == 0;   // one-way option: aisle 1 west->east, aisle 2 east->west

            if (_sim.jacksFollowAisleDirection)
            {
                spots = eastbound ? spots.OrderBy(p => p.x).ToList() : spots.OrderByDescending(p => p.x).ToList();
                yield return GoTo(new Vector3(eastbound ? a.x : a.y, 0f, a.z), $"to aisle {aisle + 1} entry");
            }
            else
                spots = NearestFirst(_sim.L(transform.position), spots);

            foreach (var slot in spots)
            {
                // Where the jack stops: at the slot, or a few metres along the aisle from it (the picker walks the rest).
                Vector3 stop = slot;
                if (rng.NextDouble() < _sim.stopShortChance)
                {
                    float off = Mathf.Lerp(_sim.minWalk, _sim.maxWalk, (float)rng.NextDouble()) * (rng.Next(2) == 0 ? -1f : 1f);
                    stop.x = Mathf.Clamp(slot.x + off, NavTestSim.RackMinX + 1f, NavTestSim.RackMaxX - 1f);
                }
                yield return GoTo(stop, $"to pick a{aisle + 1}");
                if (!_lastArrived) continue;

                // The rack face this slot is on: 0.9 m off it, so the picker stands 0.35 m from the face.
                bool faceBelow = slot.z < a.z;
                Vector3 face = slot + new Vector3(0f, 0f, faceBelow ? -0.55f : 0.55f);
                int cases = rng.Next(_sim.minCases, Mathf.Max(_sim.minCases, _sim.maxCases) + 1);
                yield return PickOnFoot(face, cases);
                Picks++;
            }
            if (_sim.jacksFollowAisleDirection)
                yield return GoTo(new Vector3(eastbound ? a.y : a.x, 0f, a.z), $"to aisle {aisle + 1} exit");

            int lane = rng.Next(_sim.laneExits.Length);
            yield return GoTo(_sim.laneExits[lane], $"to stage L{lane + 1}");
            if (_lastArrived) yield return Dwell(_sim.stageSeconds, "staging");
            Trips++;

            if (rng.NextDouble() < 0.2)
            {
                yield return GoTo(_sim.jackParking[_index % _sim.jackParking.Length], "to parking");
                yield return Dwell(8f, "parked");
            }
        }

        private IEnumerator ReachTrip()
        {
            var rng = _sim.Rng;
            int lane = rng.Next(_sim.laneExits.Length);
            _forkTarget = NavTestSim.ForkEmptyY;
            if (_load != null) _load.SetActive(false);
            yield return GoTo(_sim.laneExits[lane], $"to lane L{lane + 1}");
            if (_lastArrived)
            {
                yield return Dwell(_sim.stageSeconds, "forks in");
                if (_load != null) _load.SetActive(true);   // the pallet's cases ride on the forks
            }
            _forkTarget = NavTestSim.ForkTravelY;

            int bay = rng.Next(_sim.reachPicks.Length);
            yield return GoTo(_sim.reachPicks[bay], $"to bay a{_sim.reachPickAisle[bay] + 1}");
            if (_lastArrived)
            {
                if (_sim.reachFacesRack) yield return Face(_sim.WDir(rng.Next(2) == 0 ? Vector3.forward : Vector3.back));
                _forkTarget = NavTestSim.ForkLiftY;
                yield return Dwell(_sim.liftSeconds, "lifting");
                if (_load != null) _load.SetActive(false);   // set down in the rack
                Picks++;
            }
            _forkTarget = NavTestSim.ForkEmptyY;
            Trips++;
        }

        private static List<Vector3> NearestFirst(Vector3 fromLocal, List<Vector3> pts)
        {
            var left = new List<Vector3>(pts);
            var order = new List<Vector3>();
            Vector3 at = fromLocal;
            while (left.Count > 0)
            {
                var next = left.OrderBy(p => (p - at).sqrMagnitude).First();
                order.Add(next); left.Remove(next); at = next;
            }
            return order;
        }

        // ── The picker ──

        private void Mount()
        {
            if (_pickerAgent != null) _pickerAgent.enabled = false;
            _picker.transform.SetParent(transform, false);
            _picker.transform.localPosition = _rideLocal;
            _picker.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Step off, walk to the rack face, then carry each case back to the jack. Gets back on at the end.</summary>
        private IEnumerator PickOnFoot(Vector3 faceLocal, int cases)
        {
            // Step off on the side of the jack nearer the slot.
            Vector3 slotW = _sim.W(faceLocal);
            Vector3 side = Vector3.Dot(slotW - transform.position, transform.right) >= 0f ? transform.right : -transform.right;
            Vector3 besideJack = transform.position + side * (_sim.jackSize.x * 0.5f + 0.35f);

            _picker.transform.SetParent(null, true);
            if (_pickerAgent == null)
            {
                _picker.SetActive(false);
                _pickerAgent = _picker.AddComponent<NavMeshAgent>();
                _pickerAgent.agentTypeID = _sim.HumanAgentType;
                _pickerAgent.radius = 0.25f;
                _pickerAgent.height = 1.5f;
                _pickerAgent.acceleration = 8f;
                _pickerAgent.angularSpeed = 540f;
                _pickerAgent.stoppingDistance = 0.1f;
                _pickerAgent.avoidancePriority = 60;
                _picker.SetActive(true);
            }
            var filter = new NavMeshQueryFilter { agentTypeID = _sim.HumanAgentType, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(besideJack, out var hit, 1.5f, filter)) besideJack = hit.position;
            _pickerAgent.enabled = true;
            _pickerAgent.Warp(besideJack);
            _pickerAgent.speed = _sim.walkSpeed;

            for (int c = 0; c < cases; c++)
            {
                yield return Walk(slotW, c == 0 ? "walking to slot" : "back to slot");
                yield return Dwell(_sim.caseSeconds, $"picking case {c + 1}/{cases}");
                // Back to the jack (it has not moved).
                Vector3 back = transform.position + side * (_sim.jackSize.x * 0.5f + 0.35f);
                if (NavMesh.SamplePosition(back, out hit, 1.5f, filter)) back = hit.position;
                yield return Walk(back, "carrying case");
                yield return Dwell(0.4f, "placing case");
            }
            Mount();
        }

        private IEnumerator Walk(Vector3 worldTarget, string label)
        {
            State = label;
            Walks++;
            var filter = new NavMeshQueryFilter { agentTypeID = _sim.HumanAgentType, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(worldTarget, out var hit, 1.5f, filter)) worldTarget = hit.position;
            _pickerAgent.SetDestination(worldTarget);
            Vector3 last = _picker.transform.position;
            float t = 0f;
            while (true)
            {
                Vector3 now = _picker.transform.position;
                WalkMeters += Vector3.Distance(new Vector3(now.x, 0f, now.z), new Vector3(last.x, 0f, last.z));
                last = now;
                if (!_pickerAgent.pathPending && _pickerAgent.remainingDistance <= _pickerAgent.stoppingDistance + 0.15f) break;
                t += Time.deltaTime;
                if (t > 30f) { Debug.LogWarning($"[NavTest] {name}'s picker could not walk to {_sim.L(worldTarget)} ('{label}') in 30 s - placed there."); _pickerAgent.Warp(worldTarget); break; }
                yield return null;
            }
            _pickerAgent.ResetPath();
        }

        // ── Machine primitives ──

        /// <summary>SetDestination and wait. Sets _lastArrived. Counts stuck time; gives the target up after giveUpStuckSeconds.</summary>
        private IEnumerator GoTo(Vector3 localTarget, string label)
        {
            _lastArrived = false;
            State = label;
            Vector3 target = _sim.W(localTarget);
            var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(target, out var hit, 2f, filter)) target = hit.position;
            _agent.isStopped = false;
            if (!_agent.SetDestination(target)) { Unreachable++; Debug.LogWarning($"[NavTest] {name}: SetDestination failed for '{label}' {target}."); yield break; }
            while (_agent.pathPending) yield return null;
            if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Unreachable++;
                Debug.LogWarning($"[NavTest] {name}: path for '{label}' is {_agent.pathStatus} - driving as far as it goes.");
            }
            _stuckFor = 0f; _inStuck = false;
            while (true)
            {
                if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f) { _lastArrived = true; break; }
                if (_agent.velocity.magnitude < MovingSpeed)
                {
                    _stuckFor += Time.deltaTime;
                    if (_stuckFor > StuckAfter)
                    {
                        StuckSeconds += Time.deltaTime;
                        if (!_inStuck)
                        {
                            _inStuck = true; StuckEpisodes++;
                            Debug.Log($"[NavTest] {name} stuck '{label}' at {LocalPos()} (nearest: {Nearest()}).");
                        }
                        if (_stuckFor > LongestStuck) LongestStuck = _stuckFor;
                        if (_stuckFor > _sim.giveUpStuckSeconds)
                        {
                            GiveUps++;
                            Debug.LogWarning($"[NavTest] {name} gave up '{label}' after {_stuckFor:0} s stuck at {LocalPos()} (nearest: {Nearest()}).");
                            break;
                        }
                    }
                }
                else { _stuckFor = 0f; _inStuck = false; }
                yield return null;
            }
            _agent.ResetPath();
        }

        private IEnumerator Dwell(float seconds, string label)
        {
            State = label;
            _agent.ResetPath();
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        private IEnumerator Face(Vector3 worldDir)
        {
            State = "turning to rack";
            _agent.updateRotation = false;
            worldDir.y = 0f;
            var goal = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
            while (Quaternion.Angle(transform.rotation, goal) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, _agent.angularSpeed * Time.deltaTime);
                yield return null;
            }
            _agent.updateRotation = true;
        }

        // ── Measuring ──

        private void LateUpdate()
        {
            if (_body == null) return;
            var bt = _body.transform;
            Vector3 centre = bt.TransformPoint(_body.center);
            Vector3 half = Vector3.Scale(_body.size, bt.lossyScale) * 0.5f * 0.95f;   // 5% tolerance
            int n = Physics.OverlapBoxNonAlloc(centre, half, _hits, bt.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool machine = false, solid = false;
            string what = null;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h == null || h == _body) continue;
                var other = h.GetComponentInParent<NavTestMachine>();
                if (other != null) { if (other != this) machine = true; continue; }
                if (h.name == "Floor" || h.GetComponentInParent<Camera>() != null) continue;
                solid = true; what = h.name;
            }
            if (machine) MachineOverlapSeconds += Time.deltaTime;
            if (solid)
            {
                RackClipSeconds += Time.deltaTime;
                if (!_clipping) Debug.Log($"[NavTest] {name} clipped '{what}' at {LocalPos()} while '{State}'.");
            }
            _clipping = solid;
        }

        private string LocalPos()
        {
            var p = _sim.L(transform.position);
            return $"({p.x:0.0}, {p.z:0.0})";
        }

        private string Nearest()
        {
            NavTestMachine best = null; float bd = float.MaxValue;
            foreach (var m in _sim.Machines)
            {
                if (m == null || m == this) continue;
                float d = (m.transform.position - transform.position).magnitude;
                if (d < bd) { bd = d; best = m; }
            }
            return best == null ? "none" : $"{best.name} {bd:0.0} m, {best.State}";
        }
    }
}
