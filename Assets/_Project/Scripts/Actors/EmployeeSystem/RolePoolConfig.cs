using UnityEngine;

[CreateAssetMenu(fileName = "RolePoolConfig", menuName = "Scriptable Objects/RolePoolConfig")]
public class RolePoolConfig : ScriptableObject
{
    [Header("Tier 1 (Floor Associates)")]
    [Tooltip("Roles that replenish quickly: Sanitation, Order Selector, ReachTruck, DockStocker, Loader")]
    public EmployeeRole[] tier1Roles = new[]
    {
        EmployeeRole.Sanitation, EmployeeRole.OrderSelector,
        EmployeeRole.ReachTruckOperator, EmployeeRole.DockStockerOperator, EmployeeRole.Loader,
    };

    [Header("Tier 2 (Skilled)")]
    [Tooltip("Roles that replenish slower: InventoryControl, Receiver, Admin, Security, Supervisor, Exterminator, HR")]
    public EmployeeRole[] tier2Roles = new[]
    {
        EmployeeRole.InventoryControl, EmployeeRole.Receiver,
        EmployeeRole.Admin, EmployeeRole.Security, EmployeeRole.Supervisor,
        EmployeeRole.Exterminator, EmployeeRole.HR,
    };

    [Header("Starting Roster (non-OrderSelector)")]
    [Tooltip("Skilled/special roles used to fill the non-OrderSelector slice of the start roster")]
    public EmployeeRole[] startingSkilledRoles = new[]
    {
        EmployeeRole.Boss, EmployeeRole.Security, EmployeeRole.Admin,
        EmployeeRole.InventoryControl, EmployeeRole.Receiver, EmployeeRole.Supervisor,
    };

    [Header("Constraints")]
    [Tooltip("Maximum OrderSelectors allowed on the board at once (temporary cap until inventory system exists)")]
    public int orderSelectorHardCap = 8;
}
