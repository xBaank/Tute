using MessagePack;

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public class CardData
    {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public int Value { get; set; }
        [Key(2)]
        public string SpriteName { get; set; }
        [Key(3)]
        public CardType Type { get; set; }
    }
}
