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
    public const string Security       = "Security";
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

    public static readonly string[] IncomeOrder =
    {
        Starting, CasePick, Storage, Handling, Offload,
        AdminIncome, SpaceRental, Recycling, MiscIncome
    };

    public static readonly string[] ExpenseOrder =
    {
        LeaseMortgage, Wages, ContractLabor, Security, Maintenance,
        Bribes, Insurance, Electricity, Garbage, Groundskeeping,
        Taxes, Fines, Shrink, AdminMarketing, RentalEquip
    };

    // Maps an ObjDataSO.category string to the correct finance expense category
    // for hourly cost distribution.
    public static string ForHourlyCost(string objCategory) => objCategory switch
    {
        "Grounds" or "Ground"  => Groundskeeping,
        "Door"    or "Doors"   => Electricity,
        _                      => Maintenance,
    };
}
