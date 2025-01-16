using MagicOnion.Server.Hubs;

using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services
{
    public class GamingHub : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
    {
        private IGroup? room;
        private Player? self;
        private IInMemoryStorage<Player>? storage;

        public async ValueTask<IList<CardData>> GetCardsAsync()
        {
            return [];
        }

        public async ValueTask StartAsync()
        {
            if (room is null)
                return;

            Broadcast(room).OnGameStart([]);
        }

        public async ValueTask<Player[]> JoinAsync(string roomname, Guid guid)
        {
            self = new Player() { Id = guid };

            // Group can bundle many connections and it has inmemory-storage so add any type per group.
            if (storage?.AllValues.Count == 0)
            {
                self.IsLeader = true;
            }

            (room, storage) = await Group.AddAsync(roomname, self);

            // Typed Server->Client broadcast.
            Broadcast(room).OnJoin(self);
            return [.. storage.AllValues];
        }

        public async ValueTask LeaveAsync()
        {
            if (room is null)
                return;

            await room.RemoveAsync(this.Context);
            Broadcast(room).OnLeave(self);
        }

        public async ValueTask MakeMoveAsync(CardData card)
        {
            Console.WriteLine(card.Name);
        }
    }
}
