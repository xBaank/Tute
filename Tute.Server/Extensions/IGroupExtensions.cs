using MagicOnion.Server.Hubs;

namespace Tute.Server.Extensions
{
    internal static class IGroupExtensions
    {
        public static T Only<T>(this IGroup<T> group, Guid connectionId) => group.Only([connectionId]);
    }
}
