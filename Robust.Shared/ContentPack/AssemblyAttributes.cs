using System;

namespace Robust.Shared.ContentPack;

//Does this assembly support being unloaded and reloaded?
[AttributeUsage(AttributeTargets.Assembly)]
public class HotReloadable : Attribute;

[AttributeUsage(AttributeTargets.Assembly)]
public class RobustMod(ModAssemblyType type = ModAssemblyType.Gameplay) : Attribute
{
    public ModAssemblyType Type { get; } = type;
}
