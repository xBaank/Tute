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
    private Dictionary<string, GameRoom> gameRooms;

    public GamingHub(Dictionary<string, GameRoom> gameRooms)
    {
        this.gameRooms = gameRooms;
    }

    public async ValueTask<(Player, Player[])> JoinAsync(string roomname, string name)
    {
        self = new Player() { Name = name, ConnectionId = ConnectionId };
        (room, storage) = await Group.AddAsync(roomname, self);

        // Group can bundle many connections and it has inmemory-storage so add any type per group.
        if (storage is null || storage.AllValues.Count == 1)
        {
            self.IsLeader = true;
        }

        if (storage!.AllValues.Count > 2)
        {
            throw new Exception("Can't add more than 2 players");
        }

        // Typed Server->Client broadcast.
        Broadcast(room).OnJoin(self);
        return (self, [.. storage.AllValues]);
    }

    protected override async ValueTask OnDisconnected()
    {
        await LeaveAsync();
    }

    public async ValueTask LeaveAsync()
    {
        if (room is null)
            return;

        await room.RemoveAsync(Context);
        Broadcast(room).OnLeave(self);

        if (storage?.AllValues.Count == 0)
        {
            gameRoom = null;
            gameRooms.Remove(room.GroupName);
            return;
        }
    }

    public ValueTask StartAsync(IList<CardData> cards)
    {
        if (room is null)
            return ValueTask.CompletedTask;

        if (storage is null)
            return ValueTask.CompletedTask;

        if (self is null || !self.IsLeader)
            return ValueTask.CompletedTask;

        var gameCards = new Stack<CardData>(cards.Shuffled());
        var players = storage.AllValues.ToList();
        var pinte = gameCards.Pop();
        if (gameRooms.TryGetValue(room.GroupName, out var value))
        {
            gameRoom = value;
        }
        else
        {
            gameRoom = new()
            {
                State = GameState.Playing,
                PlayerData = [],
                UsedCards = [],
                Cards = gameCards,
                Players = players,
                NextPlayer = players.First(),
                Pinte = pinte
            };
            gameRooms[room.GroupName] = gameRoom;
        }

        foreach (var item in gameRoom.Players)
        {
            var initialHand = gameCards.PopRange(7).ToList();
            PlayerData gameData =
                new()
                {
                    Cards = initialHand,
                    GainedCards = [],
                    Player = item
                };
            gameRoom.PlayerData[item.ConnectionId] = gameData;
            BroadcastTo(room, item.ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[item.ConnectionId]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ChangePinte(CardData card)
    {
        if (gameRoom.NextPlayer.ConnectionId != ConnectionId) return ValueTask.CompletedTask;

        //TODO check if its posible
        var cards = gameRoom.PlayerData[ConnectionId].Cards;
        var toRemove = cards.FirstOrDefault(i => i.Name == card.Name);
        if (toRemove == null) return ValueTask.CompletedTask;
        cards.Remove(toRemove);
        cards.Add(gameRoom.Pinte);
        gameRoom.Pinte = toRemove;

        return ValueTask.CompletedTask;
    }

    public ValueTask<GameDataResponse> MakeMoveAsync(CardData card)
    {
        if (room is null)
            throw new InvalidOperationException();

        if (gameRoom is null)
        {
            if (gameRooms.TryGetValue(room.GroupName, out var value))
            {
                gameRoom = value;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        if (ConnectionId != gameRoom.NextPlayer.ConnectionId)
            throw new InvalidOperationException();

        //TODO check if possible

        RemovePlayerCard(card, gameRoom);
        gameRoom.NextPlayer = gameRoom.Players[GetNextPlayerIndex()];

        Broadcast(room).OnUsedCard(card, self);

        if (gameRoom.PlayerData.Count == gameRoom.UsedCards.Count)
        {
            var winner = GetWinner(gameRoom);
            gameRoom.NextPlayer = gameRoom.PlayerData[winner.Key].Player;

            gameRoom.PlayerData[winner.Key].GainedCards =
            [
                .. gameRoom.PlayerData[winner.Key].GainedCards,
                .. gameRoom.UsedCards.Values
            ];
            gameRoom.UsedCards = [];

            foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerData)
            {
                var isNext = gameRoom.Cards.TryPop(out var nextCard);
                if (isNext) playerCards.Cards.Add(nextCard);
                if (playerConnectionId == ConnectionId) continue;
                BroadcastTo(room, playerConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[playerConnectionId]));
            }
        }
        else
        {
            foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerData)
            {
                if (playerConnectionId == ConnectionId) continue;
                BroadcastTo(room, playerConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[playerConnectionId]));
            }
        }

        return ValueTask.FromResult(CreateDataFor(gameRoom.PlayerData[ConnectionId]));
    }

    private KeyValuePair<Guid, CardData> GetWinner(GameRoom gameRoom)
    {
        var firstCard = gameRoom.UsedCards.FirstOrDefault().Value;

        var bestByValue = gameRoom
            .UsedCards.Where(i => i.Value.Type == firstCard.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var bestByType = gameRoom
            .UsedCards.Where(i => i.Value.Type == gameRoom.Pinte.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var winner = bestByType ?? bestByValue ?? throw new InvalidOperationException();
        return winner;
    }

    private void RemovePlayerCard(CardData card, GameRoom gameRoom)
    {
        var cardToRemove = gameRoom
            .PlayerData[ConnectionId]
            .Cards.FirstOrDefault(i => i.Name == card.Name);

        if (!gameRoom.PlayerData[ConnectionId].Cards.Remove(cardToRemove))
            throw new InvalidOperationException();

        gameRoom.UsedCards[ConnectionId] = card;
    }

    private int GetNextPlayerIndex()
    {
        var currentIndex = gameRoom.Players.IndexOf(gameRoom.NextPlayer);
        var nextPlayer = gameRoom.Players.ElementAtOrDefault(currentIndex + 1);
        var nextIndex = nextPlayer == default ? 0 : gameRoom.Players.IndexOf(nextPlayer);
        return nextIndex;
    }

    private GameDataResponse CreateDataFor(PlayerData playerData) => new()
    {
        PlayerData = playerData,
        NextPlayer = gameRoom.NextPlayer,
        Pinte = gameRoom.Pinte,
        UsedCards = gameRoom.UsedCards,
    };
}
