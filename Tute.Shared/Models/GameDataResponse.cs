using System;
using System.Collections.Generic;
using MessagePack;
using Tute.Shared.Constants;

#nullable enable

namespace Tute.Shared.Models
{
    [MessagePackObject]
    public partial class GameDataResponse
    {
        [Key(0)]
        public CardData? Pinte { get; set; }

        [Key(1)]
        public Player? NextPlayer { get; set; }

        [Key(2)]
        public Dictionary<Guid, CardData> UsedCards { get; set; } = new();

        [Key(3)]
        public PlayerData PlayerData { get; set; } = new();

        [Key(4)]
        public GameState GameState { get; set; }

        [Key(5)]
        public List<CardData> GainedCards { get; set; } = new();

        [Key(6)]
        public CardType PinteType { get; set; }

        [Key(7)]
        public Guid? WinnerId { get; set; }

        [Key(8)]
        public int TeamIndex { get; set; }
    }

    public partial class GameDataResponse
    {
        [IgnoreMember]
        public IEnumerable<CardData> Cantes => CardsConstants.GetCantes(GainedCards);
    }
}

#nullable disable
