// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeePerformanceMetric.cs

/// <summary>
/// How an employee's output is measured.
/// Used by RoleIconLibrary and the employee list to pick the right stat column.
/// </summary>
public enum EmployeePerformanceMetric
{
	CasesPerHour,   // CPH — how many cases/lines are picked per hour
	PalletsPerHour, // PPH — how many pallets are moved/loaded per hour
	Indirect        // No direct throughput metric (supervisor, boss, IC, etc.)
}

// ─── Extensions ───────────────────────────────────────────────────────────
public static class EmployeePerformanceMetricExtensions
{
	/// <summary>Short label suitable for column headers and compact UI.</summary>
	public static string Label(this EmployeePerformanceMetric m) => m switch
	{
		EmployeePerformanceMetric.CasesPerHour   => "CPH",
		EmployeePerformanceMetric.PalletsPerHour => "PPH",
		_                                        => "Indirect"
	};
}
