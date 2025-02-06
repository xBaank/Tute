using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cysharp.Runtime.Multicast;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Tute.Server.Controllers;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Services;

public class GamingHub(ConcurrentDictionary<string, GameRoom> gameRooms)
    : StreamingHubBase<IGamingHub, IGamingHubReceiver>,
        IGamingHub
{
    private readonly ConcurrentDictionary<string, GameRoom> gameRooms = gameRooms;
    private IGroup<IGamingHubReceiver>? room;
    private Player? self;
    private string? roomName;
    private GameRoom? gameRoom;
    private GameController? gameController;
    private ChatController? chatController;

    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    //TODO fix leave join
    public async ValueTask<(Player, Player[])> JoinAsync(string roomname, string name)
    {
        var newGameRoom = await GetOrCreateRoomAsync(roomname);
        var newPlayer = new Player() { Name = name, ConnectionId = ConnectionId };

        if (gameController?.IsPlayerInRoom == true)
        {
            throw new ReturnStatusException((StatusCode)400, "You are already in a room");
        }

        if (newGameRoom.State != GameState.Room && self is null)
        {
            throw new ReturnStatusException((StatusCode)400, "Can't join an ingame room");
        }

        if (newGameRoom.Players.Any(i => i.ConnectionId == ConnectionId))
        {
            throw new ReturnStatusException((StatusCode)400, "Already joined");
        }

        if (newGameRoom.Players.Count == 0)
        {
            newPlayer.IsLeader = true;
        }

        if (newGameRoom.Players.Count > 2)
        {
            throw new ReturnStatusException((StatusCode)400, "Can't add more than 2 players");
        }

        self = newPlayer;
        gameRoom = newGameRoom;
        roomName = roomname;
        room = await Group.AddAsync(roomname);
        newGameRoom.RoomContexts[ConnectionId] = Context;
        newGameRoom.Players.Add(self);
        gameController = new(gameRoom, self, room, Context);
        chatController = new(room, self);

        // Typed Server->Client broadcast.
        room.Except(ConnectionId).OnJoin(self);
        return (self, [.. newGameRoom.Players]);
    }

    private async Task<GameRoom> GetOrCreateRoomAsync(string roomname)
    {
        if (gameRooms.TryGetValue(roomname, out var value))
        {
            return value;
        }
        else
        {
            var file = File.OpenRead("Data/deck.json");
            var cards = await JsonSerializer.DeserializeAsync<List<CardData>>(
                file,
                options: options
            );

            if (cards is null || cards.Count == 0)
                throw new ReturnStatusException((StatusCode)400, "No deck found");

            var gameRoom = new GameRoom()
            {
                InitialDeck = cards,
                State = GameState.Room,
                Players = [],
                RoomContexts = [],
                PlayerData = [],
                UsedCards = [],
                Cards = [],
            };
            gameRooms[roomname] = gameRoom;
            return gameRoom;
        }
    }

    public async ValueTask LeaveAsync()
    {
        var controller = gameController ?? ThrowNoGameRoomException<GameController>();
        var isEmpty = await controller.LeaveAsync();
        if (isEmpty && roomName is not null)
            gameRooms.TryRemove(roomName, out _);
        gameController = null;
        chatController = null;
        gameRoom = null;
        self = null;
        roomName = null;
        room = null;
    }

    public ValueTask StartAsync() =>
        gameController?.Start() ?? ThrowNoGameRoomException<ValueTask>();

    public ValueTask Tute(IList<CardData> cards) =>
        gameController?.Tute(cards) ?? ThrowNoGameRoomException<ValueTask>();

    public ValueTask Cante(CardData king, CardData prince) =>
        gameController?.Cante(king, prince) ?? ThrowNoGameRoomException<ValueTask>();

    public ValueTask ChangePinte(CardData card) =>
        gameController?.ChangePinte(card) ?? ThrowNoGameRoomException<ValueTask>();

    public ValueTask MakeMove(CardData card) =>
        gameController?.MakeMoveAsync(card) ?? ThrowNoGameRoomException<ValueTask>();

    protected override ValueTask OnDisconnected() => LeaveAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public Task WaitForDisconnect() => Task.CompletedTask;

    public IGamingHub FireAndForget() => this;

    private static T ThrowNoGameRoomException<T>() =>
        throw new ReturnStatusException((StatusCode)400, "No gameroom found");

    public ValueTask SendMessage(string message) => chatController?.SendMessage(message) ?? ThrowNoGameRoomException<ValueTask>();
}
