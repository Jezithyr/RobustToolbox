using System;
using System.Collections.Generic;
using Robust.Shared.ContentModules;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Robust.Shared.ContentPack;

public abstract class SharedEntry : IDisposable, IPostInjectInit
{
    [Dependency] private ILogManager _logManager = default!;
    protected abstract string SawmillCategory { get; }
    protected ISawmill Sawmill = default!;
    protected internal IDependencyCollection Dependencies { get; internal set; } = default!;
    protected List<ModuleTestingCallbacks> TestingCallbacks { get; private set; } = new();

    public void SetTestingCallbacks(List<ModuleTestingCallbacks> testingCallbacks)
    {
        TestingCallbacks = testingCallbacks;
    }

    public virtual void PreInit()
    {

    }

    public virtual void Init()
    {

    }

    public virtual void PostInit()
    {
        Sawmill.Warning($"=====TEST====== {typeof(RobustModuleAttribute).FullName}");
    }

    public virtual void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
    {
    }

    public virtual void Shutdown()
    {

    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    public void PostInject()
    {
        Sawmill = _logManager.GetSawmill(SawmillCategory);
    }
}
