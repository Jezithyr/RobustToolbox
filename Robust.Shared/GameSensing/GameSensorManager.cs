using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;

namespace Robust.Shared.GameSensing;

public sealed partial class GameSensorManager : IPostInjectInit
{
    [Dependency] private IReflectionManager _reflectionManager = default!;
    [Dependency] private IDynamicTypeFactoryInternal _typeFactory = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IEntityNetworkManager _networkManager = default!;

    public const string DefaultCategory = "unsorted";
    public const string LogName = "rtgs";
    private ISawmill _sawmill = default!;
    private SensorOrigin _localityMask = SensorOrigin.Networked;
    private SensorOrigin _netMask = SensorOrigin.Local;
    private List<GameSensorConfig> _configs = new();
    public void Initialize()
    {
        if (_netManager.IsClient)
        {
            _localityMask = SensorOrigin.Local;
            _netMask = SensorOrigin.Networked;
        }
        SetupConfigs();
        SetupHandlers();

        SetRegistrationLock(false);
        foreach (var config in _configs)
        {
            config.Initialize(_logManager.GetSawmill($"{LogName}.{config.GetType()}"), _netManager.IsServer);
        }
        foreach (var (type, handler) in _handlers)
        {
            handler.Initialize(_logManager.GetSawmill($"{LogName}.{type}"), _netManager.IsServer, _configs);
        }
        SetRegistrationLock();
    }

    public T GetHandler<T>() where T : GameSensorHandler, new() => (T)_handlers[typeof(T)];

    private void SetupConfigs()
    {
        foreach (var type in _reflectionManager.GetAllChildren<GameSensorConfig>())
        {
            if (type.IsAbstract)
                continue;
            var sensorConfig = (GameSensorConfig)_typeFactory.CreateInstanceUnchecked(type);
            _configs.Add(sensorConfig);
        }
    }

    private void SetupHandlers()
    {
        foreach (var type in _reflectionManager.GetAllChildren<GameSensorHandler>())
        {
            if (type.IsAbstract)
                continue;
            var sensingHandler = (GameSensorHandler)_typeFactory.CreateInstanceUnchecked(type);
            _handlers.Add(type, sensingHandler);
        }

    }

    public void PostInject()
    {
        _logManager.GetSawmill(LogName);
    }
}

public abstract class GameSensorConfig
{
    [Dependency] protected GameSensorManager SensorManager = default!;

    protected ISawmill Sawmill = default!;
    protected SensorOrigin Locality = SensorOrigin.Local;

    private readonly Dictionary<Type,List<SensorId>> _sensorIds = new();

    internal bool TryGetSensorIds(Type type,[NotNullWhen(true)] out List<SensorId>? sensorIds)
    {
        return _sensorIds.TryGetValue(type, out sensorIds);
    }



    protected bool IsServer { get; private set; }
    internal void Initialize(ISawmill sawmill, bool isServer)
    {
        Sawmill = sawmill;
        if (!isServer) return;
        IsServer = true;
        Locality = SensorOrigin.Networked;
        RegisterIds(isServer);
    }


    protected void RegId<T>(SensorId sensorId) where T: ISensorArgs, new()
    {
        RegId<T>(sensorId, Locality);
    }
    protected void RegId<T>(SensorId sensorId, SensorOrigin locality) where T: ISensorArgs, new()
    {
        SensorManager.RegisterSensorId(sensorId, typeof(T), locality, out _);
        _sensorIds.GetOrNew(typeof(T)).Add(sensorId);
    }
    protected abstract void RegisterIds(bool isServer);

}
