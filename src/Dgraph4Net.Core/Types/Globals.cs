using Dgraph4Net;

namespace System
{
    public static class Globals
    {
        public static string GetDType<T>() where T : IEntity, new() =>
            new T().GetDType();
    }
}
