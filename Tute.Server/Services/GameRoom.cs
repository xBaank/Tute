using MagicOnion.Server;
using Tute.Shared.Models;

namespace Tute.Server.Services;

public class GameRoom
{
    public Dictionary<Guid, PlayerData> PlayerData { get; set; }

    public GameState State { get; set; }

    public Player NextPlayer { get; set; }

    public IList<Player> Players { get; set; }
    public Dictionary<Guid, ServiceContext> RoomContexts { get; set; }

    public Dictionary<Guid, CardData> UsedCards { get; set; }

    public Stack<CardData> Cards { get; set; }

    public CardData? Pinte { get; set; }

    public CardData PinteType { get; set; }
}
