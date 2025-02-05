using Grpc.Net.Client;
using MagicOnion.Client;
using Moq;
using Shouldly;
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
    public async Task Should_Join()
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
    public async Task Should_Leave()
    {
        var receiverMock = new Mock<IGamingHubReceiver>();

        var client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        var client2 = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(_channel, receiverMock.Object!, cancellationToken: TestContext.Current.CancellationToken);
        _ = await client.JoinAsync("test", "first");
        var (self, players) = await client2.JoinAsync("test", "second");

        await client.LeaveAsync();
        receiverMock.Verify(i => i.OnLeave(It.Is<Player>(i => i.Name == "first")), Times.Once());
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
