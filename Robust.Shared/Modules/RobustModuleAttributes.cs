using System;

namespace Robust.Shared.Modules;
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class RobustModuleAttribute(string assemblyId, bool isContentAssembly = true, bool supportsReloading = false) : Attribute
{
    /// <summary>
    /// The Id of this assembly, this should be unique and also match the name of the dll.
    /// </summary>
    public string AssemblyId {get;} = assemblyId;
    /// <summary>
    /// Does this assembly support live-reloading
    /// </summary>
    public bool SupportsReloading { get;} = supportsReloading;
    /// <summary>
    /// Is this an engine or content assembly?
    /// </summary>
    public bool IsContentAssembly { get;} = isContentAssembly;
}
