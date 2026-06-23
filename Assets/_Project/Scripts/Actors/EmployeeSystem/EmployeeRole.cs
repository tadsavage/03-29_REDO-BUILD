// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeRole.cs

/// <summary>
/// Primary job role for an employee.  OrderSelector is value 0 so it acts as
/// the serialization/field-initializer default for new or legacy records.
/// </summary>
public enum EmployeeRole
{
	OrderSelector,        // Picks cases/orders — performance measured in CPH
	ReachTruckOperator,   // Operates reach truck — performance measured in PPH
	DockStockerOperator,  // Operates dock stocker — performance measured in PPH
	Loader,               // Loads trucks — performance measured in PPH
	Receiver,             // Receives/checks inbound freight
	Supervisor,           // Floor supervisor (indirect)
	Boss,                 // Management (indirect)
	Security,             // Guard (indirect)
	InventoryControl,     // IC clerk / cycle counts (indirect)
    Exterminator,         // Exterminator — Kills Rats - what else???
    HR,                   // PLACEHOLDER — not yet implemented
	Admin,                // PLACEHOLDER — not yet implemented
	Sanitation,           // PLACEHOLDER — not yet implemented
	TruckDriver,          // PLACEHOLDER — not yet implemented
}

// ─── Extensions ───────────────────────────────────────────────────────────
public static class EmployeeRoleExtensions
{
	/// <summary>Human-readable display name for UI labels.</summary>
	public static string DisplayName(this EmployeeRole role) => role switch
	{
		EmployeeRole.OrderSelector      => "Order Selector",
		EmployeeRole.ReachTruckOperator => "Reach Truck Operator",
		EmployeeRole.DockStockerOperator => "Dock Stocker Operator",
		EmployeeRole.Loader             => "Loader",
		EmployeeRole.Receiver           => "Receiver",
		EmployeeRole.Supervisor         => "Supervisor",
		EmployeeRole.Boss               => "Boss",
		EmployeeRole.Security           => "Security",
		EmployeeRole.InventoryControl   => "Inventory Control",
		EmployeeRole.HR                 => "HR",
		EmployeeRole.Admin              => "Admin",
		EmployeeRole.Sanitation         => "Sanitation",
		_                               => role.ToString()
	};

	/// <summary>Which productivity metric applies to this role.</summary>
	public static EmployeePerformanceMetric PerformanceMetric(this EmployeeRole role) => role switch
	{
		EmployeeRole.OrderSelector      => EmployeePerformanceMetric.CasesPerHour,
		EmployeeRole.ReachTruckOperator => EmployeePerformanceMetric.PalletsPerHour,
		EmployeeRole.DockStockerOperator => EmployeePerformanceMetric.PalletsPerHour,
		EmployeeRole.Loader             => EmployeePerformanceMetric.PalletsPerHour,
		_                               => EmployeePerformanceMetric.Indirect
	};
}
