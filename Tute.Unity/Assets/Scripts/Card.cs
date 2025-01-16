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

        private Vector2 startDif;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnMouseDown()
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            startDif = mousePos - transform.position;
        }

        private void OnMouseUp()
        {
            startDif = Vector2.zero;
        }

        private void OnMouseDrag()
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition) + startDif;
            transform.position = new Vector3(mousePos.x, mousePos.y, 0);
        }
    }
}
