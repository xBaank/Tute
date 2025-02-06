using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomController : MonoBehaviour
{
    [SerializeField]
    private TMP_Text tmp_room;

    [SerializeField]
    private TMP_Text tmp_name;

    [SerializeField]
    private TMP_Text tmp_playernames;

    [SerializeField]
    private TMP_InputField tmp_roomName;

    [SerializeField]
    private TMP_InputField tmp_playerName;

    [SerializeField]
    private Button joinButton;

    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button exitButton;

    private readonly SemaphoreSlim semaphoreSlim = new(1);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        RenderRoomName();
        RenderPlayerList();

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
            await MainManager.Instance.ChangeRoom(tmp_roomName.text, tmp_playerName.text);
            RenderRoomName();
            RenderPlayerList();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private void RenderRoomName()
    {
        if (MainManager.Instance.GameRoom is null)
        {
            tmp_room.text = string.Empty;
            tmp_name.text = string.Empty;
            return;
        }
        tmp_room.text = $"Room: {MainManager.Instance.GameRoom.RoomName}";
        tmp_name.text = $"Name: {MainManager.Instance.GameRoom.Player.Name}";
    }

    private void RoomSizeChanged()
    {
        RenderPlayerList();
    }

    private void ExitRoom()
    {
        MainManager.Instance.LeaveRoom();
        tmp_room.text = string.Empty;
        tmp_name.text = string.Empty;
        RenderPlayerList();
    }

    private void StartGame() => MainManager.Instance.StartGame();

    private void RenderPlayerList()
    {
        if (MainManager.Instance.GameRoom is null)
        {
            tmp_playernames.text = string.Empty;
            return;
        }

        var stringBuilder = new StringBuilder();
        foreach (var item in MainManager.Instance.GameRoom.Players)
        {
            var leaderTag = item.IsLeader ? "(Leader)" : "";
            stringBuilder.AppendLine($"- {leaderTag} {item.Name}");
        }
        tmp_playernames.text = stringBuilder.ToString();
    }
}
