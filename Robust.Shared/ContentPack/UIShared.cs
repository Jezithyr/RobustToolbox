using System;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Robust.Shared.ContentPack;

public interface IUICallback;

public abstract class UIShared : SharedEntry
{
    public override void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
    {
        //TODO: Hook up broadcast for when UI should get updated!
        //We only care about UI frame updates
        if (level == ModUpdateLevel.UserInterface)
            UIUpdate(frameEventArgs);
    }

    public virtual void UIUpdate(FrameEventArgs frameEventArgs)
    {
    }
}
