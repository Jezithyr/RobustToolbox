using System.Runtime.CompilerServices;
using System.Linq;

namespace Robust.Shared.NamedEvents.Systems;

public abstract partial class NamedEventSystem
{
    protected void SubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in NamedEventManager.Systems)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var sensorId in data)
            {
                if (categoryFilter != null && categoryFilter != sensorId.Category
                    || ignoreList.Contains(sensorId)
                    || !NamedEventManager.TryGetSensorData(sensorId, typeof(T), out var sensorData))
                    continue;

                switch (sensorData.Origin)
                {
                    case SensorOrigin.None:
                        continue;
                    case SensorOrigin.Local:
                        SubscribeNamedEventHandler(sensorId, namedEventHandler);
                        continue;
                    case SensorOrigin.Networked:
                        SubscribeNetNamedEventHandler(sensorId, namedEventHandler);
                        continue;
                    case SensorOrigin.Both:
                        SubscribeNamedEventHandler(sensorId, namedEventHandler);
                        SubscribeNetNamedEventHandler(sensorId, namedEventHandler);
                        continue;
                }
            }
        }
    }

    protected void SubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in NamedEventManager.Systems)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var sensorId in data)
            {
                if (categoryFilter != null && categoryFilter != sensorId.Category
                    || ignoreList.Contains(sensorId)
                    || !NamedEventManager.TryGetSensorData(sensorId, typeof(T), out var sensorData))
                    continue;

                switch (sensorData.Origin)
                {
                    case SensorOrigin.None:
                        continue;
                    case SensorOrigin.Local:
                        SubscribeNamedEventHandler(sensorId, namedEventHandler);
                        continue;
                    case SensorOrigin.Networked:
                        SubscribeNetNamedEventHandler(sensorId, namedEventHandler);
                        continue;
                    case SensorOrigin.Both:
                        SubscribeNamedEventHandler(sensorId, namedEventHandler);
                        SubscribeNetNamedEventHandler(sensorId, namedEventHandler);
                        continue;
                }
            }
        }
    }

    protected void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeSensor<T>(
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

    protected void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeSensor<T>(
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

    protected void UnSubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeSensor<T>(
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

    protected void UnSubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeSensor<T>(
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

    protected void SubscribeNetNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNetHandler<T>(
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

    protected void SubscribeNetNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNetHandler<T>(
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
}
