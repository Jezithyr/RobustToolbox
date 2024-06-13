using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Robust.Shared.GameTelemetry;


public abstract class GameTelemetryController
{
    [Dependency] protected GameTelemetryManager TelemetryManager = default!;
    [Dependency] protected INetManager NetManager = default!;
    [Dependency] protected ILogManager LogManager  = default!;

    protected ISawmill Sawmill = default!;

    private readonly Dictionary<Type,List<GameTelemetryId>> _sensorIds = new();

    internal bool TryGetTelemetryIds(Type type,[NotNullWhen(true)] out List<GameTelemetryId>? sensorIds)
    {
        return _sensorIds.TryGetValue(type, out sensorIds);
    }
    internal void Initialize()
    {
        Sawmill = LogManager.GetSawmill($"{GameTelemetryManager.LogName}.{GetType()}");
        LoadIds(NetManager.IsServer);
    }


    protected void RegisterId<T>(string name, string category = GameTelemetryManager.DefaultCategory) where T: notnull
    {
        RegisterId<T>((name, category));
    }

    protected void RegisterId<T>(GameTelemetryId gameTelemetryId, SensorOrigin locality = SensorOrigin.Local) where T: notnull
    {
        TelemetryManager.RegisterSensorId(gameTelemetryId, typeof(T), locality, out _);
        _sensorIds.GetOrNew(typeof(T)).Add(gameTelemetryId);
    }
    protected abstract void LoadIds(bool isServer);
}

