public static class FinanceCategory
{
    // ── Income ───────────────────────────────────────────────────────
    public const string Starting       = "Starting";
    public const string CasePick       = "Case Pick";
    public const string Storage        = "Storage";
    public const string Handling       = "Handling";
    public const string Offload        = "Offload";
    public const string AdminIncome    = "Admin";
    public const string SpaceRental    = "Space Rental";
    public const string Recycling      = "Recycling";
    public const string MiscIncome     = "Miscellaneous";

    // ── Expenses ─────────────────────────────────────────────────────
    public const string LeaseMortgage  = "Lease / Mortgage";
    public const string Wages          = "Wages";
    public const string ContractLabor  = "Contract Labor";
    public const string LossPrevention = "Loss Prevention";
    public const string Sanitation     = "Sanitation";
    public const string Maintenance    = "Maintenance";
    public const string Bribes         = "Bribes";
    public const string Insurance      = "Insurance";
    public const string Electricity    = "Electricity";
    public const string Garbage        = "Garbage";
    public const string Groundskeeping = "Groundskeeping";
    public const string Taxes          = "Taxes";
    public const string Fines          = "Fines";
    public const string Shrink         = "Shrink";
    public const string AdminMarketing = "Admin-Marketing";
    public const string RentalEquip    = "Rental Equip";
    public const string MHECosts       = "MHE Costs";
    public const string PalletLeaseRepair = "Pallet Lease and Repair";
    public const string Transportation = "Transportation";
    // Wholesale cost of the actual product coming in — charged the moment a Receiver processes a
    // pallet (quantity x SkuData.UnitCost). Lump sum for now; per Tad, break out by category/vendor
    // later.
    public const string PurchasedGoods = "Purchased Goods";

    // ── MHE vehicle GL lines (sub-detail under MHE Costs — see ForGLLine) ───
    public const string ReachTruckGL   = "Reach Truck";
    public const string DockStockerGL  = "Dock Stocker";
    public const string PalletJackGL   = "Pallet Jack";

    // ── Wage GL lines (sub-detail under Wages — see ForWageGLLine) ──────────
    public const string FloorWages   = "Floor Wages";
    public const string HourlyWages  = "Hourly Wages";
    public const string SalaryWages  = "Salary Wages";

    public static readonly string[] IncomeOrder =
    {
        Starting, CasePick, Storage, Handling, Offload,
        AdminIncome, SpaceRental, Recycling, MiscIncome
    };

    public static readonly string[] ExpenseOrder =
    {
        PurchasedGoods,
        LeaseMortgage, Wages, ContractLabor, LossPrevention, Sanitation, Maintenance,
        MHECosts, PalletLeaseRepair, Transportation,
        Bribes, Insurance, Electricity, Garbage, Groundskeeping,
        Taxes, Fines, Shrink, AdminMarketing, RentalEquip
    };

    // Maps an ObjDataSO.category string to the correct finance expense category
    // for hourly cost distribution. LEGACY — superseded by ObjDataSO.GL_Line +
    // ForGLLine below, kept only because AddHourlyCost/RemoveHourlyCost (the
    // display-only pooled $/hr counter on MoneyService) still take a category
    // string param for historical reasons.
    public static string ForHourlyCost(string objCategory) => objCategory switch
    {
        "Grounds" or "Ground"  => Groundskeeping,
        "Door"    or "Doors"   => Electricity,
        _                      => Maintenance,
    };

    /// <summary>
    /// Maps an ObjDataSO.GL_Line value to the top-level expense category it rolls up under in
    /// FinancialBreakdownPanel. Most GL lines (Doors, Barriers, Flavor, Loss Prevention, Floor,
    /// Walls, Racking, etc.) roll up under Maintenance — "fixed asset hourly cost" is the general
    /// rule; only the named exceptions get their own top-level row.
    /// </summary>
    public static string ForGLLine(string glLine) => glLine switch
    {
        Groundskeeping     => Groundskeeping,
        MHECosts           => MHECosts,
        ReachTruckGL       => MHECosts,
        DockStockerGL      => MHECosts,
        PalletJackGL       => MHECosts,
        PalletLeaseRepair  => PalletLeaseRepair,
        Transportation     => Transportation,
        LossPrevention     => LossPrevention,
        Sanitation         => Sanitation,
        ContractLabor      => ContractLabor,
        Wages              => Wages,
        _                  => Maintenance,
    };

    /// <summary>
    /// Maps an EmployeeRole to its wage GL line. Exterminator, Security, Sanitation, and
    /// TruckDriver are pulled out of Wages entirely onto their own top-level rows (Contract
    /// Labor / Loss Prevention / Sanitation / Transportation — Security's wage joins the same
    /// Loss Prevention line as guard shacks/fences/barriers). Everything else stays under the
    /// Wages row, tiered into Floor/Hourly/Salary — any role not explicitly tiered here falls
    /// back to the plain Wages bucket under its own role name.
    /// </summary>
    public static (string topCategory, string detailKey) ForWageGLLine(EmployeeRole role) => role switch
    {
        EmployeeRole.Exterminator        => (ContractLabor, role.ToString()),
        EmployeeRole.Security            => (LossPrevention, role.ToString()),
        EmployeeRole.Sanitation          => (Sanitation, role.ToString()),
        EmployeeRole.TruckDriver          => (Transportation, role.ToString()),

        EmployeeRole.OrderSelector        => (Wages, FloorWages),
        EmployeeRole.Loader               => (Wages, FloorWages),
        EmployeeRole.DockStockerOperator  => (Wages, FloorWages),
        EmployeeRole.ReachTruckOperator   => (Wages, FloorWages),
        EmployeeRole.Receiver             => (Wages, FloorWages),

        EmployeeRole.InventoryControl     => (Wages, HourlyWages),
        EmployeeRole.Admin                => (Wages, HourlyWages),

        EmployeeRole.HR                   => (Wages, SalaryWages),
        EmployeeRole.Boss                 => (Wages, SalaryWages),
        EmployeeRole.Supervisor           => (Wages, SalaryWages),

        _                                  => (Wages, role.ToString()),
    };
}
