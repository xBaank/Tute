using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Cards
{

    public class Card : MonoBehaviour, IPointerClickHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private SpriteRenderer spriteRenderer;
        private Vector2 startDif;
        private Vector2 startPosition;
        internal Sprite Sprite { get; set; }
        internal CardRowManager CardRowManager { get; set; }
        internal CardData CardData { get; set; }
        internal AudioController AudioController { get; set; }

        public event Func<CardData, UniTask> OnClick;
        private CancellationToken _cancellationToken;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            _cancellationToken = destroyCancellationToken;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private async UniTaskVoid DragCards()
        {
            startDif = Vector2.zero;
            AudioController.PlayFlick();
            await CardRowManager.DragCard(this, _cancellationToken);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;

            if (CardRowManager.IsOrdering)
                return;

            OnClick?.Invoke(CardData).Forget();
        }

        private void OnDestroy()
        {
            OnClick = null;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var position =
                  Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue())
                  + (Vector3)startDif;
            transform.position = position + new Vector3(0, 0, 10);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            startPosition = transform.position;
            var mousePos = eventData.pointerPressRaycast.worldPosition;
            startDif = transform.position - mousePos;
            CardRowManager.CurrentPosition = transform.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragCards().Forget();
        }
    }
}
