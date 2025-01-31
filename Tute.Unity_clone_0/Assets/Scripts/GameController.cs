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
using Newtonsoft.Json;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            _gamingHubClient.OnGameDataEvent += OnGameData;
            _gamingHubClient.OnUsedCardEvent += OnUsedCard;
            _gamingHubClient.OnJoinEvent += OnPlayerJoined;
            _gamingHubClient.OnLeaveEvent += OnPlayerLeaved;
            _gamingHubClient.OnStartEvent += () => OnStart().Forget();
            _gamingHubClient.OnFinishEvent += (i) => OnFinish(i).Forget();
            MainManager.Instance.OnRoomJoin += JoinRoom;
            MainManager.Instance.OnStartGame += async () => await StartGame();
            MainManager.Instance.OnLeaveRoom += LeaveRoom;
        }

        private async UniTaskVoid ConnectToServer()
        {
            _channel = GrpcChannelx.ForTarget(new GrpcChannelTarget("localhost", 5000, true));
            await _gamingHubClient.ConnectAsync(_channel);
        }

        private async UniTask StartGame()
        {
            if (_selfPlayer == null) return;
            await _gamingHubClient.StartAsync(GetCards());
        }

        private async UniTaskVoid OnStart()
        {
            await SceneManager.UnloadSceneAsync("Menu");
        }

        private async UniTaskVoid OnFinish(IList<GameDataResponse> playerDatas)
        {
            await UniTask.WaitForSeconds(1, cancellationToken: destroyCancellationToken);

            DOTween.Clear();
            _cardRowManager.Clear();
            _currentCardsGo.ForEach(i => Destroy(i.gameObject));
            _currentUsedCardsGo.ForEach(i => Destroy(i.gameObject));
            _currentCardsGo.Clear();
            _currentUsedCardsGo.Clear();
            Destroy(_pinte.gameObject);
            _selfPlayer = null;
            _nextPlayer = null;
            _currentData = null;
            _currentTask = null;
            _pinte = null;

            //TODO calculate winner


            await SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Additive);
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

        private void LeaveRoom()
        {
            if (_selfPlayer == null) return;
            _gamingHubClient.LeaveAsync();
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

        private async UniTask ChangePinte(CardData card)
        {
            await _currentSemaphore.WaitAsync();
            try
            {
                if (_nextPlayer?.ConnectionId != _selfPlayer.ConnectionId) return;
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

        private void SpawnPinte(GameDataResponse gameDataResponse)
        {
            if (gameDataResponse.Pinte == null)
            {
                if (_pinte != null) Destroy(_pinte.gameObject);
                return;
            }

            if (_pinte == null || _pinte.CardData.Name != gameDataResponse.Pinte.Name)
            {
                if (_pinte != null) Destroy(_pinte.gameObject);
                _pinte = InstancePinte(gameDataResponse.Pinte);
                _pinte.OnClick += (i) => ChangePinte(i).Forget();
                _pinte.transform.position = spawPosition.position;
                _pinte.transform.DOMove(pintePosition.position, 0.1f);
            }
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

        private async UniTask SetData(GameDataResponse gameDataResponse)
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

                SpawnPinte(gameDataResponse);

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

            _cardUsedPosition = instancedCard.transform.position;
            _cardRowManager.RemoveCard(instancedCard);
            _currentCardsGo.Remove(instancedCard);
            audioController.PlayFlick();
            Destroy(instancedCard.gameObject);
            var tasks = new List<UniTask>() { SetData(gameDataResponse), _cardRowManager.UpdateCardPositions() };
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
