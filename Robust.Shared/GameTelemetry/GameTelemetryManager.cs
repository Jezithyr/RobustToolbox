using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Reflection;

namespace Robust.Shared.GameTelemetry;

public sealed partial class GameTelemetryManager : IPostInjectInit
{
    [Dependency] private IReflectionManager _reflectionManager = default!;
    [Dependency] private IDynamicTypeFactoryInternal _typeFactory = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IEntityNetworkManager _networkManager = default!;

    public const string DefaultCategory = "unsorted";
    public const string LogName = "rtgt";
    private ISawmill _sawmill = default!;
    private List<GameTelemetryController> _configs = new();
    public void Initialize()
    {
        _sawmill.Info("Initializing...");
        SetupImplementation();

        SetRegistrationLock(false);
        _sawmill.Info("Unlocking Registrations");
        _sawmill.Info("Initializing Controllers");
        foreach (var controller in _configs)
        {
            controller.Initialize();
            _sawmill.Info($"{controller.GetType()}: Loaded");
        }
        _sawmill.Info("Complete");
        _sawmill.Info("=====================");
        _sawmill.Info("Initializing Handlers");
        foreach (var (_, handler) in _handlers)
        {
            handler.Initialize(_configs);
            _sawmill.Info($"{handler.GetType()}: Loaded");
        }
        SetRegistrationLock();
        _sawmill.Info("Complete");
        _sawmill.Info("=====================");
        _sawmill.Info("Locking Registrations");
        _sawmill.Info("Initialization Complete...");
    }

    public T GetHandler<T>() where T : GameTelemetryHandler, new() => (T)_handlers[typeof(T)];

    private void SetupImplementation()
    {
        _sawmill.Info("Finding Controllers...");
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryController>())
        {
            if (type.IsAbstract)
                continue;
            var sensorConfig = (GameTelemetryController)_typeFactory.CreateInstanceUnchecked(type);
            _sawmill.Info($"Found {type}, creating...");
            IoCManager.InjectDependencies(sensorConfig);
            _configs.Add(sensorConfig);
        }
        _sawmill.Info($"Complete. {_configs.Count} Controllers Created.");
        _sawmill.Info("=====================");
        _sawmill.Info("Finding Handlers...");
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryHandler>())
        {
            if (type.IsAbstract)
                continue;
            var sensingHandler = (GameTelemetryHandler)_typeFactory.CreateInstanceUnchecked(type);
            _sawmill.Info($"Found {type}, creating...");
            IoCManager.InjectDependencies(sensingHandler);
            _handlers.Add(type, sensingHandler);
        }
        _sawmill.Info($"Complete. {_configs.Count} Handlers Created.");
    }
    public void PostInject()
    {
        _logManager.GetSawmill(LogName);
    }
}
