using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private Transform stackPosition;

        [SerializeField]
        private Transform spawPosition;

        [SerializeField]
        private Sprite[] spriteSheet;

        [SerializeField]
        private AudioController audioController;

        private readonly GamingHubClient _gamingHubClient = new(Guid.NewGuid());
        private readonly List<Card> _currentCardsGo = new();
        private Player _nextPlayer;
        private Player _selfPlayer;
        private Task? currentTask;
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

            _selfPlayer = await _gamingHubClient.ConnectAsync(channel, "room");
            await UniTask.WaitUntil(
                () => Input.GetKey(KeyCode.Space),
                cancellationToken: destroyCancellationToken
            );
            await _gamingHubClient.StartAsync(GetCards());
        }

        private async UniTask OnGameData(GameData gameData, Player nextPlayer)
        {
            Debug.Log("Game data received");
            Debug.Log($"Me: {gameData.Player.ConnectionId}, Next: {nextPlayer.ConnectionId}");
            await SetData(gameData, nextPlayer);
        }

        private async Task SetData(GameData gameData, Player nextPlayer)
        {
            var newCards = gameData
                .Cards.Where(i => !CurrentCards.Any(x => x.Name == i.Name))
                .ToList();

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
        }

        private async UniTask MakeMove(CardData card)
        {
            if (currentTask != null && !currentTask.IsCompleted)
            {
                return;
            }

            if (_nextPlayer == null || _selfPlayer == null)
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
            UniTask[] tasks = { GetResponse(card), _cardRowManager.UpdateCardPositions() };
            currentTask = UniTask.WhenAll(tasks).AsTask();
            await currentTask;
        }

        private async UniTask GetResponse(CardData card)
        {
            var (gameData, nextPlayer) = await _gamingHubClient.MakeMoveAsync(card);
            await SetData(gameData, nextPlayer);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(15, 15, 100, 30), $"Leader {_selfPlayer?.IsLeader}");
        }
    }
}
