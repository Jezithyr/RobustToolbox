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
    protected void SubscribeAllHandlers<T>(
        GameTelemetryListener<T> telemetryListener,
        bool startEnabled = true,
        params GameTelemetryId[] ignoreList) where T: notnull
    {
        foreach (var config in TelemetryManager.Systems)
        {
            if (!config.TryGetTelemetryIds(typeof(T), out var data))
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
                        SubscribeHandler(sensorId, telemetryListener, startEnabled);
                        continue;
                    case SensorOrigin.Networked:
                        SubscribeNetHandler(sensorId, telemetryListener, startEnabled);
                        continue;
                    case SensorOrigin.Both:
                        SubscribeHandler(sensorId, telemetryListener, startEnabled);
                        SubscribeNetHandler(sensorId, telemetryListener, startEnabled);
                        continue;
                }
            }
        }
    }

    protected void SubscribeHandler<T>(
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

    protected void SubscribeHandler<T>(
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

    protected void SubscribeNetHandler<T>(
        GameTelemetryId id,
        GameTelemetryListener<T> eventListener,
        bool startEnabled = true)
        where T : notnull
    {
        TelemetryManager.SubscribeNetHandler<T>(
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

    protected void SubscribeNetHandler<T>(
        GameTelemetryId id,
        GameTelemetryRefHandler<T> eventHandler,
        bool startEnabled = true)
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
            true,
            startEnabled);
    }

    protected override void PostInject()
    {
        //This is cursed but I need to do this
        base.PostInject();
        TelemetryManager.RegisterTelemetryHandlerSystem(this);
    }
}
