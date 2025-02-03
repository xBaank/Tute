using System;
using System.Collections.Generic;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public partial class GameDataResponse
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
        [Key(5)]
        public IList<CardData> GainedCards { get; set; }
    }

    public partial class GameDataResponse
    {
        [IgnoreMember]
        public IEnumerable<CardData> Cantes => CardsConstants.GetCantes(GainedCards);
    }
}
