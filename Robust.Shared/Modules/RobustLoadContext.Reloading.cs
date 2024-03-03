#if LIVE_RELOAD_ENABLED
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;
using JetBrains.Annotations;
using Robust.Shared.ContentPack;

namespace Robust.Shared.Modules;

[UsedImplicitly]
internal sealed class ReloadableRobustLoadContext : RobustLoadContextBase
{
    internal override bool SupportsReloading => true;
    protected override bool TryCreateEntryPoint(Type entryPointType, [NotNullWhen(true)] out RobustEntryPoint? entryPointInstance)
    {
        if (!base.TryCreateEntryPoint(entryPointType, out entryPointInstance))
            return false;
        return entryPointInstance is not IReloadableEntrypoint;
    }
    protected override AssemblyLoadContext CreateLoadContextWithName(string? name)
    {
        return new AssemblyLoadContext(name, true);
    }
    public void BroadcastReloadLevel(ModRunLevel runLevel)
    {
        foreach (var (_, entryPoints) in EntryPoints)
        {
            foreach (var robustEntryPoint in entryPoints)
            {
                var entry = (IReloadableEntrypoint) robustEntryPoint;
                switch (runLevel)
                {
                    case ModRunLevel.PreInit:
                        entry.ReloadedPreInit();
                        break;
                    case ModRunLevel.Init:
                        entry.ReloadedInit();
                        break;
                    case ModRunLevel.PostInit:
                        entry.ReloadedPostInit();
                        break;
                    default:
                        Sawmill.Error($"Unknown RunLevel: {runLevel}");
                        break;
                }
            }
        }
    }

    internal override void BroadcastReloadStart()
    {
        foreach (var listener in ReloadListeners)
        {
            listener.LiveReloadTriggered();
        }
    }

    internal override void BroadcastReloadComplete()
    {
        foreach (var listener in ReloadListeners)
        {
            listener.LiveReloadCompleted();
        }
    }


    internal void UnloadModules()
    {

    }
}

internal abstract partial class RobustLoadContextBase
{
    protected readonly List<IReloadListenerEntryPoint> ReloadListeners = new();

    internal virtual void BroadcastReloadStart()
    {
        throw new NotSupportedException("Hot reloading is not supported in this load context!");
    }

    internal virtual void BroadcastReloadComplete()
    {
        throw new NotSupportedException("Hot reloading is not supported in this load context!");
    }

    partial void ReloadingShutdown()
    {
        ReloadListeners.Clear();
    }

    partial void SetupReloadListener(RobustEntryPoint entry)
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (entry is IReloadListenerEntryPoint reloadListener)
        {
            ReloadListeners.Add(reloadListener);
        }
    }
}
#endif
