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
    [SerializeField] private GameObject[] settingPanels;   // 오디오, 해상도, 그래픽, 키 세팅 등 패널들

    [Header("UI 연출 관련")]
    [SerializeField] private UIAnimator menuAnimator;
    [SerializeField] private UIAnimator settingsAnimator;
    [SerializeField] private UIDim dim;


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

        // 기본으로 첫 번째 탭 켜기 (예: 오디오 패널)
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
                menuAnimator.Show();       // 메뉴 다시 열기
            }
            else if (isMenuOpen) // 이미 메뉴 열려 있으면 닫기
            {
                isMenuOpen = false;
                menuAnimator.Hide();   // menuPanel.SetActive(false) 대신
                dim.HideDim();
                Time.timeScale = 1;
            }
            else // 메뉴가 닫혀 있으면 열기
            {
                isMenuOpen = true;
                menuAnimator.Show();   // menuPanel.SetActive(true) 대신
                dim.ShowDim();
                Time.timeScale = 0;
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

    // Inspector에서 버튼 OnClick에 직접 연결할 함수
    public void OpenPanel(int index)
    {
        for (int i = 0; i < settingPanels.Length; i++)
        {
            settingPanels[i].SetActive(i == index);
        }
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
