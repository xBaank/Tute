using System;
using System.Collections.Generic;
using MessagePack;
namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class GameRoom
    {
        [Key(0)]
        public Dictionary<Guid, PlayerData> PlayerData { get; set; }

        [Key(1)]
        public GameState State { get; set; }

        [Key(2)]
        public Player NextPlayer { get; set; }

        [Key(3)]
        public IList<Player> Players { get; set; }

        [Key(4)]
        public Dictionary<Guid, CardData> UsedCards { get; set; }

        [Key(5)]
        public Stack<CardData> Cards { get; set; }

        [Key(6)]
        public CardData Pinte { get; set; }
    }
}
