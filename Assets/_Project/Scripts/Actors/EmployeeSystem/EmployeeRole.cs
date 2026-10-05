// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeRole.cs

/// <summary>
/// Primary job role for an employee.  OrderSelector is value 0 so it acts as
/// the serialization/field-initializer default for new or legacy records.
///
/// VALUES ARE PINNED. Roles are stored as plain integers in saves, scenes, and assets (RoleConfig, RoleIconLibrary,
/// RolePoolConfig, every AOD part's allowedRoles), so never reorder or renumber. Value 3 was the old "Loader" role;
/// it was retired in favour of DockStockerOperator (which does the loading). Never reuse 3 for a new role —
/// <see cref="EmployeeRoleExtensions.Normalize"/> maps any leftover 3 in old data to DockStockerOperator.
/// </summary>
public enum EmployeeRole
{
	OrderSelector       = 0,   // Picks cases/orders — performance measured in CPH
	ReachTruckOperator  = 1,   // Operates reach truck — performance measured in PPH
	DockStockerOperator = 2,   // Operates dock stocker (offloads inbound, loads outbound) — performance measured in PPH
	// 3 = retired (was Loader) — do not reuse
	Receiver            = 4,   // Receives/checks inbound freight
	Supervisor          = 5,   // Floor supervisor (indirect)
	Boss                = 6,   // Management (indirect)
	Security            = 7,   // Guard (indirect)
	InventoryControl    = 8,   // IC clerk / cycle counts (indirect)
	Exterminator        = 9,   // Exterminator — Kills Rats - what else???
	HR                  = 10,  // PLACEHOLDER — not yet implemented
	Admin               = 11,  // PLACEHOLDER — not yet implemented
	Sanitation          = 12,  // PLACEHOLDER — not yet implemented
	TruckDriver         = 13,  // PLACEHOLDER — not yet implemented
}

// ─── Extensions ───────────────────────────────────────────────────────────
public static class EmployeeRoleExtensions
{
	/// <summary>The retired "Loader" role's integer value (3). Only ever seen in data saved before the role was removed.</summary>
	private const int RetiredLoaderValue = 3;

	/// <summary>Maps a role read from old saves/assets onto a live role: the retired Loader (3) becomes DockStockerOperator.</summary>
	public static EmployeeRole Normalize(this EmployeeRole role)
		=> (int)role == RetiredLoaderValue ? EmployeeRole.DockStockerOperator : role;

	/// <summary>Human-readable display name for UI labels.</summary>
	public static string DisplayName(this EmployeeRole role) => role switch
	{
		EmployeeRole.OrderSelector      => "Order Selector",
		EmployeeRole.ReachTruckOperator => "Reach Truck Operator",
		EmployeeRole.DockStockerOperator => "Dock Stocker Operator",
		EmployeeRole.Receiver           => "Receiver",
		EmployeeRole.Supervisor         => "Supervisor",
		EmployeeRole.Boss               => "Boss",
		EmployeeRole.Security           => "Security",
		EmployeeRole.InventoryControl   => "Inventory Control",
		EmployeeRole.HR                 => "HR",
		EmployeeRole.Admin              => "Admin",
		EmployeeRole.Sanitation         => "Sanitation",
		EmployeeRole.TruckDriver        => "Truck Driver",
		EmployeeRole.Exterminator       => "Exterminator",
		_                               => role.ToString()
	};

	/// <summary>Which productivity metric applies to this role.</summary>
	public static EmployeePerformanceMetric PerformanceMetric(this EmployeeRole role) => role switch
	{
		EmployeeRole.OrderSelector      => EmployeePerformanceMetric.CasesPerHour,
		EmployeeRole.ReachTruckOperator => EmployeePerformanceMetric.PalletsPerHour,
		EmployeeRole.DockStockerOperator => EmployeePerformanceMetric.PalletsPerHour,
		_                               => EmployeePerformanceMetric.Indirect
	};
}
