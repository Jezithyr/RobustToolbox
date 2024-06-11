using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Robust.Shared.GameTelemetry;


[Serializable, NetSerializable, DataRecord]
public record struct GameTelemetryId(
    string Name,
    string Category = GameTelemetryManager.DefaultCategoryName)
{
    public static implicit operator (string,string)(GameTelemetryId id) => (id.Name, id.Category);
    public static implicit operator GameTelemetryId((string,string) id) => new(id.Item1, id.Item2);
    public static implicit operator GameTelemetryId(string id) => new(id);


    public override string ToString()
    {
        return $"{Category}:{Name}";
    }
}

public delegate void GameTelemetryDelegate<in T>(T args) where T :struct, IGameTelemetryType;


public interface IGameTelemetryType
{
    public GameTelemetryId TelemetryId { get; set; }
    public EntityUid? Origin { get; set; }
    public ICollection<EntityUid>? AffectedEntities { get; }
};

public record struct GameTelemetryArgs(
    EntityUid? Origin,
    ICollection<EntityUid>? AffectedEntities) : IGameTelemetryType
{
    public GameTelemetryId TelemetryId { get; set; } = default;
}

public abstract class GameTelemetrySubscriberAttribute(params Type[] telemetryTypes) : Attribute
{
    public Type[] TelemetryTypes = telemetryTypes;
};

[Access(typeof(GameTelemetryManager))]
public abstract class GameTelemetryConfig
{
    private GameTelemetryManager _telemetryManager = default!;
    public void Initialize(GameTelemetryManager manager)
    {
        _telemetryManager = manager;
        RunConfig();
    }

    protected abstract void RunConfig();

    protected void RegId<T> (GameTelemetryId telemetryId) where T:struct, IGameTelemetryType
    {
        _telemetryManager.RegisterTelemetryId<T>(telemetryId);
    }

    protected void RemoveId<T> (GameTelemetryId telemetryId) where T:struct, IGameTelemetryType
    {
        _telemetryManager.RemoveTelemetryId<T>(telemetryId);
    }
}
