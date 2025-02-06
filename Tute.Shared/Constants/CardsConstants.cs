using System;
using System.Collections.Generic;
using System.Linq;
using Tute.Shared.Models;

namespace Tute.Shared.Constants
{
    public static class CardsConstants
    {
        public static readonly CardData DiezDelMonte = new()
        {
            Value = 10,
            Name = "Las diez del monte",
            Number = 100,
        };
        public static readonly CardData VeinteEnBastos = new()
        {
            Value = 20,
            Name = "Las veinte en vastos",
            Number = 101,
            Type = CardType.Clubs,
        };
        public static readonly CardData VeinteEnCopas = new()
        {
            Value = 20,
            Name = "Las veinte en copas",
            Number = 102,
            Type = CardType.Cups,
        };
        public static readonly CardData VeinteEnEspadas = new()
        {
            Value = 20,
            Name = "Las veinte en espadas",
            Number = 103,
            Type = CardType.Swords,
        };
        public static readonly CardData VeinteEnOros = new()
        {
            Value = 20,
            Name = "Las veinte en oros",
            Number = 104,
            Type = CardType.Coins,
        };
        public static readonly CardData CuarentaEnOros = new()
        {
            Value = 40,
            Name = "Las cuarenta en oros",
            Number = 106,
            Type = CardType.Coins,
        };
        public static readonly CardData CuarentaEnBastos = new()
        {
            Value = 40,
            Name = "Las cuarenta en bastos",
            Number = 107,
            Type = CardType.Clubs,
        };
        public static readonly CardData CuarentaEnEspadas = new()
        {
            Value = 40,
            Name = "Las cuarenta en espadas",
            Number = 108,
            Type = CardType.Swords,
        };
        public static readonly CardData CuarentaEnCopas = new()
        {
            Value = 40,
            Name = "Las cuarenta en copas",
            Number = 109,
            Type = CardType.Cups,
        };
        public static readonly CardData TuteReyes = new()
        {
            Value = 200,
            Name = "Tute en reyes",
            Number = 110,
        };
        public static readonly CardData TutePrincipes = new()
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
