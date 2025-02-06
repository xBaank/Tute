using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Assets.Scripts.Services
{
    public class GamingHubClient : IGamingHubReceiver
    {
        private IGamingHub client;

        public event Func<GameDataResponse, UniTask> OnGameDataEvent;
        public event Func<CardData, Player, UniTask> OnUsedCardEvent;
        public event Func<CardData, UniTask> OnChangedPinteEvent;
        public event Func<Player, CardData, UniTask> OnCanteEvent;
        public event Func<Player, CardData, UniTask> OnTuteEvent;
        public event Action<Player> OnJoinEvent;
        public event Action<Player> OnLeaveEvent;
        public event Action OnStartEvent;
        public event Action<List<GameDataResponse>> OnFinishEvent;
        public event Action<string, Player> OnMessageEvent;

        public async ValueTask ConnectAsync(ChannelBase grpcChannel)
        {
            client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
                grpcChannel,
                this
            );
        }

        public async ValueTask<(Player, Player[])> JoinAsync(string roomName, string playername)
        {
            var (self, roomPlayers) = await client.JoinAsync(roomName, playername);
            return (self, roomPlayers);
        }

        public ValueTask LeaveAsync() => client.LeaveAsync();

        // dispose client-connection before channel.ShutDownAsync is important!
        public Task DisposeAsync() => client.DisposeAsync();

        // You can watch connection state, use this for retry etc.
        public Task WaitForDisconnectAsync() => client.WaitForDisconnect();

        public ValueTask MakeMove(CardData card) => client.MakeMove(card);

        public ValueTask ChangePinteAsync(CardData card) => client.ChangePinte(card);

        public ValueTask Cante(CardData king, CardData prince) => client.Cante(king, prince);

        public ValueTask Tute(IList<CardData> cards) => client.Tute(cards);

        public ValueTask SendMessage(string message) => client.SendMessage(message);

        public ValueTask StartAsync() => client.StartAsync();

        public void OnJoin(Player player) => OnJoinEvent?.Invoke(player);

        public void OnLeave(Player player) => OnLeaveEvent?.Invoke(player);

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

        public void OnMessage(string message, Player player) =>
            OnMessageEvent?.Invoke(message, player);
    }
}
