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

    private ISawmill _sawmill = default!;
    private bool _checkSandbox;
    internal RobustAssemblyRef AsmRef { get; private set; } = default!;
    internal bool InvalidAssembly => AsmRef.IsValid;

    internal delegate void InspectorFinalizedCallback();
    private InspectorFinalizedCallback? _onFinalize;

    internal ModuleInspector(
        string contentPrefix,
        bool checkSandbox,
        bool forceSandbox,
        RobustAsmStream asmDataStream,
        InspectorFinalizedCallback? onFinalize = null)
    {
        Initialize(
            checkSandbox,
            forceSandbox,
            new RobustAssemblyRef(CreateInspectionContext(asmDataStream.Path), asmDataStream, contentPrefix, forceSandbox),
            onFinalize);
    }

    internal ModuleInspector(
        string contentPrefix,
        bool checkSandbox,
        bool forceSandbox,
        string path,
        InspectorFinalizedCallback? onFinalize = null)
    {
        Initialize(
            checkSandbox,
            forceSandbox,
            new RobustAssemblyRef(CreateInspectionContext(path), path, contentPrefix, forceSandbox),
            onFinalize);
    }

    internal ModuleInspector(
        RobustAssemblyRef assemblyRef,
        bool checkSandbox,
        bool forceSandbox,
        InspectorFinalizedCallback? onFinalize = null)
    {
        Initialize(
            checkSandbox,
            forceSandbox,
            assemblyRef,
            onFinalize);
    }

    private void Initialize(bool checkSandbox, bool forceSandbox, RobustAssemblyRef asmRef, InspectorFinalizedCallback? onFinalize)
    {
        IoCManager.InjectDependencies(this);

        _sawmill = _logManager.GetSawmill($"robust.mod.loader");
        _checkSandbox = checkSandbox;
        _onFinalize = onFinalize;
        AsmRef = asmRef;

        CheckAssemblyNameConflicts(_moduleManager.StaticContext);
        LiveReloadInitialization();
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

    internal bool TryGetAssemblyAttributeProperty<T1,T2>(string propertyName,[NotNullWhen(true)] out T2? value) where T1:Attribute, new()
    {
        value = default;
        return AsmRef.TryGetAssemblyAttributeProperty<T1,T2>(propertyName, out value);
    }

    internal bool TryGetAssemblyAttributeArrayProperty<T1,T2>(string propertyName,[NotNullWhen(true)] out IReadOnlyList<T2>? value) where T1:Attribute, new()
    {
        value = null;
        return AsmRef.TryGetAssemblyAttributeArrayProperty<T1,T2>(propertyName, out value);
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

    partial void LiveReloadInitialization();
}
