using System;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Managers
{
    public class MenuManager : SingletonBase<MenuManager>
    {
        private bool IsMenuLoaded;

        private void Awake()
        {
            CreateInstance();
        }

        public async UniTaskVoid SetupMenu(
            Action onLoadMenu = null,
            Action onUnloadMenu = null)
        {
            await LoadMenu();
            await HandleMenu(onLoadMenu, onUnloadMenu);
        }

        private async UniTask HandleMenu(
            Action onLoadMenu,
            Action onUnloadMenu
            )
        {
            while (!destroyCancellationToken.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Escape) && GamingHubManager.Instance.State == GameState.Playing);
                if (await LoadMenu()) onLoadMenu?.Invoke();
                await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Escape) && GamingHubManager.Instance.State == GameState.Playing);
                if (await UnloadMenu()) onUnloadMenu?.Invoke();
            }
        }

        public async UniTask<bool> LoadMenu()
        {
            if (IsMenuLoaded) return false;
            await SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Additive);
            IsMenuLoaded = true;
            return true;
        }

        public async UniTask<bool> UnloadMenu()
        {
            if (!IsMenuLoaded) return false;
            await SceneManager.UnloadSceneAsync("Menu");
            IsMenuLoaded = false;
            return true;
        }
    }
}
