using System;
using JetBrains.Annotations;
using Robust.Shared.Collections;
using Robust.Shared.Serialization;

namespace Robust.Shared.GameSensing;


[Serializable, NetSerializable]
public record struct SensorId
{
    public readonly string Name;
    public readonly string Category;
    public readonly int Hash;
    SensorId(string name, string category = GameSensorManager.DefaultCategory)
    {
        Name = name;
        Category = category;
        Hash = HashCode.Combine(Name, Category);
    }

    public bool Equals(SensorId other) => other.Hash == Hash;

    public override int GetHashCode() => Hash;

    public static implicit operator (string, string)(SensorId id) => (id.Category, id.Name);
    public static implicit operator SensorId((string,string) id) => new(id.Item2, id.Item1);
    public static implicit operator SensorId(string id) => new(id);
    public static implicit operator string(SensorId id) => $"{id.Category}:{id.Name}";
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

public delegate void GameSensorRefHandler<T>(ref T ev) where T : ISensorArgs, new();

public delegate void GameSensorListener<T>(T ev) where T : ISensorArgs, new();

[UsedImplicitly]
public interface ISensorArgs;

[Flags]
public enum SensorOrigin : byte
{
    None = 0,
    Local = 1 << 0,
    Networked = 2 << 0,

    Both = Local | Networked,
}

