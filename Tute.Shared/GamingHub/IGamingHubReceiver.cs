using System.Collections.Generic;
using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Player player);
        void OnLeave(Player player);
        void OnGameData(GameDataResponse gameData);
        void OnUsedCard(CardData card, Player userCard);
        void OnCante(Player player, CardData cante);
        void OnTute(Player player, CardData tute);
        void OnChangedPinte(CardData cardData);
        void OnStart();
        void OnFinished(IList<GameDataResponse> allPlayerData);
        void OnMessage(string message, Player player);
    }
}
