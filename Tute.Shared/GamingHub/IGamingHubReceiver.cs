using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Player uuid);
        void OnLeave(Player uuid);
        void OnMove(CardData card);
    }
}
