using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomController : MonoBehaviour
{
    [SerializeField] private TMP_InputField roomName;
    [SerializeField] private Button joinButton;
    public event Action<string> OnJoin;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() =>
        {
            OnJoin?.Invoke(roomName.text);
        });
    }

    public void Hide()
    {
        joinButton.onClick.RemoveAllListeners();
        joinButton.gameObject.SetActive(false);
        roomName.gameObject.SetActive(false);
    }
}
