using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Services;
using Cysharp.Net.Http;
using Cysharp.Threading.Tasks;
using Grpc.Net.Client;
using MagicOnion.Unity;
using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts.Managers
{
    public partial class GamingHubManager : SingletonBase<GamingHubManager>
    {
        public GameRoom GameRoom { get; private set; }
        public GamingHubClient Client { get; } = new();
        public GameDataResponse CurrentData { get; private set; }
        public GameState State => CurrentData?.GameState ?? default;

        public event Action OnRoomDataUpdated;

        private CancellationToken _cancellationToken;

        private void Awake()
        {
            Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;
            _cancellationToken = destroyCancellationToken;

            Client.OnJoinEvent += OnPlayerJoin;
            Client.OnLeaveEvent += OnPlayerLeaved;
            Client.OnUpdatedEvent += OnPlayerUpdated;
            Client.OnGameDataEvent += SetCurrentState;
            Client.OnFinishEvent += ClearCurrentData;
            Client.OnDisconnected += OnDisconneted;

            CreateInstance();
        }

        private void OnDestroy()
        {
            Client.OnJoinEvent -= OnPlayerJoin;
            Client.OnLeaveEvent -= OnPlayerLeaved;
            Client.OnUpdatedEvent -= OnPlayerUpdated;
            Client.OnGameDataEvent -= SetCurrentState;
            Client.OnFinishEvent -= ClearCurrentData;
            Client.OnDisconnected -= OnDisconneted;
        }

        private void ClearCurrentData(List<GameDataResponse> _) => CurrentData = null;

        private async void OnDisconneted()
        {
            await UniTask.Yield();
            GameRoom = null;
            OnRoomDataUpdated?.Invoke();
            MenuManager.Instance.LoadServerMenu(_cancellationToken).Forget();
        }

        private UniTask SetCurrentState(GameDataResponse gameDataResponse)
        {
            CurrentData = gameDataResponse;
            return UniTask.CompletedTask;
        }

        private void OnPlayerJoin(Player player)
        {
            var isAlready = GameRoom.Players.Any(i => i.ConnectionId == player.ConnectionId);
            if (isAlready)
                return;
            GameRoom.Players.Add(player);
            OnRoomDataUpdated?.Invoke();
        }

        private void OnPlayerLeaved(Player player)
        {
            var toRemove = GameRoom.Players.FirstOrDefault(i =>
                i.ConnectionId == player.ConnectionId
            );
            if (toRemove is null)
                return;
            GameRoom.Players.Remove(toRemove);
            OnRoomDataUpdated?.Invoke();
        }

        private void OnPlayerUpdated(Player player)
        {
            var toUpdate = GameRoom.Players.FirstOrDefault(i =>
                i.ConnectionId == player.ConnectionId
            );
            if (toUpdate is null)
                return;

            toUpdate.ConnectionId = player.ConnectionId;
            toUpdate.Name = player.Name;
            toUpdate.IsLeader = player.IsLeader;
            toUpdate.TeamIndex = player.TeamIndex;

            if (toUpdate.ConnectionId == GameRoom.Player.ConnectionId)
            {
                GameRoom.Player.ConnectionId = toUpdate.ConnectionId;
                GameRoom.Player.Name = toUpdate.Name;
                GameRoom.Player.IsLeader = toUpdate.IsLeader;
                GameRoom.Player.TeamIndex = toUpdate.TeamIndex;
            }

            OnRoomDataUpdated?.Invoke();
        }

        public async UniTask JoinRoom(string roomName, string playerName)
        {
            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentNullException(
                    "roomName, playerName",
                    "Room name and player name must not be empty"
                );
            }

            var (selfPlayer, players) = await Client.JoinAsync(roomName, playerName);
            GameRoom = new GameRoom(roomName, players.ToList(), selfPlayer);
            OnRoomDataUpdated?.Invoke();
        }

        public async UniTask LeaveRoom()
        {
            await Client.LeaveAsync();
            GameRoom = null;
            OnRoomDataUpdated?.Invoke();
        }
    }

    public partial class GamingHubManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnRuntimeInitialize()
        {
            GrpcChannelProviderHost.Initialize(
                new DefaultGrpcChannelProvider(
                    () =>
                        new GrpcChannelOptions()
                        {
                            HttpHandler = new YetAnotherHttpHandler() { Http2Only = true },
                            DisposeHttpClient = true,
                        }
                )
            );
        }
    }
}
