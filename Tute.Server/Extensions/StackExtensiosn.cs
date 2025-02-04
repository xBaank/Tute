namespace Tute.Server.Extensions;

internal static class StackExtensiosn
{
    public static IEnumerable<T> PopRange<T>(this Stack<T> values, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (values.TryPop(out var next))
                yield return next;
            else
                yield break;
        }
    }
}
