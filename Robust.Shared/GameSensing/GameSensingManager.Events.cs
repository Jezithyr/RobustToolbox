using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Collections;
using Robust.Shared.Utility;

namespace Robust.Shared.GameSensing;

public sealed partial class GameSensingManager
{
    private bool _registrationLock = true; //prevent skill issues with registration

    private FrozenDictionary<SensorType, SensorData> _eventData =
        FrozenDictionary<SensorType, SensorData>.Empty;
    private readonly Dictionary<SensorType, SensorData> _eventDataUnfrozen = new();

    private readonly Dictionary<Type,GameSensingHandler> _handlers = new();

    internal void RegisterHandler(GameSensorId id, Type type, out SensorData subs)
    {
        if (_registrationLock)
            throw new InvalidOperationException("Subscriptions locked. Register Events in a SensingHandler!");
        subs = _eventDataUnfrozen.GetOrNew((type, id));
    }

    internal bool TryGetHandler(GameSensorId id, Type type,[NotNullWhen(true)] out SensorData? subs)
    {
        return _registrationLock
            ? _eventData.TryGetValue((type, id), out subs)
            : _eventDataUnfrozen.TryGetValue((type, id), out subs);
    }


    private static void ProcessSingleEvent(
        GameSensorSource source,
        ref Unit unitRef,
        ValueList<BroadcastRegistration> subs)
    {
        foreach (var handler in subs.Span)
        {
            if ((handler.Mask & source) != 0)
                handler.GameSensorRefHandler(ref unitRef);
        }
    }

    internal void SetSubscriptionLock(bool state = true)
    {
        _registrationLock = state;
        _eventData = _eventDataUnfrozen.ToFrozenDictionary();
    }

    internal struct Unit;

    internal delegate void GameSensorRefHandler(ref Unit ev);
    private record struct SensorType(Type ArgType, GameSensorId Id)
    {
        public static implicit operator SensorType((Type,GameSensorId) data) => new(data.Item1, data.Item2);

        public bool Equals(SensorType other)
        {
            return ArgType == other.ArgType && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ArgType, Id);
        }
    }

    internal sealed record SensorData(
        ValueList<BroadcastRegistration> EnabledHandlers,
        ValueList<BroadcastRegistration> DisabledHandlers)
    {
        public SensorData() : this(
            new ValueList<BroadcastRegistration>(),
            new ValueList<BroadcastRegistration>())
        {
        }

        public bool HandlerDisabled(BroadcastRegistration handler) => DisabledHandlers.Contains(handler);
        public bool HandlerEnabled(BroadcastRegistration handler) => EnabledHandlers.Contains(handler);

        public bool Contains(BroadcastRegistration handler) =>
            HandlerEnabled(handler) ||
            HandlerDisabled(handler);

        public bool TryRegisterHandler(BroadcastRegistration handler, bool enabled = true)
        {
            if (Contains(handler))
                return false;
            if (enabled)
            {
                EnabledHandlers.Add(handler);
                return true;
            }
            DisabledHandlers.Add(handler);
            return true;
        }

    }

    internal readonly record struct BroadcastRegistration(
        GameSensorSource Mask,
        GameSensorRefHandler GameSensorRefHandler,
        object EqualityToken)
    {
        public bool Equals(BroadcastRegistration other)
        {
            return Mask == other.Mask && Equals(EqualityToken, other.EqualityToken);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Mask, EqualityToken);
        }
    }

}
