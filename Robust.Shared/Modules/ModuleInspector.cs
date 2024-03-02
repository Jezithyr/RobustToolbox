using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;
namespace Robust.Shared.Modules;

internal sealed class ModuleInspector : IDisposable
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ModuleManager _moduleManager = default!;

    private ISawmill _sawmill = default!;
    private bool _checkSandbox;
    internal  RobustAssemblyReference AsmRef { get; }
    internal bool InvalidAssembly { get; private set; }

    internal delegate void InspectorFinalizedCallback();
    private readonly InspectorFinalizedCallback? _onFinalize;

    internal ModuleInspector(string contentPrefix, bool checkSandbox, Stream asmStream, Stream? pdbStream = null, string? path = null,
        InspectorFinalizedCallback? onFinalize = null)
    {
        IoCManager.InjectDependencies(this);

        _sawmill = _logManager.GetSawmill($"robust.mod.loader");
        _checkSandbox = checkSandbox;
        _onFinalize = onFinalize;
        //Set our stream readers and make sure they are at the origin
        asmStream.Position = 0;
        if (pdbStream != null)
            pdbStream.Position = 0;

        Assembly assemblyData;
        MetadataLoadContext metaContext;
        if (path != null)
        {
            metaContext = CreateInspectionContext(path);
            assemblyData = metaContext.LoadFromAssemblyPath(path);
        }
        else
        {
            using var tempStream = new MemoryStream(); //clone our stream because Idk if it gets closed by the metadata context
            metaContext = CreateInspectionContext();
            asmStream.CopyTo(tempStream);
            asmStream.Position = 0;
            assemblyData = metaContext.LoadFromStream(tempStream);
        }
        //create an assembly reference class that stores all the assembly metadata we actually care about, so we can discard the rest!
        AsmRef = new RobustAssemblyReference(_sawmill, assemblyData, asmStream,
            contentPrefix, pdbStream, path);

        bool coreAsmConflict = false;
        //check to make sure we aren't trying to load an assembly that will conflict with core engine!
        if (_moduleManager.CoreContext.Assemblies.Any(p => p.GetName().Name! == AsmRef.ModuleId))
        {
            _sawmill.Error($"Assembly: {AsmRef.AssemblyName} with ID: {AsmRef.ModuleId} conflicts with a core engine assembly name!");
            coreAsmConflict = true;
            InvalidAssembly = true;
        }
        if (AsmRef.SupportsReloading)
        {
            InvalidAssembly |= _moduleManager.DynamicPendingAsmRefs.TryAdd(AsmRef.ModuleId, AsmRef)
                              || _moduleManager.DynamicLoadedAsmRefs.ContainsKey(AsmRef.ModuleId);
        }
        else
        {
            InvalidAssembly |= _moduleManager.StaticPendingAsmRefs.TryAdd(AsmRef.ModuleId, AsmRef)
                              || _moduleManager.StaticLoadedAsmRefs.ContainsKey(AsmRef.ModuleId);
        }

        if (InvalidAssembly && !coreAsmConflict)
        {
            _sawmill.Error($"Assembly: {AsmRef.AssemblyName} is already loaded/pending load under id: {AsmRef.ModuleId}");
        }
        //clean up the metaContext when we are done and unload the assembly reflection data
        metaContext.Dispose();
    }

    private MetadataLoadContext CreateInspectionContext(string? path = null)
    {
        var paths = new List<string>();
        if (path != null)
            paths.Add(path);
        foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
        {
            paths.Add(assembly.Location);
        }
        return new MetadataLoadContext(new PathAssemblyResolver(paths));
    }

    internal bool Inspect()
    {
        return true;
    }

    internal bool TryGetAssemblyAttribute<T>([NotNullWhen(true)] out T? attribute) where T:Attribute, new()
    {
        attribute = null;
        AsmRef.AsmAttributes.TryGetValue(typeof(T), out var atr);
        attribute = (T?) atr;
        return attribute != null;
    }

    public void Dispose()
    {
        _onFinalize?.Invoke();
    }



    internal static PEReader MakePEReader(Stream stream, bool leaveOpen=false, PEStreamOptions options=PEStreamOptions.Default)
    {
        if (!stream.CanSeek)
            stream = leaveOpen ? stream.CopyToMemoryStream() : stream.ConsumeToMemoryStream();

        if (leaveOpen)
            options |= PEStreamOptions.LeaveOpen;

        return new PEReader(stream, options);
    }
}
