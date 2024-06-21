using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Robust.Shared.GameTelemetry;


public abstract class GameTelemetrySystem : EntitySystem, IPostInjectInit
{
    [Dependency] protected GameTelemetryManager TelemetryManager = default!;
    [Dependency] protected INetManager NetManager = default!;

    private readonly Dictionary<Type,List<GameTelemetryId>> _sensorIds = new();
    private bool _canReg;

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
    protected abstract void DefineIds(bool isServer);

    protected override void PostInject()
    {
        //I know this looks cursed, but it's needed to idiot-proof this
        base.PostInject();
        TelemetryManager.RegisterTelemetrySystem(this);
        _canReg = true;
        DefineIds(NetManager.IsServer);
        _canReg = false;
    }
}

