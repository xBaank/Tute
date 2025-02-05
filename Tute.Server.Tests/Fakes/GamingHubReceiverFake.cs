using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Tests.Fakes;

internal class GamingHubReceiverFake : IGamingHubReceiver
{
    public void OnCante(Player player, CardData cante)
    {
        //TODO

    }

    public void OnChangedPinte(CardData cardData)
    {
        //TODO

    }

    public void OnFinished(IList<GameDataResponse> allPlayerData)
    {
        //TODO

    }

    public void OnGameData(GameDataResponse gameData)
    {
        //TODO

    }

    public void OnJoin(Player player)
    {
        //TODO

    }

    public void OnLeave(Player player)
    {
        //TODO

    }

    public void OnMessage(string message, Player player)
    {
        //TODO

    }

    public void OnStart()
    {
        //TODO

    }

    public void OnTute(Player player, CardData tute)
    {
        //TODO

    }

    public void OnUsedCard(CardData card, Player userCard)
    {
        //TODO

    }
}
