using System.Collections;
using UnityEngine;

/// <summary>
/// 싱글톤 오디오 매니저 - BGM, SFX, UI 사운드 통합 관리
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;      // 배경음악용
    [SerializeField] private AudioSource sfxSource;     // 효과음용
    [SerializeField] private AudioSource uiSource;      // UI 사운드용

    [Header("BGM Clips")]
    [SerializeField] private AudioClip titleBGM;
    [SerializeField] private AudioClip gameplayBGM;
    [SerializeField] private AudioClip bossBGM;
    [SerializeField] private AudioClip phase2BGM;

    [Header("Player SFX")]
    [SerializeField] private AudioClip playerAttackSFX;
    [SerializeField] private AudioClip playerParrySFX;
    [SerializeField] private AudioClip playerDashSFX;
    [SerializeField] private AudioClip playerJumpSFX;
    [SerializeField] private AudioClip playerHurtSFX;
    [SerializeField] private AudioClip talismanFireSFX;
    [SerializeField] private AudioClip talismanImpactSFX;

    [Header("Boss SFX")]
    [SerializeField] private AudioClip bossAttackSFX;
    [SerializeField] private AudioClip bossPhase2SFX;
    [SerializeField] private AudioClip bossHurtSFX;
    [SerializeField] private AudioClip bossDeathSFX;

    [Header("UI SFX")]
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip menuOpenSFX;
    [SerializeField] private AudioClip menuCloseSFX;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.8f;

    [Header("Fade Settings")]
    [SerializeField] private float defaultFadeTime = 1f;
    [SerializeField] private float quickFadeTime = 0.3f;

    // 싱글톤 인스턴스
    public static AudioManager Instance { get; private set; }

    // 현재 재생 중인 BGM
    private AudioClip currentBGM;
    private bool isFading = false;

    void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeAudioSources()
    {
        // AudioSource가 없으면 자동 생성
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.name = "BGM Source";
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.name = "SFX Source";
        }
        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.name = "UI Source";
        }

        // BGM 설정
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume * masterVolume;

        // SFX 설정
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume * masterVolume;

        // UI 설정
        uiSource.playOnAwake = false;
        uiSource.volume = uiVolume * masterVolume;
    }

    // ===== BGM 관리 =====
    public void PlayBGM(AudioClip clip, float fadeTime = -1f)
    {
        if (clip == null) return;
        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        // 같은 BGM이 이미 재생 중이면 무시
        if (currentBGM == clip && bgmSource.isPlaying) return;

        currentBGM = clip;
        StartCoroutine(FadeBGM(clip, fadeTime));
    }

    IEnumerator FadeBGM(AudioClip newClip, float fadeTime)
    {
        isFading = true;

        // 현재 BGM 페이드 아웃
        if (bgmSource.isPlaying)
        {
            float startVolume = bgmSource.volume;
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
                yield return null;
            }
            bgmSource.Stop();
        }

        // 새 BGM 재생
        bgmSource.clip = newClip;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // 새 BGM 페이드 인
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume * masterVolume, t / fadeTime);
            yield return null;
        }
        bgmSource.volume = bgmVolume * masterVolume;
        isFading = false;
    }

    public void StopBGM(float fadeTime = -1f)
    {
        if (fadeTime < 0f) fadeTime = defaultFadeTime;
        StartCoroutine(FadeOutBGM(fadeTime));
    }

    IEnumerator FadeOutBGM(float fadeTime)
    {
        if (!bgmSource.isPlaying) yield break;

        float startVolume = bgmSource.volume;
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
            yield return null;
        }
        bgmSource.Stop();
        currentBGM = null;
    }

    // ===== SFX 재생 =====
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }

    public void PlayUISFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        uiSource.PlayOneShot(clip, volume * uiVolume * masterVolume);
    }

    // ===== 편의 메서드들 =====
    public void PlayPlayerAttack() => PlaySFX(playerAttackSFX);
    public void PlayPlayerParry() => PlaySFX(playerParrySFX);
    public void PlayPlayerDash() => PlaySFX(playerDashSFX);
    public void PlayPlayerJump() => PlaySFX(playerJumpSFX);
    public void PlayPlayerHurt() => PlaySFX(playerHurtSFX);
    public void PlayTalismanFire() => PlaySFX(talismanFireSFX);
    public void PlayTalismanImpact() => PlaySFX(talismanImpactSFX);

    public void PlayBossAttack() => PlaySFX(bossAttackSFX);
    public void PlayBossPhase2() => PlaySFX(bossPhase2SFX);
    public void PlayBossHurt() => PlaySFX(bossHurtSFX);
    public void PlayBossDeath() => PlaySFX(bossDeathSFX);

    public void PlayButtonClick() => PlayUISFX(buttonClickSFX);
    public void PlayMenuOpen() => PlayUISFX(menuOpenSFX);
    public void PlayMenuClose() => PlayUISFX(menuCloseSFX);

    // ===== BGM 전환 메서드들 =====
    public void PlayTitleBGM() => PlayBGM(titleBGM);
    public void PlayGameplayBGM() => PlayBGM(gameplayBGM);
    public void PlayBossBGM() => PlayBGM(bossBGM);
    public void PlayPhase2BGM() => PlayBGM(phase2BGM, quickFadeTime);

    // ===== 볼륨 설정 =====
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null && !isFading)
            bgmSource.volume = bgmVolume * masterVolume;
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume * masterVolume;
    }

    public void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        if (uiSource != null)
            uiSource.volume = uiVolume * masterVolume;
    }

    void UpdateAllVolumes()
    {
        if (bgmSource != null && !isFading)
            bgmSource.volume = bgmVolume * masterVolume;
        if (sfxSource != null)
            sfxSource.volume = sfxVolume * masterVolume;
        if (uiSource != null)
            uiSource.volume = uiVolume * masterVolume;
    }

    // ===== 상태 확인 =====
    public bool IsBGMPlaying() => bgmSource != null && bgmSource.isPlaying;
    public AudioClip GetCurrentBGM() => currentBGM;
    public float GetMasterVolume() => masterVolume;
    public float GetBGMVolume() => bgmVolume;
    public float GetSFXVolume() => sfxVolume;
    public float GetUIVolume() => uiVolume;

    // ===== 디버그 =====
    void OnGUI()
    {
        if (!Application.isEditor) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("AudioManager Debug");
        GUILayout.Label($"BGM: {(IsBGMPlaying() ? currentBGM.name : "None")}");
        GUILayout.Label($"Master: {masterVolume:F2}");
        GUILayout.Label($"BGM Vol: {bgmVolume:F2}");
        GUILayout.Label($"SFX Vol: {sfxVolume:F2}");
        GUILayout.EndArea();
    }
}
