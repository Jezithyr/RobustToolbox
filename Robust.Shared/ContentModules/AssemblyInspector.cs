using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Robust.Shared.ContentModules;

internal sealed class AssemblyInspector : IDisposable, IPostInjectInit
{
    [Dependency] private readonly IResourceManagerInternal _res = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private bool _initialized = false;
    private ResPath _path = default!;
    private ISawmill _sawmill = default!;

    //assemblydata
    private bool _loadedMetaData = false;
    private string _assemblyName = default!;
    private List<Attribute> _assemblyAttributes = new();




    internal void SetPath(ResPath assemblyPath)
    {
        _path = assemblyPath;
        _initialized = true;
    }

    internal void ReadAssemblyMetaData()
    {
        if (_loadedMetaData)
        {
            _sawmill.Warning($"Metadata already loaded for assembly at path: {_path}! Overwriting!");
            _assemblyAttributes.Clear();
        }
        using var asmFile = _res.ContentFileRead(_path);
        using var reader = ModLoader.MakePEReader(asmFile);
        var metaReader = reader.GetMetadataReader();
        _assemblyName = metaReader.GetString(metaReader.GetAssemblyDefinition().Name);
        foreach (var attribHandle in metaReader.CustomAttributes)
        {
            var attrib = metaReader.GetCustomAttribute(attribHandle);
            if (attrib.Parent.Kind != HandleKind.AssemblyDefinition)
                continue;
            var typeHandle = attrib.Constructor;
            if (typeHandle.Kind != HandleKind.MemberReference)
                continue;
            var memberRef = metaReader.GetMemberReference((MemberReferenceHandle) typeHandle);
            var typeRef = AssemblyTypeChecker.ParseTypeReference(metaReader, (TypeReferenceHandle)memberRef.Parent);
            attrib.DecodeValue(new AttributeObjectProvider());
            var temp = attrib.DecodeValue(new AttributeObjectProvider());


        }

        // foreach (var attribHandle in metaReader.CustomAttributes)
        // {
        //     var attrib = metaReader.GetCustomAttribute(attribHandle);
        //     if (attrib.Parent.Kind != HandleKind.AssemblyDefinition)
        //         continue;
        //
        //     var ctor = attrib.Constructor;
        //     if (ctor.Kind != HandleKind.MemberReference)
        //         continue;
        //
        //     var memberRef = metaReader.GetMemberReference((MemberReferenceHandle) ctor);
        //     var typeRef = AssemblyTypeChecker.ParseTypeReference(metaReader, (TypeReferenceHandle)memberRef.Parent);
        //
        //     if (typeRef.Namespace == "Robust.Shared.ContentPack" && typeRef.Name == "SkipIfSandboxedAttribute")
        //         return true;
        // }

        _loadedMetaData = true;
    }

    internal bool VerifySandboxing()
    {
        using var asmFile = _res.ContentFileRead(_path);
        // var refData = GetAssemblyReferenceData(asmFile);
        return true;
    }

    // private (string[] refs, string name)? GetAssemblyReferenceData(Stream stream)
    // {
    //     using var reader = ModLoader.MakePEReader(stream);
    //     var metaReader = reader.GetMetadataReader();
    //
    //     var name = metaReader.GetString(metaReader.GetAssemblyDefinition().Name);
    //
    //     // Try to find SkipIfSandboxedAttribute.
    //
    //     if (_sandboxingEnabled && TryFindSkipIfSandboxed(metaReader))
    //     {
    //         _sawmill.Debug("Module {ModuleName} has SkipIfSandboxedAttribute, ignoring.", name);
    //         return null;
    //     }
    //
    //     return (metaReader.AssemblyReferences
    //         .Select(a => metaReader.GetAssemblyReference(a))
    //         .Select(a => metaReader.GetString(a.Name)).ToArray(), name);
    // }


    // internal bool TryLoadModules(IEnumerable<ResPath> paths)
    // {
    //     var sw = Stopwatch.StartNew();
    //     _sawmill.Debug("LOADING modules");
    //     var files = new Dictionary<string, (ResPath Path, string[] references)>();
    //
    //     // Find all modules we want to load.
    //     foreach (var fullPath in paths)
    //     {
    //         using var asmFile = _res.ContentFileRead(fullPath);
    //         var refData = GetAssemblyReferenceData(asmFile);
    //         if (refData == null)
    //             continue;
    //
    //         var (asmRefs, asmName) = refData.Value;
    //
    //         if (!files.TryAdd(asmName, (fullPath, asmRefs)))
    //         {
    //             _sawmill.Error("Found multiple modules with the same assembly name " +
    //                            $"'{asmName}', A: {files[asmName].Path}, B: {fullPath}.");
    //             return false;
    //         }
    //     }
    //
    //     if (_sandboxingEnabled)
    //     {
    //         var checkerSw = Stopwatch.StartNew();
    //
    //         var typeChecker = MakeTypeChecker();
    //         var resolver = typeChecker.CreateResolver();
    //
    //         Parallel.ForEach(files, pair =>
    //         {
    //             var (name, (path, _)) = pair;
    //
    //             using var stream = _res.ContentFileRead(path);
    //             if (!typeChecker.CheckAssembly(stream, resolver))
    //             {
    //                 throw new TypeCheckFailedException($"Assembly {name} failed type checks.");
    //             }
    //         });
    //
    //         _sawmill.Debug($"Verified assemblies in {checkerSw.ElapsedMilliseconds}ms");
    //         return true;
    //     }
    //     return false;
    // }

    public void Dispose()
    {
    }

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("res.mod");
    }
}

internal sealed class AttributeObjectProvider : ICustomAttributeTypeProvider<object?>
{
    public object? GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return null;
    }

    public object? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return null;
    }

    public object? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return null;
    }

    public object? GetSZArrayType(object? elementType)
    {
        return null;
    }

    public object? GetSystemType()
    {
        return null;
    }

    public object? GetTypeFromSerializedName(string name)
    {
        return null;
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(object? type)
    {
        return default;
    }

    public bool IsSystemType(object? type)
    {
        return default;
    }
}
