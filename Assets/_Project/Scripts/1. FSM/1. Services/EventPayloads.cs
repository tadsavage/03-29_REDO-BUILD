namespace GameCore.Events.Payloads
{
    /// <summary>Event payload when an object is placed on the grid.</summary>
    public struct BuildingMoveData
    {
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
        public int Rotation;
    }

    /// <summary>Event payload for financial transactions.</summary>
    public struct TransactionData
    {
        public int Amount;
        public string Reason;
        public long Timestamp;
    }

    /// <summary>Event payload for time ticks.</summary>
    public struct SimulationTimeData
    {
        public int Hour;
        public int Minute;
        public int Day;
    }
}
