using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MagicOnion;
using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHub : IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
        // The method must return `ValueTask`, `ValueTask<T>`, `Task` or `Task<T>` and can have up to 15 parameters of any type.
        ValueTask<Player[]> JoinAsync(string roomName, Guid guid);
        ValueTask LeaveAsync();
        ValueTask MakeMoveAsync(CardData card);
        ValueTask<IList<CardData>> GetCardsAsync();
    }
}
