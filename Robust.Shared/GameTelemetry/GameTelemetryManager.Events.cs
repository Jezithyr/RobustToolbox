using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.GameTelemetry;

public sealed partial class GameTelemetryManager
{
    private bool _registrationLock = true; //prevent skill issues with registration

    private FrozenDictionary<SensorType, SensorData> _eventData =
        FrozenDictionary<SensorType, SensorData>.Empty;
    private readonly Dictionary<SensorType, SensorData> _eventDataUnfrozen = new();

    private readonly Dictionary<Type,GameTelemetryHandler> _handlers = new();

    internal void RegisterSensorId(GameTelemetryId id, Type type, SensorOrigin origin, out SensorData subs)
    {
        if (origin == SensorOrigin.None)
            throw new ArgumentException($"Sensor {id} has an invalid source value!");
        if (_registrationLock)
            throw new InvalidOperationException("Registrations locked. Register Events in a Config!");
        subs = _eventDataUnfrozen.GetOrNew((type, id));
        subs.Origin = origin;
    }

    internal bool TryGetSensorData(GameTelemetryId id, Type type, [NotNullWhen(true)] out SensorData? subs)
    {
        return _registrationLock
            ? _eventData.TryGetValue((type, id), out subs)
            : _eventDataUnfrozen.TryGetValue((type, id), out subs);
    }

    public void RaiseEvent<T>(GameTelemetryId gameTelemetryId, T args) where T : IGameTelemetryArgs, new()
    {
        if (!_eventData.TryGetValue((typeof(T), gameTelemetryId), out var sensorData))
            return;
        RaiseEventBase(gameTelemetryId, sensorData.Origin, sensorData, ref args);
    }

    public void RaiseEvent<T>(GameTelemetryId gameTelemetryId,  ref T args) where T : IGameTelemetryArgs, new()
    {
        if (!_eventData.TryGetValue((typeof(T), gameTelemetryId), out var sensorData))
            return;
        RaiseEventBase(gameTelemetryId, sensorData.Origin, sensorData, ref args);
    }

    public void RaiseEvent<T>(GameTelemetryId gameTelemetryId, SensorOrigin origin, T args) where T : IGameTelemetryArgs, new()
    {
        if (!_eventData.TryGetValue((typeof(T), gameTelemetryId), out var sensorData)
            || !sensorData.Origin.HasFlag(SensorOrigin.Local))
            return;
        RaiseEventBase(gameTelemetryId, origin, sensorData, ref args);
    }

    public void RaiseEvent<T>(GameTelemetryId gameTelemetryId, SensorOrigin origin, ref T args) where T : IGameTelemetryArgs, new()
    {
        if (!_eventData.TryGetValue((typeof(T), gameTelemetryId), out var sensorData)
            || !sensorData.Origin.HasFlag(SensorOrigin.Local))
            return;
        RaiseEventBase(gameTelemetryId, origin, sensorData, ref args);
    }

    private void RaiseEventBase<T>(GameTelemetryId gameTelemetryId, SensorOrigin origin,SensorData sensorData, ref T args)
        where T : IGameTelemetryArgs, new()
    {
        switch (origin)
        {
            case SensorOrigin.Local:
                RaiseEventBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
                return;
            case SensorOrigin.Networked:
                //TriggerNetSensorBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
                return;
            case SensorOrigin.Both:
                RaiseEventBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
                //TriggerNetSensorBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
                return;
        }

    }

    private static void TriggerNetSensorBase(
        ref Unit unitRef,
        ValueList<SensorListener> subs)
    {
        foreach (var sensor in subs.Span)
        {
            //TODO: STUB
              //sensor.Listener(ref unitRef);
        }
    }

    private static void RaiseEventBase(
        ref Unit unitRef,
        ValueList<SensorListener> subs)
    {
        foreach (var sensor in subs.Span)
        {
            sensor.Listener(ref unitRef);
        }
    }

    internal void SetRegistrationLock(bool state = true)
    {
        _registrationLock = state;
        _eventData = _eventDataUnfrozen.ToFrozenDictionary();
    }


    private record struct SensorType(Type ArgType, GameTelemetryId Id)
    {
        public static implicit operator SensorType((Type,GameTelemetryId) data) => new(data.Item1, data.Item2);

        public bool Equals(SensorType other)
        {
            return ArgType == other.ArgType && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ArgType, Id);
        }
    }

    internal void SubscribeSensor<T>(
        GameTelemetryId id,
        SensorOrigin origin,
        SensorRefListener eventListener,
        bool byRef,
        bool startEnabled
    )
        where T : IGameTelemetryArgs, new()
    {
        ArgumentNullException.ThrowIfNull(eventListener);
        var eventType = typeof(T);
        var eventReference = eventType.HasCustomAttribute<ByRefEventAttribute>();
        if (eventReference != byRef)
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler with ref-value mismatch! sensorArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

        if (!TryGetSensorData(id, eventType, out var subscriptions))
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler to an unregistered SensorId: {id}! sensorArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

        if (!subscriptions.Origin.HasFlag(origin))
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler with invalid source! SensorId: {id}! sensorArgs={eventType} " +
                $"sensorSource={subscriptions.Origin} subscriptionSource={origin}");
        }

        if (!subscriptions.TrySubscribeSensor(new SensorListener(origin, eventListener, eventListener), startEnabled))
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler twice! sensorId={id} sensorArgs={eventType}" +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

    }

}
