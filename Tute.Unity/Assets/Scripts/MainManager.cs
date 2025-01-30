using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;

public class MainManager : MonoBehaviour
{
    private static MainManager instance;
    public static MainManager Instance => instance;

    public event Func<string, string, UniTask<List<Player>>> OnRoomJoin;
    public event Action OnStartGame;
    public event Action<List<Player>> OnRoomSizeChanged;
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

    public async UniTask<List<Player>> ChangeRoom(string roomName, string playerName)
    {
        if (OnRoomJoin is null) return new();
        return await OnRoomJoin.Invoke(roomName, playerName);
    }

    public void StartGame() => OnStartGame?.Invoke();
    public void RoomSizeChaged(List<Player> player) => OnRoomSizeChanged?.Invoke(player);
    public void LeaveRoom() => OnLeaveRoom?.Invoke();
}
