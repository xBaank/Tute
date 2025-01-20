using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Extensions;
using Assets.Scripts.Services;
using Cysharp.Net.Http;
using Cysharp.Threading.Tasks;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Unity;
using Newtonsoft.Json;
using Tute.Shared.Models;
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
        private Sprite[] spriteSheet;

        [SerializeField]
        private Transform stackPosition;

        private readonly GamingHubClient gamingHubClient = new(Guid.NewGuid());
        private CardRowManager cardRowManager;

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
                10f
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
                card.transform.position = new Vector3(stackPosition.position.x + card.Sprite.bounds.size.x * index, 4);
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


        private void OnGameData(GameData gameData)
        {
            //TODO instance only new ones
            var cardsGo = InstanceCards(gameData.Cards).ToList();
            foreach (var item in cardsGo)
            {
                cardRowManager.AddCard(item.transform);
            }

            Debug.Log("Game started");
        }

        private void MakeMove(CardData card)
        {
            gamingHubClient.MakeMoveAsync(card);
        }
    }
}
