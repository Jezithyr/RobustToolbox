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
        _sawmill.Debug("Initializing...");
        SetupImplementation();

        SetRegistrationLock(false);
        _sawmill.Debug("Unlocking Registrations. Initializing Controllers");
        foreach (var controller in _configs)
        {
            controller.Initialize();
            _sawmill.Verbose($"{controller.GetType().Name}: Loaded");
        }
        _sawmill.Debug("Complete. Initializing Handlers");
        foreach (var (_, handler) in _handlers)
        {
            handler.Initialize(_configs);
            _sawmill.Verbose($"{handler.GetType().Name}: Loaded");
        }
        SetRegistrationLock();
        _sawmill.Debug("Completed. Locking Registrations. Initialization Complete...");

    }

    public T GetHandler<T>() where T : GameTelemetryHandler, new() => (T)_handlers[typeof(T)];

    private void SetupImplementation()
    {
        _sawmill.Debug("Finding Controllers...");
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryController>())
        {
            if (type.IsAbstract)
                continue;
            var sensorConfig = (GameTelemetryController)_typeFactory.CreateInstanceUnchecked(type);
            _sawmill.Verbose($"Found {type.Name}, creating...");
            IoCManager.InjectDependencies(sensorConfig);
            _configs.Add(sensorConfig);
        }
        _sawmill.Debug($"Complete. {_configs.Count} Controllers Created. Finding Handlers...");
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryHandler>())
        {
            if (type.IsAbstract)
                continue;
            var sensingHandler = (GameTelemetryHandler)_typeFactory.CreateInstanceUnchecked(type);
            _sawmill.Verbose($"Found {type.Name}, creating...");
            IoCManager.InjectDependencies(sensingHandler);
            _handlers.Add(type, sensingHandler);
        }
        _sawmill.Debug($"Complete. {_configs.Count} Handlers Created.");
    }
    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill(LogName);
    }
}
