using System;
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
        private bool IsMenuLoaded;
        private CancellationTokenSource CancellationTokenSource;

        private void Awake()
        {
            CreateInstance();
        }


        public async UniTaskVoid SetupMenu(Action onLoadMenu = null, Action onUnloadMenu = null)
        {
            CancellationTokenSource = new();
            await LoadMenu();
            await HandleMenu(onLoadMenu, onUnloadMenu);
        }

        private async UniTask HandleMenu(Action onLoadMenu, Action onUnloadMenu)
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                await UniTask.WaitUntil(
                    () =>
                        Keyboard.current.cKey.wasPressedThisFrame
                        && GamingHubManager.Instance.State == GameState.Playing
                );
                if (await LoadMenu())
                    onLoadMenu?.Invoke();
                await UniTask.WaitUntil(
                    () =>
                        Keyboard.current.cKey.wasPressedThisFrame
                        && GamingHubManager.Instance.State == GameState.Playing
                );
                if (await UnloadMenu())
                    onUnloadMenu?.Invoke();
            }
        }

        public async UniTask LoadGame()
        {
            await UniTask.Yield();
            IsMenuLoaded = false;
            CancellationTokenSource?.Cancel();
            await SceneManager.LoadSceneAsync("InGame", LoadSceneMode.Single);
        }

        public async UniTask<bool> LoadServerMenu()
        {
            await UniTask.Yield();
            IsMenuLoaded = false;
            CancellationTokenSource?.Cancel();
            await SceneManager.LoadSceneAsync("ServerMenu", LoadSceneMode.Single);
            return true;
        }

        public async UniTask<bool> LoadMenu()
        {
            if (IsMenuLoaded)
                return false;
            await UniTask.Yield(cancellationToken: CancellationTokenSource?.Token ?? default);
            await SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Additive);
            IsMenuLoaded = true;
            return true;
        }

        public async UniTask<bool> UnloadMenu()
        {
            if (!IsMenuLoaded)
                return false;
            await UniTask.Yield();
            await SceneManager.UnloadSceneAsync("Menu");
            IsMenuLoaded = false;
            return true;
        }
    }
}
