using UnityEngine;
using System;
using System.Collections.Generic;

namespace GameCore.Events
{
    /// <summary>
    /// Centralized event dispatcher using pub/sub pattern.
    /// Type-safe generic events with payload support.
    /// Single-instance singleton (use EventManager.Instance).
    ///
    /// USAGE:
    /// - Subscribe: EventManager.Instance.Subscribe(GameEvents.Build.OnObjectPlaced, OnObjectPlaced);
    /// - Publish: EventManager.Instance.Publish(GameEvents.Build.OnObjectPlaced, placedObject);
    /// - Unsubscribe: EventManager.Instance.Unsubscribe(GameEvents.Build.OnObjectPlaced, OnObjectPlaced);
    /// </summary>
    [UnityEngine.DefaultExecutionOrder(-200)]
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        private Dictionary<string, Delegate> _events = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[EventManager] Initialized.");
        }

        /// <summary>
        /// Subscribe to a parameterless event.
        /// </summary>
        public void Subscribe(string eventId, Action callback)
        {
            if (string.IsNullOrEmpty(eventId) || callback == null)
            {
                Debug.LogError("[EventManager] Invalid subscribe: eventId or callback is null.");
                return;
            }

            if (!_events.ContainsKey(eventId))
            {
                _events[eventId] = null;
            }

            Action action = _events[eventId] as Action;
            if (action != null)
            {
                var invocationList = action.GetInvocationList();
                bool alreadySubscribed = false;
                for (int i = 0; i < invocationList.Length; i++)
                {
                    if (invocationList[i] == (Delegate)callback)
                    {
                        alreadySubscribed = true;
                        break;
                    }
                }
                if (alreadySubscribed)
                {
                    Debug.LogWarning($"[EventManager] Duplicate subscription: {eventId}");
                    return;
                }
            }

            _events[eventId] = (Action)_events[eventId] + callback;
        }

        /// <summary>
        /// Subscribe to an event with a payload.
        /// </summary>
        public void Subscribe<T>(string eventId, Action<string, T> callback)
        {
            if (string.IsNullOrEmpty(eventId) || callback == null)
            {
                Debug.LogError("[EventManager] Invalid subscribe: eventId or callback is null.");
                return;
            }

            if (!_events.ContainsKey(eventId))
            {
                _events[eventId] = null;
            }

            Action<string, T> action = _events[eventId] as Action<string, T>;
            if (action != null)
            {
                var invocationList = action.GetInvocationList();
                bool alreadySubscribed = false;
                for (int i = 0; i < invocationList.Length; i++)
                {
                    if (invocationList[i] == (Delegate)callback)
                    {
                        alreadySubscribed = true;
                        break;
                    }
                }
                if (alreadySubscribed)
                {
                    Debug.LogWarning($"[EventManager] Duplicate subscription: {eventId}");
                    return;
                }
            }

            _events[eventId] = (Action<string, T>)_events[eventId] + callback;
        }

        /// <summary>
        /// Unsubscribe from a parameterless event.
        /// </summary>
        public void Unsubscribe(string eventId, Action callback)
        {
            if (string.IsNullOrEmpty(eventId) || callback == null)
            {
                return;
            }

            if (!_events.ContainsKey(eventId))
            {
                return;
            }

            _events[eventId] = (Action)_events[eventId] - callback;

            if (_events[eventId] == null)
            {
                _events.Remove(eventId);
            }
        }

        /// <summary>
        /// Unsubscribe from an event with a payload.
        /// </summary>
        public void Unsubscribe<T>(string eventId, Action<string, T> callback)
        {
            if (string.IsNullOrEmpty(eventId) || callback == null)
            {
                return;
            }

            if (!_events.ContainsKey(eventId))
            {
                return;
            }

            _events[eventId] = (Action<string, T>)_events[eventId] - callback;

            if (_events[eventId] == null)
            {
                _events.Remove(eventId);
            }
        }

        /// <summary>
        /// Publish a parameterless event.
        /// </summary>
        public void Publish(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            if (!_events.ContainsKey(eventId))
            {
                return;
            }

            Action action = _events[eventId] as Action;
            action?.Invoke();
        }

        /// <summary>
        /// Publish an event with a payload.
        /// </summary>
        public void Publish<T>(string eventId, T payload)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            if (!_events.ContainsKey(eventId))
            {
                return;
            }

            Action<string, T> action = _events[eventId] as Action<string, T>;
            action?.Invoke(eventId, payload);
        }
    }
}
