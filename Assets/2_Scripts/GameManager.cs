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
        // UI 컴포넌트 자동 찾기 및 할당
        AutoAssignUIComponents();
        
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
