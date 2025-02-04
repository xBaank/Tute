using System;
using System.Collections.Generic;
using System.Linq;

namespace Tute.Shared.Models
{
    public static class CardsConstants
    {
        public static readonly CardData DiezDelMonte = new CardData()
        {
            Value = 10,
            Name = "Las diez del monte",
            Number = 100,
        };
        public static readonly CardData VeinteEnBastos = new CardData()
        {
            Value = 20,
            Name = "Las veinte en vastos",
            Number = 101,
            Type = CardType.Clubs,
        };
        public static readonly CardData VeinteEnCopas = new CardData()
        {
            Value = 20,
            Name = "Las veinte en copas",
            Number = 102,
            Type = CardType.Cups,
        };
        public static readonly CardData VeinteEnEspadas = new CardData()
        {
            Value = 20,
            Name = "Las veinte en espadas",
            Number = 103,
            Type = CardType.Swords,
        };
        public static readonly CardData VeinteEnOros = new CardData()
        {
            Value = 20,
            Name = "Las veinte en oros",
            Number = 104,
            Type = CardType.Coins,
        };
        public static readonly CardData CuarentaEnOros = new CardData()
        {
            Value = 40,
            Name = "Las cuarenta en oros",
            Number = 106,
            Type = CardType.Coins,
        };
        public static readonly CardData CuarentaEnBastos = new CardData()
        {
            Value = 40,
            Name = "Las cuarenta en bastos",
            Number = 107,
            Type = CardType.Clubs,
        };
        public static readonly CardData CuarentaEnEspadas = new CardData()
        {
            Value = 40,
            Name = "Las cuarenta en espadas",
            Number = 108,
            Type = CardType.Swords,
        };
        public static readonly CardData CuarentaEnCopas = new CardData()
        {
            Value = 40,
            Name = "Las cuarenta en copas",
            Number = 109,
            Type = CardType.Cups,
        };
        public static readonly CardData TuteReyes = new CardData()
        {
            Value = 200,
            Name = "Tute en reyes",
            Number = 110,
        };
        public static readonly CardData TutePrincipes = new CardData()
        {
            Value = 200,
            Name = "Tute en principes",
            Number = 111,
        };

        public static IEnumerable<CardData> GetCantes(IEnumerable<CardData> cards) =>
            cards.Where(i =>
                i.Number == VeinteEnEspadas.Number
                || i.Number == VeinteEnCopas.Number
                || i.Number == VeinteEnBastos.Number
                || i.Number == VeinteEnOros.Number
                || i.Number == CuarentaEnEspadas.Number
                || i.Number == CuarentaEnCopas.Number
                || i.Number == CuarentaEnBastos.Number
                || i.Number == CuarentaEnOros.Number
            );

        public static CardData GetCante(CardType cardType, CardType pinteType) =>
            cardType switch
            {
                CardType.Coins when cardType == pinteType => CuarentaEnOros,
                CardType.Swords when cardType == pinteType => CuarentaEnEspadas,
                CardType.Clubs when cardType == pinteType => CuarentaEnBastos,
                CardType.Cups when cardType == pinteType => CuarentaEnCopas,
                CardType.Coins => VeinteEnOros,
                CardType.Swords => VeinteEnEspadas,
                CardType.Clubs => VeinteEnBastos,
                CardType.Cups => VeinteEnCopas,
                _ => throw new NotImplementedException(),
            };

        public static CardData GetTute(int number) =>
            number switch
            {
                11 => TutePrincipes,
                12 => TuteReyes,
                _ => throw new NotImplementedException(),
            };
    }
}
