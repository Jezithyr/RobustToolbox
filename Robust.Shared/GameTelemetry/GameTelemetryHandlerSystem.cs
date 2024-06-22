using System.Linq;
using System.Runtime.CompilerServices;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Robust.Shared.GameTelemetry;

public abstract class GameTelemetryHandlerSystem : EntitySystem, IPostInjectInit
{
    [IoC.Dependency] protected GameTelemetryManager TelemetryManager = default!;
    [IoC.Dependency] protected INetManager NetManager = default!;
    protected void SubscribeAllTelemetryHandlers<T>(
        GameTelemetryHandler<T> telemetryHandler,
        string? categoryFilter = null,
        params GameTelemetryId[] ignoreList) where T: notnull
    {
        foreach (var config in TelemetryManager.Systems)
        {
            if (!config.TryGetTelemetryIds(typeof(T), out var data))
                continue;

            foreach (var sensorId in data)
            {
                if (categoryFilter != null && categoryFilter != sensorId.Category
                    || ignoreList.Contains(sensorId)
                    || !TelemetryManager.TryGetSensorData(sensorId, typeof(T), out var sensorData))
                    continue;

                switch (sensorData.Origin)
                {
                    case SensorOrigin.None:
                        continue;
                    case SensorOrigin.Local:
                        SubscribeTelemetryHandler(sensorId, telemetryHandler);
                        continue;
                    case SensorOrigin.Networked:
                        SubscribeNetTelemetryHandler(sensorId, telemetryHandler);
                        continue;
                    case SensorOrigin.Both:
                        SubscribeTelemetryHandler(sensorId, telemetryHandler);
                        SubscribeNetTelemetryHandler(sensorId, telemetryHandler);
                        continue;
                }
            }
        }
    }

    protected void SubscribeAllTelemetryHandlers<T>(
        GameTelemetryRefHandler<T> telemetryHandler,
        string? categoryFilter = null,
        params GameTelemetryId[] ignoreList) where T: notnull
    {
        foreach (var config in TelemetryManager.Systems)
        {
            if (!config.TryGetTelemetryIds(typeof(T), out var data))
                continue;

            foreach (var sensorId in data)
            {
                if (categoryFilter != null && categoryFilter != sensorId.Category
                    || ignoreList.Contains(sensorId)
                    || !TelemetryManager.TryGetSensorData(sensorId, typeof(T), out var sensorData))
                    continue;

                switch (sensorData.Origin)
                {
                    case SensorOrigin.None:
                        continue;
                    case SensorOrigin.Local:
                        SubscribeTelemetryHandler(sensorId, telemetryHandler);
                        continue;
                    case SensorOrigin.Networked:
                        SubscribeNetTelemetryHandler(sensorId, telemetryHandler);
                        continue;
                    case SensorOrigin.Both:
                        SubscribeTelemetryHandler(sensorId, telemetryHandler);
                        SubscribeNetTelemetryHandler(sensorId, telemetryHandler);
                        continue;
                }
            }
        }
    }

    protected void SubscribeTelemetryHandler<T>(
        GameTelemetryId id,
        GameTelemetryHandler<T> eventHandler)
        where T : notnull
    {
        TelemetryManager.SubscribeSensor<T>(
            id,
            SensorOrigin.Local,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(id,tev);
            },
            eventHandler,
            true);
    }

    protected void SubscribeTelemetryHandler<T>(
        GameTelemetryId id,
        GameTelemetryRefHandler<T> eventHandler)
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
            eventHandler,
            true);
    }

    protected void UnSubscribeTelemetryHandler<T>(
        GameTelemetryId id,
        GameTelemetryHandler<T> eventHandler)
        where T : notnull
    {
        TelemetryManager.SubscribeSensor<T>(
            id,
            SensorOrigin.Local,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(id,tev);
            },
            eventHandler,
            true);
    }

    protected void UnSubscribeTelemetryHandler<T>(
        GameTelemetryId id,
        GameTelemetryRefHandler<T> eventHandler)
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
            eventHandler,
            true);
    }

    protected void SubscribeNetTelemetryHandler<T>(
        GameTelemetryId id,
        GameTelemetryHandler<T> eventHandler)
        where T : notnull
    {
        TelemetryManager.SubscribeNetHandler<T>(
            id,
            SensorOrigin.Networked,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(id, tev);
            },
            eventHandler,
            true);
    }

    protected void SubscribeNetTelemetryHandler<T>(
        GameTelemetryId id,
        GameTelemetryRefHandler<T> eventHandler)
        where T : notnull
    {
        TelemetryManager.SubscribeNetHandler<T>(
            id,
            SensorOrigin.Networked,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(id, ref tev);
            },
            eventHandler,
            true);
    }

    protected override void PostInject()
    {
        //This is cursed but I need to do this
        base.PostInject();
        TelemetryManager.RegisterTelemetryHandlerSystem(this);
    }
}
