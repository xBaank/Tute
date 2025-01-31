using Tute.Shared.Models;

namespace Tute.Server;

internal static class CardsConstants
{
    public readonly static CardData DiezDelMonte = new() { Value = 10, Name = "Las diez del monte", Number = 100 };
    public readonly static CardData VeinteEnBastos = new() { Value = 20, Name = "Las veinte en vastos", Number = 101, Type = CardType.Clubs };
    public readonly static CardData VeinteEnCopas = new() { Value = 20, Name = "Las veinte en copas", Number = 102, Type = CardType.Cups };
    public readonly static CardData VeinteEnEspadas = new() { Value = 20, Name = "Las veinte en espadas", Number = 103, Type = CardType.Swords };
    public readonly static CardData VeinteEnOros = new() { Value = 20, Name = "Las veinte en oros", Number = 104, Type = CardType.Coins };
    public readonly static CardData CuarentaEnOros = new() { Value = 40, Name = "Las cuarenta en oros", Number = 106, Type = CardType.Coins };
    public readonly static CardData CuarentaEnBastos = new() { Value = 40, Name = "Las cuarenta en bastos", Number = 107, Type = CardType.Clubs };
    public readonly static CardData CuarentaEnEspadas = new() { Value = 40, Name = "Las cuarenta en espadas", Number = 108, Type = CardType.Swords };
    public readonly static CardData CuarentaEnCopas = new() { Value = 40, Name = "Las cuarenta en copas", Number = 109, Type = CardType.Cups };
    public readonly static CardData Tute = new() { Value = 200, Name = "Tute", Number = 110 };
}
