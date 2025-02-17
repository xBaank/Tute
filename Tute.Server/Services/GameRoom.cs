using MagicOnion.Server;
using Tute.Server.Extensions;
using Tute.Shared.Models;

namespace Tute.Server.Services;

public class GameRoom
{
    public required List<CardData> InitialDeck { get; init; }
    public required Dictionary<Guid, PlayerData> PlayerDataByConnetion { get; set; }

    public GameState State { get; set; }

    public Player? NextPlayer { get; set; }

    public required IList<Player> Players { get; set; }
    public required Dictionary<Guid, ServiceContext> RoomContextsByConnection { get; set; }

    public required Dictionary<Guid, CardData> UsedCardsByConnection { get; set; }

    public required Stack<CardData> Cards { get; set; }

    public CardData? Pinte { get; set; }

    public CardData? PinteType { get; set; }

    public Guid? WinnerId { get; set; }

    public int? StartIndex { get; set; }

    public required bool HaveShuffle { get; init; }

    public List<CardData> Shuffled() =>
        HaveShuffle ? [.. InitialDeck.Shuffled()] : [.. InitialDeck];
}
