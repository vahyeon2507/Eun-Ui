using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("일시정지 메뉴 UI")]
    [Header("���� �޴� UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;

    [Header("설정 메뉴 UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] settingPanels;   // 그래픽, 오디오, 조작, 키 설정 등 패널들

    [Header("UI 애니메이션 컴포넌트")]
    [Header("���� �޴� UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] settingPanels;   // �����, �ػ�, �׷���, Ű ���� �� �гε�

    [Header("UI ���� ����")]
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

        // UI 컴포넌트 자동 찾기 및 할당
        AutoAssignUIComponents();
        
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
        // �ػ� �ɼ� 3���� ����
        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>
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
        soundSlider.value = 1f; // ���� �⺻�� 100%
        AudioListener.volume = 1f; // ���� ������ 100%

        // �⺻���� ù ��° �� �ѱ� (��: ����� �г�)
        if (settingPanels.Length > 0)
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
            HandleEscapeKey();
        }
    }
    
    void HandleEscapeKey()
    {
        Debug.Log($"[GameManager] ESC 키 입력 - 현재 상태: isMenuOpen={isMenuOpen}, settingsPanel.activeSelf={settingsPanel.activeSelf}");
        
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            // 설정 패널이 열려있으면 메뉴로 돌아가기
            if (settingsAnimator != null)
                settingsAnimator.Hide();
            else
                settingsPanel.SetActive(false);
                
            if (menuAnimator != null)
                menuAnimator.Show();
            else
                menuPanel.SetActive(true);
                
            Debug.Log("[GameManager] 설정 패널 → 메뉴 패널");
        }
        else if (isMenuOpen) // 이미 메뉴 열린 상태면 닫기
        {
            isMenuOpen = false;
            
            if (menuAnimator != null)
                menuAnimator.Hide();
            else
                menuPanel.SetActive(false);
                
            if (dim != null)
                dim.HideDim();
                
            Time.timeScale = 1;
            Debug.Log("[GameManager] 메뉴 닫기 - 게임 재개");
        }
        else // 메뉴가 닫힌 상태면 열기
        {
            isMenuOpen = true;
            
            if (menuAnimator != null)
                menuAnimator.Show();
            else
                menuPanel.SetActive(true);
                
            if (dim != null)
                dim.ShowDim();
                
            Time.timeScale = 0;
            Debug.Log("[GameManager] 메뉴 열기 - 게임 일시정지");
        }
        
        Debug.Log($"[GameManager] ESC 처리 완료 - Time.timeScale: {Time.timeScale}");
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
        isMenuOpen = false;
        menuAnimator.Hide();   // menuPanel.SetActive(false) ���
        dim.HideDim();
        Time.timeScale = 1;
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
    void OpenSettings()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
    
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }
    
    public void BackToMenu()
    {
        if (settingsPanel.activeSelf)
        {
            CloseSettings();
        }
    }
    
    // UI 씬에서 일시정지 기능이 작동하도록 public 메서드 추가
    public void TogglePause()
    {
        if (isMenuOpen)
        {
            // 메뉴 닫기
            isMenuOpen = false;
            if (menuAnimator != null) menuAnimator.Hide();
            if (dim != null) dim.HideDim();
            Time.timeScale = 1;
            Debug.Log("[GameManager] 게임 재개");
        }
        else
        {
            // 메뉴 열기
            isMenuOpen = true;
            if (menuAnimator != null) menuAnimator.Show();
            if (dim != null) dim.ShowDim();
            Time.timeScale = 0;
            Debug.Log("[GameManager] 게임 일시정지");
        }
    }
    
    public void ForceResume()
    {
        isMenuOpen = false;
        Time.timeScale = 1;
        if (menuAnimator != null) menuAnimator.Hide();
        if (dim != null) dim.HideDim();
        Debug.Log("[GameManager] 강제 게임 재개");
    }
    
    // UI 컴포넌트들을 자동으로 찾아서 할당하는 메서드
    void AutoAssignUIComponents()
    {
        Debug.Log("[GameManager] UI 컴포넌트 자동 할당 시작");
        
        // menuAnimator가 없으면 menuPanel에서 찾기
        if (menuAnimator == null && menuPanel != null)
        {
            menuAnimator = menuPanel.GetComponent<UIAnimator>();
            if (menuAnimator == null)
            {
                menuAnimator = menuPanel.AddComponent<UIAnimator>();
                Debug.Log("[GameManager] menuPanel에 UIAnimator 추가");
            }
            else
            {
                Debug.Log("[GameManager] menuAnimator 자동 할당 완료");
            }
        }
        
        // settingsAnimator가 없으면 settingsPanel에서 찾기
        if (settingsAnimator == null && settingsPanel != null)
        {
            settingsAnimator = settingsPanel.GetComponent<UIAnimator>();
            if (settingsAnimator == null)
            {
                settingsAnimator = settingsPanel.AddComponent<UIAnimator>();
                Debug.Log("[GameManager] settingsPanel에 UIAnimator 추가");
            }
            else
            {
                Debug.Log("[GameManager] settingsAnimator 자동 할당 완료");
            }
        }
        
        // dim이 없으면 찾기
        if (dim == null)
        {
            dim = FindObjectOfType<UIDim>();
            if (dim != null)
            {
                Debug.Log("[GameManager] UIDim 자동 할당 완료");
            }
            else
            {
                Debug.LogWarning("[GameManager] UIDim을 찾을 수 없습니다!");
            }
        }
        
        // 누락된 UI 요소들 확인
        CheckMissingUIComponents();
    }
    
    void CheckMissingUIComponents()
    {
        if (menuPanel == null) Debug.LogError("[GameManager] menuPanel이 할당되지 않았습니다!");
        if (settingsPanel == null) Debug.LogError("[GameManager] settingsPanel이 할당되지 않았습니다!");
        if (menuAnimator == null) Debug.LogWarning("[GameManager] menuAnimator가 없습니다!");
        if (settingsAnimator == null) Debug.LogWarning("[GameManager] settingsAnimator가 없습니다!");
        if (dim == null) Debug.LogWarning("[GameManager] dim이 없습니다!");
        
        Debug.Log($"[GameManager] 일시정지 기능 준비 완료: {(menuAnimator != null && dim != null ? "OK" : "문제 있음")}");
    }

    // Inspector���� ��ư OnClick�� ���� ������ �Լ�
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