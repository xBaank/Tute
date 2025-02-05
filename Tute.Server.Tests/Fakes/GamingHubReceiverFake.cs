using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Tests.Fakes;

internal class GamingHubReceiverEmptyFake : IGamingHubReceiver
{
    public static readonly GamingHubReceiverEmptyFake Instance = new();

    private GamingHubReceiverEmptyFake() { }

    public void OnCante(Player player, CardData cante)
    {
    }

    public void OnChangedPinte(CardData cardData)
    {
    }

    public void OnFinished(IList<GameDataResponse> allPlayerData)
    {
    }

    public void OnGameData(GameDataResponse gameData)
    {
    }

    public void OnJoin(Player player)
    {
    }

    public void OnLeave(Player player)
    {
    }

    public void OnMessage(string message, Player player)
    {
    }

    public void OnStart()
    {
    }

    public void OnTute(Player player, CardData tute)
    {
    }

    public void OnUsedCard(CardData card, Player userCard)
    {
    }
}
