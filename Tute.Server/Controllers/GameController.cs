using Cysharp.Runtime.Multicast;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using MagicOnion.Server.Hubs;
using Tute.Server.Extensions;
using Tute.Server.Services;
using Tute.Shared.Constants;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Controllers;

public class GameController(
    GameRoom gameRoom,
    Player self,
    IGroup<IGamingHubReceiver> room,
    ServiceContext context
)
{
    private Guid ConnectionId => self.ConnectionId;
    public bool IsPlayerInRoom => gameRoom.Players.Any(i => i.ConnectionId == ConnectionId);
    private readonly SemaphoreSlim semaphoreSlim = new(1);

    public async ValueTask<bool> LeaveAsync()
    {
        if (gameRoom.State == GameState.Playing)
        {
            room.All.OnFinished(GetAllPlayersData());
            room.All.OnLeave(self);
            await ClearRoom();
        }
        else if (gameRoom.State == GameState.Room)
        {
            await ExitSelf();
            if (gameRoom.Players.Count == 1)
            {
                var player = gameRoom.Players.First();
                room.All.OnLeave(player);
                player.IsLeader = true;
                room.All.OnJoin(player);
            }
        }

        return gameRoom.Players.Count == 0;
    }

    private async ValueTask ClearRoom()
    {
        gameRoom.Players.Clear();
        gameRoom.PlayerData.Clear();
        foreach (var item in gameRoom.RoomContexts)
        {
            await room.RemoveAsync(item.Value);
        }
        await ExitSelf();
    }

    private async ValueTask ExitSelf()
    {
        gameRoom.Players?.Remove(self);
        gameRoom.PlayerData?.Remove(ConnectionId);
        await room.RemoveAsync(context);
        room.Except(ConnectionId).OnLeave(self);
    }

    public ValueTask Start()
    {
        if (self is null || !self.IsLeader)
        {
            return ValueTask.CompletedTask;
        }

        if (gameRoom.State == GameState.Playing)
        {
            return ValueTask.CompletedTask;
        }

        if (gameRoom.Players.Count != 2)
        {
            throw new ReturnStatusException((StatusCode)400, "Not enough players to start");
        }

        room.All.OnStart();

        var gameCards = new Stack<CardData>(gameRoom.InitialDeck.Shuffled());
        var pinte = gameCards.Pop();

        gameRoom.State = GameState.Playing;
        gameRoom.PlayerData = [];
        gameRoom.UsedCards = [];
        gameRoom.Cards = gameCards;
        //TODO should persist next player between games
        gameRoom.NextPlayer = gameRoom.Players.First();
        gameRoom.Pinte = pinte;
        gameRoom.PinteType = pinte;

        //TODO maybe change the way cards are distributed
        foreach (var item in gameRoom.Players)
        {
            var initialHand = gameCards.PopRange(7).ToList();
            PlayerData gameData = new()
            {
                Cards = initialHand,
                GainedCards = [],
                Player = item,
            };
            gameRoom.PlayerData[item.ConnectionId] = gameData;
            room.Single(item.ConnectionId)
                .OnGameData(CreateDataFor(gameRoom.PlayerData[item.ConnectionId]));
        }

        room.All.OnChangedPinte(pinte);
        return ValueTask.CompletedTask;
    }

    public async ValueTask Tute(IList<CardData> cards)
    {
        if (cards.Count != 4)
        {
            throw new ReturnStatusException((StatusCode)400, "Cards must be 4");
        }

        await semaphoreSlim.WaitAsync();
        try
        {

            var number = cards[0].Number;
            var cardsGrouped = cards.GroupBy(i => i.Type);

            if (
                cardsGrouped.Count() != 4
                || !cardsGrouped.All(i => i.Count() == 1 && i.First().Number == number)
            )
            {
                throw new ReturnStatusException((StatusCode)400, "Cards must be same number");
            }

            var tute = CardsConstants.GetTute(number);

            gameRoom.PlayerData[ConnectionId].GainedCards.Add(tute);
            room.Single(ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[ConnectionId]));
            room.All.OnTute(self, tute);

            foreach (var item in gameRoom.PlayerData)
            {
                item.Value.Cards.Clear();
            }

            room.All.OnFinished(GetAllPlayersData());
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask Cante(CardData king, CardData prince)
    {
        if (gameRoom.PinteType is null)
        {
            throw new ReturnStatusException((StatusCode)400, "No pinte was set");
        }

        if (king is null || prince is null)
        {
            throw new ReturnStatusException((StatusCode)400, "Cards must be prince and king");
        }

        await semaphoreSlim.WaitAsync();
        try
        {
            var hasKing = gameRoom.PlayerData[ConnectionId].Cards.Any(i => i.Name == king.Name);
            var hasPrince = gameRoom.PlayerData[ConnectionId].Cards.Any(i => i.Name == prince.Name);
            if (!hasKing || !hasPrince)
            {
                throw new ReturnStatusException((StatusCode)400, "You don't have the cards");
            }

            if (king!.Type != prince!.Type)
            {
                throw new ReturnStatusException((StatusCode)400, "Cards must be same type");
            }

            var cantes = CardsConstants
                .GetCantes(gameRoom.PlayerData[ConnectionId].GainedCards)
                .Select(i => i.Type);

            if (cantes.Contains(king!.Type))
            {
                throw new ReturnStatusException((StatusCode)400, "Already did");
            }

            var value = CardsConstants.GetCante(king!.Type, gameRoom.PinteType.Type);
            gameRoom.PlayerData[ConnectionId].GainedCards.Add(value);

            room.Single(ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[ConnectionId]));
            room.All.OnCante(self, value);
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask ChangePinte(CardData card)
    {
        if (gameRoom.NextPlayer?.ConnectionId != ConnectionId)
            throw new ReturnStatusException((StatusCode)400, "It's not your turn");
        if (gameRoom.Pinte is null)
            throw new ReturnStatusException((StatusCode)400, "You can't change pinte");

        await semaphoreSlim.WaitAsync();

        try
        {

            var number = gameRoom.Pinte.Number;

            if (number is 2)
            {
                throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
            }

            if (card.Type != gameRoom.Pinte.Type)
            {
                throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
            }

            if (card.Number is not 2 && number is 2 or 4 or 5 or 6 or 7)
            {
                throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
            }

            if (card.Number is not 7 && number is 1 or 3 or 10 or 11 or 12)
            {
                throw new ReturnStatusException((StatusCode)400, "You can't change pinte");
            }

            var cards = gameRoom.PlayerData[ConnectionId].Cards;
            var toRemove =
                cards.FirstOrDefault(i => i.Name == card.Name)
                ?? throw new ReturnStatusException((StatusCode)400, "You don't have that card");
            cards.Remove(toRemove);
            cards.Add(gameRoom.Pinte);
            gameRoom.Pinte = toRemove;

            room.All.OnChangedPinte(card);
            room.Single(ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerData[ConnectionId]));
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask MakeMove(CardData card)
    {
        if (ConnectionId != gameRoom.NextPlayer?.ConnectionId)
            throw new ReturnStatusException((StatusCode)400, "Not your turn");

        await semaphoreSlim.WaitAsync();
        try
        {
            var typeToUse = gameRoom.UsedCards.FirstOrDefault().Value;
            var isTypeDefined = typeToUse is not null;
            var isSameType = card.Type == typeToUse?.Type;
            var isPinte = card.Type == gameRoom.PinteType?.Type;
            var isGreaterCard =
                isTypeDefined
                && (
                    card.Value > typeToUse!.Value
                    || card.Value == typeToUse!.Value && card.Number > typeToUse.Number
                );
            var hasGreaterCard = gameRoom
                .PlayerData[ConnectionId]
                .Cards.Any(i => i.Type == typeToUse?.Type && i.Value > typeToUse?.Value);
            var hasSameType = gameRoom
                .PlayerData[ConnectionId]
                .Cards.Any(i => i.Type == typeToUse?.Type);
            var hasPinte = gameRoom
                .PlayerData[ConnectionId]
                .Cards.Any(i => i.Type == gameRoom.PinteType?.Type);

            if (isTypeDefined && !isSameType && hasSameType)
            {
                throw new ReturnStatusException((StatusCode)400, "You must use same type");
            }

            if (isTypeDefined && isSameType && !isGreaterCard && hasGreaterCard)
            {
                throw new ReturnStatusException(
                    (StatusCode)400,
                    "You must use same type with greater value"
                );
            }

            if (isTypeDefined && !hasSameType && !isPinte && hasPinte)
            {
                throw new ReturnStatusException((StatusCode)400, "You must use pinte");
            }

            KeyValuePair<Guid, CardData>? winner = null;
            RemovePlayerCard(card, gameRoom);
            var oldNextPlayer = gameRoom.NextPlayer;
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
                .. gameRoom.UsedCards.Values,
            ];
                gameRoom.UsedCards = [];

                foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerData)
                {
                    var isNext = gameRoom.Cards.TryPop(out var nextCard);
                    if (isNext)
                        playerCards.Cards.Add(nextCard);
                    if (!isNext && gameRoom.Pinte != null)
                    {
                        playerCards.Cards.Add(gameRoom.Pinte);
                        gameRoom.Pinte = null;
                        room.All.OnChangedPinte(gameRoom.Pinte);
                    }
                    room.Single(playerConnectionId)
                        .OnGameData(CreateDataFor(gameRoom.PlayerData[playerConnectionId]));
                }
            }
            else
            {
                EmitGameDataForEachPlayer();
            }

            var isGameFinished = gameRoom.PlayerData.All(i => i.Value.Cards.Count == 0);

            if (isGameFinished)
            {
                if (winner is not null)
                {
                    gameRoom.PlayerData[winner.Value.Key].GainedCards.Add(CardsConstants.DiezDelMonte);
                }
                gameRoom.NextPlayer = null;
                EmitGameDataForEachPlayer();
                room.All.OnFinished(GetAllPlayersData());
            }
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private void EmitGameDataForEachPlayer()
    {
        foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerData)
        {
            room.Single(playerConnectionId)
                .OnGameData(CreateDataFor(gameRoom.PlayerData[playerConnectionId]));
        }
    }

    private List<GameDataResponse> GetAllPlayersData() =>
        gameRoom.PlayerData.Values.Select(CreateDataFor).ToList();

    private KeyValuePair<Guid, CardData> GetWinner(GameRoom gameRoom)
    {
        var firstCard = gameRoom.UsedCards.FirstOrDefault().Value;

        //TODO calculate winner by number if no value difference

        var bestByValue = gameRoom
            .UsedCards.Where(i => i.Value.Type == firstCard.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var allSameValue =
            gameRoom.UsedCards.Where(i => i.Value.Type == firstCard.Type).Sum(i => i.Value.Value)
            == 0;

        if (bestByValue is not null && allSameValue)
        {
            bestByValue = gameRoom
                .UsedCards.Where(i => i.Value.Type == firstCard.Type)
                .MaxByOrDefault(i => i.Value.Number);
        }

        var bestByType = gameRoom
            .UsedCards.Where(i => i.Value.Type == gameRoom.PinteType?.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var winner =
            bestByType ?? bestByValue ?? throw new InvalidOperationException("No winner found");
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
        if (gameRoom.NextPlayer is null)
            return 0;

        var currentIndex = gameRoom.Players.IndexOf(gameRoom.NextPlayer);
        var nextPlayer = gameRoom.Players.ElementAtOrDefault(currentIndex + 1);
        var nextIndex = nextPlayer == default ? 0 : gameRoom.Players.IndexOf(nextPlayer);
        return nextIndex;
    }

    private GameDataResponse CreateDataFor(PlayerData playerData) =>
        new()
        {
            PlayerData = playerData,
            NextPlayer = gameRoom.NextPlayer,
            Pinte = gameRoom.Pinte,
            PinteType = gameRoom.PinteType!.Type,
            UsedCards = gameRoom.UsedCards,
            GameState = gameRoom.State,
            GainedCards = playerData.GainedCards,
        };
}
