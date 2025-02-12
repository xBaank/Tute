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
            FinishGame();
        }

        if (gameRoom.State == GameState.Room)
        {
            await ExitSelf();
            if (gameRoom.Players.Count == 1)
            {
                var player = gameRoom.Players.First();
                player.IsLeader = true;
                room.All.OnUpdated(player);
            }
        }

        return gameRoom.Players.Count == 0;
    }

    private async ValueTask ExitSelf()
    {
        gameRoom.Players?.Remove(self);
        gameRoom.PlayerDataByConnetion?.Remove(ConnectionId);
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

        var gameCards = new Stack<CardData>(gameRoom.Shuffled());

        gameRoom.State = GameState.Playing;
        gameRoom.PlayerDataByConnetion ??= [];
        gameRoom.UsedCardsByConnection = [];
        gameRoom.Cards = gameCards;
        //TODO should persist next player between games
        gameRoom.NextPlayer ??= gameRoom.Players.First();
        gameRoom.MoveIndex = 0;

        //Init players data
        foreach (var item in gameRoom.Players)
        {
            if (gameRoom.PlayerDataByConnetion.TryGetValue(item.ConnectionId, out var value))
            {
                value.Cards = [];
                value.GainedCards = [];
                value.Player = item;
            }
            else
            {
                gameRoom.PlayerDataByConnetion[item.ConnectionId] = new PlayerData()
                {
                    Cards = [],
                    GainedCards = [],
                    Player = item,
                };
            }
        }

        //Add cards
        while (true)
        {
            var currentplayer = gameRoom.Players.First();

            foreach (var item in gameRoom.Players)
            {
                var isNext = gameCards.TryPop(out var nextCard);
                if (isNext) gameRoom.PlayerDataByConnetion[item.ConnectionId].Cards.Add(nextCard);
            }

            var firstCardsCount = gameRoom.PlayerDataByConnetion.Values.First().Cards.Count;

            if (!gameRoom.PlayerDataByConnetion.Values.All(i => i.Cards.Count == firstCardsCount))
            {
                throw new InvalidOperationException("Error assigning cards");
            }

            if (gameRoom.PlayerDataByConnetion.Values.All(i => i.Cards.Count == 7))
            {
                break;
            }
        }

        var pinte = gameCards.Pop();
        gameRoom.Pinte = pinte;
        gameRoom.PinteType = pinte;

        EmitGameDataForEachPlayer();
        room.All.OnChangedPinte(pinte);

        return ValueTask.CompletedTask;
    }

    public async ValueTask Tute(IList<CardData> cards)
    {
        CheckPlaying();

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

            gameRoom.PlayerDataByConnetion[ConnectionId].GainedCards.Add(tute);
            room.All.OnTute(self, tute);

            foreach (var item in gameRoom.PlayerDataByConnetion)
            {
                item.Value.Cards.Clear();
            }

            gameRoom.NextPlayer = null;

            EmitGameDataForEachPlayer();
            FinishGame();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask Cante(CardData king, CardData prince)
    {
        CheckPlaying();

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
            var hasKing = gameRoom.PlayerDataByConnetion[ConnectionId].Cards.Any(i => i.Name == king.Name);
            var hasPrince = gameRoom.PlayerDataByConnetion[ConnectionId].Cards.Any(i => i.Name == prince.Name);
            if (!hasKing || !hasPrince)
            {
                throw new ReturnStatusException((StatusCode)400, "You don't have the cards");
            }

            if (king!.Type != prince!.Type)
            {
                throw new ReturnStatusException((StatusCode)400, "Cards must be same type");
            }

            var cantes = CardsConstants
                .GetCantes(gameRoom.PlayerDataByConnetion[ConnectionId].GainedCards)
                .Select(i => i.Type);

            if (cantes.Contains(king!.Type))
            {
                throw new ReturnStatusException((StatusCode)400, "Already did");
            }

            var value = CardsConstants.GetCante(king!.Type, gameRoom.PinteType.Type);
            gameRoom.PlayerDataByConnetion[ConnectionId].GainedCards.Add(value);

            room.Single(ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerDataByConnetion[ConnectionId]));
            room.All.OnCante(self, value);
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask ChangePinte(CardData card)
    {
        CheckPlaying();

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

            var cards = gameRoom.PlayerDataByConnetion[ConnectionId].Cards;
            var toRemove =
                cards.FirstOrDefault(i => i.Name == card.Name)
                ?? throw new ReturnStatusException((StatusCode)400, "You don't have that card");
            cards.Remove(toRemove);
            cards.Add(gameRoom.Pinte);
            gameRoom.Pinte = toRemove;

            room.All.OnChangedPinte(card);
            room.Single(ConnectionId).OnGameData(CreateDataFor(gameRoom.PlayerDataByConnetion[ConnectionId]));
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask MakeMove(CardData card)
    {
        CheckPlaying();

        if (ConnectionId != gameRoom.NextPlayer?.ConnectionId)
            throw new ReturnStatusException((StatusCode)400, "Not your turn");

        await semaphoreSlim.WaitAsync();
        try
        {
            var typeToUse = gameRoom.UsedCardsByConnection.FirstOrDefault().Value;
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
                .PlayerDataByConnetion[ConnectionId]
                .Cards.Any(i => i.Type == typeToUse?.Type && i.Value > typeToUse?.Value);
            var hasSameType = gameRoom
                .PlayerDataByConnetion[ConnectionId]
                .Cards.Any(i => i.Type == typeToUse?.Type);
            var hasPinte = gameRoom
                .PlayerDataByConnetion[ConnectionId]
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

            if (gameRoom.PlayerDataByConnetion.Count == gameRoom.UsedCardsByConnection.Count)
            {
                winner = GetWinner(gameRoom);
                var winnerKey = winner.Value.Key;
                gameRoom.NextPlayer = gameRoom.PlayerDataByConnetion[winnerKey].Player;
                gameRoom.MoveIndex++;

                gameRoom.PlayerDataByConnetion[winnerKey].GainedCards =
                [
                    .. gameRoom.PlayerDataByConnetion[winnerKey].GainedCards,
                    .. gameRoom.UsedCardsByConnection.Values,
                ];
                gameRoom.UsedCardsByConnection = [];

                foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerDataByConnetion)
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
                        .OnGameData(CreateDataFor(gameRoom.PlayerDataByConnetion[playerConnectionId]));
                }
            }
            else
            {
                EmitGameDataForEachPlayer();
            }

            var isGameFinished = gameRoom.PlayerDataByConnetion.All(i => i.Value.Cards.Count == 0);

            if (isGameFinished)
            {
                if (winner is not null)
                {
                    var playerData = gameRoom.PlayerDataByConnetion[winner.Value.Key];
                    playerData.GainedCards.Add(CardsConstants.DiezDelMonte);
                    room.All.OnDiezDelMonte(playerData.Player, CardsConstants.DiezDelMonte);
                }
                gameRoom.NextPlayer = null;
                EmitGameDataForEachPlayer();
                FinishGame();
            }
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private void EmitGameDataForEachPlayer()
    {
        foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerDataByConnetion)
        {
            room.Single(playerConnectionId)
                .OnGameData(CreateDataFor(gameRoom.PlayerDataByConnetion[playerConnectionId]));
        }
    }

    private List<GameDataResponse> GetAllPlayersData() =>
        gameRoom.PlayerDataByConnetion.Values.Select(CreateDataFor).ToList();

    private KeyValuePair<Guid, CardData> GetWinner(GameRoom gameRoom)
    {
        var firstCard = gameRoom.UsedCardsByConnection.FirstOrDefault().Value;

        var bestByValue = gameRoom
            .UsedCardsByConnection.Where(i => i.Value.Type == firstCard.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var allSameValue =
            gameRoom.UsedCardsByConnection.Where(i => i.Value.Type == firstCard.Type).Sum(i => i.Value.Value)
            == 0;

        var anyPinte = gameRoom.UsedCardsByConnection.Any(i => i.Value.Type == gameRoom.PinteType?.Type);

        KeyValuePair<Guid, CardData>? bestByNumber = null;

        if (bestByValue is not null && allSameValue && !anyPinte)
        {
            bestByNumber = gameRoom
                .UsedCardsByConnection.Where(i => i.Value.Type == firstCard.Type)
                .MaxByOrDefault(i => i.Value.Number);
        }

        var bestByType = gameRoom
            .UsedCardsByConnection.Where(i => i.Value.Type == gameRoom.PinteType?.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var winner =
            bestByNumber
            ?? bestByType
            ?? bestByValue
            ?? throw new InvalidOperationException("No winner found");
        return winner;
    }

    private void RemovePlayerCard(CardData card, GameRoom gameRoom)
    {
        var cardToRemove = gameRoom
            .PlayerDataByConnetion[ConnectionId]
            .Cards.FirstOrDefault(i => i.Name == card.Name);

        if (!gameRoom.PlayerDataByConnetion[ConnectionId].Cards.Remove(cardToRemove))
            throw new ReturnStatusException((StatusCode)400, "No card to remove found");

        gameRoom.UsedCardsByConnection[ConnectionId] = card;
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

    private void FinishGame()
    {
        gameRoom.State = GameState.Room;
        var winner = gameRoom.PlayerDataByConnetion.Values.OrderByDescending(i => i.GainedCards.Sum(i => i.Value)).FirstOrDefault();
        if (winner is not null) winner.WinsCount++;
        room.All.OnFinished(GetAllPlayersData());
    }

    private GameDataResponse CreateDataFor(PlayerData playerData) =>
        new()
        {
            PlayerData = playerData,
            NextPlayer = gameRoom.NextPlayer,
            Pinte = gameRoom.Pinte,
            PinteType = gameRoom.PinteType!.Type,
            UsedCards = gameRoom.UsedCardsByConnection,
            GameState = gameRoom.State,
            GainedCards = playerData.GainedCards,
            MoveIndex = gameRoom.MoveIndex,
        };

    private void CheckPlaying()
    {
        if (gameRoom.State != GameState.Playing) throw new ReturnStatusException((StatusCode)400, "Game is not started");
    }
}
