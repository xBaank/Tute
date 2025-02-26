using System;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class Player
    {
        [Key(0)]
        public Guid ConnectionId { get; set; }

        [Key(1)]
        public string Name { get; set; }

        [Key(2)]
        public bool IsLeader { get; set; }

        [Key(3)]
        public int TeamIndex { get; set; }

        public override string ToString() => Name;
    }
}
