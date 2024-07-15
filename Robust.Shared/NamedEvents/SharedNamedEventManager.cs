using System;
using System.Collections.Frozen;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using EntitySystem = Robust.Shared.GameObjects.EntitySystem;

namespace Robust.Shared.NamedEvents;

public abstract partial class SharedNamedEventManager
{
    [Dependency] private ILogManager _logManager = default!;

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
        Systems.Clear();
    }


    internal void RegisterNamedEventSystem(EntitySystem system)
    {
        if (_registrationLock)
            throw new InvalidOperationException("Registrations are locked. Do not manually call this method!");
        Systems.Add(system);
        _sawmill.Debug($"Registered {system.GetType()} as a named event system!");
    }
}
