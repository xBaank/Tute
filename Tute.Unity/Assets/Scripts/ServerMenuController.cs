using System;
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
        private Button tmp_exitButton;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            tmp_serverAdress.text = "http://localhost:5000";
            connectButton.onClick.RemoveAllListeners();
            disconnectButton.onClick.RemoveAllListeners();
            selectButton.onClick.RemoveAllListeners();
            tmp_exitButton.onClick.RemoveAllListeners();

            RenderData().Forget();

            GamingHubManager.Instance.Client.OnDisconnected += () => RenderData().Forget();

            connectButton.onClick.AddListener(() =>
            {
                Connect().Forget();
            });

            disconnectButton.onClick.AddListener(() =>
            {
                GamingHubManager.Instance.Client.DisposeAsync().AsUniTask().Forget();
            });

            selectButton.onClick.AddListener(() =>
            {
                if (GamingHubManager.Instance.Client?.IsConnected == true)
                {
                    MenuManager.Instance.LoadGame().Forget();
                }
            });

            tmp_exitButton.onClick.AddListener(() =>
            {
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private async UniTask Connect()
        {
            var uri = new Uri(tmp_serverAdress.text);
            await GamingHubManager.Instance.Client.ConnectAsync(GrpcChannelx.ForTarget(new GrpcChannelTarget(uri.Host, uri.Port, true)));
            RenderServerInfo();
            RenderServerStatus();
        }

        private async UniTask RenderData()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            RenderServerStatus();
            RenderServerInfo();
        }

        private void RenderServerStatus()
        {
            var status = GamingHubManager.Instance.Client.IsConnected ? "<color=green>Online</color>" : "<color=red>Offline</color>";
            tmp_status.text = $"<b>Server</b> {status}";
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