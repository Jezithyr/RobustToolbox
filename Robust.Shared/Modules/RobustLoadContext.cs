using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Robust.Shared.Modules;



internal sealed class RobustContentLoadContext(string? name = null, bool supportsLiveReload = false) : RobustLoadContext<GameShared>
{
};

public sealed class RobustEngineLoadContext
{
    //TODO: Setup engine entryPoints
};


internal abstract class RobustLoadContext<T>(string? name = null, bool supportsLiveReload = false) where T: RobustEntryPoint
{
    [Dependency] protected readonly IReflectionManager ReflectionManager = default!;
    [Dependency] protected readonly IDependencyCollection Dependencies = default!;

    internal string? Name { get; init; } = name;
    internal bool SupportsReloading { get; init; } = supportsLiveReload;

    private Dictionary<Assembly, List<T>> _entryPoints = new();
    internal IReadOnlyDictionary<Assembly, List<T>> EntryPoints => _entryPoints;
    protected readonly List<ModuleTestingCallbacks> TestingCallbacks = new();

    private ConcurrentQueue<RobustAssemblyReference> _assembliesToLoad = new();

    protected AssemblyLoadContext? _loadContext;
    protected AssemblyLoadContext LoadContext
    {
        get => _loadContext ?? new AssemblyLoadContext(Name, SupportsReloading);
        set => _loadContext = value;
    }


    internal void QueueAssemblyLoad(RobustAssemblyReference asmRef)
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


    internal Assembly LoadFromAssemblyRef(RobustAssemblyReference asmRef)
    {
        var assembly = asmRef.IsVirtualAsm
            ? LoadFromStream(asmRef.AsmStream, asmRef.PdbStream)
            : LoadFromAssemblyPath(asmRef.AsmPath!);
        InitModule(assembly);
        return assembly;
    }


    internal Assembly LoadFromStream(Stream assemblyStream, Stream? symbolStream)
    {
        var assembly = LoadContext.LoadFromStream(assemblyStream, symbolStream);
        InitModule(assembly);
        return assembly;
    }

    internal Assembly LoadFromAssemblyPath(string path)
    {
        var assembly = LoadContext.LoadFromAssemblyPath(path);
        InitModule(assembly);
        return assembly;
    }

    private void InitModule(Assembly assembly)
    {
        ReflectionManager.LoadAssemblies(assembly);
        var entryPointTypes = assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t));
        List<T> entryPointInstances = new();
        foreach (var entryPoint in entryPointTypes)
        {
            var entryPointInstance = (T) Activator.CreateInstance(entryPoint)!;
            entryPointInstance.Dependencies = Dependencies;
            if (TestingCallbacks != null)
            {
                entryPointInstance.SetTestingCallbacks(TestingCallbacks);
            }
            entryPointInstances.Add(entryPointInstance);
        }
        _entryPoints.Add(assembly, entryPointInstances);
    }

    internal void Unload()
    {
        if (_loadContext == null)
            return;
        if (!supportsLiveReload)
        {
            throw new InvalidOperationException($"UnloadAssemblies called on RobustLoadContext: {LoadContext.Name} that does not support live-reloading!");
        }
        UnloadModules(true);
        LoadContext.Unload();
        _loadContext = null;
    }

    private void UnloadModules(bool livereload)
    {
        foreach (var (_,entryPoints) in _entryPoints)
        {
            foreach (var entryPoint in entryPoints)
            {
                entryPoint.Shutdown(livereload);
                entryPoint.Shutdown();
            }

            foreach (var entryPoint in entryPoints)
            {
                entryPoint.Dispose();
            }
        }
        _entryPoints.Clear();
        TestingCallbacks.Clear();
    }

    public void Shutdown()
    {
        UnloadModules(false);
    }
}
