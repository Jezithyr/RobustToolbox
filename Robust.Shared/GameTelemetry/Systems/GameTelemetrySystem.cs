using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Robust.Shared.GameTelemetry.Systems;


public abstract partial class GameTelemetrySystem : EntitySystem
{
    [Dependency] protected GameTelemetryManager TelemetryManager = default!;
    [Dependency] protected INetManager NetManager = default!;
    [Dependency] protected ISharedPlayerManager PlayerManager = default!;

    private readonly Dictionary<Type,List<GameTelemetryId>> _sensorIds = new();
    private bool _canReg;

    public void ResetOneShotTelemetryEvent<T>(GameTelemetryId gameTelemetryId) where T : notnull
    {
        TelemetryManager.ResetOneShotEvent<T>(gameTelemetryId);
    }

    public void RaisePlayerTelemetryEvent<T>(GameTelemetryId gameTelemetryId, EntityUid playerUid, T args, bool oneshot = false) where T : notnull
    {
        if (PlayerManager.LocalEntity != playerUid)
            return;
        TelemetryManager.RaiseEvent(gameTelemetryId, args, oneshot);
    }

    public void RaisePlayerTelemetryEvent<T>(GameTelemetryId gameTelemetryId, EntityUid playerUid, ref T args, bool oneshot = false) where T : notnull
    {
        if (PlayerManager.LocalEntity != playerUid)
            return;
        TelemetryManager.RaiseEvent(gameTelemetryId, ref args, oneshot);
    }


    public void RaiseTelemetryEvent<T>(GameTelemetryId gameTelemetryId, T args, bool oneshot = false) where T : notnull
    {
        TelemetryManager.RaiseEvent(gameTelemetryId, args, oneshot);
    }

    public void RaiseTelemetryEvent<T>(GameTelemetryId gameTelemetryId,  ref T args, bool oneshot = false) where T : notnull
    {
        TelemetryManager.RaiseEvent(gameTelemetryId, ref args, oneshot);
    }

    internal bool TryGetTelemetryIds(Type type,[NotNullWhen(true)] out List<GameTelemetryId>? sensorIds)
    {
        return _sensorIds.TryGetValue(type, out sensorIds);
    }

    protected void RegisterId<T>(string name, string category = GameTelemetryManager.DefaultCategory) where T: notnull
    {
        if (!_canReg)
            throw new InvalidOperationException("TelemetryIds can only be registered in DefineIds");
        RegisterId<T>((name, category));
    }

    protected void RegisterId<T>(GameTelemetryId gameTelemetryId, SensorOrigin locality = SensorOrigin.Local) where T: notnull
    {
        TelemetryManager.RegisterSensorId(gameTelemetryId, typeof(T), locality, out _);
        _sensorIds.GetOrNew(typeof(T)).Add(gameTelemetryId);
    }

    protected abstract void DefineTelemetryIds(bool isServer);

    protected override void PostInject()
    {
        //I know this looks cursed, but it's needed to idiot-proof this
        base.PostInject();
        TelemetryManager.RegisterTelemetrySystem(this);
        _canReg = true;
        DefineTelemetryIds(NetManager.IsServer);
        _canReg = false;
    }
}

