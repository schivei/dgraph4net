using System;
using System.Threading.Tasks;

namespace Dgraph4Net.Tests;


public class Assert : Xunit.Assert
{
    public static void Void(Action act)
    {
        try
        {
            act.Invoke();
            True(true);
        }
        catch (Exception ex)
        {
            True(false, ex.Message);
        }
    }

    public static async Task TaskAsync(Func<Task> act, string msg = null)
    {
        try
        {
            await act.Invoke();
            True(true);
        }
        catch (Exception ex)
        {
            if (msg is null)
                True(false, ex.Message);
            else
                True(false, $"{msg}: {ex.Message}");
        }
    }

    public static Task TaskAsync<T>(Func<T, Task> act, T obj, string msg = null) =>
        TaskAsync(() => act.Invoke(obj), msg);

    public static Task TaskAsync<T, TE>(Func<T, TE, Task> act, T obj, TE obj2, string msg = null) =>
        TaskAsync(() => act.Invoke(obj, obj2), msg);
}
