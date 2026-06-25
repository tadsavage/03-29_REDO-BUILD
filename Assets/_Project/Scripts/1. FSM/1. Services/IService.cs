namespace GameCore.Services
{
    /// <summary>
    /// Common interface for all game services.
    /// Provides lifecycle management (Initialize/Shutdown).
    ///
    /// All services should implement this interface to be registered with ServiceLocator.
    ///
    /// LIFECYCLE:
    /// 1. Service is instantiated (constructor)
    /// 2. Initialize() is called to set up subscriptions, load data, etc.
    /// 3. Service runs during game
    /// 4. Shutdown() is called on exit to clean up subscriptions and resources
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Initialize the service. Called after instantiation.
        /// Subscribe to events, load data, set up state here.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shut down the service. Called on game exit or reload.
        /// Unsubscribe from events, clean up resources here.
        /// </summary>
        void Shutdown();
    }
}
