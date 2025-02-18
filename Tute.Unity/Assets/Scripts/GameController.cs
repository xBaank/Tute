using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Cards;
using Assets.Scripts.Extensions;
using Assets.Scripts.Managers;
using Assets.Scripts.Services;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Grpc.Core;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public partial class GameController : MonoBehaviour
    {
        [SerializeField]
        private Card cardPrefab;

        [SerializeField]
        private Pinte pintePrefab;

        [SerializeField]
        private CardNoBehaviour noBehaviorCardPrefab;

        [SerializeField]
        private RectTransform stackPosition;

        [SerializeField]
        private RectTransform spawPosition;

        [SerializeField]
        private RectTransform usedCardsPosition;

        [SerializeField]
        private RectTransform pintePosition;

        [SerializeField]
        private RectTransform gainedPosition;

        [SerializeField]
        private Sprite[] spriteSheet;

        [SerializeField]
        private ChatController chatController;

        [SerializeField]
        private AudioController audioController;

        [SerializeField]
        private Button menu;

        private readonly List<Card> _currentCardsGo = new();
        private readonly List<CardNoBehaviour> _currentUsedCardsGo = new();
        private readonly SemaphoreSlim _currentSemaphore = new(1);
        private Player _nextPlayer;
        private CardRowManager _cardRowManager;
        private Vector2 _cardUsedPosition;
        private Pinte _pinte;

        private Player SelfPlayer => GamingHubManager.Instance.GameRoom?.Player;
        private GameDataResponse CurrentData => GamingHubManager.Instance.CurrentData;
        private GamingHubClient Client => GamingHubManager.Instance.Client;
        private CancellationToken _cancellationToken;

        private void Start()
        {
            _cancellationToken = destroyCancellationToken;

            _cardRowManager = new CardRowManager(
                stackPosition.position.x,
                stackPosition.position.y,
                Camera.main.aspect
            );

            var backCard = new CardData { SpriteName = "back" };
            var card = InstanceUsedCard(backCard);
            var card2 = InstanceUsedCard(backCard);
            card.transform.position = spawPosition.position.ToVector2();
            card2.transform.position = gainedPosition.position.ToVector2();
            card.transform.rotation = spawPosition.rotation;
            card2.transform.rotation = gainedPosition.rotation;

            MenuManager.Instance.LoadMenu(_cancellationToken).Forget();
            MenuManager.Instance.HandleMenu(token: _cancellationToken).Forget();

            Client.OnGameDataEvent += OnGameData;
            Client.OnUsedCardEvent += OnUsedCard;
            Client.OnChangedPinteEvent += OnChangedPinte;
            Client.OnCanteEvent += OnCante;
            Client.OnTuteEvent += OnTute;
            Client.OnDiezDelMonteEvent += OnDiezDelMonte;
            Client.OnStartEvent += StartForget;
            Client.OnFinishEvent += OnFinishForget;

            menu.onClick.AddListener(
                () => MenuManager.Instance.SwapMenu(_cancellationToken).Forget()
            );
        }

        private void OnDestroy()
        {
            Client.OnGameDataEvent -= OnGameData;
            Client.OnUsedCardEvent -= OnUsedCard;
            Client.OnChangedPinteEvent -= OnChangedPinte;
            Client.OnCanteEvent -= OnCante;
            Client.OnTuteEvent -= OnTute;
            Client.OnDiezDelMonteEvent -= OnDiezDelMonte;
            Client.OnStartEvent -= StartForget;
            Client.OnFinishEvent -= OnFinishForget;
            DOTween.Clear();
        }

        private void StartForget() => OnStart().Forget();

        private void OnFinishForget(List<GameDataResponse> data) => OnFinish(data).Forget();

        private async UniTaskVoid OnStart()
        {
            await MenuManager.Instance.UnloadMenu(_cancellationToken);
        }

        private async UniTaskVoid OnFinish(IList<GameDataResponse> _)
        {
            await _currentSemaphore.WaitAsync();

            try
            {
                DOTween.Clear();
                _cardRowManager.Clear();
                _currentCardsGo.Where(i => i != null).ForEach(i => Destroy(i.gameObject));
                _currentUsedCardsGo.Where(i => i != null).ForEach(i => Destroy(i.gameObject));
                _currentCardsGo.Clear();
                _currentUsedCardsGo.Clear();
                if (_pinte != null)
                    Destroy(_pinte.gameObject);
                _nextPlayer = null;
                _pinte = null;

                await UniTask.WaitForSeconds(5, cancellationToken: _cancellationToken);
                await MenuManager.Instance.LoadMenu(_cancellationToken);
            }
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private IEnumerable<Card> InstanceCards(IList<CardData> data)
        {
            foreach ((var index, var item) in data.WithIndex())
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var card = Instantiate(cardPrefab, transform);
                card.OnClick += MakeMove;
                card.CardData = item;
                card.CardRowManager = _cardRowManager;
                card.AudioController = audioController;
                card.Sprite = spriteSheet.FirstOrDefault(sprite => sprite.name == item.SpriteName);
                card.name = item.Name;
                card.transform.position = spawPosition.position.ToVector2();
                card.transform.localScale *= Camera.main.aspect / 1.7f;
                yield return card;
            }
        }

        private CardNoBehaviour InstanceUsedCard(CardData item)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var card = Instantiate(noBehaviorCardPrefab, transform);
            card.CardData = item;
            card.Sprite = spriteSheet.FirstOrDefault(sprite => sprite.name == item.SpriteName);
            card.name = item.Name;
            card.transform.position = new Vector2(0, -10);
            card.transform.localScale *= Camera.main.aspect / 1.7f;
            return card;
        }

        private Pinte InstancePinte(CardData item)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var card = Instantiate(pintePrefab, transform);
            card.CardData = item;
            card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
            card.name = item.Name;
            card.transform.position = pintePosition.position.ToVector2();
            card.transform.localScale *= Camera.main.aspect / 1.7f;
            return card;
        }

        private async UniTask OnGameData(GameDataResponse gameDataResponse)
        {
            Debug.Log("Game data received");
            Debug.Log(
                $"Me: {gameDataResponse.PlayerData.Player.ConnectionId}, Next: {gameDataResponse.NextPlayer?.ConnectionId}"
            );
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

        private UniTask OnTute(Player player, CardData tute)
        {
            chatController.OnSystemMessage($"Player {player.Name} tute {tute.Name}");
            return UniTask.CompletedTask;
        }

        private UniTask OnDiezDelMonte(Player player, CardData tute)
        {
            chatController.OnSystemMessage($"Player {player.Name} se lleva {tute.Name}");
            return UniTask.CompletedTask;
        }

        private UniTask OnCante(Player player, CardData cante)
        {
            chatController.OnSystemMessage($"Player {player.Name} ha cantado {cante.Name}");
            return UniTask.CompletedTask;
        }

        private async UniTask ChangePinte(CardData card)
        {
            await _currentSemaphore.WaitAsync();
            try
            {
                if (_nextPlayer?.ConnectionId != SelfPlayer?.ConnectionId)
                    return;
                try
                {
                    await Client.ChangePinteAsync(card);
                    var toRemove = _currentCardsGo.FirstOrDefault(i =>
                        i.CardData.Name == card.Name
                    );
                    if (toRemove == null)
                        return;
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
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private UniTask OnChangedPinte(CardData cardData)
        {
            if (cardData == null)
            {
                if (_pinte != null)
                    Destroy(_pinte.gameObject);
                return UniTask.CompletedTask;
            }

            if (_pinte == null || _pinte.CardData.Name != cardData.Name)
            {
                if (_pinte != null)
                    Destroy(_pinte.gameObject);
                _pinte = InstancePinte(cardData);
                _pinte.OnClick += (i) => ChangePinte(i).Forget();
                _pinte.transform.position = spawPosition.position.ToVector2();
                _pinte.transform.DOMove(pintePosition.position.ToVector2(), 0.1f);
            }

            return UniTask.CompletedTask;
        }

        private void SpawnUsedCard(CardData cardData, Player player)
        {
            var item = InstanceUsedCard(cardData);

            if (player.ConnectionId != SelfPlayer.ConnectionId)
            {
                item.transform.position = new Vector3(0, 20);
            }
            else
            {
                item.transform.position = _cardUsedPosition;
            }

            var targetPosition = usedCardsPosition.transform.position + (item.Sprite.bounds.size.x * _currentUsedCardsGo.Count * Vector3.right);
            _currentUsedCardsGo.Add(item);
            audioController.PlayFlick();
            item.transform.DOMove(targetPosition.ToVector2(), 0.1f);
        }

        private async UniTask SetData(GameDataResponse gameDataResponse)
        {
            await _currentSemaphore.WaitAsync();
            try
            {
                var winned = gameDataResponse.WinnerId == SelfPlayer.ConnectionId;
                var playerData = gameDataResponse.PlayerData;

                var newCards = playerData
                    .Cards.Where(i => !_currentCardsGo.Any(x => x.CardData.Name == i.Name))
                    .ToList();

                var newUsedCards = gameDataResponse
                    .UsedCards.Values.Where(i =>
                        !_currentUsedCardsGo.Any(x => x.CardData.Name == i.Name)
                    )
                    .ToList();

                if (!newUsedCards.Any() && !gameDataResponse.UsedCards.Any())
                {
                    const float time = 0.25f;
                    await UniTask.WaitForSeconds(1, cancellationToken: _cancellationToken);
                    var moveTasks = _currentUsedCardsGo
                        .Select(i =>
                            winned
                                ? i
                                    .transform.DOMove(gainedPosition.transform.position.ToVector2(), time)
                                    .AsyncWaitForCompletion()
                                : i
                                    .transform.DOMove(new Vector2(0, 20), time)
                                    .AsyncWaitForCompletion()
                        )
                        .ToList();
                    var rotateTasks = _currentUsedCardsGo
                        .Select(i =>
                            winned
                                ? i
                                    .transform.DORotate(new Vector3(0, 0, 90), time)
                                    .AsyncWaitForCompletion()
                                : i
                                    .transform.DORotate(new Vector3(0, 0, 90), time)
                                    .AsyncWaitForCompletion()
                        )
                        .ToList();

                    var tasks = new List<List<Task>>() { moveTasks, rotateTasks }
                        .SelectMany(i => i)
                        .ToList();
                    await Task.WhenAll(tasks);

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

                _nextPlayer = gameDataResponse.NextPlayer;
            }
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private async UniTask MakeMove(CardData card)
        {
            if (_nextPlayer == null || SelfPlayer == null)
                return;

            if (_nextPlayer.ConnectionId != SelfPlayer.ConnectionId)
                return;

            await _currentSemaphore.WaitAsync();
            try
            {
                var instancedCard = _currentCardsGo.FirstOrDefault(x => x.name == card.Name);
                if (instancedCard == null)
                    return;

                _cardUsedPosition = instancedCard.transform.position;

                try
                {
                    await Client.MakeMove(card);
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
            }
            finally
            {
                _currentSemaphore.Release();
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(15, 15, 100, 30), $"Leader: {SelfPlayer?.IsLeader}");
            GUI.Label(
                new Rect(15, 30, 100, 30),
                $"Points: {CurrentData?.PlayerData.GainedCards.Sum(i => i.Value)}"
            );
            GUI.Label(new Rect(15, 45, 100, 30), $"Pinte: {CurrentData?.PinteType.ToString()}");
            GUI.Label(
                new Rect(15, 60, 100, 30),
                $"Your turn: {SelfPlayer?.ConnectionId == _nextPlayer?.ConnectionId}"
            );
            GUI.Label(new Rect(15, 75, 100, 30), $"Wins: {CurrentData?.PlayerData.WinsCount}");
        }
    }
}
