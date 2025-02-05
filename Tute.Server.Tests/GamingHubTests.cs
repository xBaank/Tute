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

    public GamingHubTests()
    {
        _fixture = new GrpcTestFixture<Program>();
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _fixture.Handler
        });
    }

    [Fact]
    public async Task Should_join_a_room()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();

        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
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
    public async Task Should_leave_a_room()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();

        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        _ = await client.JoinAsync("test", "first");
        var (self, players) = await client2.JoinAsync("test", "second");

        await client.LeaveAsync();
        receiverMock.Verify(i => i.OnLeave(It.Is<Player>(i => i.Name == "first")), Times.Once());
    }

    [Fact]
    public async Task Should_start_a_game()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();

        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
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
    public async Task Should_change_leader()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();

        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, GamingHubReceiverEmptyFake.Instance, cancellationToken: TestContext.Current.CancellationToken);
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        var (player1, _) = await client.JoinAsync("test", "first");
        var (player2, players) = await client2.JoinAsync("test", "second");

        player1.IsLeader.ShouldBeTrue();
        player2.IsLeader.ShouldBeFalse();

        await client.LeaveAsync();
        receiverMock.Verify(i => i.OnLeave(It.Is<Player>(i => i.Name == "first" && i.IsLeader == true)), Times.Once());
        receiverMock.Verify(i => i.OnLeave(It.Is<Player>(i => i.Name == "second" && i.IsLeader == false)), Times.Once());
        receiverMock.Verify(i => i.OnJoin(It.Is<Player>(i => i.Name == "second" && i.IsLeader == true)), Times.Once());
    }

    [Fact]
    public async Task Should_stop_game_while_playing_when_player_leave()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();

        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, GamingHubReceiverEmptyFake.Instance, cancellationToken: TestContext.Current.CancellationToken);
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        var (player1, _) = await client.JoinAsync("test", "first");
        var (player2, players) = await client2.JoinAsync("test", "second");

        await client.StartAsync();
        await client.LeaveAsync();

        receiverMock.Verify(i => i.OnLeave(It.IsAny<Player>()), Times.Once());
        receiverMock.Verify(i => i.OnFinished(It.Is<IList<GameDataResponse>>(i => i.Count == 1)), Times.Once());
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
