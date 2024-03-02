using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;

namespace Robust.Shared.Modules;

internal sealed partial class ModuleManager
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IReflectionManager _reflectionManager = default!;
    [Dependency] private readonly IResourceManagerInternal _resourceManager = default!;

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
    internal string ContentPrefix { get; private set; } = string.Empty;

    internal bool MidReload { get; private set; } = false;
    private bool _sandboxingEnabled;
    private ISawmill _sawmill = default!;
    internal int InspectorCount { get; private set; }

    internal readonly ConcurrentDictionary<string, RobustAssemblyReference> StaticLoadedAsmRefs = new();
    internal readonly ConcurrentDictionary<string, RobustAssemblyReference> DynamicLoadedAsmRefs = new();
    internal readonly ConcurrentDictionary<string, RobustAssemblyReference> StaticPendingAsmRefs = new();
    internal readonly ConcurrentDictionary<string, RobustAssemblyReference> DynamicPendingAsmRefs = new();
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


    private ModuleInspector CreateNewInspector(ResPath asmPath)
    {
        InspectorCount++;
        using var asmFile = _resourceManager.ContentFileRead(asmPath);
        _resourceManager.TryContentFileRead(asmPath.WithExtension("pdb").CanonPath, out var pdbFile);
        return new ModuleInspector(ContentPrefix, _sandboxingEnabled, asmFile, pdbFile, asmPath.CanonPath, InspectorFinalized);
    }

    private ModuleInspector CreateNewInspector(Stream asmStream, Stream? pdbStream)
    {
        InspectorCount++;
        return new ModuleInspector(ContentPrefix, _sandboxingEnabled,asmStream, pdbStream, onFinalize: InspectorFinalized);
    }

    public void StartLiveReload()
    {
        if (MidReload)
            _sawmill.Warning("Cannot start a live-reload while one is already in progress!");
        MidReload = true;
        //TODO Reloading stuff goes here
        FinishLiveReload(); //this will end up getting called by an event.
    }

    private void FinishLiveReload()
    {
        MidReload = false;
        BroadcastContentReloadFinished();
    }

    private void InspectorFinalized()
    {
        InspectorCount--;
        if (InspectorCount < 0)
        {
            InspectorCount = 0;
        }
        if (InspectorCount == 0 && MidReload)
        {
            FinishLiveReload();
        }
    }

    public void ShutDown()
    {
    }

}


// When the game is ran with the startup executable being content,
// we have to disable the separate load context.
// Otherwise the content assemblies will be loaded twice which causes *many* fun bugs.
