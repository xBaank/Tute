using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Extensions
{
    internal static class IEnumerableExtensions
    {
        public static IEnumerable<(int Index, T Value)> WithIndex<T>(this IEnumerable<T> source) =>
            source.Select((item, index) => (index, item));
    }
}
