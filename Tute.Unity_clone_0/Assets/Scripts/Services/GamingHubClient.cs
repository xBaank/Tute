using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Client;
using MagicOnion.Unity;
using Tute.Shared.Constants;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts.Services
{
    public class GamingHubClient : IGamingHubReceiver
    {
        private IGamingHub client;
        private ChannelBase channel;

        public event Func<GameDataResponse, UniTask> OnGameDataEvent;
        public event Func<CardData, Player, UniTask> OnUsedCardEvent;
        public event Func<CardData, UniTask> OnChangedPinteEvent;
        public event Func<Player, CardData, UniTask> OnCanteEvent;
        public event Func<Player, CardData, UniTask> OnTuteEvent;
        public event Func<Player, CardData, UniTask> OnDiezDelMonteEvent;
        public event Action<Player> OnJoinEvent;
        public event Action<Player> OnLeaveEvent;
        public event Action<Player> OnUpdatedEvent;
        public event Action OnStartEvent;
        public event Action<List<GameDataResponse>> OnFinishEvent;
        public event Action<string, Player> OnMessageEvent;
        public event Action OnDisconnected;

        public Version ServerVersion { get; private set; }
        public bool IsConnected { get; private set; }
        public string Target => channel?.Target ?? string.Empty;

        public async ValueTask ConnectAsync(ChannelBase grpcChannel)
        {
            if (IsConnected)
                throw new InvalidOperationException("Ya estas conectado");

            channel = grpcChannel;
            try
            {
                client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
                    grpcChannel,
                    this
                );
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw new IOException($"No se puede conectar al servidor {grpcChannel.Target}");
            }

            var serverVersion = new Version(await client.GetVersion());
            var currentVersion = new Version(Application.version);

            //TODO Check version with unity version and missmatch on screen
            if (serverVersion.Major != currentVersion.Major || serverVersion.Minor > currentVersion.Minor)
            {
                await DisposeAsync().AsUniTask();
                await WaitForDisconnectAsync().AsUniTask();
                throw new InvalidOperationException($"La version del cliente {currentVersion} no corresponde con la del servidor {serverVersion}");
            }

            ServerVersion = serverVersion;
            IsConnected = true;
            WaitForDisconnectAsync().AsUniTask().Forget();
        }

        public ValueTask ConnectAsync(string host, int port) =>
            ConnectAsync(GrpcChannelx.ForTarget(new GrpcChannelTarget(host, port, true)));

        public async ValueTask<(Player, Player[])> JoinAsync(string roomName, string playername)
        {
            var (self, roomPlayers) = await client.JoinAsync(
                roomName,
                playername,
                DecksConstants.NormalDeck.DeckName,
                true
            );
            return (self, roomPlayers);
        }

        public ValueTask LeaveAsync() => client.LeaveAsync();

        // dispose client-connection before channel.ShutDownAsync is important!
        public async Task DisposeAsync()
        {
            if (client is not null)
                await client.DisposeAsync();
            if (channel is not null)
                await channel.ShutdownAsync();
        }

        // You can watch connection state, use this for retry etc.
        public async Task WaitForDisconnectAsync()
        {
            await client.WaitForDisconnect();
            IsConnected = false;
            ServerVersion = null;
            channel = null;
            OnDisconnected?.Invoke();
        }

        public ValueTask MakeMove(CardData card) => client.MakeMove(card);

        public ValueTask ChangePinteAsync(CardData card) => client.ChangePinte(card);

        public ValueTask SendMessage(string message) => client.SendMessage(message);

        public ValueTask StartAsync() => client.StartAsync();

        public void OnJoin(Player player) => OnJoinEvent?.Invoke(player);

        public void OnLeave(Player player) => OnLeaveEvent?.Invoke(player);

        public void OnUpdated(Player player) => OnUpdatedEvent?.Invoke(player);

        public void OnGameData(GameDataResponse gameData) =>
            OnGameDataEvent?.Invoke(gameData).Forget();

        public void OnUsedCard(CardData card, Player userCard) =>
            OnUsedCardEvent?.Invoke(card, userCard).Forget();

        public void OnStart() => OnStartEvent?.Invoke();

        public void OnFinished(List<GameDataResponse> playerDatas) =>
            OnFinishEvent?.Invoke(playerDatas);

        public void OnChangedPinte(CardData cardData) =>
            OnChangedPinteEvent?.Invoke(cardData).Forget();

        public void OnCante(Player player, CardData cardData) =>
            OnCanteEvent?.Invoke(player, cardData).Forget();

        public void OnTute(Player player, CardData cardData) =>
            OnTuteEvent?.Invoke(player, cardData).Forget();

        public void OnDiezDelMonte(Player player, CardData cardData) =>
            OnDiezDelMonteEvent?.Invoke(player, cardData).Forget();

        public void OnMessage(string message, Player player) =>
            OnMessageEvent?.Invoke(message, player);
    }
}
