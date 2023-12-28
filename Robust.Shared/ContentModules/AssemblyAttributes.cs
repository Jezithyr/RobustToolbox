using System;

namespace Robust.Shared.ContentModules;


[AttributeUsage(AttributeTargets.Assembly)]
public class RobustModuleAttribute(string? scope = null, bool reloadable = false) : Attribute
{
    public string? Scope { get; } = scope;
    public bool Reloadable { get; } = reloadable;
}
