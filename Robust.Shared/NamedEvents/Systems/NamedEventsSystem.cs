using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Robust.Shared.NamedEvents.Systems;


public abstract partial class NamedEventSystem : EntitySystem
{
    [Dependency] protected NamedEventManager NamedEventManager = default!;
    [Dependency] protected INetManager NetManager = default!;
    [Dependency] protected ISharedPlayerManager PlayerManager = default!;

    private readonly Dictionary<Type,List<NamedEventId>> _sensorIds = new();
    private bool _canReg;

    public void ResetOneShotNamedEvent<T>(NamedEventId namedEventId) where T : notnull
    {
        NamedEventManager.ResetOneShotEvent<T>(namedEventId);
    }

    public void RaisePlayerNamedEvent<T>(NamedEventId namedEventId, EntityUid playerUid, T args, bool oneshot = false) where T : notnull
    {
        if (PlayerManager.LocalEntity != playerUid)
            return;
        NamedEventManager.RaiseEvent(namedEventId, args, oneshot);
    }

    public void RaisePlayerNamedEvent<T>(NamedEventId namedEventId, EntityUid playerUid, ref T args, bool oneshot = false) where T : notnull
    {
        if (PlayerManager.LocalEntity != playerUid)
            return;
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneshot);
    }


    public void RaiseNamedEventEvent<T>(NamedEventId namedEventId, T args, bool oneshot = false) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, args, oneshot);
    }

    public void RaiseNamedEventEvent<T>(NamedEventId namedEventId,  ref T args, bool oneshot = false) where T : notnull
    {
        NamedEventManager.RaiseEvent(namedEventId, ref args, oneshot);
    }

    internal bool TryGetNamedEventIds(Type type,[NotNullWhen(true)] out List<NamedEventId>? sensorIds)
    {
        return _sensorIds.TryGetValue(type, out sensorIds);
    }

    protected void RegisterId<T>(string name, string category = NamedEventManager.DefaultCategory) where T: notnull
    {
        if (!_canReg)
            throw new InvalidOperationException("NamedEventIds can only be registered in DefineIds");
        RegisterId<T>((name, category));
    }

    protected void RegisterId<T>(NamedEventId namedEventId, SensorOrigin locality = SensorOrigin.Local) where T: notnull
    {
        NamedEventManager.RegisterSensorId(namedEventId, typeof(T), locality, out _);
        _sensorIds.GetOrNew(typeof(T)).Add(namedEventId);
    }

    protected abstract void DefineNamedEventIds(bool isServer);

    protected override void PostInject()
    {
        //I know this looks cursed, but it's needed to idiot-proof this
        base.PostInject();
        NamedEventManager.RegisterNamedEventSystem(this);
        _canReg = true;
        DefineNamedEventIds(NetManager.IsServer);
        _canReg = false;
    }
}

