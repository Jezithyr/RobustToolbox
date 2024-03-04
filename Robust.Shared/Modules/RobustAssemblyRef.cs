using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Robust.Shared.Modules;

internal record RobustAssemblyRef : IDisposable
{
    private RobustAsmStream? _asmStreamHandle = null;
    [Dependency] private readonly ModuleManager _moduleManager = default!;
    internal Stream AsmStreamHandle
    {
        get
        {
            if (_asmStreamHandle == null)
                throw new Exception($"No assembly bytestream available for {AssemblyName}:{ModuleId}!");
            return _asmStreamHandle.AssemblyStream;
        }
    }

    internal Stream? PdbStreamHandle
    {
        get
        {
            if (_asmStreamHandle == null)
                throw new Exception($"No PDB/Debug Symbol bytestream available for {AssemblyName}:{ModuleId}!");
            return _asmStreamHandle.PdbStream;
        }
    }

    internal string AsmPath { get; private set; } = "";
    internal bool HasPdb { get; private set; }

    internal bool IsReflectionDataOnly { get; private set; }
    internal bool ReflectionDataAvailable => _assemblyData != null;

    private Assembly? _assemblyData;
    private MetadataLoadContext? _contextHandle;

    public Assembly Assembly
    {
        get
        {
            if (!TryGetAssemblyReflectionData(out var asm))
                throw new Exception($"Tried to get assembly data for {AssemblyName}:{ModuleId} which has been unloaded!");
            Validate(throwEx: true);
            return asm;
        }
    }

    internal string AssemblyName { get; private set; } = string.Empty;

    internal IReadOnlyList<string> ReferencedAssemblyNames => _referencedAssemblyNames;

    internal string ModuleId { get; private set; } = default!;
    internal bool SupportsReloading { get; private set; }
    internal bool IsVirtualAsm => AsmPath != "";
    internal bool IsContentAsm { get; private set; }
    internal bool IsSandboxed { get; private set; }
    private readonly Dictionary<string, AssemblyAttributeMeta> _assemblyAttributes = new();
    private ErrorFlag _errors = ErrorFlag.None;
    public ErrorFlag Errors => _errors;
    internal bool IsValid => _errors == ErrorFlag.None;
    private ISawmill _sawmill = default!;
    private readonly List<string> _referencedAssemblyNames = new();

    internal RobustAssemblyRef(Assembly assembly,
        string contentPrefix,
        bool forceSandbox = false)
    {
        Initialize(assembly, contentPrefix, forceSandbox, false);
    }

    internal RobustAssemblyRef(
        MetadataLoadContext loadContext,
        Assembly assembly,
        string contentPrefix,
        bool forceSandbox = false)
    {
        _contextHandle = loadContext;
        Initialize(assembly, contentPrefix, forceSandbox, true);
    }

    internal RobustAssemblyRef(
        MetadataLoadContext loadContext,
        string path,
        string contentPrefix,
        bool forceSandbox = false)
    {
        _contextHandle = loadContext;
        if (!TryLoadReflectionOnlyLoad(loadContext, path))
            return;
        Initialize(_assemblyData!, contentPrefix, forceSandbox, true);
    }

    internal RobustAssemblyRef(
        MetadataLoadContext loadContext,
        RobustAsmStream asmDataStream,
        string contentPrefix,
        bool forceSandbox = false)
    {
        _contextHandle = loadContext;
        if (!TryLoadReflectionOnlyLoad(loadContext, asmDataStream))
            return;
        Initialize(_assemblyData!, contentPrefix, forceSandbox, true);
    }


    #region AssemblyAttributeLogic

    private void CacheAssemblyAttributes(Assembly assembly)
    {
        foreach (var attrData in (List<CustomAttributeData>)assembly.GetCustomAttributesData())
        {
            if (attrData.AttributeType.FullName == null)
                continue;
            _assemblyAttributes.Add(attrData.AttributeType.FullName,new AssemblyAttributeMeta(attrData));
        }
    }

    internal bool HasAssemblyAttribute<T>() where T : Attribute
    {
        return typeof(T).FullName != null && _assemblyAttributes.ContainsKey(typeof(T).FullName!);
    }

    internal bool TryGetAssemblyAttributeMetadata<T>([NotNullWhen(true)]out AssemblyAttributeMeta? attributeMetadata) where T : Attribute
    {
        attributeMetadata = null;
        return typeof(T).FullName != null
               && _assemblyAttributes.TryGetValue(typeof(T).FullName!, out attributeMetadata);
    }

