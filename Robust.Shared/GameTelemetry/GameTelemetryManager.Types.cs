using System;
using JetBrains.Annotations;
using Robust.Shared.Collections;
using Robust.Shared.Serialization;

namespace Robust.Shared.GameTelemetry;


[Serializable, NetSerializable]
public record struct GameTelemetryId
{
    public readonly string Name;
    public readonly string Category;
    public readonly int Hash;
    GameTelemetryId(string name, string category = GameTelemetryManager.DefaultCategory)
    {
        Name = name;
        Category = category;
        Hash = HashCode.Combine(Name, Category);
    }

    public bool Equals(GameTelemetryId other) => other.Hash == Hash;

    public override int GetHashCode() => Hash;

    public static implicit operator (string, string)(GameTelemetryId id) => (id.Category, id.Name);
    public static implicit operator GameTelemetryId((string,string) id) => new(id.Item2, id.Item1);
    public static implicit operator GameTelemetryId(string id) => new(id);
    public static implicit operator string(GameTelemetryId id) => $"{id.Category}:{id.Name}";
    public override string ToString() => $"{Category}:{Name}";
}

internal sealed record GameSensorHandlerData(
    ValueList<SensorListener> EnabledHandlers,
    ValueList<SensorListener> DisabledHandlers)
{
}

internal struct Unit;
    internal delegate void SensorRefListener(ref Unit ev);

    internal sealed record SensorData
    {
        internal SensorOrigin Origin { get; set; }
        private ValueList<SensorListener> _enabledSensors;
        private ValueList<SensorListener> _disabledSensors;
        public SensorData() {}

        public ref ValueList<SensorListener> EnabledSensors => ref _enabledSensors;
        public ref ValueList<SensorListener> DisabledSensors => ref _disabledSensors;

        public bool SensorDisabled(SensorListener sensor) => _disabledSensors.Contains(sensor);
        public bool SensorEnabled(SensorListener sensor) => _enabledSensors.Contains(sensor);

        public bool Contains(SensorListener handler) =>
            SensorEnabled(handler) ||
            SensorDisabled(handler);

        public bool TrySubscribeSensor(SensorListener sensor, bool enabled = true)
        {
            if (Contains(sensor))
                return false;
            if (enabled)
            {
                _enabledSensors.Add(sensor);
                return true;
            }
            _disabledSensors.Add(sensor);
            return true;
        }
    }

    internal readonly record struct SensorListener(
        SensorOrigin Mask,
        SensorRefListener Listener,
        object EqualityToken)
    {
        public bool Equals(SensorListener other)
        {
            return Mask == other.Mask && Equals(EqualityToken, other.EqualityToken);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Mask, EqualityToken);
        }
    }

public delegate void GameTelemetryRefHandler<T>(GameTelemetryId id,ref T ev) where T : IGameTelemetryArgs, new();

public delegate void GameTelemetryListener<T>(GameTelemetryId id,T ev) where T : IGameTelemetryArgs, new();

[UsedImplicitly]
public interface IGameTelemetryArgs;

[Flags]
public enum SensorOrigin : byte
{
    None = 0,
    Local = 1 << 0,
    Networked = 2 << 0,

    Both = Local | Networked,
}

