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

        IGamingHub client;

        public async ValueTask<GameObject> ConnectAsync(
            ChannelBase grpcChannel,
            string roomName,
            Guid id
        )
        {
            client = await StreamingHubClient.ConnectAsync<IGamingHub, IGamingHubReceiver>(
                grpcChannel,
                this
            );

            var roomPlayers = await client.JoinAsync(roomName, id);
            foreach (var player in roomPlayers)
            {
                (this as IGamingHubReceiver).OnJoin(player);
            }

            return players[id];
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

        public ValueTask<IList<CardData>> GetCardsAsync()
        {
            return client.GetCardsAsync();
        }

        public void OnGameStart(IList<CardData> card)
        {
            throw new NotImplementedException();
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
    }
}
