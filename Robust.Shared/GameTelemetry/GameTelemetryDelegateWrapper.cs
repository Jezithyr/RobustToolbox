using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Robust.Shared.GameTelemetry;

[Access(typeof(GameTelemetryManager))]
public abstract class GameTelemetryDelegateWrapper
{
    protected ISawmill Sawmill = default!;

    protected IEventBus EventBus = default!;

    public void Init(ISawmill sawmill, IEventBus eventBus)
    {
        Sawmill = sawmill;
        EventBus = eventBus;
    }

    [UsedImplicitly]
    public abstract bool RegisterComponentSubscription<TComp>() where TComp : IComponent;
}

[Access(typeof(GameTelemetryManager))]
public sealed class GameTelemetryDelegateWrapper<T> : GameTelemetryDelegateWrapper
    where T : struct, IGameTelemetryType
{
    private readonly Dictionary<GameTelemetryId, GameTelemetryDelegate<T>> _handlers = new();

    private readonly HashSet<Type> _subbedComponents = new();

    public override bool RegisterComponentSubscription<TComp>()
    {
        if (!_subbedComponents.Add(typeof(TComp)))
        {
            Sawmill.Error($"Component of type: {typeof(TComp)} is already registered to TelemetryHandler: {this}");
            return false;
        }

        EventBus.SubscribeLocalEvent((EntityUid uid, TComp comp, EventBusSubscriptionWrapper wrapper) =>
        {
            wrapper.Delegate(wrapper.Args);
        });
        return true;
    }

    public CachedGameTelemetryEvent<T> CacheTelemetryEvent(GameTelemetryId telemetryId)
    {
        if (!TryGetHandler(telemetryId, out var handler))
            throw new ArgumentException($"{typeof(T)} Telemetry Listener for: {telemetryId} was not registered!");
        return new CachedGameTelemetryEvent<T>(telemetryId, handler);
    }

    public void RaiseLocalEvent(EntityUid entityId,GameTelemetryId telemetryId, ref T args)
    {
        if (!TryGetHandler(telemetryId, out var handler))
            return;

        args.Origin = entityId;
        args.TelemetryId = telemetryId;
        var ev = new EventBusSubscriptionWrapper(args, handler);
        EventBus.RaiseLocalEvent(entityId, ref ev);
    }

    public void RaiseLocalEvent(EntityUid entityId, CachedGameTelemetryEvent<T> cachedEvent, ref T args)
    {
        args.TelemetryId = cachedEvent.Id;
        args.Origin = entityId;
        var ev = new EventBusSubscriptionWrapper(args, cachedEvent.Delegate);
        EventBus.RaiseLocalEvent(entityId, ref ev);
    }

    public bool RegisterListener(GameTelemetryId telemetryId)
    {
        if (_handlers.TryAdd(telemetryId, DummyHandler))
            return true;
        Sawmill.Error($"{typeof(T)} Telemetry Listener for: {telemetryId} is already registered!");
        return false;
    }

    public bool RemoveListener(GameTelemetryId telemetryId)
    {
        if (_handlers.Remove(telemetryId))
            return true;
        Sawmill.Error($"{typeof(T)} Telemetry Listener for: {telemetryId} was already removed!");
        return false;
    }

    private bool TryGetHandler(GameTelemetryId telemetryId,[NotNullWhen(true)] out GameTelemetryDelegate<T>? handler)
    {
        if (_handlers.TryGetValue(telemetryId, out handler))
            return true;
        handler = null;
        Sawmill.Error($"{typeof(T)} Telemetry Listener for: {telemetryId} is not registered!");
        return false;
    }

    public void AddDelegate(GameTelemetryId telemetryId, GameTelemetryDelegate<T> handler)
    {
        if (!TryGetHandler(telemetryId, out var teleDelegate))
        {
            Sawmill.Error($"{typeof(T)} Telemetry Listener for: {telemetryId} is not registered!");
            return;
        }
        teleDelegate += handler;
    }

    public void RemoveHandler(GameTelemetryId telemetryId, GameTelemetryDelegate<T> handler)
    {
        if (!TryGetHandler(telemetryId, out var teleDelegate))
        {
            Sawmill.Error($"{typeof(T)} Telemetry Listener for: {telemetryId} is not registered!");
            return;
        }
        teleDelegate -= handler;
    }

    public bool RaiseEvent(GameTelemetryId telemetryId ,ref T args, EntityUid? origin = null)
    {
        if (!TryGetHandler(telemetryId, out var handler))
            return false;
        args.TelemetryId = telemetryId;
        args.Origin = origin;
        handler(args);
        return true;
    }

    [ByRefEvent]
    private record struct EventBusSubscriptionWrapper(
        T Args,
        GameTelemetryDelegate<T> Delegate);

    private void DummyHandler(T args) {}
}

public record struct CachedGameTelemetryEvent<T>(
    GameTelemetryId Id,
    GameTelemetryDelegate<T> Delegate) where T : struct, IGameTelemetryType;
