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

        internal Sprite Sprite { get; set; }

        internal CardRowManager CardRowManager { get; set; }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnMouseDown()
        {
            if (CardRowManager.IsOrdering)
                return;

            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            startDif = transform.position - mousePos;
            CardRowManager.CurrentPosition = transform.position;
        }

        private void OnMouseUp()
        {
            if (CardRowManager.IsOrdering)
                return;

            startDif = Vector2.zero;
            CardRowManager.DragCard(transform, destroyCancellationToken).Forget();
        }

        private void OnMouseDrag()
        {
            if (CardRowManager.IsOrdering)
                return;

            Vector3 mousePos =
                Camera.main.ScreenToWorldPoint(Input.mousePosition) + (Vector3)startDif;
            transform.position = new Vector3(mousePos.x, mousePos.y, 0);
        }
    }
}
