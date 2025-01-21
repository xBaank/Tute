using UnityEngine;

public class AudioController : MonoBehaviour
{
    [SerializeField]
    AudioClip flick;

    AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayFlick() => audioSource.PlayOneShot(flick);
}
