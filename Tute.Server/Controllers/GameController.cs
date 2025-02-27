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

#if !DEBUG
[Heartbeat(Enable = true, Interval = 15_000, Timeout = 10_000)]
#endif
public class GameController(
    GameRoom gameRoom,
    Player self,
    IGroup<IGamingHubReceiver> room,
    ServiceContext context
)
{
    private static readonly Dictionary<int, int> cardsCountByPlayerCount = new()
    {
        [2] = 8,
        [3] = 12,
        [4] = 10
    };

    private readonly SemaphoreSlim semaphoreSlim = new(1);
    private Guid ConnectionId => self.ConnectionId;
    public bool IsPlayerInRoom => gameRoom.Players.Any(i => i.ConnectionId == ConnectionId);

    public async ValueTask<bool> LeaveAsync()
    {
        if (gameRoom.State == GameState.Playing)
        {
            FinishGame();
        }

        if (gameRoom.State == GameState.Room)
        {
            await ExitSelf();
            if (gameRoom.Players.Any() && self.IsLeader)
            {
                var player = gameRoom.Players.First();
                player.IsLeader = true;
                player.TeamIndex = -1;
                room.All.OnUpdated(player);
            }
        }

        return gameRoom.Players.Count == 0;
    }

    private async ValueTask ExitSelf()
    {
        await semaphoreSlim.WaitAsync();
        try
        {
            gameRoom.Players?.Remove(self);
            gameRoom.PlayerDataByConnetion?.Remove(ConnectionId);
            await room.RemoveAsync(context);
        }
        finally
        {
            semaphoreSlim.Release();
        }
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

        if (gameRoom.Players.Count < 2)
        {
            throw new ReturnStatusException((StatusCode)400, "Not enough players to start");
        }

        room.All.OnStart();

        var gameCards = InitGameRoom();
        var pinte = GetPinte(gameCards);
        room.All.OnChangedPinte(pinte);
        InitPlayersData();
        AssignCards(gameCards);
        AssignTeams();
        EmitGameDataForEachPlayer();
        room.All.OnChangedPinte(gameRoom.Pinte);

        return ValueTask.CompletedTask;
    }

    private void AssignTeams()
    {
        gameRoom.PlayersByTeam.Clear();

        if (gameRoom.Players.Count is 4)
        {
            var teams = gameRoom.Players.Index().GroupBy(i => i.Index % 2 == 0);
            foreach (var (teamIndex, team) in teams.Index())
            {
                var players = team.Select(i => i.Item).ToArray();
                gameRoom.PlayersByTeam[teamIndex] = players;
                foreach (var player in players)
                {
                    player.TeamIndex = teamIndex;
                    room.All.OnUpdated(player);
                }
            }
        }
        else
        {
            foreach (var (teamIndex, player) in gameRoom.Players.Index())
            {
                gameRoom.PlayersByTeam[teamIndex] = [player];
                player.TeamIndex = teamIndex;
                room.All.OnUpdated(player);
            }
        }
    }

    private CardData GetPinte(Stack<CardData> gameCards)
    {
        var pinte = gameCards.Last();
        gameRoom.Pinte = pinte;
        gameRoom.PinteType = pinte;
        return pinte;
    }

    private Stack<CardData> InitGameRoom()
    {
        var allCardsShuffled = gameRoom.Shuffled();
        if (gameRoom.Players.Count == 3)
        {
            allCardsShuffled = allCardsShuffled.Where(i => i.Number != 2).ToList();
        }
        var gameCards = new Stack<CardData>(allCardsShuffled);
        gameRoom.State = GameState.Playing;
        gameRoom.PlayerDataByConnetion ??= [];
        gameRoom.UsedCardsByConnection = [];
        gameRoom.Cards = gameCards;
        gameRoom.StartIndex ??= 0;
        gameRoom.NextPlayer = gameRoom.Players[gameRoom.StartIndex.Value];
        gameRoom.WinnerId = null;
        return gameCards;
    }

    private void InitPlayersData()
    {
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
    }

    private void AssignCards(Stack<CardData> gameCards)
    {
        //Timeout to shuffle
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));


        //Add cards
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            var currentplayer = gameRoom.Players.First();

            foreach (var item in gameRoom.Players)
            {
                var isNext = gameCards.TryPop(out var nextCard);
                if (isNext)
                    gameRoom.PlayerDataByConnetion[item.ConnectionId].Cards.Add(nextCard);
            }

            var firstCardsCount = gameRoom.PlayerDataByConnetion.Values.First().Cards.Count;

            if (!gameRoom.PlayerDataByConnetion.Values.All(i => i.Cards.Count == firstCardsCount))
            {
                throw new InvalidOperationException("Error assigning cards");
            }

            if (gameRoom.PlayerDataByConnetion.Values.All(i => i.Cards.Count == cardsCountByPlayerCount[gameRoom.Players.Count]))
            {
                break;
            }
        }

        if (cancellationTokenSource.Token.IsCancellationRequested)
        {
            throw new TimeoutException("Server timeouted while assigning cards");
        }

        if (gameCards.Count == 0)
        {
            gameRoom.Pinte = null;
        }
    }

    public void CheckTute(PlayerData winnerPlayer)
    {
        foreach (var (id, playerData) in gameRoom.PlayerDataByConnetion)
        {
            if (winnerPlayer.TeamIndex != playerData.TeamIndex)
                continue;

            var tuteKingCards = playerData.Cards.Where(i => i.Number is 12).ToList();
            var tutePrinceCards = playerData.Cards.Where(i => i.Number is 11).ToList();

            if (tuteKingCards.Count != 4 && tutePrinceCards.Count != 4)
            {
                continue;
            }

            var toUse = tuteKingCards.Count == 4 ? tuteKingCards : tutePrinceCards;

            var tute = CardsConstants.GetTute(toUse.First().Number);

            playerData.GainedCards.Add(tute);
            room.All.OnTute(playerData.Player, tute);

            gameRoom.NextPlayer = null;
            EmitGameDataForEachPlayer();
            FinishGame();
            throw new ReturnStatusException(StatusCode.OK, "Game ended");
        }
    }

    public void CheckCantes(PlayerData winnerPlayer)
    {
        foreach (var (id, playerData) in gameRoom.PlayerDataByConnetion)
        {
            if (winnerPlayer.TeamIndex != playerData.TeamIndex)
                continue;

            var alreadycantes = playerData
                .Cantes.OrderByDescending(i => i.Number)
                .Select(i => i.Type)
                .ToList();

            var cantes = playerData
                .Cards.Where(i => i.Number is 11 || i.Number is 12)
                .GroupBy(i => i.Type)
                .Where(i => !alreadycantes.Contains(i.Key))
                .FirstOrDefault(i => i.Count() is 2);

            if (cantes == null || !cantes.Any())
            {
                return;
            }

            var king = cantes.FirstOrDefault(i => i.Number is 12);
            var prince = cantes.FirstOrDefault(i => i.Number is 11);

            if (king == null || prince == null)
            {
                return;
            }

            var value = CardsConstants.GetCante(king!.Type, gameRoom.PinteType!.Type);
            playerData.GainedCards.Add(value);
            room.All.OnCante(playerData.Player, value);
        }
    }

    public async ValueTask ChangePinte(CardData card)
    {
        AssertPlaying();

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
            room.Single(ConnectionId)
                .OnGameData(CreateDataFor(gameRoom.PlayerDataByConnetion[ConnectionId]));
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    public async ValueTask MakeMove(CardData card)
    {
        AssertPlaying();

        if (ConnectionId != gameRoom.NextPlayer?.ConnectionId)
            throw new ReturnStatusException((StatusCode)400, "Not your turn");

        await semaphoreSlim.WaitAsync();
        try
        {
            //TODO Improve checks for 2 and 3 players
            CheckCardCanBeUsed(card);

            (PlayerData winnerPlayer, CardData winnerCard)? winnerCardbyPlayer = null;
            RemovePlayerCard(card, gameRoom);
            var oldNextPlayer = gameRoom.NextPlayer;
            gameRoom.NextPlayer = gameRoom.Players[GetNextPlayerIndex()];

            room.All.OnUsedCard(card, self);

            if (gameRoom.PlayerDataByConnetion.Count == gameRoom.UsedCardsByConnection.Count)
            {
                winnerCardbyPlayer = GetWinnerCardByPlayer();
                var winnerplayer = winnerCardbyPlayer.Value.winnerPlayer;
                gameRoom.NextPlayer = winnerplayer.Player;
                gameRoom.WinnerId = winnerplayer.Player.ConnectionId;

                winnerCardbyPlayer.Value.winnerPlayer.GainedCards =
                [
                    .. gameRoom.PlayerDataByConnetion[winnerplayer.Player.ConnectionId].GainedCards,
                    .. gameRoom.UsedCardsByConnection.Values,
                ];
                gameRoom.UsedCardsByConnection = [];

                CheckTute(winnerplayer);
                CheckCantes(winnerplayer);

                foreach (var (playerConnectionId, playerCards) in gameRoom.PlayerDataByConnetion)
                {
                    var isNext = gameRoom.Cards.TryPop(out var nextCard);
                    if (isNext)
                    {
                        playerCards.Cards.Add(nextCard);
                    }
                    if (!isNext && gameRoom.Pinte != null)
                    {
                        gameRoom.Pinte = null;
                        room.All.OnChangedPinte(gameRoom.Pinte);
                    }
                    room.Single(playerConnectionId)
                        .OnGameData(
                            CreateDataFor(gameRoom.PlayerDataByConnetion[playerConnectionId])
                        );
                }
            }
            else
            {
                EmitGameDataForEachPlayer();
            }

            var isGameFinished = gameRoom.PlayerDataByConnetion.All(i => i.Value.Cards.Count == 0);

            if (isGameFinished)
            {
                if (winnerCardbyPlayer is not null)
                {
                    var playerData = winnerCardbyPlayer.Value.winnerPlayer;
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

    private void CheckCardCanBeUsed(CardData card)
    {
        if (gameRoom.UsedCardsByConnection.Count == 0) return;

        var playerCards = gameRoom.PlayerDataByConnetion[ConnectionId].Cards;
        var firstCard = gameRoom.UsedCardsByConnection.Values.First();
        var greatestCard = gameRoom.UsedCardsByConnection.Values.Where(i => i.Type == firstCard.Type).MaxBy(i => i.Value);
        var typesToUse = gameRoom.UsedCardsByConnection.Values.Select(i => i.Type).Distinct().ToList();

        var usableCardsByPlayer = playerCards.Where(i => typesToUse.Contains(i.Type)).ToList();

        if (!typesToUse.Contains(card.Type) && usableCardsByPlayer.Any(i => typesToUse.Contains(i.Type)))
        {
            throw new ReturnStatusException((StatusCode)400, "You must use same type");
        }

        if (card.Value <= greatestCard?.Value && usableCardsByPlayer.Any(i => i.Value > greatestCard?.Value))
        {
            throw new ReturnStatusException((StatusCode)400, "You must use greater card");
        }

        if (!usableCardsByPlayer.Any(i => typesToUse.Contains(i.Type)) && usableCardsByPlayer.Any(i => i.Type == gameRoom.PinteType?.Type))
        {
            throw new ReturnStatusException((StatusCode)400, "You must use pinte");
        }
    }

    private void EmitGameDataForEachPlayer()
    {
        foreach (var (playerConnectionId, _) in gameRoom.PlayerDataByConnetion)
        {
            room.Single(playerConnectionId)
                .OnGameData(CreateDataFor(gameRoom.PlayerDataByConnetion[playerConnectionId]));
        }
    }

    private List<GameDataResponse> GetAllPlayersData() =>
        gameRoom.PlayerDataByConnetion.Values.Select(CreateDataFor).ToList();

    private (PlayerData winnerPlayerData, CardData winnerCard) GetWinnerCardByPlayer()
    {
        var firstCard = gameRoom.UsedCardsByConnection.FirstOrDefault().Value;

        var bestByValue = gameRoom
            .UsedCardsByConnection.Where(i => i.Value.Type == firstCard.Type)
            .MaxByOrDefault(i => i.Value.Value);

        var allSameValue =
            gameRoom
                .UsedCardsByConnection.Where(i => i.Value.Type == firstCard.Type)
                .Sum(i => i.Value.Value) == 0;

        var anyPinte = gameRoom.UsedCardsByConnection.Any(i =>
            i.Value.Type == gameRoom.PinteType?.Type
        );

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

        return (gameRoom.PlayerDataByConnetion[winner.Key], winner.Value);
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
        var winner = gameRoom
            .PlayerDataByConnetion.Values.OrderByDescending(i => i.GainedCards.Sum(i => i.Value))
            .FirstOrDefault();
        if (winner is not null)
            winner.WinsCount++;
        if (gameRoom.StartIndex++ == gameRoom.Players.Count - 1)
            gameRoom.StartIndex = 0;
        room.All.OnFinished(GetAllPlayersData());
        foreach (var item in gameRoom.Players)
        {
            item.TeamIndex = -1;
            room.All.OnUpdated(item);
        }
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
            WinnerId = gameRoom.WinnerId,
            TeamIndex = playerData.TeamIndex,
        };

    private void AssertPlaying()
    {
        if (gameRoom.State != GameState.Playing)
            throw new ReturnStatusException((StatusCode)400, "Game is not started");
    }
}
