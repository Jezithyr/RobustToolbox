using System;

namespace Robust.Shared.ContentPack;

//What is the name of this mod and is it reloadable?
[AttributeUsage(AttributeTargets.Assembly)]
public class RobustMod(string modName, bool reloadable) : Attribute
{
    public string ModName { get; } = modName;
    public bool Reloadable { get; } = reloadable;
}
