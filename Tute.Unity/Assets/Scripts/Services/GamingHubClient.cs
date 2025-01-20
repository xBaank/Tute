using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public event Action<IList<CardData>> OnGameStartEvent;
        public event Action<GameData> OnGameDataEvent;

        public GamingHubClient(Guid guid)
        {
            this.guid = guid;
        }

        public async ValueTask<GameObject> ConnectAsync(
            ChannelBase grpcChannel,
            string roomName
        )
        {
            client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
                grpcChannel,
                this
            );

            var roomPlayers = await client.JoinAsync(roomName, guid);
            foreach (var player in roomPlayers)
            {
                (this as IGamingHubReceiver).OnJoin(player);
            }

            return players[guid];
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

        public ValueTask MakeMoveAsync(CardData card)
        {
            return client.MakeMoveAsync(card);
        }

        public ValueTask StartAsync(IList<CardData> cardDatas)
        {
            return client.StartAsync(cardDatas);
        }


        public void OnJoin(Player player)
        {
            players[player.Id] = new GameObject();
        }

        public void OnLeave(Player player)
        {
            if (players.TryGetValue(player.Id, out var cube))
            {
                GameObject.Destroy(cube);
            }
        }


        public void OnGameStart(GameRoom gameRoom)
        {
            var cards = gameRoom.Data[guid].Cards;
            OnGameStartEvent?.Invoke(cards);
        }

        public void OnGameData(GameRoom gameData)
        {
            Debug.Log("Received data");
        }
    }
}
