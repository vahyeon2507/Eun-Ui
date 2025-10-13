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
    [SerializeField] private Button settingsButton;

    [Header("설정 메뉴 UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] settingPanels;   // 그래픽, 오디오, 조작, 키 설정 등 패널들

    [Header("UI 애니메이션 컴포넌트")]
    [SerializeField] private UIAnimator menuAnimator;
    [SerializeField] private UIAnimator settingsAnimator;
    [SerializeField] private UIDim dim;

    [Header("설정 UI 요소")]
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private bool isMenuOpen = false;

    private readonly Vector2Int[] resolutions =
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720)
    };

    // 설정 저장 키
    private const string SOUND_VOLUME_KEY      = "SoundVolume";
    private const string MUSIC_VOLUME_KEY      = "MusicVolume";
    private const string SFX_VOLUME_KEY        = "SFXVolume";
    private const string RESOLUTION_INDEX_KEY  = "ResolutionIndex";
    private const string FULLSCREEN_KEY        = "Fullscreen";

    void Start()
    {
        InitializeUI();
        SetupButtonListeners();
        InitializeSettings();
        LoadSettings();

        if (menuPanel)     menuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    #region Init & Wiring
    void InitializeUI()
    {
        // 누락 경고
        if (!menuPanel)       Debug.LogError("[GameManager] menuPanel이 할당되지 않았습니다!");
        if (!settingsPanel)   Debug.LogError("[GameManager] settingsPanel이 할당되지 않았습니다!");
        if (!menuAnimator)    Debug.LogWarning("[GameManager] menuAnimator가 비어있습니다. 자동 연결을 시도합니다.");
        if (!settingsAnimator)Debug.LogWarning("[GameManager] settingsAnimator가 비어있습니다. 자동 연결을 시도합니다.");
        if (!dim)             Debug.LogWarning("[GameManager] dim이 비어있습니다. 자동 연결을 시도합니다.");

        AutoAssignUIComponents();
    }

    void SetupButtonListeners()
    {
        if (restartButton)      restartButton.onClick.AddListener(OnRestart);
        else Debug.LogError("[GameManager] restartButton이 할당되지 않았습니다!");

        if (continueButton)     continueButton.onClick.AddListener(OnContinue);
        else Debug.LogError("[GameManager] continueButton이 할당되지 않았습니다!");

        if (quitButton)         quitButton.onClick.AddListener(OnQuit);
        else Debug.LogError("[GameManager] quitButton이 할당되지 않았습니다!");

        if (settingsButton)     settingsButton.onClick.AddListener(OpenSettings);
        else Debug.LogError("[GameManager] settingsButton이 할당되지 않았습니다!");

        if (soundSlider)        soundSlider.onValueChanged.AddListener(OnSoundChanged);
        else Debug.LogError("[GameManager] soundSlider가 할당되지 않았습니다!");

        if (musicSlider)        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        else Debug.LogError("[GameManager] musicSlider가 할당되지 않았습니다!");

        if (sfxSlider)          sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        else Debug.LogError("[GameManager] sfxSlider가 할당되지 않았습니다!");

        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        else Debug.LogError("[GameManager] resolutionDropdown이 할당되지 않았습니다!");

        if (fullscreenToggle)   fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        else Debug.LogError("[GameManager] fullscreenToggle이 할당되지 않았습니다!");
    }

    void InitializeSettings()
    {
        // 해상도 옵션
        if (resolutionDropdown)
        {
            resolutionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>
            {
                "1920x1080", "1600x900", "1280x720"
            };
            resolutionDropdown.AddOptions(options);
        }

        // 기본값
        if (soundSlider)     soundSlider.value = 1f;
        if (musicSlider)     musicSlider.value = 0.7f;
        if (sfxSlider)       sfxSlider.value = 1f;
        if (fullscreenToggle) fullscreenToggle.isOn = Screen.fullScreen;
        AudioListener.volume = 1f;

        // 기본 패널
        if (settingPanels != null && settingPanels.Length > 0)
            OpenPanel(0);
    }

    void LoadSettings()
    {
        if (soundSlider)
        {
            float v = PlayerPrefs.GetFloat(SOUND_VOLUME_KEY, 1f);
            soundSlider.value = v;
            AudioListener.volume = v; // 폴백
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(v);
        }

        if (musicSlider)
        {
            float v = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.7f);
            musicSlider.value = v;
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v);
        }

        if (sfxSlider)
        {
            float v = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            sfxSlider.value = v;
            if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(v);
        }

        if (resolutionDropdown)
        {
            int idx = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, 0);
            if (idx >= 0 && idx < resolutions.Length)
            {
                resolutionDropdown.value = idx;
                ApplyResolution(idx);
            }
        }

        if (fullscreenToggle)
        {
            bool fs = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
            fullscreenToggle.isOn = fs;
            Screen.fullScreen = fs;
        }
    }
    #endregion

    void Update()
    {
        HandleEscapeKeyInput();
    }

    #region Menu Flow
    void HandleEscapeKeyInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (settingsPanel && settingsPanel.activeSelf)
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

    void OpenMenu()
    {
        isMenuOpen = true;
        if (menuPanel) menuPanel.SetActive(true);
        menuAnimator?.Show();
        dim?.ShowDim();
        Time.timeScale = 0;
    }

    void CloseMenu()
    {
        isMenuOpen = false;
        menuAnimator?.Hide();
        dim?.HideDim();
        if (menuPanel) menuPanel.SetActive(false);
        Time.timeScale = 1;
    }

    void OpenSettings()
    {
        if (menuPanel)     menuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
        settingsAnimator?.Show();
    }

    void CloseSettings()
    {
        settingsAnimator?.Hide();
        if (settingsPanel) settingsPanel.SetActive(false);
        if (menuPanel)     menuPanel.SetActive(true);
    }
    #endregion

    #region Buttons
    void OnRestart()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();

        Time.timeScale = 1;
        try
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] 씬 재시작 실패: {e.Message}");
        }
    }

    void OnContinue()
    {
        CloseMenu();
    }

    void OnQuit()
    {
        try { Application.Quit(); }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] 게임 종료 실패: {e.Message}");
        }
    }
    #endregion

    #region Public helpers (UI에서 직접 호출용)
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

    public void TogglePause()
    {
        if (isMenuOpen) OnContinue();
        else OpenMenu();
    }

    public void ForceResume()
    {
        isMenuOpen = false;
        Time.timeScale = 1;
        menuAnimator?.Hide();
        dim?.HideDim();
        if (menuPanel) menuPanel.SetActive(false);
    }
    #endregion

    #region Settings callbacks
    void OnSoundChanged(float value)
    {
        // AudioManager 우선, 없으면 폴백 + 저장
        if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(SOUND_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnMusicChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnSFXChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(value);
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
        if (index >= 0 && index < resolutions.Length)
        {
            ApplyResolution(index);
            PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, index);
            PlayerPrefs.Save();
        }
    }

    void ApplyResolution(int index)
    {
        try
        {
            var res = resolutions[index];
            Screen.SetResolution(res.x, res.y, Screen.fullScreen);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] 해상도 변경 실패: {e.Message}");
        }
    }
    #endregion

    #region Auto wiring
    void AutoAssignUIComponents()
    {
        // menuAnimator
        if (!menuAnimator && menuPanel)
        {
            menuAnimator = menuPanel.GetComponent<UIAnimator>();
            if (!menuAnimator) menuAnimator = menuPanel.AddComponent<UIAnimator>();
        }

        // settingsAnimator
        if (!settingsAnimator && settingsPanel)
        {
            settingsAnimator = settingsPanel.GetComponent<UIAnimator>();
            if (!settingsAnimator) settingsAnimator = settingsPanel.AddComponent<UIAnimator>();
        }

        if (!dim)
        {
            dim = FindObjectOfType<UIDim>();
            if (!dim) Debug.LogWarning("[GameManager] UIDim을 찾지 못했습니다.");
        }
    }
    #endregion
}
