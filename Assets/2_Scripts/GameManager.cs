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

    private readonly Vector2Int[] resolutions = new Vector2Int[]
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720)
    };

    // 설정 저장 키
    private const string SOUND_VOLUME_KEY = "SoundVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string RESOLUTION_INDEX_KEY = "ResolutionIndex";
    private const string FULLSCREEN_KEY = "Fullscreen";

    void Start()
    {
        InitializeUI();
        SetupButtonListeners();
        InitializeSettings();
        LoadSettings();
    }

    void InitializeUI()
    {
        // Null 체크
        if (menuPanel == null) Debug.LogError("[GameManager] menuPanel이 할당되지 않았습니다!");
        if (settingsPanel == null) Debug.LogError("[GameManager] settingsPanel이 할당되지 않았습니다!");
        if (menuAnimator == null) Debug.LogError("[GameManager] menuAnimator가 할당되지 않았습니다!");
        if (settingsAnimator == null) Debug.LogError("[GameManager] settingsAnimator가 할당되지 않았습니다!");
        if (dim == null) Debug.LogError("[GameManager] dim이 할당되지 않았습니다!");

        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    void SetupButtonListeners()
    {
        // 버튼 Null 체크 및 리스너 설정
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        else Debug.LogError("[GameManager] restartButton이 할당되지 않았습니다!");

        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        else Debug.LogError("[GameManager] continueButton이 할당되지 않았습니다!");

        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        else Debug.LogError("[GameManager] quitButton이 할당되지 않았습니다!");

        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        else Debug.LogError("[GameManager] settingsButton이 할당되지 않았습니다!");

        if (soundSlider != null) soundSlider.onValueChanged.AddListener(OnSoundChanged);
        else Debug.LogError("[GameManager] soundSlider가 할당되지 않았습니다!");

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        else Debug.LogError("[GameManager] musicSlider가 할당되지 않았습니다!");

        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        else Debug.LogError("[GameManager] sfxSlider가 할당되지 않았습니다!");

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        else Debug.LogError("[GameManager] resolutionDropdown이 할당되지 않았습니다!");

        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        else Debug.LogError("[GameManager] fullscreenToggle이 할당되지 않았습니다!");
    }

    void InitializeSettings()
    {
        // 해상도 옵션 설정
        if (resolutionDropdown != null)
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

        // 기본값 설정
        if (soundSlider != null) soundSlider.value = 1f;
        if (musicSlider != null) musicSlider.value = 0.7f;
        if (sfxSlider != null) sfxSlider.value = 1f;
        if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;
        AudioListener.volume = 1f;

        // 기본적으로 첫 번째 설정 패널 열기
        if (settingPanels != null && settingPanels.Length > 0)
            OpenPanel(0);
    }

    void LoadSettings()
    {
        // 저장된 설정 로드
        if (soundSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat(SOUND_VOLUME_KEY, 1f);
            soundSlider.value = savedVolume;
            AudioListener.volume = savedVolume;
        }

        if (resolutionDropdown != null)
        {
            int savedResolution = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, 0);
            if (savedResolution >= 0 && savedResolution < resolutions.Length)
            {
                resolutionDropdown.value = savedResolution;
                ApplyResolution(savedResolution);
            }
        }

        if (musicSlider != null)
        {
            float savedMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.7f);
            musicSlider.value = savedMusicVolume;
            // AudioManager가 없어도 기본 오디오 시스템 사용
            AudioListener.volume = savedMusicVolume;
        }

        if (sfxSlider != null)
        {
            float savedSFXVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            sfxSlider.value = savedSFXVolume;
            // AudioManager가 없어도 기본 오디오 시스템 사용
            AudioListener.volume = savedSFXVolume;
        }

        if (fullscreenToggle != null)
        {
            bool savedFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
            fullscreenToggle.isOn = savedFullscreen;
            Screen.fullScreen = savedFullscreen;
        }
    }

    void Update()
    {
        HandleEscapeKey();
    }

    void HandleEscapeKey()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel.activeSelf)
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

    void OpenMenu()
    {
        isMenuOpen = true;
        menuAnimator?.Show();
        dim?.ShowDim();
        Time.timeScale = 0;
    }

    void CloseMenu()
    {
        isMenuOpen = false;
        menuAnimator?.Hide();
        dim?.HideDim();
        Time.timeScale = 1;
    }

    void OpenSettings()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    void OnRestart()
    {
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
        try
        {
            Application.Quit();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] 게임 종료 실패: {e.Message}");
        }
    }

    // Inspector에서 버튼 OnClick에 연결할 수 있는 함수
    public void OpenPanel(int index)
    {
        if (settingPanels == null || index < 0 || index >= settingPanels.Length)
        {
            Debug.LogError($"[GameManager] 잘못된 패널 인덱스: {index}");
            return;
        }

        for (int i = 0; i < settingPanels.Length; i++)
        {
            if (settingPanels[i] != null)
                settingPanels[i].SetActive(i == index);
        }
    }

    void OnSoundChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(SOUND_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnMusicChanged(float value)
    {
        // AudioManager가 없어도 기본 오디오 시스템 사용
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    void OnSFXChanged(float value)
    {
        // AudioManager가 없어도 기본 오디오 시스템 사용
        AudioListener.volume = value;
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
}