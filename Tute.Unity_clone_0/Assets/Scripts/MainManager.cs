using System;
using Assets.Scripts;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MainManager : MonoBehaviour
{
    private static MainManager instance;
    public static MainManager Instance => instance;

    public GameRoom GameRoom { get; private set; }

    public event Func<string, string, UniTask<GameRoom>> OnRoomJoin;
    public event Action OnStartGame;
    public event Action OnRoomSizeChanged;
    public event Action OnLeaveRoom;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    //From menu
    public async UniTask ChangeRoom(string roomName, string playerName)
    {
        if (OnRoomJoin is null)
            return;
        GameRoom = await OnRoomJoin.Invoke(roomName, playerName);
    }

    public void StartGame() => OnStartGame?.Invoke();


    //From ingame
    public void RoomSizeChaged()
    {
        OnRoomSizeChanged?.Invoke();
    }

    public void LeaveRoom()
    {
        GameRoom = null;
        OnLeaveRoom?.Invoke();
    }
}
