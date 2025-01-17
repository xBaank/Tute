using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts
{
    public class CardRowManager
    {
        private float xPosition = 0f; // Fixed X position for the row
        private float yPosition = 3f; // Fixed Y position for the row
        private float cardSpacing = 2f; // Distance between cards
        private float snapSpeed = 30f; // Speed of snapping animation

        private readonly List<Transform> cards = new();
        public Vector2 CurrentPosition { get; set; }

        public bool IsOrdering { get; private set; }

        public CardRowManager(float xPosition, float yPosition, float cardSpacing, float snapSpeed)
        {
            this.xPosition = xPosition;
            this.yPosition = yPosition;
            this.cardSpacing = cardSpacing;
            this.snapSpeed = snapSpeed;
        }

        public void AddCard(Transform card)
        {
            cards.Add(card);
            UpdateCardPositions().Forget();
        }

        public void RemoveCard(Transform card)
        {
            cards.Remove(card);
            UpdateCardPositions().Forget();
        }

        public async UniTaskVoid DragCard(
            Transform draggedCard,
            CancellationToken cancellationToken = default
        )
        {
            // Temporarily remove the dragged card from the list
            var cardIndex = cards.IndexOf(draggedCard);
            cards.Remove(draggedCard);

            // Find the best position for the dragged card
            int insertIndex = cardIndex; // Default to the end of the row
            for (int i = 0; i < cards.Count + 1; i++)
            {
                var startTargetPosition = GetCardTargetPosition(i);
                var endTargetPosition = GetCardTargetPosition(cards.Count - i);
                if (
                    CurrentPosition.x < startTargetPosition.x
                    && draggedCard.position.x > startTargetPosition.x
                )
                {
                    insertIndex = i;
                }

                if (
                    CurrentPosition.x > endTargetPosition.x
                    && draggedCard.position.x < endTargetPosition.x
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
            IsOrdering = true;
            var tasks = new List<UniTask>();

            for (int i = 0; i < cards.Count; i++)
            {
                Vector3 targetPosition = new(xPosition + i * cardSpacing, yPosition, 0f);
                tasks.Add(SnapCard(cards[i], targetPosition, cancellationToken));
            }

            await UniTask.WhenAll(tasks);
            IsOrdering = false;
        }

        private Vector3 GetCardTargetPosition(int index)
        {
            return new Vector3(xPosition + index * cardSpacing, yPosition, 0f);
        }

        private async UniTask SnapCard(
            Transform card,
            Vector3 targetPosition,
            CancellationToken cancellationToken
        )
        {
            while (Vector3.Distance(card.position, targetPosition) > 0.01f)
            {
                card.position = Vector3.Lerp(
                    card.position,
                    targetPosition,
                    Time.deltaTime * snapSpeed
                );

                await UniTask.Yield(cancellationToken);
            }
            card.position = targetPosition;
        }
    }
}
