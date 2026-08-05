namespace Shiny.Infrastructure;

public interface IMainThread
{
    Task InvokeOnMainThreadAsync(Action action);
    Task InvokeOnMainThreadAsync(Func<Task> func);
    Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func);
    void BeginInvokeOnMainThread(Action action);
}

public class MauiMainThread : IMainThread
{
    public Task InvokeOnMainThreadAsync(Action action)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            action.Invoke();
            return Task.CompletedTask;
        }
        else
        {
            return MainThread.InvokeOnMainThreadAsync(action);
        }
    }

    public Task InvokeOnMainThreadAsync(Func<Task> func)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return func.Invoke();
        }
        else
        {
            return MainThread.InvokeOnMainThreadAsync(func);
        }
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return func.Invoke();
        }
        else
        {
            return MainThread.InvokeOnMainThreadAsync(func);
        }
    }

    public void BeginInvokeOnMainThread(Action action)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            action.Invoke();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}

// MACOS
// public static bool IsMainThread => NSThread.Current.IsMainThread;
//
// public static void BeginInvokeOnMainThread(Action action)
// {
//     if (IsMainThread)
//         action();
//     else
//         DispatchQueue.MainQueue.DispatchAsync(action);
// }
