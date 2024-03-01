using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Robust.Shared.Modules;

internal sealed class ModuleInspector : IDisposable, IPostInjectInit
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IResourceManagerInternal _res = default!;

    private ISawmill _sawmill = default!;
    private bool _checkSandbox;
    private ModuleManager _moduleManager;
    private ResPath _path;

    private List<Attribute> _assemblyAttributes = new();
    private string _assemblyName = default!;


    internal ModuleInspector(ModuleManager parent, bool checkSandbox, ResPath path)
    {
        _moduleManager = parent;
        _checkSandbox = checkSandbox;
        _path = path;
    }

    private (string[] refs, string name)? GetAssemblyReferenceData(Stream stream)
    {
        using var reader = ModLoader.MakePEReader(stream);
        var metaReader = reader.GetMetadataReader();

        var name = metaReader.GetString(metaReader.GetAssemblyDefinition().Name);

        // Try to find SkipIfSandboxedAttribute.

        if (_checkSandbox && TryFindSkipIfSandboxed(metaReader))
        {
            _sawmill.Debug("Module {ModuleName} has SkipIfSandboxedAttribute, ignoring.", name);
            return null;
        }

        return (metaReader.AssemblyReferences
            .Select(a => metaReader.GetAssemblyReference(a))
            .Select(a => metaReader.GetString(a.Name)).ToArray(), name);
    }

    public void Dispose()
    {

    }

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill($"robust.mod.loader");
    }
}
