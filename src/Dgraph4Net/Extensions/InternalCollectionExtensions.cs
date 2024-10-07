internal static class InternalCollectionExtensions
{
    public static bool Exists<T>(this IEnumerable<T> collection, Predicate<T> predicate)
    {
        foreach (var item in collection)
        {
            if (predicate(item))
                return true;
        }

        return false;
    }
}
