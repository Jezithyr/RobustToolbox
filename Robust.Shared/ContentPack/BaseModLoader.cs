using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;

namespace Robust.Shared.ContentPack
{
    internal abstract class BaseModLoader : IPostInjectInit
    {
        [Dependency] protected readonly IReflectionManager ReflectionManager = default!;
        [Dependency] protected readonly ILogManager LogManager = default!;
        [Dependency] private readonly IDependencyCollection _dependencies = default!;

        private readonly List<ModuleTestingCallbacks> _testingCallbacks = new();

        /// <summary>
        ///     Loaded assemblies.
        /// </summary>
        protected readonly List<ModInfo> Mods = new();

        protected ISawmill Sawmill { get; private set; } = default!;

        public IEnumerable<Assembly> LoadedModules => Mods.Select(p => p.GameAssembly);

        public Assembly GetAssembly(string name)
        {
            return Mods.Select(p => p.GameAssembly).Single(p => p.GetName().Name == name);
        }

        protected void InitMod(Assembly assembly)
        {
            var mod = new ModInfo(assembly);
            ReflectionManager.LoadAssemblies(mod.GameAssembly);
            var entryPoints = mod.GameAssembly.GetTypes().Where(t => typeof(SharedEntry).IsAssignableFrom(t));
            foreach (var entryPoint in entryPoints)
            {
                var entryPointInstance = (SharedEntry) Activator.CreateInstance(entryPoint)!;
                entryPointInstance.Dependencies = _dependencies;
                if (_testingCallbacks != null)
                {
                    entryPointInstance.SetTestingCallbacks(_testingCallbacks);
                }

                mod.EntryPoints.Add(entryPointInstance);
            }
            Mods.Add(mod);
        }

        public bool IsHotReloadable(Assembly typeAssembly)
        {
            foreach (var mod in Mods)
            {
                if (mod.GameAssembly == typeAssembly)
                {
                    return mod.SupportsReloading;
                }
            }
            return false;
        }

        public bool TryGetContentAssemblyType(Assembly typeAssembly,[NotNullWhen(true)] out ModAssemblyType? modType)
        {
            modType = null;
            foreach (var mod in Mods)
            {
                if (mod.GameAssembly == typeAssembly)
                {
                    modType = mod.ModType;
                    return true;
                }
            }
            return false;
        }

        public ModAssemblyType GetContentAssemblyType(Assembly contentAssembly)
        {
            foreach (var mod in Mods)
            {
                if (mod.GameAssembly == contentAssembly)
                {
                    return mod.ModType;
                }
            }
            throw new ArgumentException($"{contentAssembly} is not a Content Assembly!");
        }

        public bool IsContentAssembly(Assembly typeAssembly)
        {
            foreach (var mod in Mods)
            {
                if (mod.GameAssembly == typeAssembly)
                {
                    return true;
                }
            }

            return false;
        }

        public void BroadcastRunLevel(ModRunLevel level)
        {
            foreach (var mod in Mods)
            {
                foreach (var entry in mod.EntryPoints)
                {
                    switch (level)
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
                            Sawmill.Error($"Unknown RunLevel: {level}");
                            break;
                    }
                }
            }
        }

        public void BroadcastUpdate(ModUpdateLevel level, FrameEventArgs frameEventArgs)
        {
            foreach (var module in Mods)
            {
                foreach (var entryPoint in module.EntryPoints)
                {
                    entryPoint.Update(level, frameEventArgs);
                }
            }
        }

        public void SetModuleBaseCallbacks(ModuleTestingCallbacks testingCallbacks)
        {
            _testingCallbacks.Add(testingCallbacks);
        }

        public void Shutdown()
        {
            foreach (var module in Mods)
            {
                foreach (var entryPoint in module.EntryPoints)
                {
                    entryPoint.Shutdown();
                }

                foreach (var entryPoint in module.EntryPoints)
                {
                    entryPoint.Dispose();
                }
            }
        }

        void IPostInjectInit.PostInject()
        {
            Sawmill = LogManager.GetSawmill("res.mod");
        }

        /// <summary>
        ///     Holds info about a loaded assembly.
        /// </summary>
        protected sealed class ModInfo
        {
            public Assembly GameAssembly { get; }
            public List<SharedEntry> EntryPoints { get; } = new();
            public bool SupportsReloading { get; }
            public ModAssemblyType ModType { get; }
            public ModInfo(Assembly gameAssembly)
            {
                GameAssembly = gameAssembly;
                SupportsReloading = gameAssembly.GetCustomAttribute<HotReloadable>() != null;
                ModType = ModAssemblyType.Gameplay;
                if (gameAssembly.GetCustomAttribute<RobustMod>() is { } modAttribute)
                {
                    ModType = modAttribute.Type;
                }
            }
        }
    }
}
