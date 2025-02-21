using System;
using System.Threading;
using Assets.Scripts.Managers;
using Cysharp.Threading.Tasks;
using MagicOnion;
using MagicOnion.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public class ServerMenuController : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField tmp_serverAdress;

        [SerializeField]
        private Button connectButton;

        [SerializeField]
        private Button disconnectButton;

        [SerializeField]
        private Button selectButton;

        [SerializeField]
        private TMP_Text tmp_status;

        [SerializeField]
        private TMP_Text tmp_serverInfo;

        [SerializeField]
        private TMP_Text tmp_version;

        [SerializeField]
        private Button tmp_exitButton;

        private CancellationToken _cancellationToken;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            _cancellationToken = destroyCancellationToken;

            var defaultAdress = PlayerPrefs.GetString("server_adress", tmp_serverAdress.text);
            tmp_serverAdress.text = defaultAdress;

            connectButton.onClick.RemoveAllListeners();
            disconnectButton.onClick.RemoveAllListeners();
            selectButton.onClick.RemoveAllListeners();
            tmp_exitButton.onClick.RemoveAllListeners();

            RenderData().Forget();

            GamingHubManager.Instance.Client.OnDisconnected += RenderDataForget;
            connectButton.onClick.AddListener(ConnectForget);
            disconnectButton.onClick.AddListener(DisposeForget);
            selectButton.onClick.AddListener(LoadGameForget);
            tmp_exitButton.onClick.AddListener(Exit);
        }

        private void OnDestroy()
        {
            GamingHubManager.Instance.Client.OnDisconnected -= RenderDataForget;
            connectButton.onClick.RemoveListener(ConnectForget);
            disconnectButton.onClick.RemoveListener(DisposeForget);
            selectButton.onClick.RemoveListener(LoadGameForget);
            tmp_exitButton.onClick.RemoveListener(Exit);
        }

        private void RenderDataForget() => RenderData().Forget();

        private void ConnectForget() => Connect().Forget();

        private void DisposeForget() =>
            GamingHubManager.Instance.Client.DisposeAsync().AsUniTask().Forget();

        private void LoadGameForget()
        {
            if (GamingHubManager.Instance.Client?.IsConnected == true)
            {
                MenuManager.Instance.LoadGame(_cancellationToken).Forget();
            }
        }

        private void Exit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private async UniTask Connect()
        {
            PlayerPrefs.SetString("server_adress", tmp_serverAdress.text);
            var uri = new Uri(tmp_serverAdress.text);
            await GamingHubManager.Instance.Client.ConnectAsync(
                GrpcChannelx.ForTarget(new GrpcChannelTarget(uri.Host, uri.Port, true))
            );
            RenderServerInfo();
            RenderServerVersion();
        }

        private async UniTask RenderData()
        {
            await UniTask.Yield();
            RenderVersion();
            RenderServerVersion();
            RenderServerInfo();
        }

        private void RenderVersion()
        {
            var version = $"<color=grey><b>{Application.version}</b></color>";
            tmp_version.text = $"Version {version}";
        }

        private void RenderServerVersion()
        {
            if (!GamingHubManager.Instance.Client.IsConnected)
            {
                tmp_status.text = string.Empty;
                return;
            }

            var version = $"<color=grey><b>{GamingHubManager.Instance.Client.Version}</b></color>";
            tmp_status.text = $"Server version {version}";
        }

        private void RenderServerInfo()
        {
            if (!GamingHubManager.Instance.Client.IsConnected)
            {
                tmp_serverInfo.text = string.Empty;
                return;
            }
            var info = $"<color=green>{GamingHubManager.Instance.Client.Target}</color>";
            tmp_serverInfo.text = $"<b>{info}</b>";
        }
    }
}
