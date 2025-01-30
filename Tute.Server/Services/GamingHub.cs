using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Tute.Server.Extensions;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services;

public class GamingHub(Dictionary<string, GameRoom> gameRooms) : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
{
    private IGroup? room;
    private Player? self;
    private GameRoom? gameRoom;
    private IInMemoryStorage<Player>? storage;
    private Dictionary<string, GameRoom> gameRooms = gameRooms;

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
            throw new ReturnStatusException((StatusCode)400, "Can't add more than 2 players");
        }

        // Typed Server->Client broadcast.
        BroadcastExceptSelf(room).OnJoin(self);
        return (self, [.. storage.AllValues]);
    }

    protected override async ValueTask OnDisconnected()
    {
        await LeaveAsync();
    }

    public async ValueTask LeaveAsync()
    {
        //TODO Update current leader and check if game can persist only if its on playing

        if (room is null)
            return;

        await room.RemoveAsync(Context);
        BroadcastExceptSelf(room).OnLeave(self);



        if (storage?.AllValues.Count == 0)

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
        CheckRoom();

        if (gameRoom.NextPlayer.ConnectionId != ConnectionId) throw new ReturnStatusException((StatusCode)400, "It's not your turn");

        if (gameRoom.Pinte.Number == 2)
        {
            throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
        }

        if (gameRoom.Pinte.Number > 7 && (card.Number != 7 || card.Type != gameRoom.Pinte.Type))
        {
            throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
        }

        if (gameRoom.Pinte.Number <= 7 && (card.Number != 2 || card.Type != gameRoom.Pinte.Type))
        {
            throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
        }

        var cards = gameRoom.PlayerData[ConnectionId].Cards;
        var toRemove = cards.FirstOrDefault(i => i.Name == card.Name) ?? throw new ReturnStatusException((StatusCode)400, "You don't have that card");
        cards.Remove(toRemove);
        cards.Add(gameRoom.Pinte);
        gameRoom.Pinte = toRemove;

        foreach (var item in gameRoom.Players)
        {
            BroadcastTo(room, item.ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[item.ConnectionId]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<GameDataResponse> MakeMoveAsync(CardData card)
    {
        CheckRoom();

        if (room is null)
            throw new ReturnStatusException((StatusCode)400, "Room is null");


        if (ConnectionId != gameRoom.NextPlayer.ConnectionId)
            throw new ReturnStatusException((StatusCode)400, "Not your turn");

        var typeToUse = gameRoom.UsedCards.FirstOrDefault().Value;
        var isTypeDefined = typeToUse is not null;
        var isSameType = card.Type == typeToUse?.Type;
        var isPinte = card.Type == gameRoom.Pinte.Type;
        var hasSameType = gameRoom.PlayerData[ConnectionId].Cards.Any(i => i.Type == typeToUse?.Type);
        var hasPinte = gameRoom.PlayerData[ConnectionId].Cards.Any(i => i.Type == gameRoom.Pinte.Type);

        if (isTypeDefined && !isSameType && hasSameType)
        {
            throw new ReturnStatusException((StatusCode)400, "You must use same type");
        }

        if (isTypeDefined && !hasSameType && !isPinte && hasPinte)
        {
            throw new ReturnStatusException((StatusCode)400, "You must use pinte");
        }


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
                if (!isNext && gameRoom.Pinte != null)
                {
                    playerCards.Cards.Add(gameRoom.Pinte);
                    gameRoom.Pinte = null;
                }
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

    private void CheckRoom()
    {
        if (gameRoom is null)
        {
            if (gameRooms.TryGetValue(room.GroupName, out var value))
            {
                gameRoom = value;
            }
            else
            {
                throw new ReturnStatusException((StatusCode)400, "Gameroom is null");
            }
        }
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

        var winner = bestByType ?? bestByValue ?? throw new InvalidOperationException("No winner found");
        return winner;
    }

    private void RemovePlayerCard(CardData card, GameRoom gameRoom)
    {
        var cardToRemove = gameRoom
            .PlayerData[ConnectionId]
            .Cards.FirstOrDefault(i => i.Name == card.Name);

        if (!gameRoom.PlayerData[ConnectionId].Cards.Remove(cardToRemove))
            throw new ReturnStatusException((StatusCode)400, "No card to remove found");

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
        GameState = gameRoom.State,
    };
}
