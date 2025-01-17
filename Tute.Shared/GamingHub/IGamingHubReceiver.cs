using System.Collections.Generic;
using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Player player);
        void OnLeave(Player player);
        void OnGameStart(IList<CardData> card);
        void OnGameData(GameData gameData);
    }
}
