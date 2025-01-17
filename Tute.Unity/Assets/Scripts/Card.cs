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


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnMouseDown()
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            startDif = transform.position - mousePos;
        }

        private void OnMouseUp()
        {
            startDif = Vector2.zero;
        }

        private void OnMouseDrag()
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition) + (Vector3)startDif;
            transform.position = new Vector3(mousePos.x, mousePos.y, 0);
        }
        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!collision.collider.CompareTag("Card")) return;
            if (Vector2.Distance(transform.position, collision.transform.position) <= spriteRenderer.size.x / 2)
            {
                Debug.Log("Swapped");
                (transform.position, collision.transform.position) = (collision.transform.position, transform.position);
            }
        }
    }
}
