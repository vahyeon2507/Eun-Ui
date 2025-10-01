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
    [SerializeField] public AudioClip playerAttackSFX;
    [SerializeField] public AudioClip playerParrySFX;
    [SerializeField] public AudioClip playerDashSFX;
    [SerializeField] public AudioClip playerJumpSFX;
    [SerializeField] public AudioClip playerHurtSFX;
    [SerializeField] public AudioClip talismanFireSFX;
    [SerializeField] public AudioClip talismanImpactSFX;

    [Header("Boss SFX")]
    [SerializeField] private AudioClip bossAttackSFX;
    [SerializeField] private AudioClip bossPhase2SFX;
    [SerializeField] private AudioClip bossHurtSFX;
    [SerializeField] private AudioClip bossDeathSFX;

    [Header("UI SFX")]
    [SerializeField] public AudioClip buttonClickSFX;
    [SerializeField] public AudioClip menuOpenSFX;
    [SerializeField] public AudioClip menuCloseSFX;

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
        bgmSource.priority = 64; // 낮은 우선순위로 BGM이 다른 사운드를 방해하지 않도록

        // SFX 설정 - 지연 최소화
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume * masterVolume;
        sfxSource.priority = 128; // 높은 우선순위로 즉시 재생
        sfxSource.bypassEffects = true; // 오디오 효과 우회로 지연 감소
        sfxSource.bypassListenerEffects = true; // 리스너 효과 우회
        sfxSource.bypassReverbZones = true; // 리버브 존 우회
        sfxSource.dopplerLevel = 0f; // 도플러 효과 비활성화
        sfxSource.spread = 0f; // 스프레드 최소화
        sfxSource.rolloffMode = AudioRolloffMode.Linear; // 롤오프 모드 최적화

        // UI 설정 - 지연 최소화
        uiSource.playOnAwake = false;
        uiSource.volume = uiVolume * masterVolume;
        uiSource.priority = 128; // 높은 우선순위로 즉시 재생
        uiSource.bypassEffects = true; // 오디오 효과 우회로 지연 감소
        uiSource.bypassListenerEffects = true; // 리스너 효과 우회
        uiSource.bypassReverbZones = true; // 리버브 존 우회
        uiSource.dopplerLevel = 0f; // 도플러 효과 비활성화
        uiSource.spread = 0f; // 스프레드 최소화
        uiSource.rolloffMode = AudioRolloffMode.Linear; // 롤오프 모드 최적화
    }

    // ===== BGM 관리 =====
    public void PlayBGM(AudioClip clip, float fadeTime = -1f)
    {
        if (clip == null) 
        {
            Debug.LogWarning("[AudioManager] PlayBGM: clip is null!");
            return;
        }
        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        Debug.Log($"[AudioManager] PlayBGM: {clip.name}, fadeTime: {fadeTime}");

        // 같은 BGM이 이미 재생 중이면 무시
        if (currentBGM == clip && bgmSource.isPlaying) 
        {
            Debug.Log($"[AudioManager] BGM {clip.name} is already playing, skipping.");
            return;
        }

        currentBGM = clip;
        StartCoroutine(FadeBGM(clip, fadeTime));
    }

    IEnumerator FadeBGM(AudioClip newClip, float fadeTime)
    {
        Debug.Log($"[AudioManager] FadeBGM started: {newClip.name}");
        isFading = true;

        // 현재 BGM 페이드 아웃
        if (bgmSource.isPlaying)
        {
            Debug.Log($"[AudioManager] Fading out current BGM");
            float startVolume = bgmSource.volume;
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
                yield return null;
            }
            bgmSource.Stop();
        }

        // 새 BGM 재생
        Debug.Log($"[AudioManager] Starting new BGM: {newClip.name}");
        bgmSource.clip = newClip;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // 새 BGM 페이드 인
        Debug.Log($"[AudioManager] Fading in new BGM");
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume * masterVolume, t / fadeTime);
            yield return null;
        }
        bgmSource.volume = bgmVolume * masterVolume;
        isFading = false;
        Debug.Log($"[AudioManager] BGM fade complete: {newClip.name}");
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
        if (clip == null) 
        {
            Debug.LogWarning("[AudioManager] PlaySFX: clip is null!");
            return;
        }
        if (sfxSource == null)
        {
            Debug.LogError("[AudioManager] PlaySFX: sfxSource is null!");
            return;
        }
        
        Debug.Log($"[AudioManager] PlaySFX: {clip.name}, volume: {volume}");
        sfxSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }

    public void PlayUISFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        uiSource.PlayOneShot(clip, volume * uiVolume * masterVolume);
    }

    // ===== 즉시 재생 메서드들 (지연 최소화) =====
    public void PlaySFXImmediate(AudioClip clip, float volume = 1f)
    {
        if (clip == null) 
        {
            Debug.LogWarning("[AudioManager] PlaySFXImmediate: clip is null!");
            return;
        }
        if (sfxSource == null)
        {
            Debug.LogError("[AudioManager] PlaySFXImmediate: sfxSource is null!");
            return;
        }
        
        Debug.Log($"[AudioManager] PlaySFXImmediate: {clip.name}, volume: {volume}");
        // 기존 재생 중인 사운드 중단하고 즉시 재생
        sfxSource.Stop();
        sfxSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }

    // ===== 초고속 재생 메서드 (최대한 빠른 재생) =====
    public void PlaySFXInstant(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (sfxSource == null) return;
        
        // 모든 오디오 효과 완전 우회
        sfxSource.bypassEffects = true;
        sfxSource.bypassListenerEffects = true;
        sfxSource.bypassReverbZones = true;
        
        // 즉시 재생 (Stop 없이)
        sfxSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }

    public void PlayUISFXImmediate(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        // 기존 재생 중인 UI 사운드 중단하고 즉시 재생
        uiSource.Stop();
        uiSource.PlayOneShot(clip, volume * uiVolume * masterVolume);
    }

    // ===== UI 초고속 재생 메서드 =====
    public void PlayUISFXInstant(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (uiSource == null) return;
        
        // 모든 오디오 효과 완전 우회
        uiSource.bypassEffects = true;
        uiSource.bypassListenerEffects = true;
        uiSource.bypassReverbZones = true;
        
        // 즉시 재생 (Stop 없이)
        uiSource.PlayOneShot(clip, volume * uiVolume * masterVolume);
    }

    // ===== 편의 메서드들 =====
    public void PlayPlayerAttack() 
    {
        Debug.Log($"[AudioManager] PlayPlayerAttack called. playerAttackSFX: {(playerAttackSFX != null ? playerAttackSFX.name : "NULL")}");
        PlaySFX(playerAttackSFX);
    }
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
    public void PlayTitleBGM() 
    {
        Debug.Log($"[AudioManager] PlayTitleBGM called. titleBGM: {(titleBGM != null ? titleBGM.name : "NULL")}");
        PlayBGM(titleBGM);
    }
    public void PlayGameplayBGM() 
    {
        Debug.Log($"[AudioManager] PlayGameplayBGM called. gameplayBGM: {(gameplayBGM != null ? gameplayBGM.name : "NULL")}");
        PlayBGM(gameplayBGM);
    }
    public void PlayBossBGM() 
    {
        Debug.Log($"[AudioManager] PlayBossBGM called. bossBGM: {(bossBGM != null ? bossBGM.name : "NULL")}");
        PlayBGM(bossBGM);
    }
    public void PlayPhase2BGM() 
    {
        Debug.Log($"[AudioManager] PlayPhase2BGM called. phase2BGM: {(phase2BGM != null ? phase2BGM.name : "NULL")}");
        PlayBGM(phase2BGM, quickFadeTime);
    }

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
    public void DebugBGMStatus()
    {
        Debug.Log("=== AudioManager BGM Status ===");
        Debug.Log($"AudioManager Instance: {(Instance != null ? "EXISTS" : "NULL")}");
        Debug.Log($"bgmSource: {(bgmSource != null ? "EXISTS" : "NULL")}");
        Debug.Log($"bgmSource.isPlaying: {(bgmSource != null ? bgmSource.isPlaying.ToString() : "N/A")}");
        Debug.Log($"currentBGM: {(currentBGM != null ? currentBGM.name : "NULL")}");
        Debug.Log($"isFading: {isFading}");
        Debug.Log($"masterVolume: {masterVolume}");
        Debug.Log($"bgmVolume: {bgmVolume}");
        Debug.Log($"bgmSource.volume: {(bgmSource != null ? bgmSource.volume.ToString() : "N/A")}");
        Debug.Log($"titleBGM: {(titleBGM != null ? titleBGM.name : "NULL")}");
        Debug.Log($"gameplayBGM: {(gameplayBGM != null ? gameplayBGM.name : "NULL")}");
        Debug.Log($"bossBGM: {(bossBGM != null ? bossBGM.name : "NULL")}");
        Debug.Log($"phase2BGM: {(phase2BGM != null ? phase2BGM.name : "NULL")}");
        Debug.Log("===============================");
    }

    public void DebugSFXStatus()
    {
        Debug.Log("=== AudioManager SFX Status ===");
        Debug.Log($"AudioManager Instance: {(Instance != null ? "EXISTS" : "NULL")}");
        Debug.Log($"sfxSource: {(sfxSource != null ? "EXISTS" : "NULL")}");
        Debug.Log($"sfxSource.isPlaying: {(sfxSource != null ? sfxSource.isPlaying.ToString() : "N/A")}");
        Debug.Log($"sfxSource.volume: {(sfxSource != null ? sfxSource.volume.ToString() : "N/A")}");
        Debug.Log($"masterVolume: {masterVolume}");
        Debug.Log($"sfxVolume: {sfxVolume}");
        Debug.Log($"playerAttackSFX: {(playerAttackSFX != null ? playerAttackSFX.name : "NULL")}");
        Debug.Log($"playerParrySFX: {(playerParrySFX != null ? playerParrySFX.name : "NULL")}");
        Debug.Log($"playerDashSFX: {(playerDashSFX != null ? playerDashSFX.name : "NULL")}");
        Debug.Log($"playerJumpSFX: {(playerJumpSFX != null ? playerJumpSFX.name : "NULL")}");
        Debug.Log($"playerHurtSFX: {(playerHurtSFX != null ? playerHurtSFX.name : "NULL")}");
        Debug.Log("===============================");
    }

    void OnGUI()
    {
        if (!Application.isEditor) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("AudioManager Debug");
        GUILayout.Label($"BGM: {(IsBGMPlaying() ? currentBGM.name : "None")}");
        GUILayout.Label($"Master: {masterVolume:F2}");
        GUILayout.Label($"BGM Vol: {bgmVolume:F2}");
        GUILayout.Label($"SFX Vol: {sfxVolume:F2}");
        GUILayout.Label($"isFading: {isFading}");
        if (GUILayout.Button("Debug BGM Status"))
        {
            DebugBGMStatus();
        }
        if (GUILayout.Button("Debug SFX Status"))
        {
            DebugSFXStatus();
        }
        GUILayout.EndArea();
    }
}
