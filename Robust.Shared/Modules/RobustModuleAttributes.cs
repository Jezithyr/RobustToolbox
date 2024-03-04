using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Robust.Shared.Modules;
/// <summary>
/// Assembly attribute used for adding RobustModule Metadata to an assembly.
/// RobustModule metadata is used during the assembly loading process to properly sort assemblies.
/// If this attribute is not present, the loader will fallback to the legacy loading behavior
/// </summary>
/// <param name="id">A unique Identifier for this module, must be defined if live-reloading is enabled</param>
/// <param name="isContentAssembly">Is this a Content or Engine module, defaults to content</param>
/// <param name="supportsLiveReloading">Does this assembly support live-reloading, requires Id to be defined!</param>
/// <param name="isSandboxed">Should we check and load this assembly if sandboxing is enabled?, replaces the obsolete "SkipIfSandboxed" attribute.</param>
[AttributeUsage(AttributeTargets.Assembly), UsedImplicitly]
public sealed class RobustModuleAttribute(
    string? id = null,
    bool isContentAssembly = true,
    bool supportsLiveReloading = false,
    bool isSandboxed = true) : RobustAssemblyAttribute
{
    public string? Id { get; } = id;
    public bool IsContentAssembly { get; } = isContentAssembly;
    public bool SupportsLiveReloading { get; } = supportsLiveReloading;
    public bool IsSandboxed { get; } = isSandboxed;
}


[AttributeUsage(AttributeTargets.Assembly), UsedImplicitly]
public abstract class RobustAssemblyAttribute : Attribute;


internal record AssemblyAttributeMeta
{
    //Yeah I know this boxes but this is only run when an assembly ref
    //(which only happens when you inspect an assembly/prepare to load it).
    //I can't be arsed to make a separate dictionary for each primitive type for what is likely an unnoticable perf gain, sue me
    private readonly Dictionary<Type, Dictionary<string, object?>>  _properties = new();
    private readonly Dictionary<Type, Dictionary<string, List<object?>>> _collectionProperties = new ();

    //it's possible that attribute name might be null in some edge cases but there is no point in handling them
    //since attributeName is mainly just used for debug print info anyways.
    public string? AttributeTypeName { get; init; }

    public AssemblyAttributeMeta(CustomAttributeData attrData)
    {
        AttributeTypeName = attrData.Constructor.DeclaringType!.FullName;
        foreach (var namedArg in attrData.NamedArguments)
        {
            if (!AllowedArgTypes.Contains(namedArg.TypedValue.ArgumentType))
                continue; //Skip over any properties with types that we don't support!
            StorePropertyValueOrArray(namedArg);
        }
    }

    private void StorePropertyValueOrArray(CustomAttributeNamedArgument namedArg)
    {
        //check if our property is an array or single value
        if (namedArg.TypedValue.Value != null
            && namedArg.TypedValue.Value.GetType() == typeof(ReadOnlyCollection<CustomAttributeTypedArgument>))
        {
            List<object?> arrayArgs = new();
            foreach (CustomAttributeTypedArgument argElement in
            (ReadOnlyCollection<CustomAttributeTypedArgument>) namedArg.TypedValue.Value)
            {
                arrayArgs.Add(argElement.Value);
            }

            var dict = _collectionProperties.GetOrNew(namedArg.TypedValue.ArgumentType );
            dict.Add(namedArg.MemberName, arrayArgs);
        }
        else
        {
            var dict = _properties.GetOrNew(namedArg.TypedValue.ArgumentType );
            dict.Add(namedArg.MemberName, namedArg.TypedValue.Value);
        }

    }


    internal bool TryGetPropertyValue<T>(string name, [NotNullWhen(true)] out T? value)
    {
        value = GetPropertyValue<T>(name, out var success, false);
        return success;
    }

    internal bool TryGetPropertyArrayValue<T>(string name, [NotNullWhen(true)] out IReadOnlyList<T>? value)
    {
        value = GetPropertyArrayValue<T>(name, out var success, false);
        if (!success)
            value = null;
        return success;
    }

    internal IReadOnlyList<T> GetPropertyArrayValue<T>(string name)
    {
        return GetPropertyArrayValue<T>(name, out _);
    }

    internal IReadOnlyList<T> GetPropertyArrayValue<T>(string name, out bool success, bool throwOnFail = true)
    {
        success = false;
        CheckType<T>();
        if (!_collectionProperties.TryGetValue(typeof(T), out var data) && throwOnFail)
            throw new ArgumentException($"{typeof(T)} could not be found as a property on {AttributeTypeName}");
        if (data == null)
            return new List<T>();

        if (!data.TryGetValue(name, out var value) && throwOnFail)
            throw new ArgumentException($"{name} does not match any property on {AttributeTypeName}");
        var output = (List<T>)_collectionProperties.Cast<T>();
        return output.AsReadOnly();
    }

    internal T? GetPropertyValue<T>(string name)
    {
        return GetPropertyValue<T>(name, out _);
    }
    internal T? GetPropertyValue<T>(string name,out bool success, bool throwOnFail = true)
    {
        success = false;
        CheckType<T>();
        if (!_properties.TryGetValue(typeof(T), out var data) && throwOnFail)
                throw new ArgumentException($"{typeof(T)} could not be found as a property on {AttributeTypeName}");
        if (data == null)
            return default;
        if (!data.TryGetValue(name, out var value) && throwOnFail)
            throw new ArgumentException($"{name} does not match any property on {AttributeTypeName}");
        success = true;
        return (T?) value;
    }



    private void CheckType<T>()
    {
        if (!AllowedArgTypes.Contains(typeof(T)))
        {
            throw new ArgumentException($"Reading {typeof(T)} is not supported!");
        }
    }

    //The types of arguments that we support fetching
    private static readonly HashSet<Type> AllowedArgTypes =
    [
        typeof(bool), typeof(byte), typeof(char), typeof(double), typeof(float), typeof(int), typeof(long),
        typeof(sbyte), typeof(short), typeof(string), typeof(uint), typeof(ulong), typeof(ushort), typeof(Type), typeof(Enum)
    ];
}
