using System.Collections.Generic;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class GameData
    {
        [Key(0)]
        public Player Player { get; set; }

        [Key(1)]
        public IList<CardData> Cards { get; set; }

        [Key(2)]
        public IList<CardData> GainedCards { get; set; }

        [Key(3)]
        public CardData Pinte { get; set; }
    }
}
