using System.Threading;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Managers
{
    public class MenuManager : SingletonBase<MenuManager>
    {
        private bool IsRoomMenuLoaded = false;
        private bool IsServerMenuLoaded = true;
        private readonly SemaphoreSlim _semaphore = new(1);

        private void Awake()
        {
            CreateInstance();
        }

        public async UniTask HandleMenu(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.WaitUntil(
                    () =>
                        InputSystem.actions.FindAction("Escape").WasPressedThisFrame()
                        && GamingHubManager.Instance.State == GameState.Playing
                , cancellationToken: token);

                await SwapMenu(token);
            }
        }

        public async UniTask LoadGame(CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                IsRoomMenuLoaded = false;
                IsServerMenuLoaded = false;
                await SceneManager
                    .LoadSceneAsync("InGame", LoadSceneMode.Single)
                    .WithCancellation(token);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<bool> LoadServerMenu(CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                if (IsServerMenuLoaded)
                    return false;

                IsRoomMenuLoaded = false;
                IsServerMenuLoaded = true;
                await SceneManager
                    .LoadSceneAsync("ServerMenu", LoadSceneMode.Single)
                    .WithCancellation(token);
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<bool> LoadRoomMenu(CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                if (IsRoomMenuLoaded)
                    return false;

                await SceneManager
                    .LoadSceneAsync("Menu", LoadSceneMode.Additive)
                    .WithCancellation(token);
                IsRoomMenuLoaded = true;
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<bool> UnloadRoomMenu(CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                if (!IsRoomMenuLoaded)
                    return false;

                await SceneManager.UnloadSceneAsync("Menu").WithCancellation(token);
                IsRoomMenuLoaded = false;
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask<bool> SwapMenu(CancellationToken token) =>
            IsRoomMenuLoaded == true ? await UnloadRoomMenu(token) : await LoadRoomMenu(token);
    }
}
