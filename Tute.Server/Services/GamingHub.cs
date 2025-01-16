using MagicOnion.Server.Hubs;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services
{
    internal class GamingHub : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
    {
        private IGroup room;
        private Player self;
        private IInMemoryStorage<Player> storage;

        public async ValueTask<Guid[]> JoinAsync(string roomname, Guid guid)
        {
            self = new Player() { Id = guid };

            // Group can bundle many connections and it has inmemory-storage so add any type per group.
            (room, storage) = await Group.AddAsync(roomname, self);

            // Typed Server->Client broadcast.
            Broadcast(room).OnJoin(self);
            return storage.AllValues.ToArray();
        }

        public ValueTask LeaveAsync()
        {
            throw new NotImplementedException();
        }

        public ValueTask MoveAsync()
        {
            throw new NotImplementedException();
        }
    }
}
