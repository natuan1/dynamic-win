namespace DynamicWin.Main;

internal interface IApplicationComponent
{
    void Start();
    void Stop();
}

internal sealed class ApplicationRuntime(params IApplicationComponent[] components) : IDisposable
{
    private readonly List<IApplicationComponent> startedComponents = [];
    private bool disposed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            foreach (var component in components)
            {
                component.Start();
                startedComponents.Add(component);
            }
        }
        catch
        {
            StopStartedComponents();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        StopStartedComponents();
        disposed = true;
    }

    private void StopStartedComponents()
    {
        for (var index = startedComponents.Count - 1; index >= 0; index--)
            startedComponents[index].Stop();

        startedComponents.Clear();
    }
}

internal sealed class DelegateApplicationComponent(Action start, Action stop) : IApplicationComponent
{
    public void Start() => start();

    public void Stop() => stop();
}
