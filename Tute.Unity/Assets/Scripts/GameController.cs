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

        [SerializeField]
        AudioController audioController;

        private readonly GamingHubClient _gamingHubClient = new(Guid.NewGuid());
        private readonly List<Card> _currentCardsGo = new();
        private Player _nextPlayer;
        private Player _selfPlayer;
        private bool isReceiving;
        private CardRowManager _cardRowManager;
        private List<CardData> CurrentCards => _currentCardsGo.Select(i => i.CardData).ToList();

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
            _cardRowManager = new CardRowManager(
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
                card.CardRowManager = _cardRowManager;
                card.AudioController = audioController;
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
            var channel = GrpcChannelx.ForTarget(new GrpcChannelTarget("localhost", 5000, true));

            _gamingHubClient.OnGameDataEvent += OnGameData;

            await _gamingHubClient.ConnectAsync(channel, "room");
            await UniTask.WaitUntil(
                () => Input.GetKey(KeyCode.Space),
                cancellationToken: destroyCancellationToken
            );
            await _gamingHubClient.StartAsync(GetCards());
        }

        private async UniTask OnGameData(GameData gameData, Player nextPlayer)
        {
            isReceiving = true;
            Debug.Log("Game data received");

            var newCards = gameData
                .Cards.Where(i => !CurrentCards.Any(x => x.Name == i.Name))
                .ToList();

            if (!newCards.Any() && !spawPosition.IsDestroyed())
                Destroy(spawPosition.gameObject);

            if (!newCards.Any())
            {
                audioController.PlayFlick();
                await _cardRowManager.UpdateCardPositions();
            }

            foreach (var item in InstanceCards(newCards))
            {
                _cardRowManager.AddCard(item);
                _currentCardsGo.Add(item);
                audioController.PlayFlick();
                await _cardRowManager.UpdateCardPositions();
            }

            _nextPlayer = nextPlayer;
            _selfPlayer = gameData.Player;
            isReceiving = false;
        }

        private async UniTask MakeMove(CardData card)
        {
            if (isReceiving)
                return;

            if (_nextPlayer == null && _selfPlayer == null)
                return;

            if (_nextPlayer.ConnectionId != _selfPlayer.ConnectionId)
                return;

            var instancedCard = _currentCardsGo.FirstOrDefault(x => x.name == card.Name);
            if (instancedCard == null)
                return;

            _cardRowManager.RemoveCard(instancedCard);
            _currentCardsGo.Remove(instancedCard);
            Destroy(instancedCard.gameObject);
            Debug.Log("Game data sent");
            await _gamingHubClient.MakeMoveAsync(card);
        }
    }
}
