using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject menuPanel; // 메뉴창 Panel
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Dropdown resolutionDropdown;

    private bool isMenuOpen = false;

    void Start()
    {
        menuPanel.SetActive(false);

        restartButton.onClick.AddListener(OnRestart);
        continueButton.onClick.AddListener(OnContinue);
        quitButton.onClick.AddListener(OnQuit);

        soundSlider.onValueChanged.AddListener(OnSoundChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        // 해상도 옵션 초기화 예시
        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();
        foreach (var res in Screen.resolutions)
            options.Add(res.width + "x" + res.height);
        resolutionDropdown.AddOptions(options);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMenuOpen = !isMenuOpen;
            menuPanel.SetActive(isMenuOpen);
            Time.timeScale = isMenuOpen ? 0 : 1; // 메뉴 열릴 때 게임 일시정지
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

    void OnSoundChanged(float value)
    {
        AudioListener.volume = value;
    }

    void OnResolutionChanged(int index)
    {
        var res = Screen.resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }
}