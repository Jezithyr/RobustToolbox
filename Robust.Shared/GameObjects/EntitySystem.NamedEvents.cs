using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.NamedEvents;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects;

public  abstract partial class EntitySystem
{
    private readonly Dictionary<Type,List<NamedEventId>> _namedEventIds = new();
    private bool _canReg;

    protected virtual void RegisterNamedEventIds(bool isServer)
    {
    }

    protected void RegisterId<T>(string name, string category = NamedEventManager.DefaultCategory) where T: notnull
    {
        if (!_canReg)
            throw new InvalidOperationException("NamedEventIds can only be registered in DefineIds");
        RegisterId<T>((name, category));
    }

    protected void RegisterId<T>(NamedEventId namedEventId, Origin locality = Origin.Local) where T: notnull
    {
        NamedEventManager.RegisterNamedEventId(namedEventId, typeof(T), locality, out _);
        _namedEventIds.GetOrNew(typeof(T)).Add(namedEventId);
    }

    private void SetupNamedEvents()
    {
        //I know this looks cursed, but it's needed to idiot-proof this
        NamedEventManager.RegisterNamedEventSystem(this);
        _canReg = true;
        RegisterNamedEventIds(NetManager.IsServer);
        _canReg = false;
    }

    internal bool TryGetNamedEventIds(Type type,[NotNullWhen(true)] out List<NamedEventId>? sensorIds)
    {
        return _namedEventIds.TryGetValue(type, out sensorIds);
    }

    #region Event-Proxies

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetOneShotNamedEvent<T>(NamedEventId namedEventId, Origin origin = Origin.Local) where T : notnull
    {
        NamedEventManager.ResetOneShotEvent<T>(namedEventId, origin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseLocalPlayerNamedEvent<T>(
        NamedEventId namedEventId,
        EntityUid playerUid,
        T args,
        bool oneshot = false,
        Origin target = Origin.Local) where T : notnull
    {
        if (_playerMan.LocalEntity != playerUid)
            return;
        NamedEventManager.RaiseEvent(namedEventId, args, oneshot, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseLocalPlayerNamedEvent<T>(
        NamedEventId namedEventId,
        EntityUid playerUid,
        ref T args,
        bool oneshot = false,
        Origin target = Origin.Local) where T : notnull
    {
        if (_playerMan.LocalEntity != playerUid)
            return;
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneshot, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNamedEventEvent<T>(
        NamedEventId namedEventId,
        T args,
        bool oneshot = false,
        Origin target = Origin.Local) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, args, oneshot, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNamedEventEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneshot = false,
        Origin target = Origin.Local) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneshot, target);
    }

    #endregion

    #region Sub/UnSub-Proxies

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeLocalNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Origin.Local);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeLocalNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Origin.Local);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNetNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Origin.Networked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNetNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Origin.Networked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeSharedNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Origin.Both);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeSharedNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Origin.Both);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Origin origin)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, origin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Origin origin)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, origin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnSubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Origin origin)
        where T : notnull
    {
        NamedEventManager.UnsubscribeNamedEventHandler(id, eventHandler, origin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnSubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Origin origin)
        where T : notnull
    {
        NamedEventManager.UnsubscribeNamedEventHandler(id, eventHandler, origin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.SubscribeAllNamedEventHandlers(namedEventHandler, originFilter, categoryFilter, ignoreList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.SubscribeAllNamedEventHandlers(namedEventHandler, originFilter, categoryFilter, ignoreList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.UnsubscribeAllNamedEventHandlers(namedEventHandler, originFilter, categoryFilter, ignoreList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.UnsubscribeAllNamedEventHandlers(namedEventHandler, originFilter, categoryFilter, ignoreList);
    }

    #endregion
}
