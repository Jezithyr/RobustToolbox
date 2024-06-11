using System;
using System.Collections.Generic;
using Robust.Shared.IoC;

namespace Robust.Shared.GameTelemetry;


[Access(typeof(GameTelemetryManager))]
public abstract class GameTelemetryHandler : IPostInjectInit
{
    public virtual bool AutoListen => true;
    public List<Type> SupportedTelemetryTypes = new();

    protected Dictionary<Type,HashSet<GameTelemetryId>> ListeningToIds = new();

    public virtual void PostInject()
    {
    }

    public bool IsListeningToId<T>(GameTelemetryId telemetryId) where T: struct, IGameTelemetryType
    {
        return ListeningToIds.TryGetValue(typeof(T), out var teleSet) && teleSet.Contains(telemetryId);
    }

    public bool TryAddListenToId<T>(
        GameTelemetryId telemetryId) where T : struct, IGameTelemetryType
    {
        if (!SupportedTelemetryTypes.Contains(typeof(T))
            || (ListeningToIds.TryGetValue(typeof(T), out var teleSet) && teleSet.Contains(telemetryId)))
            return false;
        if (teleSet == null)
        {
            teleSet = new HashSet<GameTelemetryId>();
            ListeningToIds.Add(typeof(T), teleSet);
        }
        teleSet.Add(telemetryId);
        return true;
    }

    public bool TryRemoveListenFromId<T>(
        GameTelemetryId telemetryId) where T : struct, IGameTelemetryType
    {
        if (!SupportedTelemetryTypes.Contains(typeof(T))
            || (ListeningToIds.TryGetValue(typeof(T), out var teleSet) && !teleSet.Contains(telemetryId))
            || teleSet == null)
            return false;
        teleSet.Remove(telemetryId);
        return true;
    }

}

public interface IGameTelemetryHandler;
public interface IGameTelemetryHandler<T> : IGameTelemetryHandler where T: struct, IGameTelemetryType
{

    public void HandleTelemetryArgs(T args);
}
