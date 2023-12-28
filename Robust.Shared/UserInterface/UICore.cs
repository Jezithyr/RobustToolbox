using Robust.Shared.IoC;

namespace Robust.Shared.UserInterface;

public interface IUICallback;

public abstract class UICore
{
    public abstract void RegisterIoC(IDependencyCollection deps);

    public abstract void Initialize();

    public abstract void PostInit();

    public abstract void Unload();

}
