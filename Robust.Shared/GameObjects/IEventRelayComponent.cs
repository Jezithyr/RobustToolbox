using System;
using System.Collections.Generic;

namespace Robust.Shared.GameObjects;

public interface IEventRelayComponent<TSelf>
    where TSelf : Component, IEventRelayComponent<TSelf>
{
    public IReadOnlyCollection<EntityUid> GetChildren { get; }

    public static virtual void ForwardEvent<TEvent>(Entity<TSelf> self, IEntityManager entityManager, TEvent args, bool broadcast = false)
        where TEvent : notnull
    {
        foreach (var ent in self.Comp.GetChildren)
        {
            entityManager.EventBus.RaiseLocalEvent(ent, args, broadcast);
        }
    }

    public static virtual void ForwardEvent<TEvent>(Entity<TSelf> self, IEntityManager entityManager, ref TEvent args, bool broadcast = false)
        where TEvent : notnull
    {
        foreach (var ent in self.Comp.GetChildren)
        {
            entityManager.EventBus.RaiseLocalEvent(ent,ref args, broadcast);
        }
    }

    public static virtual void RelayEvent<TEvent>(Entity<TSelf> self, IEntityManager entityManager,  TEvent args, bool broadcast = false)
        where TEvent : notnull
    {
        var ev = new RelayedEvent<TEvent>(entityManager, self, args);
        TSelf.ForwardEvent(self, entityManager, ev, broadcast);
    }

    public static virtual void RelayEvent<TEvent>(Entity<TSelf> self, IEntityManager entityManager, ref TEvent args, bool broadcast = false)
        where TEvent : notnull
    {
        var ev = new RelayedEvent<TEvent>(entityManager, self, args);
        TSelf.ForwardEvent(self, entityManager, ref ev, broadcast);
        args = ev.Args;
    }

    public static virtual void RelayEvent<TParentComp,TEvent>(Entity<TSelf,TParentComp> self, IEntityManager entityManager,  TEvent args, bool broadcast = false)
        where TEvent : notnull where TParentComp : IComponent?
    {
        var ev = new RelayedEvent<TParentComp,TEvent>(entityManager, self, args);
        TSelf.ForwardEvent(self, entityManager, ev, broadcast);
    }

    public static virtual void RelayEvent<TParentComp,TEvent>(Entity<TSelf, TParentComp> self, IEntityManager entityManager, ref TEvent args, bool broadcast = false)
        where TEvent : notnull where TParentComp : IComponent?
    {
        var ev = new RelayedEvent<TParentComp,TEvent>(entityManager, self, args);
        TSelf.ForwardEvent(self, entityManager, ref ev, broadcast);
        args = ev.Args;
    }

    public static virtual void SubscribeRelayEvent<TComp, TEvent>(
        IEntityManager entityManager,
        RelayedEventHandler<TComp, TEvent> handler)
        where TComp : IComponent where TEvent : notnull
    {
        entityManager.EventBus.EnsureLocalEvent<TSelf, TEvent>((ent, ref args) =>
        {
            TSelf.RelayEvent(ent, entityManager, ref args);
        });

        entityManager.EventBus.SubscribeLocalEvent<TComp, RelayedEvent<TEvent>>((uid, component, args) =>
        {
            handler.Invoke(args.Parent, (uid,component), args.Args);
        });
    }

    public static virtual void SubscribeRelayEvent<TComp, TEvent>(
        IEntityManager entityManager,
        RelayedEventRefHandler<TComp, TEvent> handler)
        where TComp : IComponent where TEvent : notnull
    {
        entityManager.EventBus.EnsureLocalEvent<TSelf, TEvent>((ent, ref args) =>
        {
            TSelf.RelayEvent(ent, entityManager, ref args);
        });

        entityManager.EventBus.SubscribeLocalEvent<TComp, RelayedEvent<TEvent>>((uid, component, ref args) =>
        {
            var tempArgs = args.Args;
            handler.Invoke(args.Parent, (uid,component), ref tempArgs);
            args.Args = tempArgs;
        });
    }

    public static virtual void SubscribeRelayEvent<TParentComp,TComp, TEvent>(
        IEntityManager entityManager,
        RelayedEventHandler<TParentComp,TComp, TEvent> handler)
        where TParentComp : IComponent
        where TComp : IComponent
        where TEvent : notnull
    {
        entityManager.EventBus.EnsureLocalEvent<TSelf, TEvent>((ent, ref args) =>
        {
            if (!entityManager.TryGetComponent(ent, out TParentComp? parentComp))
                return;
            TSelf.RelayEvent<TParentComp, TEvent>((ent, ent.Comp, parentComp), entityManager, args);
        });

        entityManager.EventBus.SubscribeLocalEvent<TComp, RelayedEvent<TParentComp,TEvent>>((uid, component, ref args) =>
        {
            handler.Invoke(args.Parent, (uid,component), args.Args);
        });
    }

    public static virtual void SubscribeRelayEvent<TParentComp,TComp, TEvent>(
        IEntityManager entityManager,
        RelayedEventRefHandler<TParentComp, TComp,TEvent> handler)
        where TParentComp : IComponent
        where TComp : IComponent
        where TEvent : notnull
    {
        entityManager.EventBus.EnsureLocalEvent<TSelf, TEvent>((ent, ref args) =>
        {
            if (!entityManager.TryGetComponent(ent, out TParentComp? parentComp))
                return;
            TSelf.RelayEvent<TParentComp, TEvent>((ent, ent.Comp, parentComp), entityManager, ref args);
        });

        entityManager.EventBus.SubscribeLocalEvent<TComp, RelayedEvent<TParentComp,TEvent>>((uid, component, ref args) =>
        {
            var tempArgs = args.Args;
            handler.Invoke(args.Parent, (uid,component), ref tempArgs);
            args.Args = tempArgs;
        });
    }


    public delegate void RelayedEventHandler<TComp, in TEvent>(
        Entity<TSelf> relayParent,
        Entity<TComp> entity,
        TEvent args)
        where TComp : IComponent
        where TEvent : notnull;

    public delegate void RelayedEventRefHandler<TComp, TEvent>(
        Entity<TSelf> relayParent,
        Entity<TComp> entity,
        ref TEvent args)
        where TComp : IComponent
        where TEvent : notnull;

    public delegate void RelayedEventHandler<TParentComp, TComp, in TEvent>(
        Entity<TSelf, TParentComp> relayParent,
        Entity<TComp> entity,
        TEvent args)
        where TComp : IComponent
        where TEvent : notnull
        where TParentComp : IComponent?;

    public delegate void RelayedEventRefHandler< TParentComp, TComp, TEvent>(Entity<TSelf, TParentComp> relayParent,Entity<TComp> entity, ref TEvent args)
        where TComp : IComponent
        where TEvent : notnull
        where TParentComp : IComponent?;


    [ByRefEvent]
    internal record struct RelayedEvent<TEvent>(IEntityManager EntityManager, Entity<TSelf> Parent, TEvent Args) : IRelayEvent<RelayedEvent<TEvent>, TEvent>
        where TEvent : notnull;

    [ByRefEvent]
    internal record struct RelayedEvent<TParentComp, TEvent>(IEntityManager EntityManager, Entity<TSelf, TParentComp> Parent, TEvent Args): IRelayEvent<RelayedEvent<TParentComp, TEvent>, TParentComp, TEvent>
        where TParentComp: IComponent? where TEvent : notnull;


    internal interface IRelayEvent<TEventSelf, TEvent>
        where TEventSelf: IRelayEvent<TEventSelf, TEvent>
        where TEvent: notnull
    {
        public IEntityManager EntityManager { get; }
        public Entity<TSelf> Parent { get; }
        public TEvent Args { get; set; }

        internal void SubscribeLocal<TComp>(IEntityManager entityManager,RelayedEventHandler<TComp, TEvent> handler, Type orderType, Type[]? before = null, Type[]? after = null) where TComp : IComponent
        {

            entityManager.EventBus.SubscribeLocalEvent<TComp, RelayedEvent<TEvent>>((parent, ref args) =>
            {
                args.EntityManager.EventBus.RaiseLocalEvent(parent, args.Args);
            },
                orderType,
                before,
                after);
        }
    }

    internal interface IRelayEvent<TEventSelf, TParentComp,TEvent>
        where TEventSelf: IRelayEvent<TEventSelf, TParentComp,TEvent>
        where TEvent: notnull
        where TParentComp : IComponent?
    {
        public IEntityManager EntityManager { get; }
        public Entity<TSelf, TParentComp> Parent { get; }
        public TEvent Args { get; set; }
    }
}
