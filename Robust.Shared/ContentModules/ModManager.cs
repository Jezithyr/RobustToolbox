using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Robust.Shared.ContentModules;

internal sealed class ModManager
{
    private bool _sandboxingEnabled = true;
    private bool _contentReloadingEnabled = false;
    public const string DefaultContentContext = "DefaultContent";

    private Dictionary<string,ContentModContext> _scopes = new();

    internal void SetSandboxEnabled(bool sandbox)
    {
        _sandboxingEnabled = sandbox;
    }

    internal void SetReloadEnabled(bool reloading)
    {
        _contentReloadingEnabled = reloading;
    }

    internal bool TryGetContentContext(string scopeName, [NotNullWhen(true)] out ContentModContext? context)
    {
        return _scopes.TryGetValue(scopeName, out context);
    }

    internal ContentModContext GetContentContext(string scopeName)
    {
        if (TryGetContentContext(scopeName, out var context))
        {
            return context;
        }
        throw new ArgumentException($"Content Mod Scope: {scopeName} could not be found!");
    }

    internal void Initialize()
    {
        _scopes.Add(DefaultContentContext,new ContentModContext(DefaultContentContext,_contentReloadingEnabled));
    }

    internal void CreateNewScope(string scopeName, bool reloading)
    {
        if (!_contentReloadingEnabled)
            reloading = false;
        if (!_scopes.TryAdd(scopeName, new ContentModContext(scopeName, reloading)))
            throw new ArgumentException($"Could not Create Content Mod Scope: {scopeName}, it is already defined!");
    }
}
