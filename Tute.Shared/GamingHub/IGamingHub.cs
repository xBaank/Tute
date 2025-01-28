using System.Collections.Generic;
using System.Threading.Tasks;

using MagicOnion;

using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHub : IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
        // The method must return `ValueTask`, `ValueTask<T>`, `Task` or `Task<T>` and can have up to 15 parameters of any type.
        ValueTask<(Player self, Player[] players)> JoinAsync(string roomName, string name);
        ValueTask StartAsync(IList<CardData> cards);
        ValueTask LeaveAsync();
        ValueTask<GameDataResponse> MakeMoveAsync(CardData card);
        ValueTask ChangePinte(CardData card);
    }
}
