using Assets.Scripts.Services;
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
        private GamingHubClient _gamingHubClient;

        private void Start()
        {
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSubmit.AddListener(SendChatMessage);
        }

        private void OnDestroy()
        {
            if (_gamingHubClient is not null)
                _gamingHubClient.OnMessageEvent -= OnMessage;
        }

        public void SetGamingHubClient(GamingHubClient client)
        {
            if (_gamingHubClient is not null)
                _gamingHubClient.OnMessageEvent -= OnMessage;
            _gamingHubClient = client;
            _gamingHubClient.OnMessageEvent += OnMessage;
        }
        private void ClearMessages()
        {
            for (var i = 0; i < content.transform.childCount; i++)
            {
                Destroy(content.transform.GetChild(i).gameObject);
            }
        }

        private void SendChatMessage(string message) => _gamingHubClient.SendMessage(message);

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
            ClearMessages();
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}
