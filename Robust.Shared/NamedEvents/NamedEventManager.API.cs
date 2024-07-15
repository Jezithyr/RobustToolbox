using System.Linq;
using System.Runtime.CompilerServices;

namespace Robust.Shared.NamedEvents;

public sealed partial class NamedEventManager
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetOneShotEvent<T>(NamedEventId namedEventId, Origin origin = Origin.Local) where T : notnull
    {
        if (!TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
            return;
        subData.Triggered = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseEvent<T>(NamedEventId namedEventId, T args, bool oneShot = false, Origin target = Origin.Local)
        where T : notnull
    {
        if (!_eventData.TryGetValue((typeof(T), namedEventId), out var subData))
            return;
        RaiseEventBase(namedEventId, target, subData, ref args, oneShot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RaiseEvent<T>(NamedEventId namedEventId, ref T args, bool oneShot = false, Origin target = Origin.Local)
        where T : notnull
    {
        if (!_eventData.TryGetValue((typeof(T), namedEventId), out var subData))
            return;
        RaiseEventBase(namedEventId, target, subData, ref args, oneShot);
    }

    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in Systems)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                SubscribeNamedEventHandler(namedEventId, namedEventHandler, originFilter & subData.AllowedOrigins);
            }
        }
    }

    public void SubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in Systems)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                SubscribeNamedEventHandler(namedEventId, namedEventHandler, originFilter & subData.AllowedOrigins);
            }
        }
    }

    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in Systems)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                UnsubscribeNamedEventHandler(namedEventId, namedEventHandler, originFilter & subData.AllowedOrigins);
            }
        }
    }

    public void UnsubscribeAllNamedEventHandlers<T>(
        NamedEventRefHandler<T> namedEventHandler,
        Origin originFilter = Origin.Both,
        string? categoryFilter = null,
        params NamedEventId[] ignoreList) where T: notnull
    {
        foreach (var config in Systems)
        {
            if (!config.TryGetNamedEventIds(typeof(T), out var data))
                continue;

            foreach (var namedEventId in data)
            {
                if (categoryFilter != null && categoryFilter != namedEventId.Category
                    || ignoreList.Contains(namedEventId)
                    || !TryGetSubscriptionData(namedEventId, typeof(T), out var subData))
                    continue;
                UnsubscribeNamedEventHandler(namedEventId, namedEventHandler, originFilter & subData.AllowedOrigins);
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Origin origin = Origin.Local)
        where T : notnull
    {
        CreateSubscription<T>(
            id,
            origin,
            CreateSubMethod(id, eventHandler),
            eventHandler,
            false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Origin origin = Origin.Local)
        where T : notnull
    {
        CreateSubscription<T>(
            id,
            origin,
            CreateSubMethod(id, eventHandler),
            eventHandler,
            true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventHandler<T> eventHandler,
        Origin origin = Origin.Local)
        where T : notnull
    {
        RemoveSubscription<T>(
            id,
            Origin.Both,
            eventHandler,
            false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsubscribeNamedEventHandler<T>(
        NamedEventId id,
        NamedEventRefHandler<T> eventHandler,
        Origin origin = Origin.Local)
        where T : notnull
    {
        RemoveSubscription<T>(
            id,
            Origin.Both,
            eventHandler,
            true);
    }
}
