using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts.Services
{
    public class GamingHubClient : IGamingHubReceiver
    {
        private readonly Dictionary<Guid, GameObject> players = new();
        private readonly Guid guid;
        private IGamingHub client;

        public event Func<GameDataResponse, UniTask> OnGameDataEvent;
        public event Func<CardData, Player, UniTask> OnUsedCardEvent;

        public GamingHubClient(Guid guid)
        {
            this.guid = guid;
        }

        public async ValueTask<Player> ConnectAsync(ChannelBase grpcChannel, string roomName)
        {
            client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
                grpcChannel,
                this
            );

            var (self, roomPlayers) = await client.JoinAsync(roomName, guid.ToString());
            foreach (var player in roomPlayers)
            {
                (this as IGamingHubReceiver).OnJoin(player);
            }

            return self;
        }

        public ValueTask LeaveAsync()
        {
            return client.LeaveAsync();
        }

        // dispose client-connection before channel.ShutDownAsync is important!
        public Task DisposeAsync()
        {
            return client.DisposeAsync();
        }

        // You can watch connection state, use this for retry etc.
        public Task WaitForDisconnect()
        {
            return client.WaitForDisconnect();
        }

        public ValueTask<GameDataResponse> MakeMoveAsync(CardData card)
        {
            return client.MakeMoveAsync(card);
        }

        public ValueTask StartAsync(IList<CardData> cardDatas)
        {
            return client.StartAsync(cardDatas);
        }

        public void OnJoin(Player player)
        {
            players[player.ConnectionId] = new GameObject();
        }

        public void OnLeave(Player player)
        {
            if (players.TryGetValue(player.ConnectionId, out var cube))
            {
                GameObject.Destroy(cube);
            }
        }

        public void OnGameData(GameDataResponse gameData)
        {
            OnGameDataEvent?.Invoke(gameData).Forget();
        }

        public void OnUsedCard(CardData card, Player userCard)
        {
            OnUsedCardEvent?.Invoke(card, userCard).Forget();
        }
    }
}
