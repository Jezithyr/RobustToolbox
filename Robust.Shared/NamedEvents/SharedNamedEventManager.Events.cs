using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;
using EntitySystem = Robust.Shared.GameObjects.EntitySystem;

namespace Robust.Shared.NamedEvents;

public abstract partial class SharedNamedEventManager
{
    private bool _registrationLock = true; //prevent skill issues with registration

    private FrozenDictionary<SubscriptionId, SubscriptionData> _eventData =
        FrozenDictionary<SubscriptionId, SubscriptionData>.Empty;
    private readonly Dictionary<SubscriptionId, SubscriptionData> _eventDataUnfrozen = new();
    internal List<EntitySystem> Systems = new();
    internal void RegisterNamedEventId(NamedEventId id, Type type, Origin origin, out SubscriptionData subs)
    {
        if (origin == Origin.None)
            throw new ArgumentException($"NamedEvent {id} has an invalid source value!");
        if (_registrationLock)
            throw new InvalidOperationException("Registrations locked. Register Events in a Config!");
        subs = _eventDataUnfrozen.GetOrNew((type, id));
        subs.AllowedOrigins = origin;
    }

    private bool TryGetSubscriptionData(NamedEventId id, Type type, [NotNullWhen(true)] out SubscriptionData? subs)
    {
        return _registrationLock
            ? _eventData.TryGetValue((type, id), out subs)
            : _eventDataUnfrozen.TryGetValue((type, id), out subs);
    }

    private void RaiseEventBase<T>(NamedEventId namedEventId, Origin target,SubscriptionData subscriptionData, ref T args, bool oneShot)
        where T : notnull
    {
        if ((subscriptionData.AllowedOrigins & target) == Origin.None)
        {
            _sawmill.Warning($"Tried to raise event with id: {namedEventId} with invalid target: {target}!");
            return;
        }

        if (oneShot)
        {
            if (subscriptionData.Triggered)
                return;
            subscriptionData.Triggered = true;
        }
        switch (target)
        {
            case Origin.Local:
                RaiseEventBase( ref Unsafe.As<T, Unit>(ref args), subscriptionData.EnabledSubscriptions);
                return;
            case Origin.Networked:
                //RaiseNetEventBase( ref Unsafe.As<T, Unit>(ref args), subscriptionData.EnabledSubscriptions);
                return;
            case Origin.Both:
                RaiseEventBase( ref Unsafe.As<T, Unit>(ref args), subscriptionData.EnabledSubscriptions);
                //RaiseNetEventBase( ref Unsafe.As<T, Unit>(ref args), subscriptionData.EnabledSubscriptions);
                return;
        }

    }

    private static void RaiseNetEventBase(
        ref Unit unitRef,
        ValueList<Subscription> subs)
    {
        foreach (var sub in subs.Span)
        {
            //TODO: STUB
              //sub.Listener(ref unitRef);
        }
    }

    private static void RaiseEventBase(
        ref Unit unitRef,
        ValueList<Subscription> subs)
    {
        foreach (var sub in subs.Span)
        {
            sub.Listener(ref unitRef);
        }
    }

    internal void SetRegistrationLock(bool state = true)
    {
        if (state == _registrationLock)
            return;
        _registrationLock = state;
        _sawmill.Debug(state ? "Locking Registrations" : "Unlocking Registrations");
        _eventData = _eventDataUnfrozen.ToFrozenDictionary();
    }


    private readonly record struct SubscriptionId(Type ArgType, NamedEventId Id)
    {
        public static implicit operator SubscriptionId((Type,NamedEventId) data) => new(data.Item1, data.Item2);

        public bool Equals(SubscriptionId other)
        {
            return ArgType == other.ArgType && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ArgType, Id);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SubscriptionListenerRef CreateSubMethod<T>(NamedEventId id,NamedEventRefHandler<T> eventHandler) where T : notnull
    {
        return (ref Unit ev) =>
        {
            ref var tev = ref Unsafe.As<Unit, T>(ref ev);
            eventHandler(id, ref tev);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SubscriptionListenerRef CreateSubMethod<T>(NamedEventId id,NamedEventHandler<T> eventHandler) where T : notnull
    {
        return (ref Unit ev) =>
        {
            ref var tev = ref Unsafe.As<Unit, T>(ref ev);
            eventHandler(id, tev);
        };
    }

    private void CreateSubscription<T>(
        NamedEventId id,
        Origin origin,
        SubscriptionListenerRef listenerEvent,
        object listenerRef,
        bool byRef
    )
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(listenerEvent);
        var eventType = typeof(T);
        var eventReference = eventType.HasCustomAttribute<ByRefEventAttribute>();
        if (eventReference != byRef)
        {
            throw new InvalidOperationException(
                $"Attempted to subscribe a NamedEvent handler with ref-value mismatch! NamedEventArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

        if (!TryGetSubscriptionData(id, eventType, out var subscriptions))
        {
            throw new InvalidOperationException(
                $"Attempted to subscribe a NamedEvent handler to an unregistered NamedEventId: {id}! NamedEventArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

        if (!subscriptions.AllowedOrigins.HasFlag(origin))
        {
            throw new InvalidOperationException(
                $"Attempted to subscribe a NamedEvent handler with invalid source! NamedEventId: {id}! NamedEventArgs={eventType} " +
                $"NamedEventSource={subscriptions.AllowedOrigins} subscriptionSource={origin}");
        }

        if (!subscriptions.TryAddSubscription(new Subscription(origin, listenerEvent, listenerRef)))
        {
            throw new InvalidOperationException(
                $"Attempted to subscribe a NamedEvent handler twice! NamedEventId={id} NamedEventArgs={eventType}" +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }
    }


    private void RemoveSubscription<T>(
        NamedEventId id,
        Origin origin,
        object subscriber,
        bool byRef,
        bool warnIfMissing = false
    )
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        var eventType = typeof(T);
        var eventReference = eventType.HasCustomAttribute<ByRefEventAttribute>();
        if (eventReference != byRef)
        {
            throw new InvalidOperationException(
                $"Attempted to unsubscribe a NamedEvent handler with ref-value mismatch! NamedEventArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

        if (!TryGetSubscriptionData(id, eventType, out var subscriptions))
        {
            if (warnIfMissing)
            {
                _sawmill.Warning( $"Attempted to unsubscribe a NamedEvent handler from an unregistered NamedEventId: {id}! NamedEventArgs={eventType} " +
                                  $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
            }
            return;
        }

        if (!subscriptions.AllowedOrigins.HasFlag(origin))
        {
            throw new InvalidOperationException(
                $"Attempted to unsubscribe a NamedEvent handler with invalid source! NamedEventId: {id}! NamedEventArgs={eventType} " +
                $"NamedEventSource={subscriptions.AllowedOrigins} subscriptionSource={origin}");
        }

        if (!subscriptions.TryRemoveSubscription(subscriber))
        {
            throw new InvalidOperationException(
                $"Attempted to unsubscribe a NamedEvent handler twice! NamedEventId={id} NamedEventArgs={eventType}" +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }
    }
}
