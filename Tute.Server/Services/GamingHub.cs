using System.Collections.Concurrent;
using Cysharp.Runtime.Multicast;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Tute.Server.Extensions;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services;

public class GamingHub(ConcurrentDictionary<string, GameRoom> gameRooms) : StreamingHubBase<IGamingHub, IGamingHubReceiver>, IGamingHub
{
    private readonly ConcurrentDictionary<string, GameRoom> gameRooms = gameRooms;
    private IGroup<IGamingHubReceiver>? room;
    private Player? self;
    private string? roomName;
    private GameRoom? gameRoom;

    public async ValueTask<(Player, Player[])> JoinAsync(string roomname, string name)
    {
        gameRoom ??= GetOrCreateRoom(roomname);

        if (gameRoom.State != GameState.Room && self is null)
        {
            gameRoom = null;
            throw new ReturnStatusException((StatusCode)400, "Can't join an ingame room");
        }

        if (self is not null)
        {
            throw new ReturnStatusException((StatusCode)400, "Already joined");
        }

        self = new Player() { Name = name, ConnectionId = ConnectionId };
        roomName = roomname;
        room = await Group.AddAsync(roomname);

        if (gameRoom.Players.Count == 0)
        {
            self.IsLeader = true;
        }

        if (gameRoom.Players.Count > 2)
        {
            throw new ReturnStatusException((StatusCode)400, "Can't add more than 2 players");
        }

        gameRoom.Players.Add(self);

        // Typed Server->Client broadcast.
        room.Except(ConnectionId).OnJoin(self);
        return (self, [.. gameRoom.Players]);
    }

    private GameRoom GetOrCreateRoom(string roomname)
    {
        if (gameRooms.TryGetValue(roomname, out var value))
        {
            return value;
        }
        else
        {
            var gameRoom = new GameRoom()
            {
                State = GameState.Room,
                Players = [],
            };
            gameRooms[roomname] = gameRoom;
            return gameRoom;
        }
    }

    protected override async ValueTask OnDisconnected()
    {
        await LeaveAsync();
    }

    public async ValueTask LeaveAsync()
    {
        try
        {
            if (room is null)
                return;

            if (gameRoom.State == GameState.Playing)
            {
                room.All.OnFinished(GetAllPlayersData());
                await ClearRoom();
                return;
            }

            if (gameRoom.State == GameState.Room)
            {
                await ExitSelf();
                if (gameRoom.Players.Count == 1)
                {
                    var player = gameRoom.Players.First();
                    player.IsLeader = true;
                    room.All.OnLeave(player);
                    room.All.OnJoin(player);
                }
            }
        }
        finally
        {
            gameRoom = null;
            self = null;
            room = null;
        }
    }

    private async ValueTask ClearRoom()
    {
        gameRoom.Players.Clear();
        gameRoom.PlayerData.Clear();
        await ExitSelf();
        gameRooms.TryRemove(roomName, out _);
    }

    private async ValueTask ExitSelf()
    {
        gameRoom.Players?.Remove(self);
        gameRoom.PlayerData?.Remove(ConnectionId);
        await room.RemoveAsync(Context);
        room.Except(ConnectionId).OnLeave(self);
    }

