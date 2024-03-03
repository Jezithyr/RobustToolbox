using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;

namespace Robust.Shared.Modules;

internal record RobustAssemblyRef : IDisposable
{
    private readonly RobustAsmStream _robustAsmStream;
    internal Stream AsmStream => _robustAsmStream.AssemblyStream;
    internal Stream? PdbStream => _robustAsmStream.PdbStream;
    internal string? AsmPath { get; init; }
    internal bool HasPdb => _robustAsmStream.HasPdb;
    internal bool AssemblyHasPath => AsmPath != null;

    internal string AssemblyName { get; init; }
    internal List<string> ReferencedAssemblyNames { get; } = new();
    internal string ModuleId { get; init; }
    internal bool SupportsReloading { get; init; }
    internal bool IsVirtualAsm => AsmPath == null;
    internal bool IsContentAsm { get; init; }
    internal bool IsSandboxed { get; init; }
    internal IReadOnlyDictionary<Type,Attribute> AsmAttributes { get; }
    private ErrorFlag _errors = ErrorFlag.None;
    public ErrorFlag Errors => _errors;
    internal bool IsValid => _errors == ErrorFlag.None;
    private ISawmill _sawmill;

    internal RobustAssemblyRef(ISawmill sawmill,
        Assembly assembly,
        string contentPrefix,
        Stream asmSteam,
        Stream? pdbStream,
        string? asmPath = null)
        : this(sawmill, assembly, contentPrefix, new RobustAsmStream(asmSteam, pdbStream), asmPath)
    {
    }

    internal RobustAssemblyRef(ISawmill sawmill,
        Assembly assembly,
        string contentPrefix,
        RobustAsmStream robustAsmStream,
        string? asmPath = null)
    {
        AssemblyName = assembly.GetName().Name!;
        _robustAsmStream = robustAsmStream;
        AsmPath = asmPath;
        _sawmill = sawmill;

        if (!robustAsmStream.IsValid || robustAsmStream.AssemblyName != assembly.GetName().Name)
            _errors |= ErrorFlag.InvalidAssemblyData;

        foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
        {
            ReferencedAssemblyNames.Add(referencedAssemblyName.Name!);
        }
        AsmAttributes = ReadAssemblyAttributes((List<CustomAttributeData>)assembly.GetCustomAttributesData());
        //Legacy attribute handling
        var foundLegacySandboxAttr = AsmAttributes.ContainsKey(typeof(SkipIfSandboxedAttribute));
        IsSandboxed = !foundLegacySandboxAttr;

        if (!AsmAttributes.TryGetValue(typeof(RobustModuleAttribute), out var attribute) || attribute is not RobustModuleAttribute robustModuleAttr)
        {
            //Fallback behavior uses the assembly name as the moduleId
            ModuleId = assembly.GetName().Name!;
            SupportsReloading = false;
            //determines if the module is content or engine based on the name prefix
            IsContentAsm = ModuleId.StartsWith(contentPrefix);
            return;
        }

        IsSandboxed = robustModuleAttr.IsSandboxed;
        if (foundLegacySandboxAttr) //Legacy attribute handling
        {
            sawmill.Warning("Legacy SkipSandbox attribute found [SkipIfSandboxedAttribute]! " +
                            "This WILL override the IsSandboxed value in the RobustModuleAttribute");
            IsSandboxed = false;
        }

        IsContentAsm = robustModuleAttr.IsContentAssembly;
        SupportsReloading = robustModuleAttr.SupportsLiveReloading;
        ModuleId = robustModuleAttr.Id ?? assembly.GetName().Name!;
        PrintErrors();
    }

    private IReadOnlyDictionary<Type, Attribute> ReadAssemblyAttributes(IReadOnlyList<CustomAttributeData> attrsData)
    {
        Dictionary<Type, Attribute> temp = new();
        foreach (var attrData in attrsData)
        {
            //Note: this equality check will fail if you are checking for attributes that are not defined in engine core!
            var attribute = (Attribute?) Activator.CreateInstance(attrData.AttributeType, attrData.NamedArguments);
            if (attribute != null)
                temp.Add(attrData.AttributeType, attribute);
        }
        return temp;
    }

    internal void AddError(ErrorFlag newFlag)
    {
        _errors |= ErrorFlag.InvalidAssemblyData;
    }

    internal void PrintErrors()
    {
        if (_errors == ErrorFlag.None)
            return;
        if (_errors.HasFlag(ErrorFlag.InvalidAssemblyData))
        {
            _sawmill.Error($"Assembly stream data for asm {AssemblyName} is invalid or does not match!");
        }
    }
    public void Dispose()
    {
        _robustAsmStream.Dispose();
    }

    [Flags]
    internal enum ErrorFlag
    {
        None = 0,
        UnspecifiedError = 1,
        InvalidAssemblyData = 1 << 1,
        ModuleIdConflict = 1 << 2,
        SandboxViolation = 1 << 3,
    }
}
