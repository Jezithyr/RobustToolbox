using System.Linq;
using System.Runtime.CompilerServices;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Robust.Shared.NamedEvents;

public sealed partial class NamedEventManager
{

    #region RaisingEvents

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseEvent<T>(
        NamedEventId namedEventId,
        T args,
        bool oneShot = false,
        Locality target = Locality.Local,
        INetChannel? netChannel = null)
        where T : notnull
    {
        if (!CanRaiseEvent<T>(namedEventId, Locality.Networked, oneShot, out var subData))
            return;
        RaiseEventBase(namedEventId, target, subData, ref args, oneShot, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseEvent<T>(
        NamedEventId namedEventId,
        ref T args,
        bool oneShot = false,
        Locality target = Locality.Local,
        INetChannel? netChannel = null)
        where T : notnull
    {
        if (!CanRaiseEvent<T>(namedEventId, Locality.Networked, oneShot, out var subData))
            return;
        RaiseEventBase(namedEventId, target, subData, ref args, oneShot, netChannel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNetworkEvent<T>(NamedEventId id, ref T data, bool oneShot) where T : notnull
    {
        if (!CanRaiseEvent<T>(id, Locality.Networked, oneShot, out _))
            return;
        RaiseNetworkEventBase(id, ref data, oneShot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNetworkEvent<T>(NamedEventId id, ref T data, bool oneShot, INetChannel channel) where T : notnull
    {
        if (!CanRaiseEvent<T>(id, Locality.Networked, oneShot, out _))
            return;
        RaiseNetworkEventBase(id,ref data, oneShot, channel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNetworkEvent<T>(NamedEventId id, ref T data, bool oneShot, ICommonSession session) where T : notnull
    {
        if (!CanRaiseEvent<T>(id, Locality.Networked, oneShot, out _))
            return;
        RaiseNetworkEventBase(id,ref data, oneShot, session.Channel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseNetworkEvent<T>(NamedEventId id, ref T data, bool oneShot, Filter filter) where T : notnull
    {
        if (!CanRaiseEvent<T>(id, Locality.Networked, oneShot, out _))
            return;
        foreach (var session in filter.Recipients)
        {
            RaiseNetworkEventBase(id,ref data, oneShot, session.Channel);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetOneShotEvent<T>(NamedEventId namedEventId, Locality locality = Locality.Local) where T : notnull
    {
        if (!TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
            return;
        subData.Triggered = false;
    }

    #endregion

    #region Sub/Unsub

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Locality locality = Locality.Local)
        where T : notnull
    {
        CreateSubscription<T>(
            id,
            locality,
            CreateSubMethod(id, eventHandler),
            eventHandler,
            false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Locality locality = Locality.Local)
        where T : notnull
    {
        CreateSubscription<T>(
            id,
            locality,
            CreateSubMethod(id, eventHandler),
            eventHandler,
            true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Locality locality = Locality.Local)
        where T : notnull
    {
        RemoveSubscription<T>(
            id,
            Locality.Both,
            eventHandler,
            false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Locality locality = Locality.Local)
        where T : notnull
    {
        RemoveSubscription<T>(
            id,
            Locality.Both,
            eventHandler,
            true);
    }

    #endregion

    #region Sub/Unsub From All

    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in _systemsWithIds)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                SubscribeNamedEventHandler(namedEventId, namedEventHandler, localityFilter & subData.AllowedOrigins);
            }
        }
    }

    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in _systemsWithIds)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                SubscribeNamedEventHandler(namedEventId, namedEventHandler, localityFilter & subData.AllowedOrigins);
            }
        }
    }

    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in _systemsWithIds)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                UnsubscribeNamedEventHandler(namedEventId, namedEventHandler, localityFilter & subData.AllowedOrigins);
            }
        }
    }

    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Locality localityFilter = Locality.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in _systemsWithIds)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                UnsubscribeNamedEventHandler(namedEventId, namedEventHandler, localityFilter & subData.AllowedOrigins);
            }
        }
    }


    #endregion



}