    internal bool TryGetAssemblyAttributeProperty<T1, T2>(string name, [NotNullWhen(true)] out T2? value)
        where T1 : Attribute
    {
        value = default;
        return TryGetAssemblyAttributeMetadata<T1>(out var metaData)
               && metaData.TryGetPropertyValue(name, out value);
    }

    internal bool TryGetAssemblyAttributeArrayProperty<T1, T2>(string name,
        [NotNullWhen(true)] out IReadOnlyList<T2>? values) where T1 : Attribute
    {
        values = default;
        return TryGetAssemblyAttributeMetadata<T1>(out var metaData)
               && metaData.TryGetPropertyArrayValue(name, out values);
    }

    #endregion

    #region AssemblyReflectionHandling
    /// <summary>
    /// Unloads the MetaDataLoadContext and invalidates the reflection data
    /// </summary>
    internal void UnloadReflectionData()
    {
        _assemblyData = null;
        AddError(ErrorFlag.AssemblyDataUnloaded);
    }

    /// <summary>
    /// Tries to get the assembly reflection data if it is available
    /// </summary>
    /// <param name="assembly">assembly reflection data</param>
    /// <param name="warnIfMissing">should we log a warning if the assembly data is missing/unloaded?</param>
    /// <returns>True if reflection data was successfully loaded</returns>
    private bool TryGetAssemblyReflectionData([NotNullWhen(true)] out Assembly? assembly, bool warnIfMissing = false)
    {
        assembly = _assemblyData;
        if (warnIfMissing && !ReflectionDataAvailable)
        {
            _sawmill.Warning($"Assembly data for {AssemblyName}:{ModuleId} is unloaded!");
        }
        return ReflectionDataAvailable;
    }

    private bool CheckAsmLoadArgs([NotNullWhen(true)] MetadataLoadContext? loadContext)
    {
        if (loadContext == null || !ValidateLoadContext(loadContext))
            return false;
        if (_assemblyData == null)
            return true;
        AddError(ErrorFlag.AssemblyAlreadyLoaded, true);
        return false;
    }

    private bool TryLoadReflectionOnlyLoad(MetadataLoadContext? loadContext, RobustAsmStream? asmStream)
    {
        if (!CheckAsmLoadArgs(loadContext) || asmStream == null)
            return false;
        _assemblyData = loadContext.LoadFromStream(asmStream.AssemblyStream);
        _contextHandle = loadContext;
        return true;
    }

    private bool TryLoadReflectionOnlyLoad(MetadataLoadContext? loadContext, string path)
    {
        if (!CheckAsmLoadArgs(loadContext))
            return false;
        _assemblyData = loadContext.LoadFromAssemblyPath(path);
        _contextHandle = loadContext;
        return true;
    }


    #endregion

    #region Validation

    private bool ValidateDataStream(RobustAsmStream? streamHandle)
    {
        if (streamHandle == null)
            return false;
        if (_assemblyData == null || streamHandle.AssemblyName == _assemblyData.GetName().Name)
            return true;
        //This should never ever happen and if it does we should throw because you really fucked up somewhere and shit is about to catch fire
        AddError(ErrorFlag.AssemblyStreamMismatch, true, true);
        return false;
    }

    private bool ValidateAssemblyData(Assembly? assembly, bool reflectionOnly)
    {
        if (assembly == null)
            return false;
        if (reflectionOnly != assembly.ReflectionOnly)
        {
            //This indicates that something just caused a sandbox violation so we want to pull the fire alarm here
            //(specifically: loading an assembly fully before running sandbox checks)
            AddError(ErrorFlag.InvalidAssemblyData, true, true);
            return false;
        }
        if (_contextHandle == null && reflectionOnly)
        {
            //If we are using a reflection-only Assembly we must unload BOTH the MetadataLoadContext AND any references to
            //its included assemblies otherwise we prevent the context from unloading until all references are removed
            AddError(ErrorFlag.AssemblyUnloadPinned, true);
            return false;
        }
        return true;
    }

    private bool ValidateLoadContext(MetadataLoadContext? loadContext)
    {
        if (loadContext == null)
            return false;
        if (_assemblyData != null && !loadContext.GetAssemblies().Contains(_assemblyData!))
        {
            //The currently loaded assembly does not match the load context, this should never happen
            AddError(ErrorFlag.InvalidLoadContext, true);
            //this mismatch also means that our assembly variable is pinning another context unload!
            if (_assemblyData.ReflectionOnly)
                AddError(ErrorFlag.AssemblyUnloadPinned, true);
            return false;
        }
        return true;
    }

    #endregion

    #region Setup

    private void CacheAssemblyData(Assembly assembly)
    {
        IsReflectionDataOnly = assembly.ReflectionOnly;
        AssemblyName = assembly.GetName().Name!;
        foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
        {
            _referencedAssemblyNames.Add(referencedAssemblyName.Name!);
        }
        CacheAssemblyAttributes(assembly);
    }

