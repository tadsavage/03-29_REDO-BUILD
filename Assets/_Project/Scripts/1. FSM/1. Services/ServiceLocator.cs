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

            if (_services.TryGetValue(type, out var existing) && existing != null)
            {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} already registered. " +
                                  "Shutting down the previous instance before replacing.");

                // Without this, the OLD instance stays fully alive and subscribed to any STATIC events
                // it registered in its own Initialize() (e.g. OrderService.OnOrderArrived) — it's gone
                // from this registry so nothing can reach it through ServiceLocator, but nothing ever
                // told it to stop listening either. Every subsequent event fires BOTH the old orphaned
                // instance and the new one, which is exactly what caused guard-shack announcements to
                // print twice: two live DockScheduleServices, only one of them actually reachable.
                (existing as IService)?.Shutdown();
            }

            _services[type] = service;
        }

        /// <summary>
        /// Get a service instance. Throws if not found.
        /// </summary>
        public static T Get<T>() where T : class, IService
        {
            Type type = typeof(T);

            if (!_services.ContainsKey(type))
                throw new InvalidOperationException($"[ServiceLocator] Service {type.Name} not found. Ensure it was registered in GameContext.Awake().");

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
                _services.Remove(type);
        }

        /// <summary>
        /// Shutdown all registered services and clear the registry.
        /// </summary>
        public static void ShutdownAll()
        {
            foreach (var kvp in _services)
            {
                var service = kvp.Value as IService;
                service?.Shutdown();
            }

            _services.Clear();
        }
    }
}
