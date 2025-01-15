using UnityEngine;

namespace Assets.Scripts
{
    public class Card : MonoBehaviour
    {
        [SerializeField]
        internal CardType cardType;

        [SerializeField]
        internal int value;

        private SpriteRenderer spriteRenderer;

        internal Sprite Sprite { get; set; }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }
    }
}
