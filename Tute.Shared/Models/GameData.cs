using System;
using System.Collections.Generic;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class GameData
    {
        [Key(0)]
        public Dictionary<Guid, IList<CardData>> Cards { get; set; }
    }
}
