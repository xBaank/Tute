using System.Threading.Tasks;
using MagicOnion;
using Tute.Shared.Models;

#nullable enable

namespace Tute.Shared.GamingHub
{
    public interface IGamingHub : IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
        // The method must return `ValueTask`, `ValueTask<T>`, `Task` or `Task<T>` and can have up to 15 parameters of any type.
        ValueTask<(Player self, Player[] players)> JoinAsync(
            string roomName,
            string name,
            string? deckName = null,
            bool shuffled = true
        );
        ValueTask StartAsync();
        ValueTask LeaveAsync();
        ValueTask MakeMove(CardData card);
        ValueTask ChangePinte(CardData card);
        ValueTask SendMessage(string message);
    }
}
#nullable disable
