using System.Linq;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Unity;
using Newtonsoft.Json;
using UnityEngine;

namespace Assets.Scripts
{
    using System;
    using Assets.Scripts.Services;
    using Cysharp.Threading.Tasks;
    using Tute.Shared.Models;

    public class GameController : MonoBehaviour
    {
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

        [SerializeField]
        private Card cardPrefab;

        [SerializeField]
        private TextAsset cardsData;

        [SerializeField]
        private Sprite[] spriteSheet;

        private readonly GamingHubClient gamingHubClient = new();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            Server().Forget();
            CardData[] data = JsonConvert.DeserializeObject<CardData[]>(cardsData.text);
            foreach (CardData item in data)
            {
                Card card = Instantiate(cardPrefab, transform);
                card.cardType = item.Type;
                card.value = item.Value;
                card.Sprite = spriteSheet.First(sprite => sprite.name == item.SpriteName);
                card.name = item.Name;
            }
        }

        private async UniTaskVoid Server()
        {
            // Connect to the server using gRPC channel.
            GrpcChannelx channel = GrpcChannelx.ForTarget(
                new GrpcChannelTarget("localhost", 5000, true)
            );

            await gamingHubClient.ConnectAsync(channel, "room", Guid.NewGuid());
            await gamingHubClient.StartAsync();
            await gamingHubClient.MakeMoveAsync(new CardData { Name = "Something" });
        }
    }
}
