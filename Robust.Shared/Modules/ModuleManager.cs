using System.Runtime.Loader;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;

namespace Robust.Shared.Modules;

internal sealed class ModuleManager
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IReflectionManager _reflectionManager = default!;

    /// <summary>
    /// Assembly context for core engine and dependencies.
    /// When launching from content, content will also be present here!
    /// </summary>
    internal AssemblyLoadContext CoreContext => AssemblyLoadContext.Default;
    /// <summary>
    /// Assembly context for engine modules and dependencies that do not support live-reloading.
    /// </summary>
    internal AssemblyLoadContext EngineStaticContext { get; private set; } = default!;
    /// <summary>
    /// Assembly context for engine modules and dependencies that DO support live-reloading.
    /// </summary>
    internal AssemblyLoadContext EngineDynamicContext { get; private set; } = default!;
    /// <summary>
    /// Assembly context for content modules and dependencies that do not support live-reloading.
    /// </summary>
    internal RobustContentLoadContext ContentStaticContext { get; private set; } = default!;
    /// <summary>
    /// Assembly context for content modules and dependencies that DO support live-reloading.
    /// </summary>
    internal RobustContentLoadContext ContentDynamicContext { get; private set; } = default!;

    private bool _sandboxingEnabled;

    private ISawmill _sawmill = default!;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("robust.mod");
        ContentStaticContext = new RobustContentLoadContext("Content.Static");
        ContentDynamicContext = new RobustContentLoadContext("Content.Dynamic", true);
    }

    public void SetEnableSandboxing(bool sandboxing)
    {
        _sandboxingEnabled = sandboxing;
        _sawmill.Debug("{0} sandboxing", sandboxing ? "ENABLING" : "DISABLING");
    }

    public void LoadAssemblies()
    {

    }

    public void ShutDown()
    {
        EngineDynamicContext.Unload();
        ContentDynamicContext.Unload();
    }

}


// When the game is ran with the startup executable being content,
// we have to disable the separate load context.
// Otherwise the content assemblies will be loaded twice which causes *many* fun bugs.
