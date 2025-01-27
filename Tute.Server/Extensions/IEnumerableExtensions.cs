namespace Tute.Server.Extensions
{
    internal static class IEnumerableExtensions
    {
        public static TSource? MaxByOrDefault<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TSource : struct
        {
            try
            {
                return source.MaxBy(selector);
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                return default(TSource);
            }
        }
    }
}
