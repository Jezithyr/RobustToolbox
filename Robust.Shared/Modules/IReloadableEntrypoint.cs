namespace Robust.Shared.Modules;

//interface for broadcasting reload events to reloadable assemblies
public interface IReloadableEntrypoint
{
    public void ReloadedPreInit();
    public void ReloadedInit();
    public void ReloadedPostInit();
}

//opt-in interface for broadcasting reload events to any assembly
public interface IReloadListenerEntryPoint
{
    public virtual void LiveReloadTriggered()
    {
    }

    public virtual void LiveReloadCompleted()
    {
    }
}

