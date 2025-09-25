using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private GameObject[] settingPanels;   // 사운드, 해상도, 그래픽, 키 설정 등 패널

    [Header("UI 애니메이션 설정")]
    [SerializeField] private UIAnimator menuAnimator;
    [SerializeField] private UIAnimator settingsAnimator;
    [SerializeField] private UIDim dim;

    [Header("Audio")]
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

        soundSlider.onValueChanged.AddListener(OnSoundChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        // 해상도 옵션 3가지 추가
        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>
        {
            "1920x1080",
            "1600x900",
            "1280x720"
        };
        resolutionDropdown.AddOptions(options);

        // AudioManager와 연동
        if (soundSlider != null)
        {
            soundSlider.value = AudioManager.Instance != null ? AudioManager.Instance.masterVolume : 1f;
        }

        // 기본적으로 첫 번째 탭 켜기 (예: 사운드 패널)
        if (settingPanels.Length > 0)
            OpenPanel(0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel.activeSelf)
            {
                settingsAnimator.Hide();   // 설정 패널 닫기
                menuAnimator.Show();       // 메뉴 다시 보이기
            }
            else if (isMenuOpen) // 이미 메뉴 열린 상태면 닫기
            {
                isMenuOpen = false;
                menuAnimator.Hide();   // menuPanel.SetActive(false) 대신
                dim.HideDim();
                Time.timeScale = 1;
                AudioManager.Instance?.OnGamePause(false);
            }
            else // 메뉴가 닫힌 상태면 열기
            {
                isMenuOpen = true;
                menuAnimator.Show();   // menuPanel.SetActive(true) 대신
                dim.ShowDim();
                Time.timeScale = 0;
                AudioManager.Instance?.OnGamePause(true);
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
        menuAnimator.Hide();   // menuPanel.SetActive(false) 대신
        dim.HideDim();
        Time.timeScale = 1;
        AudioManager.Instance?.OnGamePause(false);
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

    // Inspector에서 버튼 OnClick에 연결 사용할 함수
    public void OpenPanel(int index)
    {
        for (int i = 0; i < settingPanels.Length; i++)
        {
            settingPanels[i].SetActive(i == index);
        }
    }

    void OnSoundChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(value);
        }
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