using UnityEngine;

public class AudioController : MonoBehaviour
{
    public static AudioController Instance;

    public AudioSource audioSource;

    public AudioClip jumpSound;
    public AudioClip clickSound;
    public AudioClip treeFallSound;
    public AudioClip poopSound;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayJump()
    {
        audioSource.PlayOneShot(jumpSound);
    }

    public void PlayNut()
    {
        audioSource.PlayOneShot(clickSound);
    }

    public void PlayTreeFall()
    {
        audioSource.PlayOneShot(treeFallSound);
    }

    public void PlayPoop()
    {
        audioSource.PlayOneShot(poopSound);
    }
}