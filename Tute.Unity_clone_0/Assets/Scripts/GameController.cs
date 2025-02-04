using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Cards;
using Assets.Scripts.Extensions;
using Assets.Scripts.Services;
using Cysharp.Net.Http;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Unity;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public class GameController : MonoBehaviour
    {
        [SerializeField]
        private Card cardPrefab;

        [SerializeField]
        private Pinte pintePrefab;

        [SerializeField]
        private CardNoBehaviour noBehaviorCardPrefab;

        [SerializeField]
        private TextAsset cardsData;

        [SerializeField]
        private Transform stackPosition;

        [SerializeField]
        private Transform spawPosition;

        [SerializeField]
        private Transform usedCardsPosition;

        [SerializeField]
        private Transform pintePosition;

        [SerializeField]
        private Sprite[] spriteSheet;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private AudioController audioController;

        private readonly GamingHubClient _gamingHubClient = new();
        private readonly List<Card> _currentCardsGo = new();
        private readonly List<CardNoBehaviour> _currentUsedCardsGo = new();
        private readonly SemaphoreSlim _currentSemaphore = new(1);
        private readonly List<Player> _players = new();
        private Player _nextPlayer;
        private Player _selfPlayer;
        private Task _currentTask;
        private CardRowManager _cardRowManager;
        private GameDataResponse _currentData;
        private Vector2 _cardUsedPosition;
        private Pinte _pinte;
        private GrpcChannelx _channel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnRuntimeInitialize()
        {
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

        private void Start()
        {
            _cardRowManager = new CardRowManager(
                stackPosition.position.x,
                stackPosition.position.y,
                1.5f,
                15f
            );

            SceneManager.LoadScene("Menu", LoadSceneMode.Additive);

            ConnectToServer().Forget();

            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(() => LeaveRoom().Forget());

            _gamingHubClient.OnGameDataEvent += OnGameData;
            _gamingHubClient.OnUsedCardEvent += OnUsedCard;
            _gamingHubClient.OnChangedPinteEvent += OnChangedPinte;
            _gamingHubClient.OnCanteEvent += OnCante;
            _gamingHubClient.OnTuteEvent += OnTute;
            _gamingHubClient.OnJoinEvent += OnPlayerJoined;
            _gamingHubClient.OnLeaveEvent += OnPlayerLeaved;
            _gamingHubClient.OnStartEvent += () => OnStart().Forget();
            _gamingHubClient.OnFinishEvent += (i) => OnFinish(i).Forget();
            MainManager.Instance.OnRoomJoin += JoinRoom;
            MainManager.Instance.OnStartGame += () => StartGame().Forget();
            MainManager.Instance.OnLeaveRoom += () => LeaveRoom().Forget();
        }

        private async UniTaskVoid ConnectToServer()
        {
            _channel = GrpcChannelx.ForTarget(new GrpcChannelTarget("localhost", 5000, true));
            await _gamingHubClient.ConnectAsync(_channel);
        }

        private async UniTask StartGame()
        {
            if (_selfPlayer == null) return;
            await _gamingHubClient.StartAsync();
        }

        private async UniTaskVoid OnStart()
        {
            await SceneManager.UnloadSceneAsync("Menu");
        }

        private async UniTaskVoid OnFinish(IList<GameDataResponse> playerDatas)
        {
            await _currentSemaphore.WaitAsync();

            try
            {
                DOTween.Clear();
                _players.Clear();
                _cardRowManager.Clear();
                _currentCardsGo.Where(i => i != null).ForEach(i => Destroy(i.gameObject));
                _currentUsedCardsGo.Where(i => i != null).ForEach(i => Destroy(i.gameObject));
                _currentCardsGo.Clear();
                _currentUsedCardsGo.Clear();
                if (_pinte != null) Destroy(_pinte.gameObject);
                _selfPlayer = null;
                _nextPlayer = null;
                _currentData = null;
                _currentTask = null;
                _pinte = null;

                //TODO calculate winner
                await SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Additive);
            }
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private async UniTask<List<Player>> JoinRoom(string roomName, string playerName)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentNullException("roomName, playerName", "Room name and player name must not be empty");
            }

            var (selfPlayer, players) = await _gamingHubClient.JoinAsync(roomName, playerName);
            _selfPlayer = selfPlayer;
            _players.AddRange(players);
            return _players;
        }

        private async UniTask LeaveRoom()
        {
            if (_selfPlayer == null) return;
            await _gamingHubClient.LeaveAsync();
            _selfPlayer = null;
            _players.Clear();
        }

        private void OnPlayerJoined(Player player)
        {
            var isAlready = _players.Any(i => i.ConnectionId == player.ConnectionId);
            if (isAlready) return;
            _players.Add(player);
            MainManager.Instance.RoomSizeChaged(_players);

        }

        private void OnPlayerLeaved(Player player)
        {
            var toRemove = _players.FirstOrDefault(i => i.ConnectionId == player.ConnectionId);
            if (toRemove == null) return;
            _players.Remove(toRemove);
            MainManager.Instance.RoomSizeChaged(_players);
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
                card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
                card.name = item.Name;
                card.transform.position = spawPosition.position;
                yield return card;
            }
        }

        private CardNoBehaviour InstanceUsedCard(CardData item)
        {
            var card = Instantiate(noBehaviorCardPrefab, transform);
            card.CardData = item;
            card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
            card.name = item.Name;
            card.transform.position = new Vector2(0, -10);
            return card;
        }

        private Pinte InstancePinte(CardData item)
        {
            var card = Instantiate(pintePrefab, transform);
            card.CardData = item;
            card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
            card.name = item.Name;
            card.transform.position = new Vector2(0, -10);
            return card;
        }

        private async UniTask OnGameData(GameDataResponse gameDataResponse)
        {
            Debug.Log("Game data received");
            Debug.Log($"Me: {gameDataResponse.PlayerData.Player.ConnectionId}, Next: {gameDataResponse.NextPlayer?.ConnectionId}");
            await SetData(gameDataResponse);

        }

        private async UniTask OnUsedCard(CardData cardData, Player player)
        {
            await _currentSemaphore.WaitAsync();
            try
            {
                SpawnUsedCard(cardData, player);
            }
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private async UniTask CheckTute()
        {
            var cards = _currentCardsGo.Select(i => i.CardData).ToList();
            var tuteKingCards = cards.Where(i => i.Number == 12).ToList();
            var tutePrinceCards = cards.Where(i => i.Number == 11).ToList();

            if (tuteKingCards.Count != 4 && tutePrinceCards.Count != 4)
            {
                return;
            }

            var toUse = tuteKingCards.Count == 4 ? tuteKingCards : tutePrinceCards;
            await _gamingHubClient.Tute(toUse);
        }

        private UniTask OnTute(Player player, CardData tute)
        {
            //TODO show tute info
            Debug.Log($"Player {player.Name} tute {tute.Name}");
            return UniTask.CompletedTask;
        }

        private async UniTask CheckCantar()
        {
            var alreadycantes = _currentData.Cantes
                .OrderByDescending(i => i.Number)
                .Select(i => i.Type)
                .ToList();

            var cantes = _currentCardsGo.Select(i => i.CardData)
                .Where(i => i.Number == 11 || i.Number == 12)
                .GroupBy(i => i.Type)
                .Where(i => !alreadycantes.Contains(i.Key))
                .FirstOrDefault(i => i.Count() == 2);

            if (cantes == null || !cantes.Any())
            {
                return;
            }

            var king = cantes.FirstOrDefault(i => i.Number == 12);
            var prince = cantes.FirstOrDefault(i => i.Number == 11);

            if (king == null || prince == null)
            {
                return;
            }

            await _gamingHubClient.Cante(king, prince);
        }

        private UniTask OnCante(Player player, CardData cante)
        {
            //TODO show cante info
            Debug.Log($"Player {player.Name} ha canta {cante.Name}");
            return UniTask.CompletedTask;
        }

        private async UniTask ChangePinte(CardData card)
        {
            await _currentSemaphore.WaitAsync();
            try
            {
                if (_nextPlayer?.ConnectionId != _selfPlayer?.ConnectionId) return;
                try
                {
                    await _gamingHubClient.ChangePinteAsync(card);
                    var toRemove = _currentCardsGo.FirstOrDefault(i => i.CardData.Name == card.Name);
                    if (toRemove == null) return;
                    _currentCardsGo.Remove(toRemove);
                    _cardRowManager.RemoveCard(toRemove);
                    Destroy(toRemove.gameObject);
                }
                catch (RpcException ex)
                {
                    //Cant perform operation
                    Debug.LogException(ex);
                }
            }
            finally { _currentSemaphore.Release(); }
        }

        private UniTask OnChangedPinte(CardData cardData)
        {
            if (cardData == null)
            {
                if (_pinte != null) Destroy(_pinte.gameObject);
                return UniTask.CompletedTask;
            }

            if (_pinte == null || _pinte.CardData.Name != cardData.Name)
            {
                if (_pinte != null) Destroy(_pinte.gameObject);
                _pinte = InstancePinte(cardData);
                _pinte.OnClick += (i) => ChangePinte(i).Forget();
                _pinte.transform.position = spawPosition.position;
                _pinte.transform.DOMove(pintePosition.position, 0.1f);
            }

            return UniTask.CompletedTask;
        }

        private void SpawnUsedCard(CardData cardData, Player player)
        {
            var item = InstanceUsedCard(cardData);

            if (player.ConnectionId != _selfPlayer.ConnectionId)
            {
                item.transform.position = new Vector3(0, 20);
            }
            else
            {
                item.transform.position = _cardUsedPosition;
            }

            _currentUsedCardsGo.Add(item);
            audioController.PlayFlick();
            item.transform.DOMove(
                usedCardsPosition.transform.position
                    + (item.Sprite.bounds.size.x * _currentUsedCardsGo.Count * Vector3.right),
                0.1f
            );
        }

        private async UniTask SetData(GameDataResponse gameDataResponse, bool isResponse = false)
        {
            await _currentSemaphore.WaitAsync();
            try
            {

                _currentData = gameDataResponse;
                var playerData = gameDataResponse.PlayerData;



                var newCards = playerData
                    .Cards.Where(i => !_currentCardsGo.Any(x => x.CardData.Name == i.Name))
                    .ToList();

                var newUsedCards = gameDataResponse.UsedCards.Values
                    .Where(i => !_currentUsedCardsGo.Any(x => x.CardData.Name == i.Name))
                    .ToList();

                if (!newUsedCards.Any() && !gameDataResponse.UsedCards.Any())
                {
                    await UniTask.WaitForSeconds(1, cancellationToken: destroyCancellationToken);
                    foreach (var item in _currentUsedCardsGo)
                    {
                        Destroy(item.gameObject);
                    }
                    _currentUsedCardsGo.Clear();
                }

                if (!newCards.Any())
                {
                    audioController.PlayFlick();
                    await _cardRowManager.UpdateCardPositions();
                }

                if (!gameDataResponse.UsedCards.Any())
                {
                    foreach (var item in _currentUsedCardsGo)
                    {
                        Destroy(item.gameObject);
                    }
                    _currentUsedCardsGo.Clear();
                }

                foreach (var item in InstanceCards(newCards))
                {
                    _cardRowManager.AddCard(item);
                    _currentCardsGo.Add(item);
                    audioController.PlayFlick();
                    await _cardRowManager.UpdateCardPositions();
                }

                //If we win
                if (isResponse && gameDataResponse.NextPlayer?.ConnectionId == _selfPlayer?.ConnectionId)
                {
                    await CheckTute();
                    await CheckCantar();
                }

                _nextPlayer = gameDataResponse.NextPlayer;
                _selfPlayer = playerData.Player;
            }
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private async UniTask MakeMove(CardData card)
        {
            if (_currentTask != null && !_currentTask.IsCompleted)
                return;

            if (_nextPlayer == null || _selfPlayer == null)
                return;

            if (_nextPlayer.ConnectionId != _selfPlayer.ConnectionId)
                return;

            var instancedCard = _currentCardsGo.FirstOrDefault(x => x.name == card.Name);
            if (instancedCard == null)
                return;

            _cardUsedPosition = instancedCard.transform.position;

            GameDataResponse gameDataResponse;
            try
            {
                gameDataResponse = await _gamingHubClient.MakeMoveAsync(card);
                Debug.Log("Game data sent");
            }
            catch (RpcException ex)
            {
                //Cant perform 
                Debug.LogException(ex);
                return;
            }

            _cardRowManager.RemoveCard(instancedCard);
            _currentCardsGo.Remove(instancedCard);
            audioController.PlayFlick();
            Destroy(instancedCard.gameObject);
            var tasks = new List<UniTask>() { SetData(gameDataResponse, true), _cardRowManager.UpdateCardPositions() };
            _currentTask = UniTask.WhenAll(tasks).AsTask();
            await _currentTask;
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(15, 15, 100, 30), $"Leader: {_selfPlayer?.IsLeader}");
            GUI.Label(
                new Rect(15, 30, 100, 30),
                $"Points: {_currentData?.PlayerData.GainedCards.Sum(i => i.Value)}"
            );
            GUI.Label(new Rect(15, 45, 100, 30), $"Pinte: {_currentData?.Pinte?.Type.ToString()}");
            GUI.Label(
                new Rect(15, 60, 100, 30),
                $"Your turn: {_selfPlayer?.ConnectionId == _nextPlayer?.ConnectionId}"
            );
        }
    }
}
