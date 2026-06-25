using GameCore.Economy;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace GameCore.Services
{
    /// <summary>
    /// Static registry of game services (Service Locator pattern).
    /// Enables dependency injection without tight coupling.
    ///
    /// USAGE:
    /// - Register: ServiceLocator.Register<MoneyService>(moneyService);
    /// - Get: var money = ServiceLocator.Get<MoneyService>();
    /// - Try Get: if (ServiceLocator.TryGet<MoneyService>(out var money)) { ... }
    ///
    /// BEST PRACTICES:
    /// - Register services in GameContext.Awake()
    /// - Query services in Initialize() methods, not constructors
    /// - Unregister services in Shutdown() or on scene exit
    /// </summary>
    public static class ServiceLocator
    {
        private static Dictionary<Type, object> _services = new();

        /// <summary>
        /// Register a service instance.
        /// </summary>
        public static void Register<T>(T service) where T : class, IService
        {
            Type type = typeof(T);

            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} already registered. Replacing.");
            }

            _services[type] = service;
            Debug.Log($"[ServiceLocator] Registered service: {type.Name}");
        }

        /// <summary>
        /// Get a service instance. Throws if not found.
        /// </summary>
        public static T Get<T>() where T : class, IService
        {
            Type type = typeof(T);

            if (!_services.ContainsKey(type))
            {
                Debug.LogError($"[ServiceLocator] Service {type.Name} not found.");
                return null;
            }

            return _services[type] as T;
        }

        /// <summary>
        /// Try to get a service instance. Returns true if found.
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class, IService
        {
            Type type = typeof(T);

            if (_services.ContainsKey(type))
            {
                service = _services[type] as T;
                return service != null;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Unregister a service.
        /// </summary>
        public static void Unregister<T>() where T : class, IService
        {
            Type type = typeof(T);

            if (_services.ContainsKey(type))
            {
                _services.Remove(type);
                Debug.Log($"[ServiceLocator] Unregistered service: {type.Name}");
            }
        }

        /// <summary>
        /// Shutdown all registered services and clear the registry.
        /// </summary>
        public static void ShutdownAll()
        {
            Debug.Log("[ServiceLocator] Shutting down all services...");

            foreach (var kvp in _services)
            {
                var service = kvp.Value as IService;
                service?.Shutdown();
            }

            _services.Clear();
            Debug.Log("[ServiceLocator] All services shut down.");
        }
    }
}
