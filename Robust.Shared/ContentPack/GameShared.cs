using System;
using System.Collections.Generic;
using Robust.Shared.IoC;
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

    public abstract class RobustEntryPoint : IDisposable
    {
        protected internal IDependencyCollection Dependencies { get; internal set; } = default!;
        protected List<ModuleTestingCallbacks> TestingCallbacks { get; private set; } = new();

        public void SetTestingCallbacks(List<ModuleTestingCallbacks> testingCallbacks)
        {
            TestingCallbacks = testingCallbacks;
        }

        //LEGACY
        public virtual void PreInit()
        {
        }

        //LEGACY
        public virtual void Init()
        {
        }

        //LEGACY
        public virtual void PostInit()
        {
        }

        public virtual void PreInit(bool reloaded)
        {
        }

        public virtual void Init(bool reloaded)
        {
        }

        public virtual void PostInit(bool reloaded)
        {
        }

        public virtual void LiveReloadComplete()
        {
        }

        //LEGACY
        public virtual void Shutdown()
        {
        }

        public virtual void Shutdown(bool reloading)
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

