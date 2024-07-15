using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.NamedEvents;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects;

public  abstract partial class EntitySystem
{
    private Dictionary<Type,List<NamedEventId>>? _namedEventIds = null;

    public bool RegistersNamedEventIds => _namedEventIds != null;

    private bool _canReg;

    protected virtual void RegisterNamedEventIds(bool isServer)
    {
    }

    protected void RegisterId<T>(string name, string category = NamedEvents.NamedEventManager.DefaultCategory) where T: notnull
    {
        if (!_canReg)
            throw new InvalidOperationException("NamedEventIds can only be registered in DefineIds");
        RegisterId<T>((name, category));
    }

    protected void RegisterId<T>(NamedEventId namedEventId, Locality locality = Locality.Local, bool replayCapture = true)
        where T: notnull
    {
        NamedEventManager.RegisterEventId<T>(namedEventId, locality, out _, replayCapture);
        _namedEventIds ??= new();
        _namedEventIds.GetOrNew(typeof(T)).Add(namedEventId);
    }

    private void SetupNamedEvents()
    {
        //I know this looks cursed, but it's needed to idiot-proof this
        NamedEventManager.RegisterSystemWithNamedEventIds(this);
        _canReg = true;
        RegisterNamedEventIds(NetManager.IsServer);
        _canReg = false;
    }

    internal bool TryGetNamedEventIds(Type type,[NotNullWhen(true)] out List<NamedEventId>? namedEventIds)
    {
        namedEventIds = null;
        return _namedEventIds != null && _namedEventIds.TryGetValue(type, out namedEventIds);
    }

    #region Event-Proxies

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetOneShotNamedEvent<T>(NamedEventId namedEventId, Locality locality = Locality.Local) where T : notnull
    {
        NamedEventManager.ResetOneShotEvent<T>(namedEventId, locality);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseLocalPlayerNamedEvent<T>(
        NamedEventId namedEventId,
        EntityUid playerUid,
        T args,
        bool oneshot = false,
        Locality target = Locality.Local) where T : notnull
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
        Locality target = Locality.Local) where T : notnull
    {
        if (_playerMan.LocalEntity != playerUid)
            return;
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneshot, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNamedEvent<T>(
        NamedEventId namedEventId,
        T args,
        bool oneShot = false,
        Locality target = Locality.Local,
        INetChannel? netChannel = null
        ) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, args, oneShot, target, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNamedEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneShot = false,
        Locality target = Locality.Local,
        INetChannel? netChannel = null) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneShot, target, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseLocalNamedEvent<T>(
        NamedEventId namedEventId,
        T args,
        bool oneShot = false
    ) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, args, oneShot, Locality.Local);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseLocalNamedEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneShot = false) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneShot, Locality.Local);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNetNamedEvent<T>(
        NamedEventId namedEventId,
        T args,
        bool oneShot = false,
        INetChannel? netChannel = null
    ) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, args, oneShot, Locality.Networked, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNetNamedEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneShot = false,
        INetChannel? netChannel = null) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneShot, Locality.Networked, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseSharedNamedEvent<T>(
        NamedEventId namedEventId,
        T args,
        bool oneShot = false,
        INetChannel? netChannel = null) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, args, oneShot, Locality.Both, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseSharedNamedEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneShot = false,
        INetChannel? netChannel = null) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneShot, Locality.Both, netChannel);
    }

    #endregion

    #region Sub/UnSub-Proxies

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeLocalNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Locality.Local);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeLocalNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Locality.Local);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNetNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Locality.Networked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNetNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Locality.Networked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeSharedNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Locality.Both);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeSharedNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, Locality.Both);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Locality locality)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, locality);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Locality locality)
        where T : notnull
    {
        NamedEventManager.SubscribeNamedEventHandler(id, eventHandler, locality);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnSubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Locality locality)
        where T : notnull
    {
        NamedEventManager.UnsubscribeNamedEventHandler(id, eventHandler, locality);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnSubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Locality locality)
        where T : notnull
    {
        NamedEventManager.UnsubscribeNamedEventHandler(id, eventHandler, locality);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.SubscribeAllNamedEventHandlers(namedEventHandler, localityFilter, categoryFilter, ignoreList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.SubscribeAllNamedEventHandlers(namedEventHandler, localityFilter, categoryFilter, ignoreList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.UnsubscribeAllNamedEventHandlers(namedEventHandler, localityFilter, categoryFilter, ignoreList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        NamedEventManager.UnsubscribeAllNamedEventHandlers(namedEventHandler, localityFilter, categoryFilter, ignoreList);
    }

    #endregion
}
