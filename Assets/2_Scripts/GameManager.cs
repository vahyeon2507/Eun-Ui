using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // ───────────────────────────────── UI 참조 ─────────────────────────────────
    [Header("일시정지 메뉴 UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;

    [Header("설정 메뉴 UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] settingPanels;   // 그래픽/오디오/조작/키설정 등의 패널들

    [Header("UI 애니메이션/디밍")]
    [SerializeField] private UIAnimator menuAnimator;
    [SerializeField] private UIAnimator settingsAnimator;
    [SerializeField] private UIDim dim;

    [Header("설정 UI 요소")]
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    // ─────────────────────────────── 상태/상수 ───────────────────────────────
    private bool isMenuOpen = false;

    private readonly Vector2Int[] resolutions = new Vector2Int[]
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720)
    };

    private const string SOUND_VOLUME_KEY = "SoundVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string RESOLUTION_INDEX_KEY = "ResolutionIndex";
    private const string FULLSCREEN_KEY = "Fullscreen";

    // ─────────────────────────────── Unity Hooks ───────────────────────────────
    private void Start()
    {
        InitializeUI();
        SetupButtonListeners();
        InitializeSettings();
        LoadSettings();
    }

    private void Update()
    {
        // ESC 처리
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscapeKey();
    }

    // ─────────────────────────────── 초기화 루틴 ───────────────────────────────
    private void InitializeUI()
    {
        // 누락 자동 할당 시도 및 경고 출력
        AutoAssignUIComponents();

        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void SetupButtonListeners()
    {
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart); else Debug.LogError("[GameManager] restartButton 미할당");
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue); else Debug.LogError("[GameManager] continueButton 미할당");
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit); else Debug.LogError("[GameManager] quitButton 미할당");
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings); else Debug.LogError("[GameManager] settingsButton 미할당");

        if (soundSlider != null) soundSlider.onValueChanged.AddListener(OnSoundChanged); else Debug.LogError("[GameManager] soundSlider 미할당");
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged); else Debug.LogError("[GameManager] musicSlider 미할당");
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXChanged); else Debug.LogError("[GameManager] sfxSlider 미할당");
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged); else Debug.LogError("[GameManager] resolutionDropdown 미할당");
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged); else Debug.LogError("[GameManager] fullscreenToggle 미할당");
    }

    private void InitializeSettings()
    {
        // 해상도 드롭다운 옵션 구성
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            var opts = new System.Collections.Generic.List<string>
            {
                "1920x1080",
                "1600x900",
                "1280x720"
            };
            resolutionDropdown.AddOptions(opts);
        }

        // 기본값
        if (soundSlider != null) soundSlider.value = 1f;
        if (musicSlider != null) musicSlider.value = 0.7f;
        if (sfxSlider != null) sfxSlider.value = 1f;
        if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;

        // 기본적으로 첫 번째 설정 패널 열기
        if (settingPanels != null && settingPanels.Length > 0)
            OpenPanel(0);
    }

    private void LoadSettings()
    {
        // 볼륨
        if (soundSlider != null)
        {
            float v = PlayerPrefs.GetFloat(SOUND_VOLUME_KEY, 1f);
            soundSlider.value = v;
            AudioListener.volume = v; // 글로벌 볼륨(간단 버전)
        }
        if (musicSlider != null)
        {
            float v = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.7f);
            musicSlider.value = v;
        }
        if (sfxSlider != null)
        {
            float v = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            sfxSlider.value = v;
        }

        // 해상도
        if (resolutionDropdown != null)
        {
            int idx = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, 0);
            idx = Mathf.Clamp(idx, 0, resolutions.Length - 1);
            resolutionDropdown.value = idx;
            ApplyResolution(idx);
        }

        // 전체 화면
        if (fullscreenToggle != null)
        {
            bool fs = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
            fullscreenToggle.isOn = fs;
            Screen.fullScreen = fs;
        }
    }

    // ─────────────────────────────── ESC 처리 ───────────────────────────────
    private void HandleEscapeKey()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            // 설정 → 메뉴
            CloseSettings();
            return;
        }

        if (isMenuOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    // ─────────────────────────────── 메뉴 여닫기 ───────────────────────────────
    private void OpenMenu()
    {
        isMenuOpen = true;

        if (menuAnimator != null) menuAnimator.Show();
        else if (menuPanel != null) menuPanel.SetActive(true);

        if (dim != null) dim.ShowDim();
        Time.timeScale = 0;
    }

    private void CloseMenu()
    {
        isMenuOpen = false;

        if (menuAnimator != null) menuAnimator.Hide();
        else if (menuPanel != null) menuPanel.SetActive(false);

        if (dim != null) dim.HideDim();
        Time.timeScale = 1;
    }

    // ─────────────────────────────── 설정 창 ───────────────────────────────
    private void OpenSettings()
    {
        if (menuAnimator != null) menuAnimator.Hide();
        if (menuPanel != null) menuPanel.SetActive(false);

        if (settingsAnimator != null) settingsAnimator.Show();
        else if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsAnimator != null) settingsAnimator.Hide();
        else if (settingsPanel != null) settingsPanel.SetActive(false);

        if (menuAnimator != null) menuAnimator.Show();
        else if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
            CloseSettings();
    }

    // ─────────────────────────────── 버튼 콜백 ───────────────────────────────
    private void OnRestart()
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

    private void OnContinue()
    {
        CloseMenu();
    }

    private void OnQuit()
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

    // 외부에서 일시정지 토글이 필요할 때
    public void TogglePause()
    {
        if (isMenuOpen) CloseMenu();
        else OpenMenu();
    }

    public void ForceResume()
    {
        isMenuOpen = false;
        Time.timeScale = 1;
        if (menuAnimator != null) menuAnimator.Hide();
        if (dim != null) dim.HideDim();
    }

    // ─────────────────────────────── 설정 값 변경 콜백 ───────────────────────────────
    public void OpenPanel(int index)
    {
        if (settingPanels == null || index < 0 || index >= settingPanels.Length)
        {
            Debug.LogError($"[GameManager] 잘못된 패널 인덱스: {index}");
            return;
        }

        for (int i = 0; i < settingPanels.Length; i++)
            if (settingPanels[i] != null)
                settingPanels[i].SetActive(i == index);
    }

    private void OnSoundChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(SOUND_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    private void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    private void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnResolutionChanged(int index)
    {
        if (index >= 0 && index < resolutions.Length)
        {
            ApplyResolution(index);
            PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, index);
            PlayerPrefs.Save();
        }
    }

    private void ApplyResolution(int index)
    {
        try
        {
            var r = resolutions[index];
            Screen.SetResolution(r.x, r.y, Screen.fullScreen);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] 해상도 변경 실패: {e.Message}");
        }
    }

    // ─────────────────────────────── 도우미 ───────────────────────────────
    private void AutoAssignUIComponents()
    {
        // menuAnimator
        if (menuAnimator == null && menuPanel != null)
        {
            menuAnimator = menuPanel.GetComponent<UIAnimator>();
            if (menuAnimator == null) menuAnimator = menuPanel.AddComponent<UIAnimator>();
        }

        // settingsAnimator
        if (settingsAnimator == null && settingsPanel != null)
        {
            settingsAnimator = settingsPanel.GetComponent<UIAnimator>();
            if (settingsAnimator == null) settingsAnimator = settingsPanel.AddComponent<UIAnimator>();
        }

        // dim
        if (dim == null) dim = FindObjectOfType<UIDim>();

        // 필수 요소 경고
        if (menuPanel == null) Debug.LogError("[GameManager] menuPanel 미할당");
        if (settingsPanel == null) Debug.LogError("[GameManager] settingsPanel 미할당");
        if (menuAnimator == null) Debug.LogWarning("[GameManager] menuAnimator 없음(비애니메이션 모드로 동작)");
        if (settingsAnimator == null) Debug.LogWarning("[GameManager] settingsAnimator 없음(비애니메이션 모드로 동작)");
        if (dim == null) Debug.LogWarning("[GameManager] UIDim 없음(디밍 생략)");
    }
}
