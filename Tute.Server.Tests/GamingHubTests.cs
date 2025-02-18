using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Moq;
using Shouldly;
using Tute.Server.Tests.Extensions;
using Tute.Server.Tests.Fakes;
using Tute.Server.Tests.Helpers;
using Tute.Shared.Constants;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Tests;

public class GamingHubTests : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly GrpcTestFixture<Program> _fixture;
    private readonly ITestOutputHelper output;
    private readonly TimeSpan _verifyTimeout = TimeSpan.FromSeconds(5);

    public GamingHubTests(ITestOutputHelper output)
    {
        _fixture = new GrpcTestFixture<Program>();
        _channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = _fixture.Handler }
        );
        this.output = output;
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_join_a_room()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        _ = await client.JoinAsync("test", "first");
        var (self, players) = await client2.JoinAsync("test", "second");
        var playerNames = players.Select(p => p.Name).ToList();

        self.ShouldNotBeNull();
        self.Name.ShouldBe("second");
        players.Length.ShouldBe(2);
        playerNames.ShouldContain("first");
        playerNames.ShouldContain("second");

        await receiverMock.AsyncVerify(
            i => i.OnJoin(It.IsAny<Player>()),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_not_join_twice_a_room()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        _ = await client.JoinAsync("test", "first");

        var join = async () => await client.JoinAsync("test", "first");

        await join.ShouldThrowAsync<RpcException>();
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_leave_a_room()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        _ = await client.JoinAsync("test", "first");
        var (self, players) = await client2.JoinAsync("test", "second");

        await client.LeaveAsync();

        await receiverMock.AsyncVerify(
            i => i.OnLeave(It.Is<Player>(i => i.Name == "first")),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_not_leave_if_user_is_not_in_a_room()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var leave = async () => await client.LeaveAsync();

        await leave.ShouldThrowAsync<RpcException>();
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_start_a_game()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first");
        var (player2, players) = await client2.JoinAsync("test", "second");

        player1.IsLeader.ShouldBeTrue();
        player2.IsLeader.ShouldBeFalse();

        await client2.StartAsync();
        await receiverMock.AsyncVerify(
            i => i.OnStart(),
            Times.Never(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );

        await client.StartAsync();
        await receiverMock.AsyncVerify(
            i => i.OnStart(),
            Times.Exactly(2),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_not_start_a_game_if_there_are_not_enough_players()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        _ = await client.JoinAsync("test", "first");

        var start = async () => await client.StartAsync();

        await start.ShouldThrowAsync<RpcException>();
        await receiverMock.AsyncVerify(
            i => i.OnStart(),
            Times.Never(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_change_leader()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            GamingHubReceiverEmptyFake.Instance,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first");
        var (player2, players) = await client2.JoinAsync("test", "second");

        player1.IsLeader.ShouldBeTrue();
        player2.IsLeader.ShouldBeFalse();

        await client.LeaveAsync();
        await receiverMock.AsyncVerify(
            i => i.OnLeave(It.Is<Player>(i => i.Name == "first" && i.IsLeader == true)),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
        await receiverMock.AsyncVerify(
            i => i.OnUpdated(It.Is<Player>(i => i.Name == "second" && i.IsLeader == true)),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Fact(Timeout = 30_000)]
    public async Task Should_stop_game_while_playing_when_player_leave()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            GamingHubReceiverEmptyFake.Instance,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            receiverMock.Object!,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first");
        var (player2, players) = await client2.JoinAsync("test", "second");

        await client.StartAsync();
        await client.LeaveAsync();

        await receiverMock.AsyncVerify(
            i => i.OnLeave(It.IsAny<Player>()),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
        await receiverMock.AsyncVerify(
            i => i.OnFinished(It.Is<List<GameDataResponse>>(i => i.Count == 2)),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Theory(Timeout = 30_000)]
    [InlineData("deck", false)]
    [InlineData("short_deck", false)]
    [InlineData("cante_20_deck", false)]
    [InlineData("cante_40_deck", false)]
    public async Task Should_play_the_game_and_finish(string deckName, bool shuffled)
    {
        var clientFake1 = new GamingHubReceiverFake(output);
        var clientFake2 = new GamingHubReceiverFake(output);
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake2,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first", deckName, shuffled);
        var (player2, _) = await client2.JoinAsync("test", "second");

        await client.StartAsync();

        var handler1 = GameHandler(
            clientFake1,
            client,
            player1,
            TestContext.Current.CancellationToken
        );
        var handler2 = GameHandler(
            clientFake2,
            client2,
            player2,
            TestContext.Current.CancellationToken
        );

        await Task.WhenAll(handler1, handler2);

        clientFake1.IsFinished.Task.IsCompletedSuccessfully.ShouldBeTrue();
        clientFake2.IsFinished.Task.IsCompletedSuccessfully.ShouldBeTrue();
        clientFake1.GameDataResponse.ShouldNotBeNull();
        clientFake2.GameDataResponse.ShouldNotBeNull();
        clientFake1.GameDataResponse.PlayerData.Cards.ShouldBeEmpty();
        clientFake2.GameDataResponse.PlayerData.Cards.ShouldBeEmpty();
        clientFake1.GameDataResponse.NextPlayer.ShouldBeNull();
        clientFake2.GameDataResponse.NextPlayer.ShouldBeNull();
        clientFake1.FinalGameDataResponse.ShouldNotBeNull();
        clientFake2.FinalGameDataResponse.ShouldNotBeNull();
        clientFake1.FinalGameDataResponse.Count.ShouldBe(2);
        clientFake2.FinalGameDataResponse.Count.ShouldBe(2);
        clientFake1.FinalGameDataResponse.All(i => i.GameState == GameState.Room).ShouldBeTrue();
        clientFake2.FinalGameDataResponse.All(i => i.GameState == GameState.Room).ShouldBeTrue();
        clientFake1.FinalGameDataResponse.First().PlayerData.WinsCount.ShouldBe(1);
        clientFake2.FinalGameDataResponse.First().PlayerData.WinsCount.ShouldBe(1);
        clientFake1.FinalGameDataResponse.ElementAt(1).PlayerData.WinsCount.ShouldBe(0);
        clientFake2.FinalGameDataResponse.ElementAt(1).PlayerData.WinsCount.ShouldBe(0);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("deck", false)]
    public async Task Should_change_next_player_on_next_game(string deckName, bool shuffled)
    {
        var clientFake1 = new GamingHubReceiverFake(output);
        var clientFake2 = new GamingHubReceiverFake(output);
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake2,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first", deckName, shuffled);
        var (player2, _) = await client2.JoinAsync("test", "second");

        await client.StartAsync();

        clientFake1.GameDataResponse?.NextPlayer.ShouldNotBeNull();
        clientFake1.GameDataResponse!.NextPlayer?.ConnectionId.ShouldBe(player1.ConnectionId);
        clientFake2.GameDataResponse?.NextPlayer.ShouldNotBeNull();
        clientFake2.GameDataResponse!.NextPlayer?.ConnectionId.ShouldBe(player1.ConnectionId);

        var handler1 = GameHandler(
            clientFake1,
            client,
            player1,
            TestContext.Current.CancellationToken
        );
        var handler2 = GameHandler(
            clientFake2,
            client2,
            player2,
            TestContext.Current.CancellationToken
        );

        await Task.WhenAll(handler1, handler2);

        await client.StartAsync();

        await clientFake1.Mock.AsyncVerify(i => i.OnGameData(It.IsAny<GameDataResponse>()), Times.AtLeastOnce(), TimeSpan.FromSeconds(5), token: TestContext.Current.CancellationToken);
        await clientFake2.Mock.AsyncVerify(i => i.OnGameData(It.IsAny<GameDataResponse>()), Times.AtLeastOnce(), TimeSpan.FromSeconds(5), token: TestContext.Current.CancellationToken);

        clientFake1.GameDataResponse?.NextPlayer.ShouldNotBeNull();
        clientFake1.GameDataResponse!.NextPlayer?.ConnectionId.ShouldBe(player2.ConnectionId);
        clientFake2.GameDataResponse?.NextPlayer.ShouldNotBeNull();
        clientFake2.GameDataResponse!.NextPlayer?.ConnectionId.ShouldBe(player2.ConnectionId);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("short_deck", false)]
    public async Task Should_get_las_diez_del_monte(string deckName, bool shuffled)
    {
        var clientFake1 = new GamingHubReceiverFake(output);
        var clientFake2 = new GamingHubReceiverFake(output);
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake2,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first", deckName, shuffled);
        var (player2, players) = await client2.JoinAsync("test", "second");

        await client.StartAsync();

        var handler1 = GameHandler(
            clientFake1,
            client,
            player1,
            TestContext.Current.CancellationToken
        );
        var handler2 = GameHandler(
            clientFake2,
            client2,
            player2,
            TestContext.Current.CancellationToken
        );

        await Task.WhenAll(handler1, handler2);

        await clientFake1.Mock.AsyncVerify(
            i =>
                i.OnDiezDelMonte(
                    It.Is<Player>(i => i.ConnectionId == player1.ConnectionId),
                    It.Is<CardData>(i => i.Number == CardsConstants.DiezDelMonte.Number)
                ),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
        await clientFake2.Mock.AsyncVerify(
            i =>
                i.OnDiezDelMonte(
                    It.Is<Player>(i => i.ConnectionId == player1.ConnectionId),
                    It.Is<CardData>(i => i.Number == CardsConstants.DiezDelMonte.Number)
                ),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Theory(Timeout = 30_000)]
    [InlineData("cante_20_deck", false, CardsConstants.VeinteEnCopasNumber)]
    [InlineData("cante_40_deck", false, CardsConstants.CuarentaEnCopasNumber)]
    public async Task Should_cantar_las_veinte(string deckName, bool shuffled, int cardNumber)
    {
        var clientFake1 = new GamingHubReceiverFake(output);
        var clientFake2 = new GamingHubReceiverFake(output);
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake2,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first", deckName, shuffled);
        var (player2, players) = await client2.JoinAsync("test", "second");

        await client.StartAsync();

        var handler1 = GameHandler(
            clientFake1,
            client,
            player1,
            TestContext.Current.CancellationToken
        );
        var handler2 = GameHandler(
            clientFake2,
            client2,
            player2,
            TestContext.Current.CancellationToken
        );

        await Task.WhenAll(handler1, handler2);

        await clientFake1.Mock.AsyncVerify(
            i =>
                i.OnCante(
                    It.Is<Player>(i => i.ConnectionId == player2.ConnectionId),
                    It.Is<CardData>(i => i.Number == cardNumber)
                ),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
        await clientFake2.Mock.AsyncVerify(
            i =>
                i.OnCante(
                    It.Is<Player>(i => i.ConnectionId == player2.ConnectionId),
                    It.Is<CardData>(i => i.Number == cardNumber)
                ),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    [Theory(Timeout = 30_000)]
    [InlineData("tute_kings_deck", false, CardsConstants.TuteReyesNumber)]
    [InlineData("tute_prince_deck", false, CardsConstants.TutePrincipesNumber)]
    public async Task Should_tute(string deckName, bool shuffled, int cardNumber)
    {
        var clientFake1 = new GamingHubReceiverFake(output);
        var clientFake2 = new GamingHubReceiverFake(output);
        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
            _channel,
            clientFake2,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var (player1, _) = await client.JoinAsync("test", "first", deckName, shuffled);
        var (player2, players) = await client2.JoinAsync("test", "second");

        await client.StartAsync();

        var handler1 = GameHandler(
            clientFake1,
            client,
            player1,
            TestContext.Current.CancellationToken
        );
        var handler2 = GameHandler(
            clientFake2,
            client2,
            player2,
            TestContext.Current.CancellationToken
        );

        await Task.WhenAll(handler1, handler2);

        clientFake1.IsFinished.Task.IsCompletedSuccessfully.ShouldBeTrue();
        clientFake2.IsFinished.Task.IsCompletedSuccessfully.ShouldBeTrue();
        await clientFake1.Mock.AsyncVerify(
            i =>
                i.OnTute(
                    It.Is<Player>(i => i.ConnectionId == player2.ConnectionId),
                    It.Is<CardData>(i => i.Number == cardNumber)
                ),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
        await clientFake2.Mock.AsyncVerify(
            i =>
                i.OnTute(
                    It.Is<Player>(i => i.ConnectionId == player2.ConnectionId),
                    It.Is<CardData>(i => i.Number == cardNumber)
                ),
            Times.Once(),
            _verifyTimeout,
            token: TestContext.Current.CancellationToken
        );
    }

    private async Task GameHandler(
        GamingHubReceiverFake receiver,
        IGamingHub client,
        Player player,
        CancellationToken cancellationToken
    ) =>
        await Task.Run(
            async () =>
            {
                while (!receiver.IsFinished.Task.IsCompletedSuccessfully)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (receiver.GameDataResponse?.NextPlayer?.ConnectionId == player.ConnectionId)
                    {
                        var pinte = receiver.GameDataResponse?.PinteType;

                        var firstCard =
                            receiver.GameDataResponse?.UsedCards.Count != 0 == true
                                ? receiver.GameDataResponse?.UsedCards.First().Value
                                : null;

                        var cardTouse =
                            receiver
                                .GameDataResponse?.PlayerData.Cards.OrderByDescending(i => i.Value)
                                .FirstOrDefault(i => i.Type == firstCard?.Type)
                            ?? receiver
                                .GameDataResponse?.PlayerData.Cards.OrderByDescending(i => i.Value)
                                .FirstOrDefault(i => i.Type == pinte)
                            ?? receiver
                                .GameDataResponse?.PlayerData.Cards.OrderByDescending(i => i.Value)
                                .FirstOrDefault();

                        //Just wait till onFinish is received
                        if (cardTouse is null || receiver.GameDataResponse?.NextPlayer is null)
                        {
                            await receiver.IsFinished.Task;
                            break;
                        }

                        output.WriteLine($"Player {player.Name} is trying to make a move");
                        try
                        {
                            await client.MakeMove(cardTouse);
                        }
                        catch (RpcException e)
                        {
                            if (e.StatusCode == StatusCode.OK)
                                break;
                        }
                        output.WriteLine($"Player {player.Name} completed a move");
                    }

                    await Task.Yield();
                }
            },
            cancellationToken
        );

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
