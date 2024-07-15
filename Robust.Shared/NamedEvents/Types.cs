using System;
using JetBrains.Annotations;
using Robust.Shared.Collections;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Robust.Shared.NamedEvents;


[Serializable, NetSerializable]
public record struct NamedEventId
{
    [DataField(readOnly:true)]
    public readonly string Name;
    [DataField(readOnly:true)]
    public readonly string Category;
    [DataField(readOnly:true)]
    public readonly int Hash;
    NamedEventId(string name, string category = SharedNamedEventManager.DefaultCategory)
    {
        Name = name;
        Category = category;
        Hash = HashCode.Combine(Name, Category);
    }

    public bool Equals(NamedEventId other) => other.Hash == Hash;

    public override int GetHashCode() => Hash;

    public static implicit operator (string, string)(NamedEventId id) => (id.Name, id.Category);
    public static implicit operator NamedEventId((string,string) id) => new(id.Item1, id.Item2);
    public static implicit operator NamedEventId(string id) => new(id);
    public static implicit operator string(NamedEventId id) => $"{id.Category}:{id.Name}";
    public override string ToString() => $"{Category}:{Name}";
}

internal sealed record GameSensorHandlerData(
    ValueList<Subscription> EnabledHandlers,
    ValueList<Subscription> DisabledHandlers)
{
}

internal struct Unit;
    internal delegate void SubscriptionListenerRef(ref Unit ev);

    internal sealed record SubscriptionData
    {
        internal Origin AllowedOrigins { get; set; }
        private ValueList<Subscription> _subscriptions;
        public SubscriptionData()
        {
        }

        public ref ValueList<Subscription> EnabledSubscriptions => ref _subscriptions;
        public bool HasSubscription(Subscription sensor) => _subscriptions.Contains(sensor);
        public bool Triggered { get; set; }

        public bool Contains(Subscription listener) =>
            _subscriptions.Contains(listener);


        internal bool TryAddSubscription(Subscription sensor)
        {
            if (Contains(sensor))
                return false;
            _subscriptions.Add(sensor);
            return true;
        }

        internal bool TryRemoveSubscription(object equalityToken)
        {
            for (int index = 0; index < _subscriptions.Count; index++)
            {
                if (_subscriptions[index].EqualityToken != equalityToken)
                    continue;
                _subscriptions.RemoveAt(index);
                return true;
            }
            return false;
        }
    }

    internal readonly record struct Subscription(
        Origin Mask,
        SubscriptionListenerRef Listener,
        object EqualityToken)
    {
        public bool Equals(Subscription other)
        {
            return Mask == other.Mask && Equals(EqualityToken, other.EqualityToken);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Mask, EqualityToken);
        }
    }

public delegate void NamedEventRefHandler<T>(NamedEventId id,ref T ev) where T : notnull;

public delegate void NamedEventHandler<T>(NamedEventId id,T ev) where T : notnull;

[Flags]
public enum Origin : byte
{
    None = 0,
    Local = 1 << 0,
    Networked = 1 << 2,
    Both = Local | Networked,
}

