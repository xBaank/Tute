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
    private Guid Id => self!.Id;

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

        var gameCards = cards.Shuffled().ToList();

        gameRoom = new()
        {
            State = GameState.Playing,
            Data = [],
            UsedCards = [],
            Cards = gameCards,
            LastPlayed = null
        };


        foreach (var item in storage?.AllValues ?? [])
        {
            var initialHand = gameCards[0..7];
            gameCards.RemoveRange(0, 7);
            GameData gameData = new() { Cards = initialHand, GainedCards = [], PlayerGuid = Id };
            gameRoom.Data[Id] = gameData;
        }

        Broadcast(room).OnGameStart(gameRoom);

        return ValueTask.CompletedTask;
    }

    public async ValueTask MakeMoveAsync(CardData card)
    {
        if (gameRoom is null) return;

        var cardToRemove = gameRoom.Data[Id].Cards.FirstOrDefault(i => i.Value == card.Value && i.Type == card.Type);
        gameRoom.Data[Id].Cards.Remove(cardToRemove);
        gameRoom.UsedCards[Id] = card;
        gameRoom.LastPlayed = Id;

        if (gameRoom.Data.Count == gameRoom.UsedCards.Count)
        {
            //TODO Calculate winner

            gameRoom.Data[Id].GainedCards = [.. gameRoom.UsedCards.Values];
            gameRoom.UsedCards = [];

            foreach (var (player, cards) in gameRoom.Data)
            {
                var next = gameRoom.Cards.FirstOrDefault();
                if (next is not null) cards.Cards.Add(next);
            }
        }

        Broadcast(room).OnGameData(gameRoom);
    }
}
