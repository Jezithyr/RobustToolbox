using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;

namespace Robust.Shared.Modules;


internal sealed class RobustLoadContext : RobustLoadContextBase;

internal abstract partial class RobustLoadContextBase
{
    [Dependency] protected readonly IReflectionManager ReflectionManager = default!;
    [Dependency] protected readonly IDependencyCollection Deps = default!;
    [Dependency] protected readonly ILogManager LogManager = default!;
    protected ISawmill Sawmill;
    internal string? GlobalName { get; init; }
    protected const string LoadContextEnginePrefix = "Robust.";
    protected const string LoadContextContentPrefix = "Content.";
    internal virtual bool SupportsReloading => false;

    private readonly Dictionary<Assembly, List<RobustEntryPoint>> _entryPoints = new();
    internal IReadOnlyDictionary<Assembly, List<RobustEntryPoint>> EntryPoints => _entryPoints;
    protected readonly List<ModuleTestingCallbacks> TestingCallbacks = new();

    internal readonly ConcurrentDictionary<string, RobustAssemblyRef> LoadedAssemblies = new();
    internal readonly ConcurrentDictionary<string, RobustAssemblyRef> PendingAssemblies = new();

    private ConcurrentQueue<RobustAssemblyRef> _assembliesToLoad = new();
    private AssemblyLoadContext? _contentLoadContext;
    private AssemblyLoadContext? _engineLoadContext;
    protected RobustLoadContextBase(string? globalName = null)
    {
        IoCManager.InjectDependencies(this);
        GlobalName = globalName;
        Sawmill = LogManager.GetSawmill("robust.mod");
    }

    protected AssemblyLoadContext ContentLoadContext
    {
        get
        {
            if (_contentLoadContext != null)
                return _contentLoadContext;
            _contentLoadContext = CreateLoadContext(true);
            return _contentLoadContext;

        }
        set => _contentLoadContext = value;
    }
    protected AssemblyLoadContext EngineLoadContext
    {
        get
        {
            if (_engineLoadContext != null)
                return _engineLoadContext;
            _engineLoadContext = CreateLoadContext(false);
            return _engineLoadContext;

        }
        set => _engineLoadContext = value;
    }

    private AssemblyLoadContext CreateLoadContext(bool isContent)
    {
        string prefix = isContent ? LoadContextContentPrefix : LoadContextEnginePrefix;
        return CreateLoadContextWithName($"{prefix}{GlobalName}");
    }

    protected virtual AssemblyLoadContext CreateLoadContextWithName(string name)
    {
        return new AssemblyLoadContext(name);
    }


    internal void QueueAssemblyLoad(RobustAssemblyRef asmRef)
    {
        _assembliesToLoad.Enqueue(asmRef);
    }

    internal void LoadQueuedAssemblies()
    {
        foreach (var asmRef in _assembliesToLoad)
        {
            LoadFromAssemblyRef(asmRef);
        }
    }


    internal Assembly LoadFromAssemblyRef(RobustAssemblyRef asmRef)
    {
        var assembly = asmRef.IsVirtualAsm
            ? LoadFromStream(asmRef.AsmStreamHandle, asmRef.PdbStreamHandle)
            : LoadFromAssemblyPath(asmRef.AsmPath!);
        InitModule(assembly);
        return assembly;
    }


    internal Assembly LoadFromStream(Stream assemblyStream, Stream? symbolStream)
    {
        var assembly = ContentLoadContext.LoadFromStream(assemblyStream, symbolStream);
        InitModule(assembly);
        return assembly;
    }

    internal Assembly LoadFromAssemblyPath(string path)
    {
        var assembly = ContentLoadContext.LoadFromAssemblyPath(path);
        InitModule(assembly);
        return assembly;
    }

    private List<RobustEntryPoint> CreateEntryPoints(Assembly assembly)
    {
        ReflectionManager.LoadAssemblies(assembly);
        var entryPointTypes = assembly.GetTypes().Where(t => typeof(RobustEntryPoint).IsAssignableFrom(t));
        List<RobustEntryPoint> entryPointInstances = new();

        foreach (var entryPoint in entryPointTypes)
        {
            if (!TryCreateEntryPoint(entryPoint, out var entry))
                continue;
            entryPointInstances.Add(entry);
            entry.Dependencies = Deps;
            entry.SetTestingCallbacks(TestingCallbacks);
            SetupReloadListener(entry);
        }
        return entryPointInstances;
    }

    protected virtual bool TryCreateEntryPoint(Type entryPointType, [NotNullWhen(true)] out RobustEntryPoint? entryPointInstance)
    {
        entryPointInstance = Activator.CreateInstance(entryPointType) as RobustEntryPoint;
        if (entryPointInstance == null)
            return false;
        return true;
    }

    private void InitModule(Assembly assembly)
    {
        _entryPoints.Add(assembly, CreateEntryPoints(assembly));
    }

    public void BroadcastRunLevel(ModRunLevel runLevel)
    {
        foreach (var (_, entryPoints) in EntryPoints)
        {
            foreach (var entry in entryPoints)
            {
                switch (runLevel)
                {
                    case ModRunLevel.PreInit:
                        entry.PreInit();
                        break;
                    case ModRunLevel.Init:
                        entry.Init();
                        break;
                    case ModRunLevel.PostInit:
                        entry.PostInit();
                        break;
                    default:
                        Sawmill.Error($"Unknown RunLevel: {runLevel}");
                        break;
                }
            }
        }
    }

    protected void ShutdownModules()
    {
        foreach (var (_,entryPoints) in _entryPoints)
        {
            foreach (var entryPoint in entryPoints)
            {
                entryPoint.Shutdown();
            }

            foreach (var entryPoint in entryPoints)
            {
                entryPoint.Dispose();
            }
        }
        _entryPoints.Clear();
        ReloadingShutdown();
        TestingCallbacks.Clear();
    }
    public void Shutdown()
    {
        ShutdownModules();
    }
    partial void ReloadingShutdown();
    partial void SetupReloadListener(RobustEntryPoint entry);
}
