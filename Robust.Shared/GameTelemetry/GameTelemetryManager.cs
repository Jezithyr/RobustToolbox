using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;

namespace Robust.Shared.GameTelemetry;

public sealed partial class GameTelemetryManager : IPostInjectInit
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
    private List<GameTelemetryConfig> _configs = new();
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

    public T GetHandler<T>() where T : GameTelemetryHandler, new() => (T)_handlers[typeof(T)];

    private void SetupConfigs()
    {
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryConfig>())
        {
            if (type.IsAbstract)
                continue;
            var sensorConfig = (GameTelemetryConfig)_typeFactory.CreateInstanceUnchecked(type);
            IoCManager.InjectDependencies(sensorConfig);
            _configs.Add(sensorConfig);
        }
    }

    private void SetupHandlers()
    {
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryHandler>())
        {
            if (type.IsAbstract)
                continue;
            var sensingHandler = (GameTelemetryHandler)_typeFactory.CreateInstanceUnchecked(type);
            IoCManager.InjectDependencies(sensingHandler);
            _handlers.Add(type, sensingHandler);
        }

    }

    public void PostInject()
    {
        _logManager.GetSawmill(LogName);
    }
}

public abstract class GameTelemetryConfig
{
    [Dependency] protected GameTelemetryManager TelemetryManager = default!;

    protected ISawmill Sawmill = default!;

    private readonly Dictionary<Type,List<GameTelemetryId>> _sensorIds = new();

    internal bool TryGetSensorIds(Type type,[NotNullWhen(true)] out List<GameTelemetryId>? sensorIds)
    {
        return _sensorIds.TryGetValue(type, out sensorIds);
    }



    protected bool IsServer { get; private set; }
    internal void Initialize(ISawmill sawmill, bool isServer)
    {
        Sawmill = sawmill;
        LoadIds(isServer);
    }


    protected void RegId<T>(string name, string category = GameTelemetryManager.DefaultCategory) where T: IGameTelemetryArgs, new()
    {
        RegId<T>((name, category));
    }

    protected void RegId<T>(GameTelemetryId gameTelemetryId, SensorOrigin locality = SensorOrigin.Local) where T: IGameTelemetryArgs, new()
    {
        TelemetryManager.RegisterSensorId(gameTelemetryId, typeof(T), locality, out _);
        _sensorIds.GetOrNew(typeof(T)).Add(gameTelemetryId);
    }
    protected abstract void LoadIds(bool isServer);

}
