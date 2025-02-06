using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Moq;
using Shouldly;
using Tute.Server.Tests.Fakes;
using Tute.Server.Tests.Helpers;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Tests;

public class GamingHubTests : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly GrpcTestFixture<Program> _fixture;
    private readonly ITestOutputHelper output;

    public GamingHubTests(ITestOutputHelper output)
    {
        _fixture = new GrpcTestFixture<Program>();
        _channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = _fixture.Handler }
        );
        this.output = output;
    }

    [Fact]
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
        receiverMock.Verify(i => i.OnJoin(It.IsAny<Player>()), Times.Once());
    }

    [Fact]
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

    [Fact]
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

        receiverMock.Verify(i => i.OnLeave(It.Is<Player>(i => i.Name == "first")), Times.Once());
    }

    [Fact]
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

    [Fact]
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
        receiverMock.Verify(i => i.OnStart(), Times.Never());

        await client.StartAsync();
        receiverMock.Verify(i => i.OnStart(), Times.Exactly(2));
    }

    [Fact]
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
        receiverMock.Verify(i => i.OnStart(), Times.Never());
    }

    [Fact]
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
        receiverMock.Verify(
            i => i.OnLeave(It.Is<Player>(i => i.Name == "first" && i.IsLeader == true)),
            Times.Once()
        );
        receiverMock.Verify(
            i => i.OnLeave(It.Is<Player>(i => i.Name == "second" && i.IsLeader == false)),
            Times.Once()
        );
        receiverMock.Verify(
            i => i.OnJoin(It.Is<Player>(i => i.Name == "second" && i.IsLeader == true)),
            Times.Once()
        );
    }

    [Fact]
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

        receiverMock.Verify(i => i.OnLeave(It.IsAny<Player>()), Times.Once());
        receiverMock.Verify(
            i => i.OnFinished(It.Is<List<GameDataResponse>>(i => i.Count == 2)),
            Times.Once()
        );
    }

    [Theory(Timeout = 10_000)]
    [InlineData("deck", false)]
    [InlineData("short_deck", false)]
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

        var handler1 = GameHandler(clientFake1, client, player1, TestContext.Current.CancellationToken);
        var handler2 = GameHandler(clientFake2, client2, player2, TestContext.Current.CancellationToken);

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
    }

    private async Task GameHandler(
        GamingHubReceiverFake receiver,
        IGamingHub client,
        Player player,
        CancellationToken cancellationToken
    )
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
                    ?? receiver.GameDataResponse?.PlayerData.Cards.FirstOrDefault();

                //Just wait till onFinish is received
                if (cardTouse is null || receiver.GameDataResponse?.NextPlayer is null)
                {
                    await receiver.IsFinished.Task;
                    break;
                }


                output.WriteLine($"Player {player.Name} is trying to make a move");
                await client.MakeMove(cardTouse);
                output.WriteLine($"Player {player.Name} completed a move");
            }

            await Task.Yield();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
