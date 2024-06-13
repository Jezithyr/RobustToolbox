﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Robust.Shared.Log;
namespace Robust.Shared.GameTelemetry;

public abstract class GameTelemetryHandler
{
    [IoC.Dependency] protected GameTelemetryManager TelemetryManager = default!;
    protected ISawmill Sawmill = default!;
    private List<GameTelemetryConfig> _configs = default!;
    protected bool IsServer { get; private set; }

    internal void Initialize(ISawmill sawmill, bool isServer, List<GameTelemetryConfig> configs)
    {
        Sawmill = sawmill;
        _configs = configs;
        IsServer = isServer;
        RegisterHandlers(isServer);
    }

    protected void SubscribeAllListeners<T>(
        GameTelemetryListener<T> telemetryListener,
        bool startEnabled = true,
        params GameTelemetryId[] ignoreList) where T: notnull
    {
        foreach (var config in _configs)
        {
            if (!config.TryGetSensorIds(typeof(T), out var data))
                continue;
            foreach (var sensorId in data)
            {
                if (ignoreList.Contains(sensorId) || !TelemetryManager.TryGetSensorData(sensorId, typeof(T), out var sensorData))
                    continue;

                switch (sensorData.Origin)
                {
                    case SensorOrigin.None:
                        continue;
                    case SensorOrigin.Local:
                        SubscribeSensor(sensorId, telemetryListener, startEnabled);
                        continue;
                    case SensorOrigin.Networked:
                        SubscribeNetSensor(sensorId, telemetryListener, startEnabled);
                        continue;
                    case SensorOrigin.Both:
                        SubscribeSensor(sensorId, telemetryListener, startEnabled);
                        SubscribeNetSensor(sensorId, telemetryListener, startEnabled);
                        continue;
                }
            }
        }
    }

    protected abstract void RegisterHandlers(bool isServer);



    protected void SubscribeSensor<T>(
        GameTelemetryId id,
        GameTelemetryListener<T> eventListener,
        bool startEnabled = true)
        where T : notnull
    {
        TelemetryManager.SubscribeSensor<T>(
            id,
            SensorOrigin.Local,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventListener(id,tev);
            },
            true,
            startEnabled);
    }

    protected void SubscribeSensor<T>(
        GameTelemetryId id,
        GameTelemetryRefHandler<T> eventHandler,
        bool startEnabled = true)
        where T : notnull
    {
        TelemetryManager.SubscribeSensor<T>(
            id,
            SensorOrigin.Local,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(id, ref tev);
            },
            true,
            startEnabled);
    }

    protected void SubscribeNetSensor<T>(
        GameTelemetryId id,
        GameTelemetryListener<T> eventListener,
        bool startEnabled = true)
        where T : notnull
    {
        TelemetryManager.SubscribeNetSensor<T>(
            id,
            SensorOrigin.Networked,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventListener(id, tev);
            },
            true,
            startEnabled);
    }

    protected void SubscribeNetSensor<T>(
        GameTelemetryId id,
        GameTelemetryRefHandler<T> eventHandler,
        bool startEnabled = true)
        where T : notnull
    {
        TelemetryManager.SubscribeNetSensor<T>(
            id,
            SensorOrigin.Networked,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(id, ref tev);
            },
            true,
            startEnabled);
    }
}