    private void SetupDataStreams(RobustAsmStream streamHandle)
    {
        _asmStreamHandle = streamHandle;
        HasPdb = streamHandle.HasPdb;
        ValidateDataStream(streamHandle);
    }

    private void Initialize(Assembly assembly, string contentPrefix, bool forceSandbox, bool reflectionOnly)
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _moduleManager.Sawmill;
        CacheAssemblyData(assembly);
        ValidateAssemblyData(assembly, reflectionOnly);
        if (!TryGetAssemblyAttributeMetadata<RobustModuleAttribute>(out var robustModAttrData))
        {
            //Fallback behavior uses the assembly name as the moduleId
            ModuleId = assembly.GetName().Name!;
            SupportsReloading = false;
            //determines if the module is content or engine based on the name prefix
            IsContentAsm = ModuleId.StartsWith(contentPrefix);
            return;
        }
        IsSandboxed = robustModAttrData.GetPropertyValue<bool>("IsSandboxed") | forceSandbox;
        if (HasAssemblyAttribute<SkipIfSandboxedAttribute>()) //Legacy attribute handling
        {
            _sawmill.Warning("Legacy SkipSandbox attribute found [SkipIfSandboxedAttribute]! " +
                            "This WILL override the IsSandboxed value in the RobustModuleAttribute ");
            IsSandboxed = false | forceSandbox;
        }
        IsContentAsm = robustModAttrData.GetPropertyValue<bool>("IsContentAssembly");
        SupportsReloading = robustModAttrData.GetPropertyValue<bool>("SupportsLiveReloading");
        ModuleId = robustModAttrData.GetPropertyValue<string>("Id") ?? assembly.GetName().Name!;
    }

    #endregion


    #region TearDown

    public void Dispose()
    {
        UnloadReflectionData();
        _asmStreamHandle?.Dispose();
    }

    #endregion


    #region ErrorTracking/Validation

    [Flags]
    internal enum ErrorFlag
    {
        None = 0,
        UnspecifiedError = 1,
        InvalidAssemblyData = 1 << 1,
        AssemblyDataUnloaded = 1 << 2,
        ModuleIdConflict = 1 << 3,
        SandboxingFailed = 1 << 4,
        AssemblyStreamMismatch = 1 << 5,
        AssemblyUnloadPinned = 1 << 6,
        InvalidLoadContext = 1 << 7,
        AssemblyAlreadyLoaded = 1 << 8
    }

    internal void AddError(ErrorFlag newFlag, bool logImmediately = false, bool throwImmediately = false)
    {
        if (newFlag == ErrorFlag.None)
            return;
        _errors |= newFlag;
        if (logImmediately || throwImmediately)
            CheckErrors(newFlag, logErrors:logImmediately, throwEx:throwImmediately);
    }

    private bool CheckIfErrorFlagSet(ErrorFlag errorsToCheck, string errorText, ErrorFlag skippedFlags,
        ErrorFlag errorVariable, bool shouldLog = true, bool throwEx = false)
    {
        if (!(skippedFlags == ErrorFlag.None || !skippedFlags.HasFlag(errorsToCheck))
            && errorVariable.HasFlag(errorsToCheck))
            return false;
        if (shouldLog)
            _sawmill.Error(errorText);
        if (throwEx)
            throw new Exception(errorText);
        return true;
    }

    private bool CheckErrors(ErrorFlag toCheck, ErrorFlag skippedFlags = ErrorFlag.None, bool logErrors = true,
        bool throwEx = false)
    {
        var errorFound = false;
        errorFound |= CheckIfErrorFlagSet(
            ErrorFlag.UnspecifiedError,
            $"Unspecified error with {AssemblyName}!",
            toCheck, skippedFlags, logErrors, throwEx);
        errorFound |= CheckIfErrorFlagSet(
            ErrorFlag.InvalidAssemblyData,
            $"Assembly stream data for asm {AssemblyName} is invalid or does not match!",
            skippedFlags, toCheck, logErrors, throwEx);
        errorFound |= CheckIfErrorFlagSet(
            ErrorFlag.ModuleIdConflict,
            $"Assembly stream data for asm {AssemblyName} is invalid or does not match!",
            skippedFlags, toCheck, logErrors, throwEx);

        return errorFound;
    }

    internal bool Validate(ErrorFlag skippedFlags = ErrorFlag.None, bool logErrors = true, bool throwEx = false)
    {
        return CheckErrors(_errors, skippedFlags, logErrors, throwEx);
    }

    #endregion
}
