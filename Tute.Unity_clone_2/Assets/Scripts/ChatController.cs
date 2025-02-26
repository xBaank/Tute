using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Extensions;
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
            inputField.onSubmit.AddListener(SendChatMessage);
            Client.OnMessageEvent += OnMessage;
            Client.OnJoinEvent += SendJoinMessage;
            Client.OnLeaveEvent += SendLeaveMessage;
            Client.OnStartEvent += SendStartMessage;
            Client.OnFinishEvent += SendFinishMessage;
        }

        private void OnDestroy()
        {
            inputField.onSubmit.RemoveListener(SendChatMessage);
            Client.OnMessageEvent -= OnMessage;
            Client.OnJoinEvent -= SendJoinMessage;
            Client.OnLeaveEvent -= SendLeaveMessage;
            Client.OnStartEvent -= SendStartMessage;
            Client.OnFinishEvent -= SendFinishMessage;
        }

        private void SendJoinMessage(Player player) =>
            OnSystemMessage($"El jugador {player.Name} se ha unido");

        private void SendLeaveMessage(Player player) =>
            OnSystemMessage($"El jugador {player.Name} se ha ido");

        private void SendStartMessage() => OnSystemMessage("La partida ha comenzado");

        private void SendFinishMessage(List<GameDataResponse> data)
        {
            var players = data.GroupBy(i => i.TeamIndex)
                .OrderByDescending(i => i.SelectMany(i => i.GainedCards)
                .Sum(i => i.Value))
                .WithIndex();

            foreach (var (index, item) in players)
            {
                var playersNames = item.Select(i => i.PlayerData.Player.Name);
                var teamName = string.Join(" y ", playersNames);
                var total = item.SelectMany(i => i.GainedCards).Sum(i => i.Value);

                if (index == 0)
                {
                    OnSystemMessage($"El ganador es {teamName} con {total} puntos");
                }
                else
                {
                    OnSystemMessage($"{teamName} ha perdido con {total} puntos");
                }
            }
        }

        private void SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Client.SendMessage(message).AsUniTask().Forget();
        }

        public void OnMessage(string message, Player player, int teamIndex)
        {
            var tmp_text = Instantiate(chatMessagePrefab, content.transform);
            tmp_text.richText = true;
            tmp_text.text = $"{Utils.GetPlayerName(player.Name, teamIndex)} : {message}";
        }

        public void OnSystemMessage(string message)
        {
            var tmp_text = Instantiate(chatMessagePrefab, content.transform);
            tmp_text.color = Color.cyan;
            tmp_text.text = $"{message}";
        }
    }
}
