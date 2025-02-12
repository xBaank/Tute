using System;
using System.Collections.Generic;
using System.Linq;
using Tute.Shared.Models;

namespace Assets.Scripts.Extensions
{
    internal static class IEnumerableExtensions
    {
        public static IEnumerable<(int Index, T Value)> WithIndex<T>(this IEnumerable<T> source) =>
            source.Select((item, index) => (index, item));

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        public static GameDataResponse GetWinner(this IEnumerable<GameDataResponse> gameDataResponses) => gameDataResponses
            .OrderByDescending(i => i.GainedCards.Sum(i => i.Value))
            .FirstOrDefault();
    }
}
