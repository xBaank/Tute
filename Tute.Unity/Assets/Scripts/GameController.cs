using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Extensions;
using Assets.Scripts.Services;
using Cysharp.Net.Http;
using Cysharp.Threading.Tasks;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Unity;
using Newtonsoft.Json;
using Tute.Shared.Models;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Scripts
{
    public class GameController : MonoBehaviour
    {
        [SerializeField]
        private Card cardPrefab;

        [SerializeField]
        private TextAsset cardsData;

        [SerializeField]
        private Transform stackPosition;

        [SerializeField]
        private Transform spawPosition;

        [SerializeField]
        private Sprite[] spriteSheet;


        private readonly GamingHubClient gamingHubClient = new(Guid.NewGuid());
        private readonly List<Card> currentCardsGo = new();
        private readonly SemaphoreSlim semaphoreSlim = new(1);
        private CardRowManager cardRowManager;
        private List<CardData> CurrentCards => currentCardsGo.Select(i => i.CardData).ToList();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnRuntimeInitialize()
        {
            // Initialize gRPC channel provider when the application is loaded.
            GrpcChannelProviderHost.Initialize(
                new DefaultGrpcChannelProvider(
                    () =>
                        new GrpcChannelOptions()
                        {
                            HttpHandler = new YetAnotherHttpHandler() { Http2Only = true, },
                            DisposeHttpClient = true,
                        }
                )
            );
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            cardRowManager = new CardRowManager(
                stackPosition.position.x,
                stackPosition.position.y,
                1.5f,
                15f
            );
            Server().Forget();
        }

        private IList<CardData> GetCards()
        {
            var data = JsonConvert.DeserializeObject<CardData[]>(cardsData.text);
            return data;
        }

        private IEnumerable<Card> InstanceCards(IList<CardData> data)
        {
            foreach ((var index, var item) in data.WithIndex())
            {
                var card = Instantiate(cardPrefab, transform);
                card.Clicked += MakeMove;
                card.CardData = item;
                card.CardRowManager = cardRowManager;
                card.cardType = item.Type;
                card.value = item.Value;
                card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
                card.name = item.Name;
                card.transform.position = spawPosition.position;
                yield return card;
            }
        }

        private async UniTaskVoid Server()
        {
            var channel = GrpcChannelx.ForTarget(
                new GrpcChannelTarget("localhost", 5000, true)
            );

            gamingHubClient.OnGameDataEvent += OnGameData;

            await gamingHubClient.ConnectAsync(channel, "room");
            await UniTask.WaitUntil(() => Input.GetKey(KeyCode.Space), cancellationToken: destroyCancellationToken);
            await gamingHubClient.StartAsync(GetCards());
        }


        private async UniTask OnGameData(GameData gameData)
        {
            await semaphoreSlim.WaitAsync(destroyCancellationToken);
            try
            {
                var newCards = gameData.Cards.Where(i => !CurrentCards.Any(x => x.Name == i.Name)).ToList();
                if (!newCards.Any() && !spawPosition.IsDestroyed()) Destroy(spawPosition.gameObject);
                var newCardsGo = InstanceCards(newCards).ToList();
                currentCardsGo.AddRange(newCardsGo);
                foreach (var item in newCardsGo) cardRowManager.AddCard(item);
                await cardRowManager.UpdateCardPositions();

                Debug.Log("Game data received");
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private async UniTask MakeMove(CardData card)
        {
            await semaphoreSlim.WaitAsync(destroyCancellationToken);
            try
            {
                var instancedCard = currentCardsGo.FirstOrDefault(x => x.name == card.Name);
                if (instancedCard == null) return;

                cardRowManager.RemoveCard(instancedCard);
                currentCardsGo.Remove(instancedCard);
                Destroy(instancedCard.gameObject);
                await gamingHubClient.MakeMoveAsync(card);

                Debug.Log("Game data sent");
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }
    }
}
