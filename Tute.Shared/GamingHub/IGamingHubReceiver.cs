using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Player player);
        void OnLeave(Player player);
        void OnGameData(GameData gameData);
    }
}
