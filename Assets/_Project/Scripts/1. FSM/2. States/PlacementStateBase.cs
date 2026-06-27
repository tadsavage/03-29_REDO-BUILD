using UnityEngine;
using GameCore.Events;
using GameCore.Economy;
using GameCore.Services;

namespace GameCore.Build
{
    /// <summary>
    /// Abstract base class for all FSM placement states.
    /// Implements IPlacementState and provides shared initialization, cleanup, and event handling.
    ///
    /// INHERITED BY:
    /// - IdleState (hover inspection, popup display)
    /// - RaycastPlacementState (grid hover, object selection)
    /// - BuildState (place new objects)
    /// - MoveState (relocate existing objects)
    /// - DeleteState (remove objects)
    ///
    /// SHARED RESPONSIBILITIES:
    /// - Implement IPlacementState contract (OnEnter/OnExit/Tick/IsPlacementState)
    /// - Track PlacementGrid and services
    /// - Manage input action subscriptions (OnEnter/OnExit)
    /// - Provide protected access to BuildService and data services
    /// - Coordinate with EventManager for event publishing
    ///
    /// INTEGRATION:
    /// - Update() is called by Tick() internally
    /// - Subclasses override Update() for per-frame logic
    /// - Subclasses set IsPlacementState property in their implementation
    /// </summary>
    public abstract class PlacementStateBase : IPlacementState
    {
        // ============ CORE DEPENDENCIES ============
        protected PlacementGrid _grid;
        protected IBuildService _buildService;
        protected MoneyService _moneyService;
        protected SimulationTimeService _timeService;
        protected EventManager _eventManager;

        // ============ INTERFACE: IPlacementState ============

        public abstract bool IsPlacementState { get; }

        /// <summary>
        /// Called when state is pushed onto the FSM stack (entering state).
        /// Subscribe to input actions and initialize state-specific logic here.
        /// </summary>
        public virtual void OnEnter()
        {
            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogWarning("[PlacementStateBase] EventManager not found during state enter.");
            }
        }

        /// <summary>
        /// Called every frame while state is active (from FSM.Tick()).
        /// Maps to Update() which subclasses override for per-frame logic.
        /// </summary>
        public void Tick()
        {
            Update();
        }

        /// <summary>
        /// Override this in subclasses for per-frame logic.
        /// Handle input, raycasting, and visual feedback here.
        /// </summary>
        public virtual void Update()
        {
        }

        /// <summary>
        /// Called when state is popped from the FSM stack (exiting state).
        /// Unsubscribe from input actions and clean up state-specific logic here.
        /// </summary>
        public virtual void OnExit()
        {
        }

        // ============ INITIALIZATION ============

        /// <summary>
        /// Initialize state with dependencies.
        /// Called by PlacementStateMachine when state is instantiated.
        /// </summary>
        public virtual void Initialize(PlacementGrid grid, IBuildService buildService)
        {
            _grid = grid;
            _buildService = buildService;

            // Cache services for quick access
            ServiceLocator.TryGet<MoneyService>(out _moneyService);
            ServiceLocator.TryGet<SimulationTimeService>(out _timeService);
            _eventManager = EventManager.Instance;

            if (_grid == null) Debug.LogError("[PlacementStateBase] PlacementGrid is null.");
            if (_buildService == null) Debug.LogError("[PlacementStateBase] IBuildService is null.");
        }

        // ============ PROTECTED HELPERS ============

        /// <summary>
        /// Publish a build event via EventManager.
        /// </summary>
        protected void PublishBuildEvent(string eventId, object payload = null)
        {
            if (_eventManager == null) return;

            if (payload == null)
            {
                _eventManager.Publish(eventId);
            }
            else if (payload is PlacedObject placedObj)
            {
                _eventManager.Publish<PlacedObject>(eventId, placedObj);
            }
        }

        /// <summary>
        /// Check if player can afford a cost.
        /// </summary>
        protected bool CanAfford(int cost)
        {
            if (_moneyService == null) return true; // Fail open if service unavailable
            return _moneyService.CanAfford(cost);
        }

        /// <summary>
        /// Get current player capital.
        /// </summary>
        protected int CurrentCapital
        {
            get
            {
                if (_moneyService == null) return 0;
                return _moneyService.CurrentCapital;
            }
        }

        /// <summary>
        /// Get current time (for debugging/logging).
        /// </summary>
        protected string CurrentTime
        {
            get
            {
                if (_timeService == null) return "??:??";
                return _timeService.TimeString;
            }
        }
    }
}
