using System;

namespace Robust.Shared.ContentPack;

[AttributeUsage(AttributeTargets.Assembly)]
public class UIAssembly : Attribute;

//Does this assembly support being unloaded and reloaded?
[AttributeUsage(AttributeTargets.Assembly)]
public class HotReloadable : Attribute;
