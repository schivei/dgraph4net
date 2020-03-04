using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Dgraph4Net.Tests
{

    public class Assert : Xunit.Assert
    {
        public static void Json(string expected, string actual, string message = null)
        {
            var assert = JToken.DeepEquals(JToken.Parse(expected), JToken.Parse(actual));

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

        public static async Task TaskAsync(Func<Task> act)
        {
            try
            {
                await act.Invoke();
                True(true);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                True(false, ex.Message);
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public static Task TaskAsync<T>(Func<T, Task> act, T obj) =>
            TaskAsync(() => act.Invoke(obj));

        public static Task TaskAsync<T, TE>(Func<T, TE, Task> act, T obj, TE obj2) =>
            TaskAsync(() => act.Invoke(obj, obj2));
    }
}
