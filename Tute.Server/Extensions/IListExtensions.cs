namespace Tute.Server.Extensions;

internal static class IListExtensions
{
    public static IList<T> Shuffled<T>(this IList<T> values) =>
        [.. values.OrderBy(i => Guid.NewGuid())];
}
