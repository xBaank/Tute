namespace Tute.Server.Extensions
{
    internal static class IEnumerableExtensions
    {
        public static TSource? MaxByOrDefault<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TSource : struct
        {
            try
            {
                var sourceList = source.ToList();
                if (sourceList.Count == 0) return null;
                return sourceList.MaxBy(selector);
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                return null;
            }
        }
    }
}
