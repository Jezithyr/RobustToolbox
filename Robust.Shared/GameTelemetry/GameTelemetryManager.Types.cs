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

    public static implicit operator (string, string)(GameTelemetryId id) => (id.Name, id.Category);
    public static implicit operator GameTelemetryId((string,string) id) => new(id.Item1, id.Item2);
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
        private ValueList<SensorListener> _sensors;
        public SensorData() {}

        public ref ValueList<SensorListener> EnabledSensors => ref _sensors;
        public bool HasSensor(SensorListener sensor) => _sensors.Contains(sensor);
        public bool Triggered { get; set; }

        public bool Contains(SensorListener listener) =>
            _sensors.Contains(listener);


        public bool TrySubscribeSensor(SensorListener sensor)
        {
            if (Contains(sensor))
                return false;
            _sensors.Add(sensor);
            return true;
        }

        public bool TryUnSubscribeSensor(SensorListener sensor)
        {
            for (int index = 0; index < _sensors.Count; index++)
            {
                if (_sensors[index].EqualityToken != sensor.EqualityToken)
                    continue;
                _sensors.RemoveAt(index);
                return true;
            }
            return false;
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

public delegate void GameTelemetryRefHandler<T>(GameTelemetryId id,ref T ev) where T : notnull;

public delegate void GameTelemetryHandler<T>(GameTelemetryId id,T ev) where T : notnull;

[Flags]
public enum SensorOrigin : byte
{
    None = 0,
    Local = 1 << 0,
    Networked = 2 << 0,

    Both = Local | Networked,
}

