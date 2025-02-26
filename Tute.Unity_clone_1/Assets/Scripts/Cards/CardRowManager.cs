using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class CardRowManager
    {
        private readonly float xPosition = 0f; // Fixed X position for the row
        private readonly float yPosition = 4f; // Fixed Y position for the row
        private readonly float cardSpacing = 0.5f; // Distance between cards

        private readonly SemaphoreSlim se = new(1);
        private readonly List<Card> cards = new();
        public Vector2 CurrentPosition { get; set; }

        public bool IsOrdering { get; private set; }

        public CardRowManager(float xPosition, float yPosition, float cardSpacing)
        {
            this.xPosition = xPosition;
            this.yPosition = yPosition;
            this.cardSpacing = cardSpacing;
        }

        public void Clear() => cards.Clear();

        public void AddCard(Card card) => cards.Add(card);

        public void RemoveCard(Card card) => cards.Remove(card);

        public async UniTask DragCard(
            Card draggedCard,
            CancellationToken cancellationToken = default
        )
        {
            // Temporarily remove the dragged card from the list
            var cardIndex = cards.IndexOf(draggedCard);
            cards.Remove(draggedCard);

            // Find the best position for the dragged card
            var insertIndex = cardIndex; // Default to the end of the row
            for (var i = 0; i < cards.Count + 1; i++)
            {
                var startTargetPosition = GetCardTargetPosition(i);
                var endTargetPosition = GetCardTargetPosition(cards.Count - i);
                if (
                    CurrentPosition.x < startTargetPosition.x
                    && draggedCard.transform.position.x > startTargetPosition.x
                )
                {
                    insertIndex = i;
                }

                if (
                    CurrentPosition.x > endTargetPosition.x
                    && draggedCard.transform.position.x < endTargetPosition.x
                )
                {
                    insertIndex = cards.Count - i;
                }
            }

            // Insert the dragged card back into the list at the determined index
            if (!cards.Contains(draggedCard))
                cards.Insert(insertIndex, draggedCard);

            // Update positions of all cards (not snapping yet)
            await UpdateCardPositions(cancellationToken);
            CurrentPosition = default;
        }

        public async UniTask UpdateCardPositions(CancellationToken cancellationToken = default)
        {
            await se.WaitAsync(cancellationToken);
            IsOrdering = true;

            try
            {
                var tasks = new List<UniTask>();

                for (var i = 0; i < cards.Count; i++)
                {
                    Vector3 targetPosition = new(
                        xPosition + i * cardSpacing,
                        yPosition,
                        (i / 10f) * -1
                    );

                    tasks.Add(
                        SnapCard(
                            cards[i],
                            targetPosition,
                            cards[i].destroyCancellationToken
                        )
                    );
                }

                await UniTask.WhenAll(tasks);
            }
            finally
            {
                IsOrdering = false;
                se.Release();
            }
        }

        private Vector3 GetCardTargetPosition(int index)
        {
            return new Vector3(xPosition + index * cardSpacing, yPosition, 0f);
        }

        private async UniTask SnapCard(
            Card card,
            Vector3 targetPosition,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            await card.transform
                .DOMove(targetPosition, 0.15f)
                .SetEase(Ease.InOutExpo)
                .WithCancellation(cancellationToken);
        }
    }
}
