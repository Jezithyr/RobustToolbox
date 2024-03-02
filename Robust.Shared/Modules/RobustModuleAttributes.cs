using System;
using JetBrains.Annotations;

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
    bool isSandboxed = true) : Attribute
{
    public string? Id { get; } = id;
    public bool IsContentAssembly { get; } = isContentAssembly;
    public bool SupportsLiveReloading { get; } = supportsLiveReloading;
    public bool IsSandboxed { get; } = isSandboxed;
}
