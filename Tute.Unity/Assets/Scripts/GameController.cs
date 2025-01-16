using System.Linq;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Unity;
using Newtonsoft.Json;
using Tute.Shared;
using UnityEngine;

namespace Assets.Scripts
{
    using System;
    using Assets.Scripts.Services;
    using Cysharp.Threading.Tasks;
    using MagicOnion.Client;
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

            var me = await gamingHubClient.ConnectAsync(channel, "room", Guid.NewGuid());

            await gamingHubClient.MakeMoveAsync(new CardData());

            // NOTE: If your project targets non-.NET Standard 2.1, use `Grpc.Core.Channel` class instead.
            // var channel = new Channel("localhost", 5001, new SslCredentials());

            // Create a proxy to call the server transparently.
            ITestService serviceClient = MagicOnionClient.Create<ITestService>(channel);
            int result = await serviceClient.SumAsync(1, 2);
            Debug.Log(result);
        }
    }
}
