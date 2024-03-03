using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
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
    /// When launching from content, content stub will also be present here!
    /// </summary>
    internal AssemblyLoadContext CoreContext => AssemblyLoadContext.Default;
    /// <summary>
    /// Assembly context for additional modules and dependencies that do not support live-reloading.
    /// </summary>
    internal RobustLoadContext StaticContext { get; private set; } = default!;

    internal string ContentPrefix { get; private set; } = string.Empty;
    internal string EnginePrefix { get; private set; } = string.Empty;
    private bool _sandboxingEnabled;

    private ISawmill _sawmill = default!;

    private int _inspectorCount;
    internal int InspectorCount => _inspectorCount;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("robust.mod");
        StaticContext = new();
        InitializeReloadData();
    }

    public void SetPrefixes(string contentPrefix, string enginePrefix)
    {
        ContentPrefix = contentPrefix;
        EnginePrefix = enginePrefix;
    }

    public void SetEnableSandboxing(bool sandboxing)
    {
        _sandboxingEnabled = sandboxing;
        _sawmill.Debug("{0} sandboxing", sandboxing ? "ENABLING" : "DISABLING");
    }


    private ModuleInspector CreateNewInspector(ResPath asmPath)
    {
        Interlocked.Increment(ref _inspectorCount);
        using var asmFile = _resourceManager.ContentFileRead(asmPath);
        _resourceManager.TryContentFileRead(asmPath.WithExtension("pdb").CanonPath, out var pdbFile);
        return new ModuleInspector(ContentPrefix, _sandboxingEnabled, asmFile, pdbFile, asmPath.CanonPath, InspectorClosed);
    }

    private ModuleInspector CreateNewInspector(Stream asmStream, Stream? pdbStream)
    {
        Interlocked.Increment(ref _inspectorCount);
        return new ModuleInspector(ContentPrefix, _sandboxingEnabled, asmStream, pdbStream, onFinalize: InspectorClosed);
    }

    private void InspectorClosed()
    {
        var newCount = Interlocked.Decrement(ref _inspectorCount);
        if (newCount < 0)
        {
            newCount = 0;
        }

        if (newCount == 0)
            LastInspectorClosed();
    }

    private void LastInspectorClosed()
    {
        //TODO: track inspectors and their active threads.
            FinishLiveReload();
    }

    public void ShutDown()
    {
        //TODO: Terminate running inspectors
    }

    partial void InitializeReloadData();
    partial void FinishLiveReload();
    partial void LiveReload_Implementation();
}


// When the game is ran with the startup executable being content,
// we have to disable the separate load context.
// Otherwise the content assemblies will be loaded twice which causes *many* fun bugs.
