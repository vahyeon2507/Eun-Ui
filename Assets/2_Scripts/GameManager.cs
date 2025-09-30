using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("���� �޴� UI")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;

    [Header("���� �޴� UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] settingPanels;   // �����, �ػ�, �׷���, Ű ���� �� �гε�

    [Header("UI ���� ����")]
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

        // �ػ� �ɼ� 3���� ����
        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>
        {
            "1920x1080",
            "1600x900",
            "1280x720"
        };
        resolutionDropdown.AddOptions(options);

        soundSlider.value = 1f; // ���� �⺻�� 100%
        AudioListener.volume = 1f; // ���� ������ 100%

        // �⺻���� ù ��° �� �ѱ� (��: ����� �г�)
        if (settingPanels.Length > 0)
            OpenPanel(0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel.activeSelf)
            {
                settingsAnimator.Hide();   // ���� �г� �ݱ�
                menuAnimator.Show();       // �޴� �ٽ� ����
            }
            else if (isMenuOpen) // �̹� �޴� ���� ������ �ݱ�
            {
                isMenuOpen = false;
                menuAnimator.Hide();   // menuPanel.SetActive(false) ���
                dim.HideDim();
                Time.timeScale = 1;
            }
            else // �޴��� ���� ������ ����
            {
                isMenuOpen = true;
                menuAnimator.Show();   // menuPanel.SetActive(true) ���
                dim.ShowDim();
                Time.timeScale = 0;
            }
        }

    }

    void OnRestart()
    {
        // 버튼 클릭 사운드
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
            
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnContinue()
    {
        isMenuOpen = false;
        menuAnimator.Hide();   // menuPanel.SetActive(false) ���
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

    // Inspector���� ��ư OnClick�� ���� ������ �Լ�
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
        else
        {
            AudioListener.volume = value; // 폴백
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
