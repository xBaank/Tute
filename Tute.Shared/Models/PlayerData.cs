using System.Collections.Generic;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class PlayerData
    {
        [Key(0)]
        public Player Player { get; set; }

        [Key(1)]
        public List<CardData> Cards { get; set; }

        [Key(2)]
        public List<CardData> GainedCards { get; set; }
    }
}
