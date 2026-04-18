// ChemLabSim v3 — Service Locator
// Simple static service container. Services register at boot and are
// accessible from any controller without direct coupling.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>Register a service instance. Only one per type.</summary>
        public static void Register<T>(T service) where T : class
        {
            Type key = typeof(T);
            if (Services.ContainsKey(key))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service: {key.Name}");
            }
            Services[key] = service;
        }

        /// <summary>Retrieve a registered service. Returns null if not found.</summary>
        public static T Get<T>() where T : class
        {
            Type key = typeof(T);
            if (Services.TryGetValue(key, out object service))
                return service as T;

            Debug.LogError($"[ServiceLocator] Service not found: {key.Name}. Was V3Bootstrap.Init() called?");
            return null;
        }

        /// <summary>Check if a service type is registered.</summary>
        public static bool Has<T>() where T : class
        {
            return Services.ContainsKey(typeof(T));
        }

        /// <summary>Remove all registrations. Called during teardown.</summary>
        public static void Clear()
        {
            Services.Clear();
        }

#if UNITY_EDITOR
        public static int RegisteredCount => Services.Count;
#endif
    }
}
