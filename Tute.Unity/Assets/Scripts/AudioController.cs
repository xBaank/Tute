using UnityEngine;

namespace Assets.Scripts
{
    public class AudioController : MonoBehaviour
    {
        [SerializeField]
        private AudioClip flick;
        private AudioSource audioSource;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlayFlick() => audioSource.PlayOneShot(flick);
    }
}
