using System;
using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class Pinte : MonoBehaviour
    {
        internal CardData CardData { get; set; }

        private SpriteRenderer spriteRenderer;
        internal Sprite Sprite { get; set; }

        public event Action<CardData> OnClick;

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Card"))
            {
                if (collision.gameObject.TryGetComponent<Card>(out var card))
                    OnClick?.Invoke(card.CardData);
            }
        }
    }
}