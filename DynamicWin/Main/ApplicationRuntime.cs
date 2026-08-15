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
        catch (Exception startupException)
        {
            try
            {
                StopStartedComponents();
            }
            catch (AggregateException cleanupException)
            {
                var exceptions = new List<Exception> { startupException };
                exceptions.AddRange(cleanupException.InnerExceptions);
                throw new AggregateException(exceptions);
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        try
        {
            StopStartedComponents();
        }
        finally
        {
            disposed = true;
        }
    }

    private void StopStartedComponents()
    {
        List<Exception>? exceptions = null;
        for (var index = startedComponents.Count - 1; index >= 0; index--)
        {
            try
            {
                startedComponents[index].Stop();
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        startedComponents.Clear();

        if (exceptions is not null)
            throw new AggregateException(exceptions);
    }
}

internal sealed class DelegateApplicationComponent(Action start, Action stop) : IApplicationComponent
{
    public void Start() => start();

    public void Stop() => stop();
}
