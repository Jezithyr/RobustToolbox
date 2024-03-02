using System.Collections.Generic;
using Robust.Shared.ContentPack;

namespace Robust.Shared.Modules;

internal sealed partial class ModuleManager
{

    internal void BroadcastContentRunLevel(ModRunLevel level)
    {
        foreach (var (_, entryPoints) in ContentStaticContext.EntryPoints)
        {
            BroadcastRunLevelToContentEntries(entryPoints, level, false);
        }
        foreach (var (_, entryPoints) in ContentStaticContext.EntryPoints)
        {
            BroadcastRunLevelToContentEntries(entryPoints, level, true);
        }
    }

    internal void BroadcastContentShutDown()
    {
        foreach (var (_, entryPoints) in ContentStaticContext.EntryPoints)
        {
            BroadcastShutdownToContentEntries(entryPoints);
        }
        foreach (var (_, entryPoints) in ContentDynamicContext.EntryPoints)
        {
            BroadcastShutdownToContentEntries(entryPoints);
        }
    }

    private void BroadcastRunLevelToContentEntries(List<GameShared> entries, ModRunLevel runLevel, bool supportsReloading)
    {
        var isReloading = supportsReloading && MidReload;
        foreach (var entry in entries)
        {
            switch (runLevel)
            {
                case ModRunLevel.PreInit:
                    entry.PreInit(); //LEGACY
                    entry.PreInit(isReloading);
                    break;
                case ModRunLevel.Init:
                    entry.Init(); //LEGACY
                    entry.Init(isReloading);
                    break;
                case ModRunLevel.PostInit:
                    entry.PostInit(); //LEGACY
                    entry.PostInit(isReloading);
                    break;
                default:
                    _sawmill.Error($"Unknown RunLevel: {runLevel}");
                    break;
            }
        }
    }

    private void BroadcastShutdownToContentEntries(List<GameShared> entries)
    {
        foreach (var entry in entries)
        {
            entry.Shutdown(); //LEGACY
            entry.Shutdown(false);
        }
    }

    private void BroadcastContentReloadFinished()
    {
        foreach (var (_, entryPoints) in ContentDynamicContext.EntryPoints)
        {
            foreach (var entry in entryPoints)
            {
                entry.LiveReloadComplete();
            }
        }
    }
}
