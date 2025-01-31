using System;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class Card : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private Vector2 startDif;
        private Vector2 startPosition;
        internal Sprite Sprite { get; set; }
        internal CardRowManager CardRowManager { get; set; }
        internal CardData CardData { get; set; }
        internal AudioController AudioController { get; set; }

        public event Func<CardData, UniTask> Clicked;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnMouseOver()
        {
            //TODO Stop animation if its dragging
            return;
            transform.position = new Vector3(transform.position.x, transform.position.y, -1);
            animator.SetBool("IsOver", true);
        }

        private void OnMouseExit()
        {
            return;
            transform.position = new Vector3(transform.position.x, transform.position.y, 0);
            animator.SetBool("IsOver", false);
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

            DragCards().Forget();
        }

        private void OnMouseDrag()
        {
            if (CardRowManager.IsOrdering)
                return;

            var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition) + (Vector3)startDif;
            Vector3 newPos = new(mousePos.x, mousePos.y, 0);
            transform.position = newPos;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Card"))
            {
                Debug.Log("Card triggered");
            }
        }

        private async UniTaskVoid DragCards()
        {
            startDif = Vector2.zero;
            AudioController.PlayFlick();
            await CardRowManager.DragCard(this, destroyCancellationToken);
        }
    }
}
