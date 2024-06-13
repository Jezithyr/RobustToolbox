using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.GameSensing;

public sealed partial class GameSensorManager
{
    internal void SubscribeNetSensor<T>(
        SensorId id,
        SensorOrigin origin,
        SensorRefListener eventListener,
        bool byRef,
        bool startEnabled
    )
        where T : ISensorArgs, new()
    {
        //TODO: STUB IMPLEMENT NET EVENTS
    }
}
