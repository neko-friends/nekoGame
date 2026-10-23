using UnityEngine;

public class AudioController : MonoBehaviour
{
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    void PlayBGM()
        {
            audioSource.Play();
        }
    private void Update()
    {
        PlayBGM();
    }
}
