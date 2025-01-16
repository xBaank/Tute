using System;
using Tute.Shared.Models;

namespace Tute.Shared.GamingHub
{
    public interface IGamingHubReceiver
    {
        void OnJoin(Guid uuid);
        void OnLeave(Guid uuid);
        void OnMove(CardData card);
    }
}
