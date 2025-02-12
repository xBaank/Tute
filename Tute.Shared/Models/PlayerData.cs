using System.Collections.Generic;
using MessagePack;
using Tute.Shared.Constants;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public partial class PlayerData
    {
        [Key(0)]
        public Player Player { get; set; }

        [Key(1)]
        public List<CardData> Cards { get; set; }

        [Key(2)]
        public List<CardData> GainedCards { get; set; }

        [Key(3)]
        public int WinsCount { get; set; }
    }

    public partial class PlayerData
    {
        [IgnoreMember]
        public IEnumerable<CardData> Cantes => CardsConstants.GetCantes(GainedCards);
    }
}
