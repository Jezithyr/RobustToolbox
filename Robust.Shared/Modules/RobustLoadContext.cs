using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Robust.Shared.Modules;



public sealed class RobustContentLoadContext(string? name = null, bool supportsLiveReload = false)
{
    [Dependency] private readonly IReflectionManager _reflectionManager = default!;
    [Dependency] private readonly IDependencyCollection _dependencies = default!;

    private Dictionary<Assembly, List<GameShared>> _entryPoints = new();
    private readonly List<ModuleTestingCallbacks> _testingCallbacks = new();

    private AssemblyLoadContext? _loadContext;
    private AssemblyLoadContext LoadContext
    {
        get => _loadContext ?? new AssemblyLoadContext(name, supportsLiveReload);
        set => _loadContext = value;
    }

    public Assembly LoadFromStream(Stream assembly, Stream? symbols)
    {
        return LoadContext.LoadFromStream(assembly, symbols);
    }

    public Assembly LoadFromAssemblyPath(string path)
    {
        var assembly = LoadContext.LoadFromAssemblyPath(path);
        InitModule(assembly);
        return assembly;
    }

    private void InitModule(Assembly assembly)
    {
        _reflectionManager.LoadAssemblies(assembly);
        var entryPointTypes = assembly.GetTypes().Where(t => typeof(GameShared).IsAssignableFrom(t));
        List<GameShared> entryPointInstances = new();
        foreach (var entryPoint in entryPointTypes)
        {
            var entryPointInstance = (GameShared) Activator.CreateInstance(entryPoint)!;
            entryPointInstance.Dependencies = _dependencies;
            if (_testingCallbacks != null)
            {
                entryPointInstance.SetTestingCallbacks(_testingCallbacks);
            }
            entryPointInstances.Add(entryPointInstance);
        }
        _entryPoints.Add(assembly, entryPointInstances);
    }

    public void Unload()
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
            if (livereload)
            {
                foreach (var entryPoint in entryPoints)
                {
                    entryPoint.PreUnload();
                }
            }

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
        _testingCallbacks.Clear();
    }

    public void Shutdown()
    {
        UnloadModules(false);
    }
};

public sealed class RobustEngineLoadContext
{
    //TODO: Setup engine entryPoints
};
