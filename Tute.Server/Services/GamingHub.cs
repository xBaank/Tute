using MagicOnion.Server.Hubs;
using Tute.Server.Extensions;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services
{
    public class GamingHub : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
    {
        private IGroup? room;
        private Player? self;
        private IInMemoryStorage<Player>? storage;
        private GameState gameState;

        public async ValueTask<Player[]> JoinAsync(string roomname, Guid guid)
        {
            self = new Player() { Id = guid };

            // Group can bundle many connections and it has inmemory-storage so add any type per group.
            if (storage?.AllValues.Count == 0)
            {
                self.IsLeader = true;
            }

            if (storage?.AllValues.Count == 2)
            {
                throw new Exception("Can't add more than 2 players");
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

            if (storage?.AllValues.Count == 0)
            {
                gameState = GameState.None;
                return;
            }

            await room.RemoveAsync(this.Context);
            Broadcast(room).OnLeave(self);
        }

        public ValueTask StartAsync(IList<CardData> cards)
        {
            if (room is null)
                return ValueTask.CompletedTask;

            if (gameState == GameState.Playing)
                throw new Exception("Already playing");

            List<CardData> shuffled = [.. cards.Shuffled()];
            gameState = GameState.Playing;
            Broadcast(room).OnGameStart(shuffled[0..7]);

            return ValueTask.CompletedTask;
        }

        public async ValueTask MakeMoveAsync(CardData card)
        {
            Console.WriteLine(card.Name);
        }
    }
}
