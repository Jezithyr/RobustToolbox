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

internal sealed partial class ModuleInspector : IDisposable
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ModuleManager _moduleManager = default!;

    private readonly ISawmill _sawmill;
    private bool _checkSandbox;
    internal  RobustAssemblyRef AsmRef { get; }
    internal bool InvalidAssembly => AsmRef.IsValid;

    internal delegate void InspectorFinalizedCallback();
    private readonly InspectorFinalizedCallback? _onFinalize;

    internal ModuleInspector(string contentPrefix,
        bool checkSandbox,
        Stream asmStream,
        Stream? pdbStream = null,
        string? path = null,
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
        AsmRef = new RobustAssemblyRef(_sawmill, assemblyData,
            contentPrefix, asmStream ,pdbStream, path);

        bool coreAsmConflict = false;
        //check to make sure we aren't trying to load an assembly that will conflict with core engine!
        if (_moduleManager.CoreContext.Assemblies.Any(p => p.GetName().Name! == AsmRef.AssemblyName))
        {
            _sawmill.Error($"Assembly: {AsmRef.AssemblyName} with ID: {AsmRef.ModuleId} conflicts with a core engine assembly name!");
            AsmRef.AddError(RobustAssemblyRef.ErrorFlag.ModuleIdConflict);
        }

        CheckAssemblyNameConflicts(_moduleManager.StaticContext);

#if LIVE_RELOAD_ENABLED
        CheckAssemblyNameConflicts(_moduleManager.DynamicContext);
#endif
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

    private void CheckAssemblyNameConflicts(RobustLoadContextBase loadContext)
    {
        if (InvalidAssembly
            || AsmRef.Errors.HasFlag(RobustAssemblyRef.ErrorFlag.ModuleIdConflict)
            || loadContext.LoadedAssemblies.All(p => p.Value.AssemblyName == AsmRef.AssemblyName)
            || loadContext.PendingAssemblies.All(p => p.Value.AssemblyName == AsmRef.AssemblyName))
            return;
        _sawmill.Error($"Assembly: {AsmRef.AssemblyName} is already loaded/pending load under id: {AsmRef.ModuleId}");
        AsmRef.AddError(RobustAssemblyRef.ErrorFlag.ModuleIdConflict);
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
