using System;
using System.Collections.Frozen;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Reflection;

namespace Robust.Shared.GameTelemetry;

public sealed partial class GameTelemetryManager
{
    [Dependency] private IReflectionManager _reflectionManager = default!;
    [Dependency] private IDynamicTypeFactoryInternal _typeFactory = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IEntityNetworkManager _networkManager = default!;
    [Dependency] private IEntityManager _entManager = default!;

    public const string DefaultCategory = "unsorted";
    public const string LogName = "rtgt";
    private ISawmill _sawmill = default!;

    public void Startup()
    {
        _sawmill = _logManager.GetSawmill(LogName);
        _sawmill.Debug("Initializing...");
        SetRegistrationLock(false);
    }

    public void PostStart()
    {
        SetRegistrationLock();
        _sawmill.Debug("Init Complete!");
    }

    public void Shutdown()
    {
        _sawmill.Debug("Shutting Down...");
        Terminate();
    }

    public void Cleanup()
    {
        _sawmill.Debug("Cleaning Up...");
        Terminate();
    }
    private void Terminate()
    {
        SetRegistrationLock();
        _eventDataUnfrozen.Clear();
        _eventData = _eventDataUnfrozen.ToFrozenDictionary();
        _handlers.Clear();
        Systems.Clear();
    }


    internal void RegisterTelemetrySystem(GameTelemetrySystem system)
    {
        if (_registrationLock)
            throw new InvalidOperationException("Registrations are locked. Do not manually call this method!");
        Systems.Add(system);
        _sawmill.Debug($"Registered {system.GetType()} as a telemetry system!");
    }

    internal void RegisterTelemetryHandlerSystem(GameTelemetryHandlerSystem system)
    {
        if (_registrationLock)
            throw new InvalidOperationException("Registrations are locked. Do not manually call this method!");
        _handlers.Add(system);
        _sawmill.Debug($"Registered {system.GetType()} as a telemetry handler system!");
    }
}
