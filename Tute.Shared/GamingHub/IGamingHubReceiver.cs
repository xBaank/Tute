using System.Collections.Generic;
using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Player uuid);
        void OnLeave(Player uuid);
        void OnGameStart(IList<CardData> card);
    }
}
