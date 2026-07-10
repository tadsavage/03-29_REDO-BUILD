namespace GameCore.Events
{
    /// <summary>
    /// Central registry of all event IDs in the game.
    /// Uses string IDs for type-safe event dispatch via EventManager.
    /// Organized by system (Build, Economy, Time, UI, GameState).
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Build system events.</summary>
        public static class Build
        {
            public const string OnObjectPlaced = "Build.OnObjectPlaced";
            public const string OnObjectDeleted = "Build.OnObjectDeleted";
            public const string OnObjectMoved = "Build.OnObjectMoved";
            public const string OnPlacementFailed = "Build.OnPlacementFailed";
            public const string OnStateChanged = "Build.OnStateChanged";
            public const string OnValidationFailed = "Build.OnValidationFailed";
            public const string OnCommandUndone = "Build.OnCommandUndone";
            public const string OnCommandRedone = "Build.OnCommandRedone";
        }

        /// <summary>Economy system events.</summary>
        public static class Economy
        {
            public const string OnMoneyChanged = "Economy.OnMoneyChanged";
            public const string OnTransactionApplied = "Economy.OnTransactionApplied";
            public const string OnBankrupt = "Economy.OnBankrupt";
            public const string OnSpentTodayChanged = "Economy.OnSpentTodayChanged";
        }

        /// <summary>Time system events.</summary>
        public static class Time
        {
            public const string OnMinutePassed = "Time.OnMinutePassed";
            public const string OnHourChanged = "Time.OnHourChanged";
            public const string OnDayChanged = "Time.OnDayChanged";
            public const string OnTimeScaleChanged = "Time.OnTimeScaleChanged";
        }

        /// <summary>UI system events.</summary>
        public static class UI
        {
            public const string OnUIStateChanged = "UI.OnUIStateChanged";
            public const string OnMenuOpened = "UI.OnMenuOpened";
            public const string OnMenuClosed = "UI.OnMenuClosed";
        }

        /// <summary>Game state events.</summary>
        public static class GameState
        {
            public const string OnGamePaused = "GameState.OnGamePaused";
            public const string OnGameResumed = "GameState.OnGameResumed";
            public const string OnSceneLoaded = "GameState.OnSceneLoaded";
        }

        /// <summary>Inventory system events.</summary>
        public static class Inventory
        {
            public const string OnPalletReceived = "Inventory.OnPalletReceived";
            public const string OnPalletMoved = "Inventory.OnPalletMoved";
            public const string OnShipmentFullyReceived = "Inventory.OnShipmentFullyReceived";
            public const string OnSpoilageDetected = "Inventory.OnSpoilageDetected";
        }

        /// <summary>Dock system events.</summary>
        public static class Dock
        {
            public const string OnShipmentReadyToDeparture = "Dock.OnShipmentReadyToDeparture";
            public const string OnDoorOpened = "Dock.OnDoorOpened";
            public const string OnDoorClosed = "Dock.OnDoorClosed";
        }

        /// <summary>Placement system events (grid positions, heights, object tracking).</summary>
        public static class Placement
        {
            public const string OnPlacedObjectHeightChanged = "Placement.OnPlacedObjectHeightChanged";
        }
    }
}
