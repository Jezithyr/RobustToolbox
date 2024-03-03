using System.Linq;

#if LIVE_RELOAD_ENABLED
namespace Robust.Shared.Modules;

internal sealed partial class ModuleInspector
{
    internal bool CanReload { get; init; }

    private void CheckIfReloadable()
    {

    }
}
#endif
