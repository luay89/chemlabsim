using System;
using System.Collections.Generic;
using ChemLabSimV3.Domain.Events;
using ChemLabSimV3.Infrastructure.Logging;

namespace ChemLabSimV3.Infrastructure.EventBus
{
    /// <summary>Minimal implementation of IDomainEventBus.</summary>
    public class DomainEventBus : IDomainEventBus
    {
        private readonly Dictionary<Type, Delegate> _subscribers = new Dictionary<Type, Delegate>();
        private readonly UnityLogger _logger;

        public DomainEventBus(UnityLogger logger)
        {
            _logger = logger;
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
        {
            var type = typeof(TEvent);
            if (!_subscribers.ContainsKey(type))
                _subscribers[type] = null;

            var del = _subscribers[type] as Action<TEvent>;
            _subscribers[type] = (Action<TEvent>)Delegate.Combine(del, handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
        {
            var type = typeof(TEvent);
            if (_subscribers.ContainsKey(type))
            {
                var del = _subscribers[type] as Action<TEvent>;
                _subscribers[type] = (Action<TEvent>)Delegate.Remove(del, handler);
            }
        }

        public void Publish<TEvent>(TEvent evt) where TEvent : IDomainEvent
        {
            var type = typeof(TEvent);
            if (_subscribers.ContainsKey(type))
            {
                var del = _subscribers[type] as Action<TEvent>;
                del?.Invoke(evt);
            }
        }

        public int GetSubscriberCount<TEvent>() where TEvent : IDomainEvent
        {
            var type = typeof(TEvent);
            if (_subscribers.ContainsKey(type))
            {
                var del = _subscribers[type] as Action<TEvent>;
                return del?.GetInvocationList().Length ?? 0;
            }
            return 0;
        }

        public void Clear()
        {
            _subscribers.Clear();
        }
    }
}
