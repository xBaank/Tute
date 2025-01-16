using System;
using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class Player
    {
        [Key(0)]
        public Guid Id { get; set; }
    }
}
