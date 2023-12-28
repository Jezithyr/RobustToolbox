using System;
using System.Runtime.Loader;

namespace Robust.Shared.ContentModules;

internal sealed class ContentModContext(string scopeName, bool reloadable)
{
    internal string ScopeName => scopeName;
    internal bool Reloadable => reloadable;
    internal AssemblyLoadContext Context { get; private set; } = new(scopeName);
    internal AssemblyLoadContext? DynamicContext { get; private set; } = new(scopeName, true);

    internal void LoadNewContext(AssemblyLoadContext newContext)
    {
        if (DynamicContext != null)
            throw new ArgumentException($"ContentModContext: {DynamicContext} is still loaded, unload it first!");
        if (!reloadable)
            throw new ArgumentException($"ContentModContext with scope: {scopeName} does not support Reloading!");
        if (newContext.Name != scopeName)
            throw new ArgumentException($"ContentModContext: {newContext} scopeName does not match {ScopeName}!");
        DynamicContext = newContext;
    }

    internal void UnloadContext()
    {
        if (DynamicContext == null)
            throw new ArgumentException($"ContentModContext: {ScopeName} is already unloaded!");
        if (!reloadable)
            throw new ArgumentException($"ContentModContext with scope: {scopeName} does not support Reloading!");
        DynamicContext.Unload();
        DynamicContext = null;
    }


}
