using System;
using System.Collections.Generic;
using Robust.Shared.IoC;
using Robust.Shared.Modules;
using Robust.Shared.Timing;

namespace Robust.Shared.ContentPack
{
    /// <summary>
    ///     Common entry point for Content assemblies.
    /// </summary>
    public abstract class GameShared : RobustEntryPoint
    {
        public virtual void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
        {
        }
    }

    /// <summary>
    ///     Common entry point for Live-Reloadable Content assemblies.
    /// </summary>
    public abstract class ReloadableGameShared : ReloadableRobustEntryPoint
    {
        public virtual void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
        {
        }
    }


    public abstract class ReloadableRobustEntryPoint : RobustEntryPoint, IReloadableEntrypoint
    {
        //runs just before assemblies are reloaded
        public virtual void ModuleReloadStart()
        {
        }

        //runs after all modules are reloaded but BEFORE all the init callbacks
        public virtual void ModuleReloadCompleted()
        {
        }

        public virtual void ReloadPreInit()
        {
        }

        public virtual void ReloadInit()
        {
        }

        public virtual void ReloadPostInit()
        {
        }

        public void ReloadedPreInit()
        {
        }

        public void ReloadedInit()
        {
        }

        public void ReloadedPostInit()
        {
        }
    }

    public abstract class RobustEntryPoint : IDisposable
    {
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
    }

}

