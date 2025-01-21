using MagicOnion.Server.Hubs;
using Tute.Server.Extensions;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services;

public class GamingHub : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
{
    private IGroup? room;
    private Player? self;
    private GameRoom? gameRoom;
    private IInMemoryStorage<Player>? storage;

    public async ValueTask<(Guid, Player[])> JoinAsync(string roomname, string name)
    {
        self = new Player() { Name = name, ConnectionId = ConnectionId };

        // Group can bundle many connections and it has inmemory-storage so add any type per group.
        if (storage is null || storage?.AllValues.Count == 0)
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
        return (ConnectionId, [.. storage.AllValues]);
    }

    public async ValueTask LeaveAsync()
    {
        if (room is null)
            return;

        if (storage?.AllValues.Count == 0)
        {
            gameRoom = null;
            return;
        }

        await room.RemoveAsync(Context);
        Broadcast(room).OnLeave(self);
    }

    public ValueTask StartAsync(IList<CardData> cards)
    {
        if (room is null)
            return ValueTask.CompletedTask;

        if (storage is null)
            return ValueTask.CompletedTask;

        var gameCards = new Stack<CardData>(cards.Shuffled());
        var players = storage.AllValues.ToList();
        gameRoom = new()
        {
            State = GameState.Playing,
            Data = [],
            UsedCards = [],
            Cards = gameCards,
            Players = players,
            NextPlayer = players.First()
        };

        foreach (var item in storage?.AllValues ?? [])
        {
            var initialHand = gameCards.PopRange(7).ToList();
            GameData gameData =
                new()
                {
                    Cards = initialHand,
                    GainedCards = [],
                    Player = self
                };
            gameRoom.Data[ConnectionId] = gameData;
            BroadcastTo(room, item.ConnectionId)
                .OnGameData(gameRoom.Data[ConnectionId], gameRoom.NextPlayer);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MakeMoveAsync(CardData card)
    {
        if (gameRoom is null)
            return ValueTask.CompletedTask;

        if (room is null)
            return ValueTask.CompletedTask;

        if (ConnectionId != gameRoom.NextPlayer.ConnectionId)
            return ValueTask.CompletedTask;

        var cardToRemove = gameRoom
            .Data[ConnectionId]
            .Cards.FirstOrDefault(i => i.Name == card.Name);

        if (!gameRoom.Data[ConnectionId].Cards.Remove(cardToRemove))
            return ValueTask.CompletedTask;

        var currentIndex = gameRoom.Players.IndexOf(gameRoom.NextPlayer);
        var nextPlayer = gameRoom.Players.ElementAtOrDefault(currentIndex + 1);
        var nextIndex = nextPlayer == default ? 0 : gameRoom.Players.IndexOf(nextPlayer);
        gameRoom.UsedCards[ConnectionId] = card;
        gameRoom.NextPlayer = gameRoom.Players[nextIndex];

        if (gameRoom.Data.Count == gameRoom.UsedCards.Count)
        {
            //TODO Calculate winner

            gameRoom.Data[ConnectionId].GainedCards = [.. gameRoom.UsedCards.Values];
            gameRoom.UsedCards = [];

            foreach (var (connectionId, cards) in gameRoom.Data)
            {
                var isNext = gameRoom.Cards.TryPop(out var nextCard);
                if (isNext)
                    cards.Cards.Add(nextCard);
                BroadcastTo(room, connectionId)
                    .OnGameData(gameRoom.Data[ConnectionId], gameRoom.NextPlayer);
            }
        }

        return ValueTask.CompletedTask;
    }
}