    public ValueTask StartAsync(IList<CardData> cards)
    {
        if (room is null)
            return ValueTask.CompletedTask;

        if (self is null || !self.IsLeader)
            return ValueTask.CompletedTask;

        if (gameRoom.State == GameState.Playing)
            return ValueTask.CompletedTask;

        room.All.OnStart();

        var gameCards = new Stack<CardData>(cards.Shuffled());
        var pinte = gameCards.Pop();

        gameRoom.State = GameState.Playing;
        gameRoom.PlayerData = [];
        gameRoom.UsedCards = [];
        gameRoom.Cards = gameCards;
        //TODO should persist next player between games
        gameRoom.NextPlayer = gameRoom.Players.First();
        gameRoom.Pinte = pinte;
        gameRoom.PinteType = pinte;

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
            room.Only(item.ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[item.ConnectionId]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask Cante(IList<CardData> cards)
    {
        var king = cards.FirstOrDefault(i => i.Number == 12);
        var prince = cards.FirstOrDefault(i => i.Number == 11);

        if (king is null || prince is null)
        {
            throw new ReturnStatusException((StatusCode)400, "Cards must be prince and king");
        }
        if (king?.Type != prince?.Type)
        {
            throw new ReturnStatusException((StatusCode)400, "Cards must be same type");
        }

        var value = king!.Type == gameRoom.PinteType.Type ?

        return ValueTask.CompletedTask;
    }


    public ValueTask ChangePinte(CardData card)
    {
        if (gameRoom.NextPlayer.ConnectionId != ConnectionId) throw new ReturnStatusException((StatusCode)400, "It's not your turn");

        if (gameRoom.Pinte?.Number == 2)
        {
            throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
        }

        if (gameRoom.Pinte?.Number > 7 && (card.Number != 7 || card.Type != gameRoom.Pinte.Type))
        {
            throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
        }

        if (gameRoom.Pinte?.Number <= 7 && (card.Number != 2 || card.Type != gameRoom.Pinte.Type))
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
            room.Only(item.ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[item.ConnectionId]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<GameDataResponse> MakeMoveAsync(CardData card)
    {
        if (room is null)
            throw new ReturnStatusException((StatusCode)400, "Room is null");


        if (ConnectionId != gameRoom.NextPlayer.ConnectionId)
            throw new ReturnStatusException((StatusCode)400, "Not your turn");

        var typeToUse = gameRoom.UsedCards.FirstOrDefault().Value;
        var isTypeDefined = typeToUse is not null;
        var isSameType = card.Type == typeToUse?.Type;
        var isPinte = card.Type == gameRoom.PinteType.Type;
        var hasSameType = gameRoom.PlayerData[ConnectionId].Cards.Any(i => i.Type == typeToUse?.Type);
        var hasPinte = gameRoom.PlayerData[ConnectionId].Cards.Any(i => i.Type == gameRoom.PinteType.Type);

        if (isTypeDefined && !isSameType && hasSameType)
        {
            throw new ReturnStatusException((StatusCode)400, "You must use same type");
        }

        if (isTypeDefined && !hasSameType && !isPinte && hasPinte)
        {
            throw new ReturnStatusException((StatusCode)400, "You must use pinte");
        }

        KeyValuePair<Guid, CardData>? winner = null;
        RemovePlayerCard(card, gameRoom);
        gameRoom.NextPlayer = gameRoom.Players[GetNextPlayerIndex()];

        room.All.OnUsedCard(card, self);

        if (gameRoom.PlayerData.Count == gameRoom.UsedCards.Count)
        {
            winner = GetWinner(gameRoom);
            var winnerKey = winner.Value.Key;
            gameRoom.NextPlayer = gameRoom.PlayerData[winnerKey].Player;

            gameRoom.PlayerData[winnerKey].GainedCards =
            [
                .. gameRoom.PlayerData[winnerKey].GainedCards,
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
                room.Only(playerConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[playerConnectionId]));
            }
        }
        else
        {
            foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerData)
            {
                if (playerConnectionId == ConnectionId) continue;
                room.Only(playerConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[playerConnectionId]));
            }
        }

        if (gameRoom.PlayerData.All(i => i.Value.Cards.Count == 0))
        {
            if (winner is not null)
            {
                gameRoom.PlayerData[winner.Value.Key].GainedCards.Add(new CardData { Name = "Las 10 del monte", Value = 10 });
            }
            room.All.OnFinished(GetAllPlayersData());
        }

        return ValueTask.FromResult(CreateDataFor(gameRoom.PlayerData[ConnectionId]));
    }

    private List<GameDataResponse> GetAllPlayersData() => gameRoom.PlayerData.Values.Select(CreateDataFor).ToList();

    private KeyValuePair<Guid, CardData> GetWinner(GameRoom gameRoom)
    {
        var firstCard = gameRoom.UsedCards.FirstOrDefault().Value;

        //TODO calculate winner by number if no value difference

        var bestByValue = gameRoom
            .UsedCards.Where(i => i.Value.Type == firstCard.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var allSameValue = gameRoom.UsedCards
            .Where(i => i.Value.Type == firstCard.Type)
            .Sum(i => i.Value.Value) == 0;

        if (bestByValue is not null && allSameValue)
        {
            bestByValue = gameRoom.UsedCards
                .Where(i => i.Value.Type == firstCard.Type)
                .MaxByOrDefault(i => i.Value.Number);
        }

        var bestByType = gameRoom
            .UsedCards.Where(i => i.Value.Type == gameRoom.PinteType.Type)
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
