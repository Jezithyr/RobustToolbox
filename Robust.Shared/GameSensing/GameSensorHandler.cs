using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Robust.Shared.Log;
using static Robust.Shared.GameSensing.GameSensorManager;
namespace Robust.Shared.GameSensing;

public abstract class GameSensorHandler
{
    [IoC.Dependency] protected GameSensorManager SensorManager = default!;
    protected ISawmill Sawmill = default!;
    private SensorOrigin _localityMask = SensorOrigin.Networked;
    private List<GameSensorConfig> _configs = default!;
    protected bool IsServer { get; private set; }

    internal void Initialize(ISawmill sawmill, bool isServer, List<GameSensorConfig> configs)
    {
        Sawmill = sawmill;
        _configs = configs;
        IsServer = isServer;
        RegisterHandlers(isServer);
    }

    protected void SubscribeAllListeners<T>(
        GameSensorListener<T> sensorListener,
        bool startEnabled = true,
        params SensorId[] ignoreList) where T: ISensorArgs, new()
    {
        foreach (var config in _configs)
        {
            if (!config.TryGetSensorIds(typeof(T), out var data))
                continue;
            foreach (var sensorId in data)
            {
                if (ignoreList.Contains(sensorId) || !SensorManager.TryGetSensorData(sensorId, typeof(T), out var sensorData))
                    continue;

                switch (sensorData.Origin)
                {
                    case SensorOrigin.None:
                        continue;
                    case SensorOrigin.Local:
                        Sub(sensorId, sensorListener, startEnabled);
                        continue;
                    case SensorOrigin.Networked:
                        NetSub(sensorId, sensorListener, startEnabled);
                        continue;
                    case SensorOrigin.Both:
                        Sub(sensorId, sensorListener, startEnabled);
                        NetSub(sensorId, sensorListener, startEnabled);
                        continue;
                }
            }
        }
    }

    protected abstract void RegisterHandlers(bool isServer);



    protected void Sub<T>(
        SensorId id,
        GameSensorListener<T> eventListener,
        bool startEnabled = true)
        where T : ISensorArgs, new()
    {
        SensorManager.SubscribeSensor<T>(
            id,
            SensorOrigin.Local,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventListener(tev);
            },
            true,
            startEnabled);
    }

    protected void Sub<T>(
        SensorId id,
        GameSensorRefHandler<T> eventHandler,
        bool startEnabled = true)
        where T : ISensorArgs, new()
    {
        SensorManager.SubscribeSensor<T>(
            id,
            SensorOrigin.Local,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(ref tev);
            },
            true,
            startEnabled);
    }

    protected void NetSub<T>(
        SensorId id,
        GameSensorListener<T> eventListener,
        bool startEnabled = true)
        where T : ISensorArgs, new()
    {
        SensorManager.SubscribeNetSensor<T>(
            id,
            SensorOrigin.Networked,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventListener(tev);
            },
            true,
            startEnabled);
    }

    protected void NetSub<T>(
        SensorId id,
        GameSensorRefHandler<T> eventHandler,
        bool startEnabled = true)
        where T : ISensorArgs, new()
    {
        SensorManager.SubscribeNetSensor<T>(
            id,
            SensorOrigin.Networked,
            (ref Unit ev) =>
            {
                ref var tev = ref Unsafe.As<Unit, T>(ref ev);
                eventHandler(ref tev);
            },
            true,
            startEnabled);
    }
}
