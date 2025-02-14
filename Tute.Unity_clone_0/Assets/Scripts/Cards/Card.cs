using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Cards
{
    internal interface IPointHandler : IPointerDownHandler, IPointerUpHandler { }
    public class Card : MonoBehaviour, IPointHandler
    {
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private Vector2 startDif;
        private Vector2 startPosition;
        internal Sprite Sprite { get; set; }
        internal CardRowManager CardRowManager { get; set; }
        internal CardData CardData { get; set; }
        internal AudioController AudioController { get; set; }
        private CancellationTokenSource _cancellationTokenSource = new();

        public event Func<CardData, UniTask> OnClick;
        private CancellationToken _cancellationToken;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            _cancellationToken = destroyCancellationToken;
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
        }

        private void OnMouseDrag()
        {
            if (CardRowManager.IsOrdering)
                return;

            var mouseX = Mouse.current.position.x.ReadValue();
            var mouseY = Mouse.current.position.y.ReadValue();

            var mousePos = Camera.main.ScreenToWorldPoint(new(mouseX, mouseY, 0)) + (Vector3)startDif;
            Vector3 newPos = new(mousePos.x, mousePos.y, -5f);
            transform.Rotate(Vector3.zero);
            transform.position = newPos;
        }

        private async UniTaskVoid DragCards()
        {
            startDif = Vector2.zero;
            AudioController.PlayFlick();
            await CardRowManager.DragCard(this, _cancellationToken);
        }

        private async UniTaskVoid FollowCardWithMouse(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var position = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) + (Vector3)startDif;
                transform.position = position + new Vector3(0, 0, 10);
                await UniTask.Yield();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            startPosition = transform.position;
            if (CardRowManager.IsOrdering)
                return;

            var mousePos = eventData.pointerPressRaycast.worldPosition;
            startDif = transform.position - mousePos;
            CardRowManager.CurrentPosition = transform.position;

            FollowCardWithMouse(_cancellationTokenSource.Token).Forget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new();
            if (Vector2.Distance(startPosition, transform.position) < 0.1f)
            {
                OnClick?.Invoke(CardData).Forget();
                return;
            }

            if (CardRowManager.IsOrdering)
                return;

            DragCards().Forget();
        }

        private void OnDestroy()
        {
            OnClick = null;
        }
    }
}
