// ReSharper disable All
namespace System;

public interface IDefer : IDisposable, IAsyncDisposable;

public delegate Defered<T> Defered<T>(T value);

public class Deferer(Action action, bool ignoreErrors = false) : IDefer
{
    private Task Run() => Task.Run(action).ContinueWith(tsk =>
    {
        if (tsk.IsFaulted && !ignoreErrors)
        {
            throw tsk.Exception;
        }
    });

    public void Dispose()
    {
        Run().Wait();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await Run().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

public static class Defer
{
    public static IDefer defer(Action action, bool ignoreErrors = false) => new Deferer(action, ignoreErrors);

    public static IDefer defer(Func<Task> func, bool ignoreErrors = false) => new Deferer(async () => await Task.Run(func), ignoreErrors);

    public static IDefer defer(Func<ValueTask> func, bool ignoreErrors = false) => new Deferer(async () => await Task.Run(func), ignoreErrors);
}
