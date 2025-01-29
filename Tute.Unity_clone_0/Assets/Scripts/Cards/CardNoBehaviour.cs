using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class CardNoBehaviour : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        internal Sprite Sprite { get; set; }
        internal CardData CardData { get; set; }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }
    }
}
