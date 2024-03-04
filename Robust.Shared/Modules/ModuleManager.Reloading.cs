#if LIVE_RELOAD_ENABLED
using System.Collections.Generic;
using Robust.Shared.ContentPack;

namespace Robust.Shared.Modules;

internal sealed partial class ModuleManager
{
    /// <summary>
    /// Assembly context for modules and dependencies that DO support live-reloading.
    /// </summary>
    internal ReloadableRobustLoadContext DynamicContext { get; private set; } = default!;
    internal bool MidReload { get; private set; } = false;

    partial void InitializeReloadData()
    {
        DynamicContext = new();
    }


    partial void LiveReload_Implementation()
    {
        if (MidReload)
            Sawmill.Warning("Cannot start a live-reload while one is already in progress!");
        MidReload = true;
        //TODO Reloading stuff goes here
        FinishLiveReload(); //this will end up getting called by an event.
    }

    public void StartLiveReload()
    {
        LiveReload_Implementation();
    }

    partial void FinishLiveReload()
    {
        if (!MidReload)
            return;
        MidReload = false;
    }

}
#endif
