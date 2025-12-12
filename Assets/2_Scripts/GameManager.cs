using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("일시정지 메뉴 UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button keysettingsButton;

    [Header("설정 메뉴 UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] settingPanels; // 그래픽/오디오/키설정 등 각 패널

    [Header("UI 애니메이션 컴포넌트(선택)")]
    [SerializeField] private UIAnimator menuAnimator;
    [SerializeField] private UIAnimator settingsAnimator;
    [SerializeField] private UIDim dim;

    [Header("설정 UI 요소")]
    [SerializeField] private Slider soundSlider;      // 마스터
    [SerializeField] private Slider musicSlider;      // BGM (저장만, 실제 적용은 프로젝트별 오디오매니저가 처리)
    [SerializeField] private Slider sfxSlider;        // SFX (저장만)
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TMP_Dropdown inputPresetDropdown;

    private PlayerController playerController;
    private bool isMenuOpen = false;

    // 지원 해상도 목록
    private readonly Vector2Int[] resolutions = new Vector2Int[]
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720),
    };

    // PlayerPrefs 키
    private const string SOUND_VOLUME_KEY = "SoundVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string RESOLUTION_INDEX_KEY = "ResolutionIndex";
    private const string FULLSCREEN_KEY = "Fullscreen";

    void Start()
    {
        playerController = Object.FindFirstObjectByType<PlayerController>();
        InitializeUI();
        SetupButtonListeners();
        InitializeSettings();
        LoadSettings();
        InitializeInputPresetDropdown();
    }

    // ---------- 초기화 ----------
    void InitializeUI()
    {
        if (menuPanel == null) Debug.LogError("[GameManager] menuPanel 미할당");
        if (settingsPanel == null) Debug.LogError("[GameManager] settingsPanel 미할당");

        // 시작 상태는 모두 닫힘
        if (menuPanel) menuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    void SetupButtonListeners()
    {
        if (restartButton) restartButton.onClick.AddListener(OnRestart);
        else Debug.LogWarning("[GameManager] restartButton 미할당");

        if (continueButton) continueButton.onClick.AddListener(OnContinue);
        else Debug.LogWarning("[GameManager] continueButton 미할당");

        if (quitButton) quitButton.onClick.AddListener(OnQuit);
        else Debug.LogWarning("[GameManager] quitButton 미할당");

        if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
        else Debug.LogWarning("[GameManager] settingsButton 미할당");

        if (soundSlider) soundSlider.onValueChanged.AddListener(OnSoundChanged);
        else Debug.LogWarning("[GameManager] soundSlider 미할당");

        if (musicSlider) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        else Debug.LogWarning("[GameManager] musicSlider 미할당");

        if (sfxSlider) sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        else Debug.LogWarning("[GameManager] sfxSlider 미할당");

        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        else Debug.LogWarning("[GameManager] resolutionDropdown 미할당");

        if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        else Debug.LogWarning("[GameManager] fullscreenToggle 미할당");
    }

    void InitializeSettings()
    {
        // 해상도 드롭다운 채우기
        if (resolutionDropdown)
        {
            resolutionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>
            {
                "1920x1080",
                "1600x900",
                "1280x720"
            };
            resolutionDropdown.AddOptions(options);
        }

        // 기본값
        if (soundSlider) soundSlider.value = 1f;
        if (musicSlider) musicSlider.value = 0.7f;
        if (sfxSlider) sfxSlider.value = 1f;

        if (fullscreenToggle) fullscreenToggle.isOn = Screen.fullScreen;

        // 첫 설정 탭
        if (settingPanels != null && settingPanels.Length > 0)
            OpenPanel(0);

        // 기본 마스터 볼륨(폴백)
        AudioListener.volume = soundSlider ? soundSlider.value : 1f;
    }
    void InitializeInputPresetDropdown()
    {
        if (inputPresetDropdown == null) return;

        inputPresetDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string> { "프리셋 1 (←/→, X, C, LShif)", "프리셋 2 (tA/D, Space, 마우스, LCtrl, LShift)" };
        inputPresetDropdown.AddOptions(options);

        // 저장된 값 불러오기(없으면 0)
        int savedPreset = PlayerPrefs.GetInt("InputPresetIndex", 0);
        inputPresetDropdown.value = savedPreset;

        inputPresetDropdown.onValueChanged.AddListener(OnInputPresetChanged);

        // 시작 시 적용
        ApplyInputPresetToPlayer(savedPreset);
    }
    void OnInputPresetChanged(int index)
    {
        ApplyInputPresetToPlayer(index);
        PlayerPrefs.SetInt("InputPresetIndex", index);
        PlayerPrefs.Save();
    }

    void ApplyInputPresetToPlayer(int index)
    {
        if (playerController == null) return;
        var preset = (PlayerController.InputPreset)index;
        playerController.inputPreset = preset;
        playerController.ApplyInputPreset(preset);
    }

    void LoadSettings()
    {
        if (soundSlider)
        {
            float v = PlayerPrefs.GetFloat(SOUND_VOLUME_KEY, soundSlider.value);
            soundSlider.value = v;
            AudioListener.volume = v; // 폴백
        }

        if (musicSlider)
            musicSlider.value = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, musicSlider.value);

        if (sfxSlider)
            sfxSlider.value = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, sfxSlider.value);

        if (fullscreenToggle)
        {
            bool fs = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;
            fullscreenToggle.isOn = fs;
            Screen.fullScreen = fs;
        }

        if (resolutionDropdown)
        {
            int idx = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, 0);
            idx = Mathf.Clamp(idx, 0, resolutions.Length - 1);
            resolutionDropdown.value = idx;
            ApplyResolution(idx);
        }
    }

    // ---------- 루프 ----------
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else if (isMenuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }
    }

    // ---------- 메뉴 / 설정 ----------
    void OpenMenu()
    {
        isMenuOpen = true;
        if (menuAnimator) menuAnimator.Show(); else if (menuPanel) menuPanel.SetActive(true);
        if (dim) dim.ShowDim();
        Time.timeScale = 0f;
    }

    void CloseMenu()
    {
        isMenuOpen = false;
        if (menuAnimator) menuAnimator.Hide(); else if (menuPanel) menuPanel.SetActive(false);
        if (dim) dim.HideDim();
        Time.timeScale = 1f;
    }

    void OpenSettings()
    {
        if (settingsAnimator) settingsAnimator.Show(); else if (settingsPanel) settingsPanel.SetActive(true);
        if (menuAnimator) menuAnimator.Hide(); else if (menuPanel) menuPanel.SetActive(false);
    }

    void CloseSettings()
    {
        if (settingsAnimator) settingsAnimator.Hide(); else if (settingsPanel) settingsPanel.SetActive(false);
        if (menuAnimator) menuAnimator.Show(); else if (menuPanel) menuPanel.SetActive(true);
    }

    // ---------- 버튼 핸들러 ----------
    void OnRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnContinue() => CloseMenu();

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- 설정 로직 ----------
    public void OpenPanel(int index)
    {
        if (settingPanels == null || index < 0 || index >= settingPanels.Length)
        {
            Debug.LogError($"[GameManager] 잘못된 패널 인덱스: {index}");
            return;
        }

        for (int i = 0; i < settingPanels.Length; i++)
            if (settingPanels[i]) settingPanels[i].SetActive(i == index);
    }

    void OnSoundChanged(float value)
    {
        // 폴백: 프로젝트 전역 오디오 매니저가 없어도 마스터 볼륨을 AudioListener로 반영
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(SOUND_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnMusicChanged(float value)
    {
        // 값만 저장 (실제 적용은 프로젝트 오디오 매니저에서 읽어 사용)
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnSFXChanged(float value)
    {
        // 값만 저장
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutions.Length) return;
        ApplyResolution(index);
        PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, index);
        PlayerPrefs.Save();
    }

    void ApplyResolution(int index)
    {
        var r = resolutions[index];
        Screen.SetResolution(r.x, r.y, Screen.fullScreen);
    }

    // ---------- 외부에서 호출 가능 ----------
    public void TogglePause()
    {
        if (isMenuOpen) CloseMenu();
        else OpenMenu();
    }

    public void ForceResume()
    {
        isMenuOpen = false;
        Time.timeScale = 1f;
        if (menuAnimator) menuAnimator.Hide(); else if (menuPanel) menuPanel.SetActive(false);
        if (dim) dim.HideDim();
    }
    // === Compatibility helpers for old UI scripts ===
    public void BackToMenu()
    {
        // 예전 UI에서 호출하던 이름: 설정 화면 -> 메뉴로
        CloseSettings();
    }

    public void OpenGameplayTab()  // 만약 UIButtonFixer에서 OpenPanel(3) 같은 하드코드 대신 이걸 쓰고 싶다면
    {
        OpenPanel(3);
    }

}
