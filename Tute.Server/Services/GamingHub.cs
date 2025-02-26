using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cysharp.Runtime.Multicast;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Tute.Server.Controllers;
using Tute.Shared.Constants;
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

    public ValueTask<string> GetVersion() => ValueTask.FromResult(Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown");

    public async ValueTask<(Player, Player[])> JoinAsync(
        string roomname,
        string name,
        string? deckName,
        bool shuffled
    )
    {
        var newGameRoom = await GetOrCreateRoomAsync(roomname, deckName ?? "deck", shuffled);
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

        if (newGameRoom.Players.Count > 4)
        {
            throw new ReturnStatusException((StatusCode)400, "Can't add more than 4 players");
        }

        self = newPlayer;
        gameRoom = newGameRoom;
        roomName = roomname;
        room = await Group.AddAsync(roomname);
        newGameRoom.RoomContextsByConnection[ConnectionId] = Context;
        newGameRoom.Players.Add(self);
        gameController = new(gameRoom, self, room, Context);
        chatController = new(room, gameRoom, self);

        // Typed Server->Client broadcast.
        room.Except(ConnectionId).OnJoin(self);
        return (self, [.. newGameRoom.Players]);
    }

    private static string GetDeckPath(Deck deckName) =>
        deckName switch
        {
            _ when deckName == DecksConstants.NormalDeck => "Data/deck.json",
            _ when deckName == DecksConstants.ShortDeck => "Data/short_deck.json",
            _ when deckName == DecksConstants.Cante20Deck => "Data/cante_20_deck.json",
            _ when deckName == DecksConstants.Cante40Deck => "Data/cante_40_deck.json",
            _ when deckName == DecksConstants.TuteKingsDeck => "Data/tute_kings_deck.json",
            _ when deckName == DecksConstants.TutePrinceDeck => "Data/tute_prince_deck.json",
            _ => throw new ReturnStatusException((StatusCode)400, $"{deckName} does not exist"),
        };

    private async Task<GameRoom> GetOrCreateRoomAsync(string roomname, Deck deck, bool shuffled)
    {
        if (gameRooms.TryGetValue(roomname, out var value))
        {
            return value;
        }
        else
        {
            var file = File.OpenRead(GetDeckPath(deck));
            var cards = await JsonSerializer.DeserializeAsync<List<CardData>>(
                file,
                options: options
            );

            if (cards is null || cards.Count == 0)
                throw new ReturnStatusException((StatusCode)400, "No deck found");

            //For some reason the array is deserialized reversed
            cards.Reverse();

            var gameRoom = new GameRoom()
            {
                InitialDeck = cards,
                State = GameState.Room,
                Players = [],
                RoomContextsByConnection = [],
                PlayerDataByConnetion = [],
                UsedCardsByConnection = [],
                Cards = [],
                HaveShuffle = shuffled,
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

    public ValueTask ChangePinte(CardData card) =>
        gameController?.ChangePinte(card) ?? ThrowNoGameRoomException<ValueTask>();

    public ValueTask MakeMove(CardData card) =>
        gameController?.MakeMove(card) ?? ThrowNoGameRoomException<ValueTask>();

    protected override ValueTask OnDisconnected() => LeaveAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public Task WaitForDisconnect() => Task.CompletedTask;

    public IGamingHub FireAndForget() => this;

    private static T ThrowNoGameRoomException<T>() =>
        throw new ReturnStatusException((StatusCode)400, "No gameroom found");

    public ValueTask SendMessage(string message) =>
        chatController?.SendMessage(message) ?? ThrowNoGameRoomException<ValueTask>();
}
