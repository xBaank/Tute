using Assets.Scripts.Managers;
using Assets.Scripts.Services;
using Cysharp.Threading.Tasks;
using TMPro;
using Tute.Shared.Models;
using UnityEngine;

namespace Assets.Scripts
{
    public class ChatController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text chatMessagePrefab;

        [SerializeField]
        private GameObject content;

        [SerializeField]
        private TMP_InputField inputField;

        private GamingHubClient Client => GamingHubManager.Instance.Client;

        private void Start()
        {
            Client.OnMessageEvent += OnMessage;
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSubmit.AddListener(SendChatMessage);
        }

        private void OnDestroy()
        {
            if (Client is not null)
                Client.OnMessageEvent -= OnMessage;
        }


        private void SendChatMessage(string message) => Client.SendMessage(message).AsUniTask().Forget();

        public void OnMessage(string message, Player player)
        {
            var tmp_text = Instantiate(chatMessagePrefab, content.transform);
            tmp_text.richText = true;
            tmp_text.text = $"<color=lightblue>{player.Name}</color> : {message}";
        }

        public void OnSystemMessage(string message)
        {
            var tmp_text = Instantiate(chatMessagePrefab, content.transform);
            tmp_text.color = Color.cyan;
            tmp_text.text = $"{message}";
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}
