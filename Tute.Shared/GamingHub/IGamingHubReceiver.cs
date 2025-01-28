using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Player player);
        void OnLeave(Player player);
        void OnGameData(GameDataResponse gameData);
        void OnUsedCard(CardData card, Player userCard);
    }
}
