using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BGMPlayer : MonoBehaviour
{
    [Header("BGM Settings")]
    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float volume = 0.6f;
    public bool playOnStart = true;
    public bool loop = true;
    public bool dontDestroyOnLoad = true;

    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (playOnStart && bgmClip != null)
        {
            PlayBGM(bgmClip);
        }
    }

    public void PlayBGM(AudioClip clip = null)
    {
        if (clip != null)
            bgmClip = clip;

        if (bgmClip == null)
        {
            Debug.LogWarning("[BGMPlayer] BGM 클립이 비어있어!");
            return;
        }

        _audio.clip = bgmClip;
        _audio.loop = loop;
        _audio.volume = volume;
        _audio.Play();
    }

    public void StopBGM()
    {
        if (_audio.isPlaying)
            _audio.Stop();
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        _audio.volume = volume;
    }
}
