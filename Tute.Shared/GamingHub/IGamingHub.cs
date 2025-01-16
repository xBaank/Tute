using System;
using System.Threading.Tasks;
using MagicOnion;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHub : IStreamingHub<IGamingHub, IGamingHubReceiver>
    {
        // The method must return `ValueTask`, `ValueTask<T>`, `Task` or `Task<T>` and can have up to 15 parameters of any type.
        ValueTask<Guid[]> JoinAsync(string roomName, Guid guid);
        ValueTask LeaveAsync();
        ValueTask MoveAsync();
    }
}
