using System;
using JetBrains.Annotations;
using Robust.Shared.Collections;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;

namespace Robust.Shared.GameSensing;


[Serializable, NetSerializable]
public record struct GameSensorId
{
    public readonly string Name;
    public readonly string Category;
    public readonly int Hash;
    GameSensorId(string name, string category = GameSensingManager.DefaultCategory)
    {
        Name = name;
        Category = category;
        Hash = HashCode.Combine(Name, Category);
    }

    public bool Equals(GameSensorId other) => other.Hash == Hash;

    public override int GetHashCode() => Hash;

    public static implicit operator (string, string)(GameSensorId id) => (id.Category, id.Name);
    public static implicit operator GameSensorId((string,string) id) => new(id.Item2, id.Item1);
    public static implicit operator GameSensorId(string id) => new(id);
    public static implicit operator string(GameSensorId id) => $"{id.Category}:{id.Name}";
    public override string ToString() => $"{Category}:{Name}";
}

internal sealed record GameSensorHandlerData(
    ValueList<GameSensingManager.BroadcastRegistration> EnabledHandlers,
    ValueList<GameSensingManager.BroadcastRegistration> DisabledHandlers)
{
}

public delegate void GameSensorRefHandler<T>(ref T ev) where T : IGameSensorArgs, new();

public delegate void GameSensorHandler<T>(T ev) where T : IGameSensorArgs, new();

[UsedImplicitly]
public interface IGameSensorArgs;

[Flags]
public enum GameSensorSource : byte
{
    None = 0,
    Client = 1 << 0,
    Server = 2 << 0,

    Both = Client | Server,
}

