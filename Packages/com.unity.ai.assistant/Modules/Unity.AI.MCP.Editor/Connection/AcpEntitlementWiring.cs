using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;

namespace Unity.AI.MCP.Editor.Connection
{
    /// <summary>
    /// Bridges <see cref="Account.settings"/> entitlement values into the
    /// <see cref="ConnectionCensus"/> policy (which the Bridge reads directly)
    /// and points <see cref="GatewayCapacityGuard"/> at the census so the
    /// assistant-side <c>AcpSessionRegistry</c> can consult it without a
    /// build-time dependency on this assembly.
    /// </summary>
    static class AcpEntitlementWiring
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            GatewayCapacityGuard.Check = Probe;
            Account.settings.OnChange += Apply;
            Apply();
        }

        /// <summary>
        /// Re-applies the entitlement-driven caps to the census. Called when
        /// <see cref="Account.settings"/> changes, and exposed to dev tools so
        /// the "Reset to entitlement" button can undo a tier-simulator override.
        /// </summary>
        /// <remarks>
        /// When a <see cref="ConnectionPolicyOverride"/> is active (a dev-tool
        /// tier simulation persisted in SessionState) entitled values are
        /// suppressed and the persisted override is re-applied instead. This is
        /// what restores the override into <see cref="ConnectionCensus"/> after
        /// the domain reload on Edit→Play — the static <c>s_Policy</c> field
        /// resets to <see cref="ConnectionPolicy.Unlimited"/> on reload, so
        /// without this re-apply the override would be silently lost (UUM-141585).
        /// </remarks>
        internal static void Apply()
        {
            if (ConnectionPolicyOverride.IsActive)
            {
                ConnectionCensus.SetPolicy(ConnectionPolicyOverride.Value);
                return;
            }

            var limits = Account.settings.ConnectionLimits;
            ConnectionCensus.SetPolicy(new ConnectionPolicy(
                MaxDirect: limits.AllowedMcpConnections,
                MaxGateway: limits.AllowedGatewayConnections));
        }

        /// <summary>
        /// Translate a census pre-check into the assistant-side capacity struct.
        /// Kept allocation-free so it can be called on every acquire.
        /// </summary>
        static GatewayCapacityCheck Probe()
        {
            var r = ConnectionCensus.TryReserveGatewaySlot();
            return new GatewayCapacityCheck(
                canAcquire: r.Allowed,
                gatewayCount: r.PoolCount,
                gatewayCap: r.PoolCap);
        }
    }
}
