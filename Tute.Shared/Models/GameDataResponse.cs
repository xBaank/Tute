using System;
using System.Collections.Generic;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class GameDataResponse
    {
        [Key(0)]
        public CardData Pinte { get; set; }
        [Key(1)]
        public Player NextPlayer { get; set; }
        [Key(2)]
        public Dictionary<Guid, CardData> UsedCards { get; set; }
        [Key(3)]
        public PlayerData PlayerData { get; set; }
        [Key(4)]
        public GameState GameState { get; set; }
    }
}
