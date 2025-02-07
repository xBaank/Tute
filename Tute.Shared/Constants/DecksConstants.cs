using System.Collections.Generic;

namespace Tute.Shared.Constants
{
    public readonly struct Deck
    {
        public Deck(string deckName)
        {
            DeckName = deckName;
        }

        public string DeckName { get; }

        public static implicit operator Deck(string deck) => new(deck);
        public static bool operator ==(Deck first, Deck second) => first.DeckName == second.DeckName;
        public static bool operator !=(Deck first, Deck second) => first.DeckName != second.DeckName;
        public override bool Equals(object obj) => obj is Deck deck && DeckName == deck.DeckName;
        public override int GetHashCode() => 1847699267 + EqualityComparer<string>.Default.GetHashCode(DeckName);
    }

    public static class DecksConstants
    {
        public readonly static Deck NormalDeck = "deck";
        public readonly static Deck ShortDeck = "short_deck";
        public readonly static Deck Cante20Deck = "cante_20_deck";
    }
}
