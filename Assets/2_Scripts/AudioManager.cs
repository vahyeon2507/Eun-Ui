using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    
    [Header("BGM Settings")]
    [SerializeField] private AudioClip[] bgmTracks;
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private float defaultVolume = 0.7f;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    
    private Dictionary<string, AudioClip> bgmDictionary;
    private Coroutine fadeCoroutine;
    
    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeAudioManager()
    {
        // AudioSource 초기화
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
        
        // BGM 딕셔너리 초기화
        bgmDictionary = new Dictionary<string, AudioClip>();
        if (bgmTracks != null)
        {
            foreach (var track in bgmTracks)
            {
                if (track != null)
                    bgmDictionary[track.name] = track;
            }
        }
        
        // 볼륨 설정
        UpdateVolumes();
    }
    
    // BGM 재생 메서드들
    public void PlayBGM(string trackName, bool fadeIn = true)
    {
        if (bgmDictionary.ContainsKey(trackName))
        {
            PlayBGM(bgmDictionary[trackName], fadeIn);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] BGM track '{trackName}' not found!");
        }
    }
    
    public void PlayBGM(AudioClip clip, bool fadeIn = true)
    {
        if (clip == null) return;
        
        // 현재 재생 중인 BGM과 같으면 무시
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        
        // 페이드 아웃 후 새 BGM 재생
        if (fadeIn && bgmSource.isPlaying)
        {
            StartCoroutine(FadeOutAndPlay(clip));
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.Play();
        }
    }
    
    public void StopBGM(bool fadeOut = true)
    {
        if (fadeOut && bgmSource.isPlaying)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            bgmSource.Stop();
        }
    }
    
    public void PauseBGM()
    {
        bgmSource.Pause();
    }
    
    public void ResumeBGM()
    {
        bgmSource.UnPause();
    }
    
    // 페이드 시스템
    private IEnumerator FadeOutAndPlay(AudioClip newClip)
    {
        // 기존 페이드 코루틴 중지
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        
        // 페이드 아웃
        yield return StartCoroutine(FadeOut());
        
        // 새 BGM 재생
        bgmSource.clip = newClip;
        bgmSource.volume = 0f;
        bgmSource.Play();
        
        // 페이드 인
        yield return StartCoroutine(FadeIn());
    }
    
    private IEnumerator FadeOut()
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
            yield return null;
        }
        
        bgmSource.volume = 0f;
        bgmSource.Stop();
    }
    
    private IEnumerator FadeIn()
    {
        float targetVolume = bgmVolume * masterVolume;
        float elapsed = 0f;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeTime);
            yield return null;
        }
        
        bgmSource.volume = targetVolume;
    }
    
    // 볼륨 관리
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    private void UpdateVolumes()
    {
        if (bgmSource != null)
            bgmSource.volume = bgmVolume * masterVolume;
        
        if (sfxSource != null)
            sfxSource.volume = sfxVolume * masterVolume;
    }
    
    // SFX 재생
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }
    
    // 게임 일시정지 처리
    public void OnGamePause(bool isPaused)
    {
        if (isPaused)
            PauseBGM();
        else
            ResumeBGM();
    }
    
    // 현재 재생 중인 BGM 정보
    public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;
    public string CurrentBGMName => bgmSource != null && bgmSource.clip != null ? bgmSource.clip.name : "";
    
    // BGM 트랙 추가 (런타임에서)
    public void AddBGMTrack(AudioClip clip)
    {
        if (clip != null && !bgmDictionary.ContainsKey(clip.name))
        {
            bgmDictionary[clip.name] = clip;
        }
    }
    
    // BGM 트랙 제거
    public void RemoveBGMTrack(string trackName)
    {
        if (bgmDictionary.ContainsKey(trackName))
        {
            bgmDictionary.Remove(trackName);
        }
    }
    
    // 사용 가능한 BGM 트랙 목록
    public string[] GetAvailableBGMTracks()
    {
        string[] tracks = new string[bgmDictionary.Count];
        bgmDictionary.Keys.CopyTo(tracks, 0);
        return tracks;
    }
}
