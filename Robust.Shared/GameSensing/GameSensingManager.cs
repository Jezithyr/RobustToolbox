using System;
using System.Collections.Generic;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;

namespace Robust.Shared.GameSensing;

public sealed partial class GameSensingManager : IPostInjectInit
{
    [Dependency] private IReflectionManager _reflectionManager = default!;
    [Dependency] private IDynamicTypeFactoryInternal _typeFactory = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IEntityNetworkManager _networkManager = default!;

    public const string DefaultCategory = "unsorted";
    public const string LogName = "rtgs";
    private ISawmill _sawmill = default!;
    private GameSensorSource _localityMask = GameSensorSource.Server;
    private ValueList<GameSensorConfig> _configs = new();
    public void Initialize()
    {
        if (_netManager.IsClient)
            _localityMask = GameSensorSource.Client;
        SetupConfigs();
        SetupHandlers();

        _registrationLock = false;
        foreach (var (type, handler) in _handlers)
        {
            handler.Initialize(_logManager.GetSawmill($"{LogName}.{type}"), _netManager.IsServer, _configs.ToArray());
        }
        _registrationLock = true;
    }

    public T GetHandler<T>() where T : GameSensingHandler, new() => (T)_handlers[typeof(T)];

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
        foreach (var type in _reflectionManager.GetAllChildren<GameSensingHandler>())
        {
            if (type.IsAbstract)
                continue;
            var sensingHandler = (GameSensingHandler)_typeFactory.CreateInstanceUnchecked(type);
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
    [Dependency] protected GameSensingManager SensingManager = default!;

    protected ISawmill Sawmill = default!;
    protected GameSensorSource LocalityMask = GameSensorSource.Client;

    internal readonly Dictionary<Type,Dictionary<GameSensorId,GameSensorSource>> SensorIds = new();

    protected bool IsServer { get; private set; }
    internal void Initialize(ISawmill sawmill, bool isServer)
    {
        Sawmill = sawmill;
        if (!isServer) return;
        IsServer = true;
        LocalityMask = GameSensorSource.Server;
        RegisterIds(isServer);
    }

    protected void RegId<T>(GameSensorId sensorId, GameSensorSource locality = GameSensorSource.Both) where T: IGameSensorArgs, new()
    {
        SensingManager.RegisterHandler(sensorId, typeof(T), out _);
        SensorIds.GetOrNew(typeof(T)).Add(sensorId, locality);
    }


    protected abstract void RegisterIds(bool isServer);

}
