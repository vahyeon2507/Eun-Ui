using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("메인 메뉴 UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;

    [Header("설정 메뉴 UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject resolutionPanel;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button resolutionTabButton;
    [SerializeField] private Slider soundSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private bool isMenuOpen = false;
    private readonly Vector2Int[] resolutions = new Vector2Int[]
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720)
    };

    void Start()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);

        restartButton.onClick.AddListener(OnRestart);
        continueButton.onClick.AddListener(OnContinue);
        quitButton.onClick.AddListener(OnQuit);
        settingsButton.onClick.AddListener(OpenSettings);

        backButton.onClick.AddListener(CloseSettings);

        audioTabButton.onClick.AddListener(() => ShowSettingsTab(true));
        resolutionTabButton.onClick.AddListener(() => ShowSettingsTab(false));

        soundSlider.onValueChanged.AddListener(OnSoundChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        // 해상도 옵션 3개로 고정
        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>
        {
            "1920x1080",
            "1600x900",
            "1280x720"
        };
        resolutionDropdown.AddOptions(options);

        soundSlider.value = 1f; // 사운드 기본값 100%
        AudioListener.volume = 1f; // 실제 볼륨도 100%

        ShowSettingsTab(true); // 기본 오디오 탭 활성화
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else
            {
                isMenuOpen = !isMenuOpen;
                menuPanel.SetActive(isMenuOpen);
                Time.timeScale = isMenuOpen ? 0 : 1;
            }
        }
    }

    void OnRestart()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnContinue()
    {
        isMenuOpen = false;
        menuPanel.SetActive(false);
        Time.timeScale = 1;
    }

    void OnQuit()
    {
        Application.Quit();
    }

    void OpenSettings()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    void CloseSettings()
    {
        settingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    void ShowSettingsTab(bool audio)
    {
        audioPanel.SetActive(audio);
        resolutionPanel.SetActive(!audio);
    }

    void OnSoundChanged(float value)
    {
        AudioListener.volume = value;
    }

    void OnResolutionChanged(int index)
    {
        if (index >= 0 && index < resolutions.Length)
        {
            var res = resolutions[index];
            Screen.SetResolution(res.x, res.y, Screen.fullScreen);
        }
    }
}