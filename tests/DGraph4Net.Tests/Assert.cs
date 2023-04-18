using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dgraph4Net.Tests;


public class Assert : Xunit.Assert
{
    public static void Json(string expected, string actual, string message = null)
    {
        var expectedDoc = JsonDocument.Parse(expected).RootElement;
        var actualDoc = JsonDocument.Parse(actual).RootElement;
        // deep equals
        var assert = expectedDoc.Equals(actualDoc);

        if (message is null)
            True(assert);
        else
            True(assert, message);
    }

    public static void Void(Action act)
    {
        try
        {
            act.Invoke();
            True(true);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            True(false, ex.Message);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public static async Task TaskAsync(Func<Task> act, string msg = null)
    {
        try
        {
            await act.Invoke();
            True(true);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            if (msg is null)
                True(false, ex.Message);
            else
                True(false, $"{msg}: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public static Task TaskAsync<T>(Func<T, Task> act, T obj, string msg = null) =>
        TaskAsync(() => act.Invoke(obj), msg);

    public static Task TaskAsync<T, TE>(Func<T, TE, Task> act, T obj, TE obj2, string msg = null) =>
        TaskAsync(() => act.Invoke(obj, obj2), msg);
}
