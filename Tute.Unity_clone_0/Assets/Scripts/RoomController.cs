using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Managers;
using Cysharp.Threading.Tasks;
using TMPro;
using Tute.Shared.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public class RoomController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text tmp_room;

        [SerializeField]
        private TMP_Text tmp_name;

        [SerializeField]
        private GameObject textContainer;

        [SerializeField]
        private TMP_Text playerNamePrefab;

        [SerializeField]
        private TMP_InputField tmp_roomName;

        [SerializeField]
        private TMP_InputField tmp_playerName;

        [SerializeField]
        private Button joinButton;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button leaveButton;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private Button closeMenu;

        private readonly SemaphoreSlim semaphoreSlim = new(1);
        private readonly Dictionary<Player, TMP_Text> textsByPlayer = new();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            RenderChangedData().Forget();

            GamingHubManager.Instance.OnRoomDataUpdated += RenderChangedDataForget;
            GamingHubManager.Instance.Client.OnDisconnected += RenderChangedDataForget;

            joinButton.onClick.RemoveAllListeners();
            startButton.onClick.RemoveAllListeners();
            leaveButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();
            closeMenu.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(Join);
            startButton.onClick.AddListener(StartGame);
            leaveButton.onClick.AddListener(LeaveRoomForget);
            exitButton.onClick.AddListener(LeaveServer);
            closeMenu.onClick.AddListener(() => MenuManager.Instance.UnloadMenu(default).Forget());
        }

        private void OnDestroy()
        {
            GamingHubManager.Instance.OnRoomDataUpdated -= RenderChangedDataForget;
            GamingHubManager.Instance.Client.OnDisconnected -= RenderChangedDataForget;
            joinButton.onClick.RemoveListener(Join);
            startButton.onClick.RemoveListener(StartGame);
            leaveButton.onClick.RemoveListener(LeaveRoomForget);
            exitButton.onClick.RemoveListener(LeaveServer);
        }

        private async void Join()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                await GamingHubManager.Instance.JoinRoom(tmp_roomName.text, tmp_playerName.text);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private void RenderRoomName()
        {
            if (GamingHubManager.Instance.GameRoom is null)
            {
                tmp_room.text = string.Empty;
                tmp_name.text = string.Empty;
                return;
            }
            tmp_room.text =
                $"<b><color=grey>Room</color></b>: {GamingHubManager.Instance.GameRoom.RoomName}";
            tmp_name.text =
                $"<b><color=grey>Name</color></b>: {GamingHubManager.Instance.GameRoom.Player.Name}";
        }

        private void RenderChangedDataForget() => RenderChangedData().Forget();

        private async UniTask RenderChangedData()
        {
            await UniTask.Yield();
            UpdateMenu();
            RenderRoomName();
            RenderPlayerList();
        }

        private void LeaveServer()
        {
            GamingHubManager.Instance.Client.DisposeAsync().AsUniTask().Forget();
        }

        private void LeaveRoomForget() => LeaveRoom().Forget();

        private async UniTask LeaveRoom()
        {
            await GamingHubManager.Instance.LeaveRoom();
            tmp_room.text = string.Empty;
            tmp_name.text = string.Empty;
            UpdateMenu();
            RenderPlayerList();
        }

        private void StartGame()
        {
            GamingHubManager.Instance.Client.StartAsync().AsUniTask().Forget();
        }

        private void UpdateMenu()
        {
            if (closeMenu == null || startButton == null) return;

            var showPlayingButtons = GamingHubManager.Instance.State == GameState.Playing;
            closeMenu.gameObject.SetActive(showPlayingButtons);
            startButton.gameObject.SetActive(!showPlayingButtons && GamingHubManager.Instance.GameRoom?.Player.IsLeader == true);
        }

        private void RenderPlayerList()
        {
            if (textContainer.IsDestroyed())
                return;
            if (!textContainer.activeSelf)
                return;

            //Remove all if player is not in a room
            if (GamingHubManager.Instance.GameRoom is null)
            {
                for (var i = 0; i < textContainer.transform.childCount; i++)
                {
                    var go = textContainer.transform.GetChild(i).gameObject;
                    if (!go.IsDestroyed())
                        Destroy(textContainer.transform.GetChild(i).gameObject);
                    textsByPlayer.Clear();
                }
                return;
            }

            //Remove players that left the room
            foreach (var item in textsByPlayer.ToDictionary(i => i.Key, i => i.Value))
            {
                if (!GamingHubManager.Instance.GameRoom.Players.Contains(item.Key))
                {
                    if (!item.Value.IsDestroyed())
                        Destroy(item.Value);
                    textsByPlayer.Remove(item.Key);
                }
            }

            //Add new player in the room
            foreach (var item in GamingHubManager.Instance.GameRoom.Players)
            {
                var leaderTag = item.IsLeader ? "<b><color=orange>(Leader)</color></b>" : "";
                var text = $"- {leaderTag} {item.Name}";
                var tmpText = !textsByPlayer.ContainsKey(item)
                    ? Instantiate(playerNamePrefab, textContainer.transform)
                    : textsByPlayer[item];
                tmpText.richText = true;
                tmpText.text = text;
                tmpText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                tmpText.fontSize = 18;
                textsByPlayer[item] = tmpText;
            }
        }
    }
}
