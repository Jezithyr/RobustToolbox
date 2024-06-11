using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Reflection;

namespace Robust.Shared.GameTelemetry;

public sealed class GameTelemetryManager : IEntityEventSubscriber, IPostInjectInit
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IReflectionManager _reflectionManager = default!;
    [Dependency] private IDynamicTypeFactoryInternal _typeFactory = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public const string DefaultCategoryName = "general";

    private readonly Dictionary<Type, GameTelemetryDelegateWrapper> _subscribers = new();
    private readonly Dictionary<Type, GameTelemetryHandler> _telemetryHandlers = new();
    private readonly List<GameTelemetryHandler> _autoListenerHandlers = new();

    public void Initialize()
    {
        _sawmill.Info("Start Init!!");
        CreateTelemetryHandlers();
        CreateTelemetryDelegates();
        SubscribeTelemetryComponents();
        LoadConfigs();
        _sawmill.Info("Initialized!");
    }

    public void RaiseLocalEvent<TArgs>(EntityUid entityId, GameTelemetryId telemetryId,ref TArgs args)
        where TArgs : struct, IGameTelemetryType
    {
        if (!TryGetSubscriber<TArgs>(out var subscriber))
            return;
        subscriber.RaiseLocalEvent(entityId, telemetryId,ref args);
    }

    public void RaiseLocalEvent<TArgs>(EntityUid entityId, CachedGameTelemetryEvent<TArgs> cachedEvent,ref TArgs args)
        where TArgs : struct, IGameTelemetryType
    {
        if (!TryGetSubscriber<TArgs>(out var subscriber))
            return;
        subscriber.RaiseLocalEvent(entityId, cachedEvent,ref args);
    }


    public bool RegisterTelemetryId<TArgs>(GameTelemetryId telemetryId)
        where TArgs : struct, IGameTelemetryType
    {
        if (!TryGetSubscriber<TArgs>(out var subscriber)
            || !subscriber.RegisterListener(telemetryId))
            return false;

        foreach (var handler in _autoListenerHandlers)
        {
            if (handler is IGameTelemetryHandler<TArgs> typeHandler && handler.TryAddListenToId<TArgs>(telemetryId))
                subscriber.AddDelegate(telemetryId,typeHandler.HandleTelemetryArgs);
        }
        return true;
    }

    public bool RemoveTelemetryId<TArgs>(GameTelemetryId telemetryId)
        where TArgs : struct, IGameTelemetryType
    {
        if (!TryGetSubscriber<TArgs>(out var subscriber)
            || !subscriber.RemoveListener(telemetryId))
            return false;

        foreach (var handler in _autoListenerHandlers)
        {
            if (!(handler is IGameTelemetryHandler<TArgs> typeHandler) || !handler.TryRemoveListenFromId<TArgs>(telemetryId))
                continue;
            subscriber.RemoveHandler(telemetryId,typeHandler.HandleTelemetryArgs);
        }
        return true;
    }


    public void RaiseLocalEvent<T>(GameTelemetryId telemetryId, ref T args, EntityUid? origin = null)
        where T : struct, IGameTelemetryType
    {
        if (!TryGetSubscriber<T>(out var subscriber))
            return;
        args.Origin = origin;
        subscriber.RaiseEvent(telemetryId, ref args);
    }

    public void RaiseLocalEvent<TArgs>(CachedGameTelemetryEvent<TArgs> cachedEvent, ref TArgs args, EntityUid? origin = null)
        where TArgs : struct, IGameTelemetryType
    {
        args.Origin = null;
        args.TelemetryId = cachedEvent.Id;
        cachedEvent.Delegate(args);
    }

    private bool TryGetSubscriber<TArgs>([NotNullWhen(true)]out GameTelemetryDelegateWrapper<TArgs>? subscriber)
        where TArgs : struct, IGameTelemetryType
    {

        if (!_subscribers.TryGetValue(typeof(TArgs), out var rawSub))
        {
            subscriber = null;
            _sawmill.Error($"{typeof(TArgs)} is not a valid GameplayTelemetry Type or is unregistered!");
            return false;
        }
        subscriber = (GameTelemetryDelegateWrapper<TArgs>) rawSub;
        return true;
    }

    private void CreateTelemetryDelegates()
    {
        foreach (var argumentType in _reflectionManager.GetAllChildren(typeof(IGameTelemetryType)))
        {
            if (argumentType.IsAbstract)
                continue;
            var delegateWrapperType = typeof(GameTelemetryDelegateWrapper<>).MakeGenericType(argumentType);
            var subscriber = (GameTelemetryDelegateWrapper)_typeFactory.CreateInstanceUnchecked(delegateWrapperType, true);
            subscriber.Init(_sawmill, _entityManager.EventBus);
            _subscribers.Add(argumentType, subscriber);
            _sawmill.Info($"Loading TelemetryType: {argumentType}!");
        }
        _sawmill.Info("TelemetryTypes loaded!");
    }

    private void SubscribeTelemetryComponents()
    {
        foreach (var compType in _reflectionManager.FindTypesWithAttribute<GameTelemetrySubscriberAttribute>())
        {
            if (!typeof(IComponent).IsAssignableFrom(compType))
                continue;
            var attr = (GameTelemetrySubscriberAttribute)compType.GetCustomAttribute(typeof(GameTelemetrySubscriberAttribute))!;
            foreach (var teleType in attr.TelemetryTypes)
            {
                if (!typeof(IGameTelemetryType).IsAssignableFrom(teleType)
                    || !_subscribers.TryGetValue(teleType, out var subscriber))
                {
                    _sawmill.Error($"{teleType} is not a valid GameplayTelemetry Type!");
                    return;
                }
                var compReg = teleType.GetMethod("RegisterComponentSubscription")!.MakeGenericMethod(compType);
                compReg.Invoke(subscriber, null);
            }
        }
        _sawmill.Info("Telemetry ComponentSubscribers Loaded!");
    }

    private void CreateTelemetryHandlers()
    {
        foreach (var type in _reflectionManager.GetAllChildren(typeof(GameTelemetryHandler)))
        {
            if (type.IsAbstract)
                continue;
            var teleHandler = (GameTelemetryHandler)_typeFactory.CreateInstanceUnchecked(type);
            foreach (var iHandlerType in type.GetInterfaces())
            {
                if (!typeof(IGameTelemetryHandler).IsAssignableFrom(iHandlerType) || !iHandlerType.IsGenericType)
                    continue;
                teleHandler.SupportedTelemetryTypes.Add(iHandlerType.GenericTypeArguments[0]);
            }
            IoCManager.InjectDependencies(teleHandler);
            teleHandler.PostInject();
            _sawmill.Info($"Loading TelemetryHandler: {type}!");
            _telemetryHandlers.Add(type, teleHandler);
            if (teleHandler.AutoListen)
                _autoListenerHandlers.Add(teleHandler);
        }
        _sawmill.Info("TelemetryHandlers loaded!");
    }

    public bool AddHandlerForId<T>(GameTelemetryId telemetryId)
        where T:struct, IGameTelemetryType
    {
        if (!TryGetHandler<T>(out var handler, out var typeHandler))
            return false;

        if (!handler.TryAddListenToId<T>(telemetryId))
        {
            _sawmill.Error($"Telemetry handler {handler} is already listening for events with ID: {telemetryId}!");
            return false;
        }

        if (!_subscribers.TryGetValue(typeof(T), out var delegateWrapper))
            return false;
        ((GameTelemetryDelegateWrapper<T>)delegateWrapper).AddDelegate(telemetryId, typeHandler.HandleTelemetryArgs);
        return true;
    }

    public bool RemoveHandlerForId<T>(GameTelemetryId telemetryId)
        where T:struct, IGameTelemetryType
    {
        if (!TryGetHandler<T>(out var handler,out var typeHandler))
            return false;
        if (!handler.TryRemoveListenFromId<T>(telemetryId))
        {
            _sawmill.Error($"Telemetry handler {handler} is already listening for events with ID: {telemetryId}!");
            return false;
        }

        if (!_subscribers.TryGetValue(typeof(T), out var delegateWrapper))
            return false;

        ((GameTelemetryDelegateWrapper<T>)delegateWrapper).AddDelegate(telemetryId, typeHandler.HandleTelemetryArgs);
        return true;
    }

    public bool TryGetHandler<T>(
        [NotNullWhen(true)] out GameTelemetryHandler? handler,
        [NotNullWhen(true)] out IGameTelemetryHandler<T>? typeHandler)
        where T : struct, IGameTelemetryType
    {
        if (!_telemetryHandlers.TryGetValue(typeof(T), out handler))
        {
            handler = null;
            typeHandler = null;
            _sawmill.Error($"Game Telemetry handler for telemetry type {typeof(T)} not found!");
            return false;
        }
        typeHandler = (IGameTelemetryHandler<T>) handler;
        return true;
    }

    private void LoadConfigs()
    {
        foreach (var type in _reflectionManager.GetAllChildren<GameTelemetryConfig>())
        {
            if (type.IsAbstract)
                continue;
            ((GameTelemetryConfig)_typeFactory.CreateInstance(type)).Initialize(this);
        }
    }

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("game.telemetry");
        _sawmill.Info("Created!");
    }
}
