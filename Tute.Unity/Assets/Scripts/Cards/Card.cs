using System;
using System.Threading;
using Assets.Scripts.Extensions;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Cards
{

    public class Card : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Vector2 startDif;
        internal Sprite Sprite { get; set; }
        internal CardRowManager CardRowManager { get; set; }
        internal CardData CardData { get; set; }
        internal AudioController AudioController { get; set; }

        public event Func<CardData, UniTask> OnClick;
        private CancellationToken _cancellationToken;
        private bool _isClicked;
        private InputAction _hold;
        private InputAction _leftClick;
        private Collider2D _collider2D;

        private void Start()
        {
            _cancellationToken = destroyCancellationToken;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            spriteRenderer.sprite = Sprite;
            _hold = InputSystem.actions.FindAction("Hold");
            _leftClick = InputSystem.actions.FindAction("Left Click");
            _collider2D = GetComponent<Collider2D>();
            CheckDrag().Forget();
            // CheckClick().Forget();
        }

        private async UniTaskVoid CheckDrag()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var holdPos = Camera.main.ScreenToWorldPoint(_hold.ReadValue<Vector2>()).ToVector2();
                if (_leftClick.WasPressedThisFrame() && _collider2D.OverlapPoint(holdPos))
                {
                    startDif = transform.position.ToVector2() - holdPos;
                    CardRowManager.CurrentPosition = transform.position;
                    var (startPos, endPos) = await MoveTask();

                    if (Vector2.Distance(startPos, endPos) > 0.1f)
                    {
                        await DragCards();
                    }
                    else
                    {
                        await OnPointerClickTask();
                    }
                }

                await UniTask.NextFrame(cancellationToken: _cancellationToken);
            }
        }

        private async UniTask DragCards()
        {
            startDif = Vector2.zero;
            AudioController.PlayFlick();
            await CardRowManager.DragCard(this, _cancellationToken);
        }


        public async UniTask OnPointerClickTask()
        {
            if (_isClicked)
                return;

            try
            {
                OnClick?.Invoke(CardData).Forget();
                _isClicked = true;
                await UniTask.WaitForSeconds(0.3f, cancellationToken: _cancellationToken);
            }
            finally
            {
                _isClicked = false;
            }
        }

        private async UniTask<(Vector2 startPos, Vector2 endPos)> MoveTask()
        {
            var startPos = transform.position.ToVector2();
            while (_leftClick.IsPressed())
            {
                var position = Camera.main.ScreenToWorldPoint(_hold.ReadValue<Vector2>()).ToVector2() + startDif;
                transform.position = position.ToVector3();
                await UniTask.Yield(cancellationToken: _cancellationToken);
            }
            return (startPos, transform.position.ToVector2());
        }


        private void OnDestroy()
        {
            OnClick = null;
        }

        /*
        public void OnDrag(PointerEventData eventData)
        {
            Debug.Log(_hold.ReadValue<Vector2>());
            var position =
                  Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue())
                  + (Vector3)startDif;
            transform.position = position + new Vector3(0, 0, 10);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var position =
                 Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue())
                 + (Vector3)startDif;
            transform.position = position + new Vector3(0, 0, 10);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragCards().Forget();
        }
        */
    }
}
