using System;
using System.Collections.Generic;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class GameRoom
    {
        [Key(0)]
        public Dictionary<Guid, GameData> Data { get; set; }
        [Key(1)]
        public GameState State { get; set; }
        [Key(2)]
        public Guid? LastPlayed { get; set; }
        [Key(3)]
        public Dictionary<Guid, CardData> UsedCards { get; set; }
        [Key(4)]
        public Stack<CardData> Cards { get; set; }
    }
}
