using System;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Collections.Generic;

namespace Robust.Shared.GameObjects;

public abstract partial class MetaDataSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private EntityPausedEvent _pausedEvent;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private Dictionary<Guid, EntityUid> _guidLookup = new();

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        SubscribeLocalEvent<MetaDataComponent, ComponentHandleState>(OnMetaDataHandle);
        SubscribeLocalEvent<MetaDataComponent, ComponentGetState>(OnMetaDataGetState);
        EntityManager.AfterEntityFlush += PostEntityFlush;
    }

    private void PostEntityFlush()
    {
        _guidLookup.Clear();
    }

    private void OnMetaDataGetState(EntityUid uid, MetaDataComponent component, ref ComponentGetState args)
    {
        args.State = new MetaDataComponentState(component._entityName, component._entityDescription, component._entityPrototype?.ID, component.PauseTime, component.Guid);
    }

    private void OnMetaDataHandle(EntityUid uid, MetaDataComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MetaDataComponentState state)
            return;

        component._entityName = state.Name;
        component._entityDescription = state.Description;
        component.Guid = state.Guid;

        if(state.PrototypeId != null && state.PrototypeId != component._entityPrototype?.ID)
            component._entityPrototype = ProtoMan.Index<EntityPrototype>(state.PrototypeId);

        component.PauseTime = state.PauseTime;
    }

    public bool TryGetEntityByGuid(Guid guid, out EntityUid entity)
    {
        return _guidLookup.TryGetValue(guid, out entity);
    }

    public EntityUid GetEntityByGuid(Guid guid)
    {
        return !TryGetEntityByGuid(guid, out var ent) ? throw new KeyNotFoundException() : ent;
    }

    public void RemoveEntityGuid(Entity<MetaDataComponent?> entity, bool raiseEvents = true)
    {
        if (!_metaQuery.Resolve(entity, ref entity.Comp) || !entity.Comp.HasGuid)
            return;
        entity.Comp.Guid = Guid.Empty;
        _guidLookup.Remove(entity.Comp.Guid);
        if (raiseEvents)
        {
            var ev = new EntityGuidRemovedEvent(entity, entity.Comp.Guid);
            RaiseLocalEvent(entity, ref ev);
            var changedEv = new EntityGuidChangedEvent(entity, entity.Comp.Guid, Guid.Empty);
            RaiseLocalEvent(entity, ref changedEv);
        }
        SetFlag(entity, MetaDataFlags.HasGuid, false);
        Dirty(entity, entity.Comp, entity.Comp);
    }

    private void AddEntityGuid(Entity<MetaDataComponent?> entity, Guid newGuid, bool raiseEvents = true, bool errorIfUsed = true)
    {
        if (!_metaQuery.Resolve(entity, ref entity.Comp) || entity.Comp.Guid.Equals(newGuid))
            return;
        DebugTools.Assert(!newGuid.Equals(Guid.Empty));
        entity.Comp.Guid = newGuid;
        if (_guidLookup.TryAdd(newGuid, entity))
        {
            if (errorIfUsed) Log.Error($"Guid:{newGuid} is already in use!");
            return;
        }
        if (raiseEvents)
        {
            var ev = new EntityGuidAddedEvent(entity, entity.Comp.Guid);
            RaiseLocalEvent(entity, ref ev);
            var changedEv = new EntityGuidChangedEvent(entity, newGuid, entity.Comp.Guid);
            RaiseLocalEvent(entity, ref changedEv);
        }
        SetFlag(entity, MetaDataFlags.HasGuid, true);
        Dirty(entity, entity.Comp, entity.Comp);
    }

    public void SetEntityGuid(Entity<MetaDataComponent?> entity, Guid newGuid, bool raiseEvents = true, bool errorIfUsed = true)
    {
        if (newGuid.Equals(Guid.Empty))
        {
            RemoveEntityGuid(entity, raiseEvents);
            return;
        }
        if (!_metaQuery.Resolve(entity, ref entity.Comp) || newGuid.Equals(entity.Comp.Guid) || newGuid.Equals( entity.Comp.Guid))
            return;
        if (entity.Comp.Guid.Equals(Guid.Empty))
        {
            AddEntityGuid(entity, newGuid, raiseEvents);
            return;
        }
        if (_guidLookup.TryAdd(newGuid, entity))
        {
            if (errorIfUsed) Log.Error($"Guid:{newGuid} is already in use!");
            return;
        }
        var oldGuid = entity.Comp.Guid;
        entity.Comp.Guid = newGuid;
        _guidLookup.Remove(oldGuid);
        if (raiseEvents)
        {
            var ev = new EntityGuidChangedEvent(entity, oldGuid, newGuid);
            RaiseLocalEvent(entity, ref ev, true);
        }
        SetFlag(entity, MetaDataFlags.HasGuid, !newGuid.Equals(Guid.Empty) && newGuid.Equals(entity.Comp.Guid));

        Dirty(entity, entity.Comp, entity.Comp);
    }

    public void SetEntityName(EntityUid uid, string value, MetaDataComponent? metadata = null, bool raiseEvents = true)
    {
        if (!_metaQuery.Resolve(uid, ref metadata) || value.Equals(metadata.EntityName))
            return;

        var oldName = metadata.EntityName;

        metadata._entityName = value;

        if (raiseEvents)
        {
            var ev = new EntityRenamedEvent(uid, oldName, value);
            RaiseLocalEvent(uid, ref ev, true);
        }

        Dirty(uid, metadata, metadata);
    }

    public void SetEntityDescription(EntityUid uid, string value, MetaDataComponent? metadata = null)
    {
        if (!_metaQuery.Resolve(uid, ref metadata) || value.Equals(metadata.EntityDescription))
            return;

        metadata._entityDescription = value;
        Dirty(uid, metadata, metadata);
    }

    internal void SetEntityPrototype(EntityUid uid, EntityPrototype? value, MetaDataComponent? metadata = null)
    {
        if (!_metaQuery.Resolve(uid, ref metadata) || value?.Equals(metadata._entityPrototype) == true)
            return;

        // The ID string should never change after an entity has been created.
        // Otherwise this breaks networking in multiplayer games.
        DebugTools.Assert(value?.ID == metadata._entityPrototype?.ID);

        metadata._entityPrototype = value;
    }

    public bool EntityPaused(EntityUid uid, MetaDataComponent? metadata = null)
    {
        if (!_metaQuery.Resolve(uid, ref metadata))
            return true;

        return metadata.EntityPaused;
    }

    public void SetEntityPaused(EntityUid uid, bool value, MetaDataComponent? metadata = null)
    {
        if (!_metaQuery.Resolve(uid, ref metadata)) return;

        if (metadata.EntityPaused == value) return;

        if (value)
        {
            DebugTools.Assert(metadata.PauseTime == null);
            metadata.PauseTime = _timing.CurTime;
            RaiseLocalEvent(uid, ref _pausedEvent);
        }
        else
        {
            DebugTools.Assert(metadata.PauseTime != null);
            var ev = new EntityUnpausedEvent(_timing.CurTime - metadata.PauseTime!.Value);
            metadata.PauseTime = null;
            RaiseLocalEvent(uid, ref ev);
        }

        Dirty(uid, metadata, metadata);
    }

    /// <summary>
    /// Gets how long this entity has been paused.
    /// </summary>
    public TimeSpan GetPauseTime(EntityUid uid, MetaDataComponent? metadata = null)
    {
        if (!_metaQuery.Resolve(uid, ref metadata))
            return TimeSpan.Zero;

        return (_timing.CurTime - metadata.PauseTime) ?? TimeSpan.Zero;
    }

    /// <summary>
    /// Offsets the specified time by how long the entity has been paused.
    /// </summary>
    public void PauseOffset(EntityUid uid, ref TimeSpan time, MetaDataComponent? metadata = null)
    {
        var paused = GetPauseTime(uid, metadata);
        time += paused;
    }

    public void SetFlag(Entity<MetaDataComponent?> entity, MetaDataFlags flags, bool enabled)
    {
        if (!_metaQuery.Resolve(entity, ref entity.Comp))
            return;

        if (enabled)
            entity.Comp.Flags |= flags;
        else
            RemoveFlag(entity, flags, entity.Comp);
    }

    public void AddFlag(EntityUid uid, MetaDataFlags flags, MetaDataComponent? comp = null)
        => SetFlag((uid, comp), flags, true);

    /// <summary>
    /// Attempts to remove the specific flag from metadata.
    /// Other systems can choose not to allow the removal if it's still relevant.
    /// </summary>
    public void RemoveFlag(EntityUid uid, MetaDataFlags flags, MetaDataComponent? component = null)
    {
        if (!_metaQuery.Resolve(uid, ref component))
            return;

        var toRemove = component.Flags & flags;
        if (toRemove == 0x0)
            return;

        // TODO PERF
        // does this need to be a broadcast event?
        var ev = new MetaFlagRemoveAttemptEvent(toRemove);
        RaiseLocalEvent(uid, ref ev, true);

        component.Flags &= ~ev.ToRemove;
    }
}

/// <summary>
/// Raised if <see cref="MetaDataSystem"/> is trying to remove a particular flag.
/// </summary>
[ByRefEvent]
public struct MetaFlagRemoveAttemptEvent
{
    public MetaDataFlags ToRemove;

    public MetaFlagRemoveAttemptEvent(MetaDataFlags toRemove)
    {
        ToRemove = toRemove;
    }
}
