using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using static Robust.Shared.GameSensing.GameSensingManager;
namespace Robust.Shared.GameSensing;

public abstract class GameSensingHandler
{
    [IoC.Dependency] protected GameSensingManager SensingManager = default!;
    protected ISawmill Sawmill = default!;
    private GameSensorSource _localityMask = GameSensorSource.Client;
    private GameSensorSource _netMask = GameSensorSource.Server;
    private GameSensorConfig[] _configs = default!;
    protected bool IsServer { get; private set; }

    internal void Initialize(ISawmill sawmill, bool isServer, GameSensorConfig[] configs)
    {
        Sawmill = sawmill;
        _configs = configs;
        if (!isServer)
        {
            IsServer = true;
            _localityMask = GameSensorSource.Server;
            _netMask = GameSensorSource.Client;
        }
        RegisterHandlers(isServer);
    }

    protected void RegisterAllHandlers<T>(
        GameSensorHandler<T> eventHandler,
        bool startEnabled = true ,
        params GameSensorId[] ignoreList) where T: IGameSensorArgs, new()
    {
        foreach (var config in _configs)
        {
            if (!config.SensorIds.TryGetValue(typeof(T), out var data))
                continue;
            foreach (var (id, locality) in data)
            {
                if (ignoreList.Contains(id))
                    continue;
                if (locality.HasFlag(_localityMask))
                    RegLocalHandler(id, eventHandler, startEnabled);
                if (locality.HasFlag(_netMask))
                    RegNetHandler(id, eventHandler, startEnabled);
            }
        }
    }

    protected abstract void RegisterHandlers(bool isServer);



    protected void RegLocalHandler<T>(
        GameSensorId id,
        GameSensorHandler<T> eventHandler,
        bool startEnabled = true)
        where T : IGameSensorArgs, new()
    {
        RegHandlerBase<T>(
            id,
            _localityMask,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(tev);
            },
            true,
            startEnabled);
    }

    protected void RegLocalHandler<T>(
        GameSensorId id,
        GameSensorRefHandler<T> eventHandler,
        bool startEnabled = true)
        where T : IGameSensorArgs, new()
    {
        RegHandlerBase<T>(
            id,
            _localityMask,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(ref tev);
            },
            true,
            startEnabled);
    }

    private void RegHandlerBase<T>(
        GameSensorId id,
        GameSensorSource source,
        GameSensorRefHandler eventHandler,
        bool byRef,
        bool startEnabled
        )
        where T : IGameSensorArgs, new()
    {
        ArgumentNullException.ThrowIfNull(eventHandler);
        var eventType = typeof(T);
        var eventReference = eventType.HasCustomAttribute<ByRefEventAttribute>();
        if (eventReference != byRef)
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler with ref-value mismatch! sensorArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

        if (!SensingManager.TryGetHandler(id, eventType, out var subscriptions))
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler to an unregistered SensorId: {id}! sensorArgs={eventType} " +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }
        if (!subscriptions.TryRegisterHandler(new BroadcastRegistration(source, eventHandler, eventHandler), startEnabled))
        {
            throw new InvalidOperationException(
                $"Attempted to register a gameSensor handler twice sensorId={id} sensorArgs={eventType}" +
                $"eventIsByRef={eventReference} subscriptionIsByRef={byRef}");
        }

    }


    protected void RegNetHandler<T>(
        GameSensorId id,
        GameSensorHandler<T> eventHandler,
        bool startEnabled = true)
        where T : IGameSensorArgs, new()
    {
        RegNetHandlerBase<T>(
            id,
            _localityMask,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(tev);
            },
            true,
            startEnabled);
    }

    protected void RegNetHandler<T>(
        GameSensorId id,
        GameSensorRefHandler<T> eventHandler,
        bool startEnabled = true)
        where T : IGameSensorArgs, new()
    {
        RegNetHandlerBase<T>(
            id,
            _localityMask,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(ref tev);
            },
            true,
            startEnabled);
    }

    private void RegNetHandlerBase<T>(
        GameSensorId id,
        GameSensorSource source,
        GameSensorRefHandler eventHandler,
        bool byRef,
        bool startEnabled
    )
        where T : IGameSensorArgs, new()
    {
        //TODO: STUB IMPLEMENT NET EVENTS
    }

}
