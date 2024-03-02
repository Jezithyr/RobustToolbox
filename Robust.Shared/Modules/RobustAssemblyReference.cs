using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;

namespace Robust.Shared.Modules;

internal record RobustAssemblyReference : IDisposable
{
    private readonly Stream _asmStream = default!;
    private readonly Stream? _pdbStream;
    internal string? AsmPath { get; init; }
    internal Stream AsmStream
    {
        get => _asmStream;
        private init
        {
            _asmStream  = new MemoryStream(); //clone our stream because Idk if it gets closed by the metadata context
            value.CopyTo(_asmStream);
            value.Position = 0;
            _asmStream.Position = 0;
        }
    }
    internal Stream? PdbStream
    {
        get => _pdbStream;
        private init
        {
            if (value == null)
            {
                _pdbStream = null;
                return;
            }
            _pdbStream  = new MemoryStream(); //clone our stream because Idk if it gets closed by the metadata context
            value.CopyTo(_pdbStream);
            value.Position = 0;
            _pdbStream.Position = 0;
        }
    }
    internal bool HasPdb => _pdbStream != null;
    internal bool AssemblyHasPath => AsmPath != null;

    internal string AssemblyName { get; init; }
    internal List<string> ReferencedAssemblyNames { get; } = new();
    internal string ModuleId { get; init; }
    internal bool SupportsReloading { get; init; }
    internal bool IsVirtualAsm => AsmPath == null;
    internal bool IsContentAsm { get; init; }
    internal bool IsSandboxed { get; init; }
    internal IReadOnlyDictionary<Type,Attribute> AsmAttributes { get; }


    internal RobustAssemblyReference(ISawmill sawmill,
        Assembly assembly,
        Stream asmStream,
        string contentPrefix,
        Stream? pdbStream = null,
        string? asmPath = null)
    {
        AssemblyName = assembly.GetName().Name!;
        AsmStream = asmStream;
        PdbStream = pdbStream;
        AsmPath = asmPath;

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

    public void Dispose()
    {
        _asmStream.Dispose();
        _pdbStream?.Dispose();
    }
}
