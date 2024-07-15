using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using EntitySystem = Robust.Shared.GameObjects.EntitySystem;

namespace Robust.Shared.NamedEvents;

public sealed partial class NamedEventManager
{
    private bool _registrationLock = true; //prevent skill issues with registration

    private FrozenDictionary<SubscriptionId, NamedEventData> _eventData =
        FrozenDictionary<SubscriptionId, NamedEventData>.Empty;
    private readonly Dictionary<SubscriptionId, NamedEventData> _eventDataUnfrozen = new();
    private readonly List<EntitySystem> _systems = new();
    internal void RegisterEventId<T>(NamedEventId id, Locality locality, out NamedEventData subs, bool replayCaptured = true) where T: notnull
    {
        if (locality == Locality.None)
            throw new ArgumentException($"NamedEvent {id} has an invalid source value!");
        if (_registrationLock)
            throw new InvalidOperationException("Registrations locked. Register Events in a Config!");
        if (_eventDataUnfrozen.TryGetValue((typeof(T), id), out var foundSubs))
        {
            subs = foundSubs;
            _sawmill.Warning($"NamedEvent with ID: {id} was already registered!");
            return;
        }
        subs = new NamedEventData(locality, replayCaptured);
        _eventDataUnfrozen.Add((typeof(T), id), subs);
        if (locality.HasFlag(Locality.Networked))
            RegisterNetworkEvent<T>();
    }

    private bool TryGetSubscriptionData(NamedEventId id, Type type, [NotNullWhen(true)] out NamedEventData? subs)
    {
        return _registrationLock
            ? _eventData.TryGetValue((type, id), out subs)
            : _eventDataUnfrozen.TryGetValue((type, id), out subs);
    }

    private bool CanRaiseEvent<T>(
        NamedEventId namedEventId,
        Locality target,
        bool oneShot,
        [NotNullWhen(true)] out NamedEventData? data)
    {
        if (!_eventData.TryGetValue((typeof(T), namedEventId), out data))
            return false;
        if ((data.AllowedOrigins & target) != Locality.None)
            return UpdateOneShot(data, oneShot);
        _sawmill.Warning($"Tried to raise event with id: {namedEventId} with invalid target: {target}!");
        return false;
    }

    private bool UpdateOneShot(
        NamedEventData namedEventData,
        bool oneShot)
    {
        if (!oneShot) return true;
        if (namedEventData.Triggered)
            return false;
        namedEventData.Triggered = true;

        return true;
    }

    private void RaiseEventBase<T>(
        NamedEventId namedEventId,
        Locality target,
        NamedEventData namedEventData,
        ref T args,
        bool oneShot,
        INetChannel? channel = null)
        where T : notnull
    {
        switch (target)
        {
            case Locality.Local:
                RaiseEventBase( ref Unsafe.As<T, Unit>(ref args), namedEventData.EnabledSubscriptions);
                return;
            case Locality.Networked:
                RaiseNetworkEventBase(namedEventId,ref args, oneShot);
                return;
            case Locality.Both:
                RaiseEventBase(ref Unsafe.As<T, Unit>(ref args), namedEventData.EnabledSubscriptions);
                RaiseNetworkEventBase(namedEventId,ref args, oneShot);
                return;
        }

    }

    private void ReceiveNetEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneShot) where T: notnull
    {
        if (!_eventData.TryGetValue((typeof(T), namedEventId), out var subData))
        {
            _sawmill.Error($"Could not find event registration for event with ID: {namedEventId}, Type: {typeof(T)}");
            return;
        }
        if (!UpdateOneShot(subData, oneShot))
            return;
        RaiseEventBase( ref Unsafe.As<T, Unit>(ref args), subData.EnabledSubscriptions);
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
        Locality locality,
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

        if (!subscriptions.AllowedOrigins.HasFlag(locality))
        {
            throw new InvalidOperationException(
                $"Attempted to subscribe a NamedEvent handler with invalid source! NamedEventId: {id}! NamedEventArgs={eventType} " +
                $"NamedEventSource={subscriptions.AllowedOrigins} subscriptionSource={locality}");
        }

        if (!subscriptions.TryAddSubscription(new Subscription(locality, listenerEvent, listenerRef)))
        {
            throw new InvalidOperationException(
                $"Attempted to subscribe a NamedEvent handler twice! NamedEventId={id} NamedEventArgs={eventType}" +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }
    }


    private void RemoveSubscription<T>(
        NamedEventId id,
        Locality locality,
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

        if (!subscriptions.AllowedOrigins.HasFlag(locality))
        {
            throw new InvalidOperationException(
                $"Attempted to unsubscribe a NamedEvent handler with invalid source! NamedEventId: {id}! NamedEventArgs={eventType} " +
                $"NamedEventSource={subscriptions.AllowedOrigins} subscriptionSource={locality}");
        }

        if (!subscriptions.TryRemoveSubscription(subscriber))
        {
            throw new InvalidOperationException(
                $"Attempted to unsubscribe a NamedEvent handler twice! NamedEventId={id} NamedEventArgs={eventType}" +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }
    }
}
