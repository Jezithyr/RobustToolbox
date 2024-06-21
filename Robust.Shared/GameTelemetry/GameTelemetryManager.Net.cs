using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.GameTelemetry;

public sealed partial class GameTelemetryManager
{
    internal void SubscribeNetHandler<T>(
        GameTelemetryId id,
        SensorOrigin origin,
        SensorRefListener eventListener,
        bool byRef,
        bool startEnabled
    )
        where T : notnull
    {
        //TODO: STUB IMPLEMENT NET EVENTS
    }
}
