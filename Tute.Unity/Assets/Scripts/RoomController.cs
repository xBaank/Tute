using System.Collections.Generic;
using System.Text;
using System.Threading;
using TMPro;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.UI;

public class RoomController : MonoBehaviour
{
    [SerializeField] private TMP_Text tmp_room;
    [SerializeField] private TMP_Text tmp_name;
    [SerializeField] private TMP_Text tmp_playernames;
    [SerializeField] private TMP_InputField tmp_roomName;
    [SerializeField] private TMP_InputField tmp_playerName;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    private List<Player> _players = new();
    private SemaphoreSlim semaphoreSlim = new(1);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        MainManager.Instance.OnRoomSizeChanged += RoomSizeChanged;

        joinButton.onClick.RemoveAllListeners();
        startButton.onClick.RemoveAllListeners();
        exitButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(Join);
        startButton.onClick.AddListener(StartGame);
        exitButton.onClick.AddListener(ExitRoom);
    }


    private async void Join()
    {
        await semaphoreSlim.WaitAsync();
        try
        {
            _players = await MainManager.Instance.ChangeRoom(tmp_roomName.text, tmp_playerName.text);
            tmp_room.text = $"Room: {tmp_roomName.text}";
            tmp_name.text = $"Name: {tmp_playerName.text}";
            RenderPlayerList();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private void RoomSizeChanged(List<Player> players)
    {
        _players = players;
        RenderPlayerList();
    }

    private void ExitRoom()
    {
        MainManager.Instance.LeaveRoom();
        tmp_room.text = string.Empty;
        tmp_name.text = string.Empty;
        _players.Clear();
        RenderPlayerList();
    }

    private void StartGame() => MainManager.Instance.StartGame();
    private void RenderPlayerList()
    {
        var stringBuilder = new StringBuilder();
        foreach (var item in _players)
        {
            var leaderTag = item.IsLeader ? "(Leader)" : "";
            stringBuilder.AppendLine($"- {leaderTag} {item.Name}");
        }
        tmp_playernames.text = stringBuilder.ToString();
    }
}
