using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Mobile
{
    [RequireComponent(typeof(Button))]
    public class MobileButtonDelay : MonoBehaviour
    {
        private Button _button;

        private void Start()
        {
            if (!Application.isMobilePlatform)
            {
                Destroy(this);
                return;
            }

            _button = GetComponent<Button>();
            ConfigureDelay().Forget();
        }

        private async UniTaskVoid ConfigureDelay()
        {
            await UniTask.Yield(_button.GetCancellationTokenOnDestroy());

            await _button.OnClickAsAsyncEnumerable().ForEachAwaitAsync(async (i) =>
             {
                 await UniTask.Yield(_button.GetCancellationTokenOnDestroy());
                 Debug.Log("Button clicked");
                 _button.interactable = false;
                 await UniTask.WaitForSeconds(0.3f, cancellationToken: _button.GetCancellationTokenOnDestroy());
                 _button.interactable = true;
             });
        }
    }
}