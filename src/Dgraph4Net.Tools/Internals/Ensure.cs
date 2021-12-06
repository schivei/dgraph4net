// Copyright (c) .NET Foundation. All rights reserved.

using System;

namespace Dgraph4Net.Tools.Internals
{
    internal static class Ensure
    {
        public static T NotNull<T>(T obj, string paramName)
            where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(paramName);
            }
            return obj;
        }

        public static string NotNullOrEmpty(string obj, string paramName)
        {
            if (string.IsNullOrEmpty(obj))
            {
                throw new ArgumentException("Value cannot be null or an empty string.", paramName);
            }
            return obj;
        }
    }
}
