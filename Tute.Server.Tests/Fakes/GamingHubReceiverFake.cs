using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Tests.Fakes;

internal class GamingHubReceiverFake(ITestOutputHelper outputHelper) : IGamingHubReceiver
{
    public GameDataResponse? GameDataResponse { get; private set; }
    public TaskCompletionSource IsFinished { get; set; } = new();

    private SemaphoreSlim _semaphore = new(1);

    public void OnCante(Player player, CardData cante) { }

    public void OnChangedPinte(CardData cardData) { }

    public void OnFinished(List<GameDataResponse> allPlayerData)
    {
        outputHelper.WriteLine("Game finished");
        IsFinished.TrySetResult();
    }

    public void OnGameData(GameDataResponse gameData)
    {
        _semaphore.Wait();
        try
        {
            outputHelper.WriteLine($"Received data for {gameData.PlayerData.Player.Name}, Next player is {gameData.NextPlayer.Name}");
            GameDataResponse = gameData;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void OnJoin(Player player) { }

    public void OnLeave(Player player) { }

    public void OnMessage(string message, Player player) { }

    public void OnStart() { }

    public void OnTute(Player player, CardData tute) { }

    public void OnUsedCard(CardData card, Player userCard) { }
}
