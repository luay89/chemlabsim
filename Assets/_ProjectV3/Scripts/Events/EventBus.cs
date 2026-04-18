// ChemLabSim v3 — Strongly-Typed Event Bus
// Central pub/sub message broker. Controllers and services communicate through
// this bus instead of holding direct references to each other.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Events
{
    /// <summary>
    /// Lightweight, strongly-typed event bus.
    /// <para>Publish:  <c>EventBus.Publish(new MyEvent { ... });</c></para>
    /// <para>Subscribe:  <c>EventBus.Subscribe&lt;MyEvent&gt;(OnMyEvent);</c></para>
    /// <para>Unsubscribe:  <c>EventBus.Unsubscribe&lt;MyEvent&gt;(OnMyEvent);</c></para>
    /// </summary>
    public static class EventBus
    {
        // Each event type T gets its own invocation list stored here.
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        /// <summary>Register a handler for event type <typeparamref name="T"/>.</summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;

            Type key = typeof(T);
            if (Handlers.TryGetValue(key, out Delegate existing))
                Handlers[key] = Delegate.Combine(existing, handler);
            else
                Handlers[key] = handler;
        }

        /// <summary>Remove a previously registered handler.</summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;

            Type key = typeof(T);
            if (Handlers.TryGetValue(key, out Delegate existing))
            {
                Delegate updated = Delegate.Remove(existing, handler);
                if (updated == null)
                    Handlers.Remove(key);
                else
                    Handlers[key] = updated;
            }
        }

        /// <summary>Broadcast an event to all subscribers of type <typeparamref name="T"/>.</summary>
        public static void Publish<T>(T evt) where T : struct, IGameEvent
        {
            Type key = typeof(T);
            if (Handlers.TryGetValue(key, out Delegate existing))
            {
                if (existing is Action<T> action)
                {
                    // Iterate each handler individually so one exception
                    // does not kill remaining subscribers in the chain.
                    foreach (Delegate d in action.GetInvocationList())
                    {
                        try
                        {
                            ((Action<T>)d).Invoke(evt);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }
            }
        }

        /// <summary>Remove all handlers. Call during teardown or scene unload if needed.</summary>
        public static void Clear()
        {
            Handlers.Clear();
        }

        /// <summary>Remove all handlers for a specific event type.</summary>
        public static void Clear<T>() where T : struct, IGameEvent
        {
            Handlers.Remove(typeof(T));
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: number of registered event types (for diagnostics).</summary>
        public static int RegisteredTypeCount => Handlers.Count;
#endif
    }
}
