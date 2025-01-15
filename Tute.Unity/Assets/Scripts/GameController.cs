using System.Linq;
using Newtonsoft.Json;
using Tute.Shared;
using UnityEngine;

namespace Assets.Scripts
{
    public class GameController : MonoBehaviour
    {
        [SerializeField]
        Card cardPrefab;

        [SerializeField]
        TextAsset cardsData;

        [SerializeField]
        Sprite[] spriteSheet;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            var data = JsonConvert.DeserializeObject<CardData[]>(cardsData.text);
            foreach (var item in data)
            {
                var card = Instantiate(cardPrefab, transform);
                card.cardType = item.Type switch
                {
                    "Cups" => CardType.Cups,
                    "Coins" => CardType.Coins,
                    "Clubs" => CardType.Clubs,
                    "Swords" => CardType.Swords,
                    _ => throw new System.Exception($"Unknow type {item.Type}")
                };
                card.value = item.Value;
                card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
                card.name = item.Name;
            }
        }

        // Update is called once per frame
        void Update() { }
    }
}
