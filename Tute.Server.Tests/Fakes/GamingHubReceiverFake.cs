using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Tests.Fakes;

internal class GamingHubReceiverFake : IGamingHubReceiver
{
    public GameDataResponse? GameDataResponse { get; private set; }
    public bool IsFinished { get; set; }

    private SemaphoreSlim _semaphoreSlim = new(1);

    public void OnCante(Player player, CardData cante) { }

    public void OnChangedPinte(CardData cardData) { }

    public void OnFinished(List<GameDataResponse> allPlayerData)
    {
        IsFinished = true;
    }

    public void OnGameData(GameDataResponse gameData) => SetData(gameData).GetAwaiter().GetResult();
    public async Task SetData(GameDataResponse gameData)
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            Console.WriteLine($"Setted {gameData.PlayerData.Player.Name}");
            GameDataResponse = gameData;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public void OnJoin(Player player) { }

    public void OnLeave(Player player) { }

    public void OnMessage(string message, Player player) { }

    public void OnStart() { }

    public void OnTute(Player player, CardData tute) { }

    public void OnUsedCard(CardData card, Player userCard) { }
}
