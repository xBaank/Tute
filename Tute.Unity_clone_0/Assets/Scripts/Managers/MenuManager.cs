using System.Threading;
using Cysharp.Threading.Tasks;
using Tute.Shared.Models;
using UnityEngine.InputSystem;
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

        public async UniTask HandleMenu(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.WaitUntil(
                    () =>
                        InputSystem.actions.FindAction("Escape").WasPressedThisFrame()
                        && GamingHubManager.Instance.State == GameState.Playing
                );

                await SwapMenu(token);
            }
        }

        public async UniTask LoadGame(CancellationToken token)
        {
            IsMenuLoaded = false;
            await SceneManager.LoadSceneAsync("InGame", LoadSceneMode.Single).WithCancellation(token);
        }

        public async UniTask<bool> LoadServerMenu(CancellationToken token)
        {
            IsMenuLoaded = false;
            await SceneManager.LoadSceneAsync("ServerMenu", LoadSceneMode.Single).WithCancellation(token);
            return true;
        }

        public async UniTask<bool> LoadMenu(CancellationToken token)
        {
            if (IsMenuLoaded)
                return false;
            await SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Additive).WithCancellation(token);
            IsMenuLoaded = true;
            return true;
        }

        public async UniTask<bool> UnloadMenu(CancellationToken token)
        {
            if (!IsMenuLoaded)
                return false;
            await SceneManager.UnloadSceneAsync("Menu").WithCancellation(token);
            IsMenuLoaded = false;
            return true;
        }

        public async UniTask<bool> SwapMenu(CancellationToken token) =>
            IsMenuLoaded == true ? await UnloadMenu(token) : await LoadMenu(token);
    }
}
