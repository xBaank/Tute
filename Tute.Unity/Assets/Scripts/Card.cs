using System;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
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
        private Vector2 startDif;
        private Vector2 startPosition;

        internal Sprite Sprite { get; set; }

        internal CardRowManager CardRowManager { get; set; }
        internal CardData CardData { get; set; }

        public event Func<CardData, UniTask> Clicked;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnMouseDown()
        {
            startPosition = transform.position;
            if (CardRowManager.IsOrdering)
                return;

            var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            startDif = transform.position - mousePos;
            CardRowManager.CurrentPosition = transform.position;
        }

        private void OnMouseUp()
        {
            if (Vector2.Distance(startPosition, transform.position) < 0.1f)
            {
                Clicked?.Invoke(CardData).Forget();
                return;
            }

            if (CardRowManager.IsOrdering)
                return;

            startDif = Vector2.zero;
            CardRowManager.DragCard(this, destroyCancellationToken).Forget();
        }

        private void OnMouseDrag()
        {
            if (CardRowManager.IsOrdering)
                return;

            var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition) + (Vector3)startDif;
            Vector3 newPos = new(mousePos.x, mousePos.y, 0);
            transform.position = newPos;
        }
    }
}
