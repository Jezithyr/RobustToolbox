using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.GameSensing;

public sealed partial class GameSensorManager
{
    private bool _registrationLock = true; //prevent skill issues with registration

    private FrozenDictionary<SensorType, SensorData> _eventData =
        FrozenDictionary<SensorType, SensorData>.Empty;
    private readonly Dictionary<SensorType, SensorData> _eventDataUnfrozen = new();

    private readonly Dictionary<Type,GameSensorHandler> _handlers = new();

    internal void RegisterSensorId(SensorId id, Type type, SensorOrigin origin, out SensorData subs)
    {
        if (origin == SensorOrigin.None)
            throw new ArgumentException($"Sensor {id} has an invalid source value!");
        if (_registrationLock)
            throw new InvalidOperationException("Registrations locked. Register Events in a Config!");
        subs = _eventDataUnfrozen.GetOrNew((type, id));
        subs.Origin = origin;
    }

    internal bool TryGetSensorData(SensorId id, Type type, [NotNullWhen(true)] out SensorData? subs)
    {
        return _registrationLock
            ? _eventData.TryGetValue((type, id), out subs)
            : _eventDataUnfrozen.TryGetValue((type, id), out subs);
    }


    private void TriggerSensor<T>(SensorId sensorId,  ref T args) where T : ISensorArgs, new()
    {
        if (!_eventData.TryGetValue((typeof(T), sensorId), out var sensorData))
            return;
        TriggerSensorBase(sensorId, sensorData.Origin, sensorData, ref args);
    }

    private void TriggerSensor<T>(SensorId sensorId, SensorOrigin origin, ref T args) where T : ISensorArgs, new()
    {
        if (!_eventData.TryGetValue((typeof(T), sensorId), out var sensorData)
            || !sensorData.Origin.HasFlag(SensorOrigin.Local))
            return;
        TriggerSensorBase(sensorId, origin, sensorData, ref args);
    }

    private void TriggerSensorBase<T>(SensorId sensorId, SensorOrigin origin,SensorData sensorData, ref T args)
        where T : ISensorArgs, new()
    {
        switch (origin)
        {
            case SensorOrigin.Local:
                TriggerSensorBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
                return;
            case SensorOrigin.Networked:
                //TriggerNetSensorBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
                return;
            case SensorOrigin.Both:
                TriggerSensorBase( ref Unsafe.As<T, Unit>(ref args), sensorData.EnabledSensors);
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

    private static void TriggerSensorBase(
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


    private record struct SensorType(Type ArgType, SensorId Id)
    {
        public static implicit operator SensorType((Type,SensorId) data) => new(data.Item1, data.Item2);

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
        SensorId id,
        SensorOrigin origin,
        SensorRefListener eventListener,
        bool byRef,
        bool startEnabled
    )
        where T : ISensorArgs, new()
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
