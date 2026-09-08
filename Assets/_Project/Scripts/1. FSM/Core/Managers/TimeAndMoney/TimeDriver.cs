using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;

public class TimeDriver : MonoBehaviour
{
    public SimulationTimeService TimeService { get; private set; }

    // Resolved lazily on first Update rather than passed into Initialize: VendorDealService is
    // registered by GameContext.Awake() in the same pass as TimeService, so grabbing it here avoids
    // adding a second constructor-time dependency for one optional per-frame tick.
    private VendorDealService _vendorDeals;
    private bool _resolvedVendorDeals;

    public void Initialize(SimulationTimeService service)
    {
        TimeService = service;
    }

    private void Update()
    {
        // TimeService is wired by GameContext.Awake(). Guard against the brief window
        // before that runs, and against private state being reset by a play-mode domain
        // reload (which doesn't re-run Awake), so we don't spam NullReferenceExceptions.
        if (TimeService == null) return;
        TimeService.Tick(Time.deltaTime);

        // Real-seconds vendor deal countdowns — deliberately unscaled: OrdersPauseGate sets
        // Time.timeScale to 0 for as long as the Purchasing/Contracts panel is open, which is exactly
        // when the player is looking at the VENDORS tab and would expect a deal's bar to keep draining
        // and new deals to keep rolling, not freeze the instant they open the panel. Resolved once;
        // retried every frame until found in case this Update ever ran before GameContext registered it.
        if (!_resolvedVendorDeals)
        {
            _resolvedVendorDeals = ServiceLocator.TryGet(out _vendorDeals);
        }
        _vendorDeals?.Tick(Time.unscaledDeltaTime);
    }
}
