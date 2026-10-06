using System;

namespace Robust.Shared.GameObjects;



/// <summary>
/// Raised directed on an entity when its guid is changed.
/// Contains the EntityUid as systems may need to subscribe to it without targeting a specific component.
/// </summary>
[ByRefEvent]
public readonly record struct EntityGuidChangedEvent(EntityUid Uid, Guid OldGuid, Guid NewGuid);

/// <summary>
/// Raised directed on an entity when its guid is removed.
/// Contains the EntityUid as systems may need to subscribe to it without targeting a specific component.
/// </summary>
[ByRefEvent]
public readonly record struct EntityGuidRemovedEvent(EntityUid Uid, Guid Guid);

/// <summary>
/// Raised directed on an entity when its guid is removed.
/// Contains the EntityUid as systems may need to subscribe to it without targeting a specific component.
/// </summary>
[ByRefEvent]
public readonly record struct EntityGuidAddedEvent(EntityUid Uid, Guid Guid);
