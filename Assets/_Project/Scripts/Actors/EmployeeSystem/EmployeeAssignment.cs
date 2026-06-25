/// <summary>
/// What an employee is currently tasked to do, set via the Actions dropdown
/// (EmployeeInfoUI) and carried out by EmployeeAssignmentService. Patrol is the universal
/// default (value 0, so legacy/unset records read as Patrol); the rest are role-gated —
/// see EmployeeRoleExtensions.RoleSpecificAssignment.
/// </summary>
public enum EmployeeAssignment
{
    Patrol,
    DriveReach,
    DriveDockstalker,
    OrderSelection,
}

public static class EmployeeAssignmentExtensions
{
    /// <summary>Human-readable label for the Actions dropdown.</summary>
    public static string DisplayName(this EmployeeAssignment assignment) => assignment switch
    {
        EmployeeAssignment.Patrol           => "Patrol",
        EmployeeAssignment.DriveReach       => "Drive Reach",
        EmployeeAssignment.DriveDockstalker => "Drive Dockstalker",
        EmployeeAssignment.OrderSelection   => "Order Selection",
        _                                   => assignment.ToString()
    };

    /// <summary>
    /// The single role-specific assignment available on top of the universal Patrol, or
    /// null for roles with no driving/picking capability yet. DockStockerOperator and Loader
    /// both map to DriveDockstalker — they're treated as one forklift-operator capability
    /// everywhere in this system, matching how EmployeeSpawner already groups them.
    /// </summary>
    public static EmployeeAssignment? RoleSpecificAssignment(this EmployeeRole role) => role switch
    {
        EmployeeRole.ReachTruckOperator   => EmployeeAssignment.DriveReach,
        EmployeeRole.DockStockerOperator  => EmployeeAssignment.DriveDockstalker,
        EmployeeRole.Loader               => EmployeeAssignment.DriveDockstalker,
        EmployeeRole.OrderSelector        => EmployeeAssignment.OrderSelection,
        _                                 => null
    };
}
