using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.NamedEvents;

public sealed partial class NamedEventManager
{
    internal void SubscribeNetHandler<T>(
        NamedEventId id,
        SensorOrigin origin,
        SensorRefListener eventListener,
        object listenerRef,
        bool byRef
    )
        where T : notnull
    {
        //TODO: STUB IMPLEMENT NET EVENTS
    }
}
