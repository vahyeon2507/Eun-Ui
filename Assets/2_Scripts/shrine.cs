using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class sh : MonoBehaviour
{
    [Header("플레이어 감지 설정")]
    [Tooltip("플레이어를 감지할 거리")]
    public float detectionDistance = 5f;
    
    [Header("표시할 오브젝트")]
    [Tooltip("플레이어가 근처에 오면 보이게 할 오브젝트")]
    public GameObject objectToShow;
    
    [Header("플레이어 참조 (선택사항)")]
    [Tooltip("플레이어 오브젝트를 직접 지정하지 않으면 자동으로 찾습니다")]
    public Transform playerTransform;
    
    [Header("UI 캔버스 설정")]
    [Tooltip("F키를 누르면 열릴 UI 캔버스 (GameObject 또는 Canvas)")]
    public GameObject uiCanvas;
    [Tooltip("UI를 열 때 사용할 키 (기본: F)")]
    public KeyCode openKey = KeyCode.F;
    [Tooltip("UIAnimator 컴포넌트가 있으면 자동으로 사용합니다")]
    public bool useUIAnimator = true;
    
    [Header("F키로 열릴 기본 패널")]
    [Tooltip("F키를 누르면 자동으로 열릴 패널들 (최대 2개)")]
    public GameObject[] defaultPanels = new GameObject[2];
    
    [Header("일시정지 설정")]
    [Tooltip("UI가 열릴 때 게임을 일시정지합니다")]
    public bool pauseGameOnOpen = true;
    [Tooltip("UI가 열릴 때 플레이어 입력을 비활성화합니다")]
    public bool disablePlayerInputOnOpen = true;
    
    [Header("버튼 및 패널 설정")]
    [Tooltip("버튼과 연결할 패널들 (버튼 순서대로 지정)")]
    public GameObject[] panels;
    [Tooltip("버튼들 (자동으로 찾거나 수동으로 지정)")]
    public Button[] buttons;
    [Tooltip("버튼을 자동으로 찾을지 여부 (uiCanvas의 자식에서 Button 컴포넌트 검색)")]
    public bool autoFindButtons = true;
    [Tooltip("한 번에 하나의 패널만 열리도록 할지 여부")]
    public bool singlePanelMode = true;
    
    private bool isPlayerNearby = false;
    private bool wasObjectVisible = false;
    private bool isUIOpen = false;
    private PlayerController cachedPlayerController;
    private GameObject currentOpenPanel = null;

    void Start()
    {
        // 플레이어를 자동으로 찾기
        if (playerTransform == null)
        {
            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
                cachedPlayerController = playerController;
            }
            else
            {
                // PlayerController가 없으면 "Player" 태그로 찾기
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    playerTransform = playerObj.transform;
                    cachedPlayerController = playerObj.GetComponent<PlayerController>();
                }
            }
        }
        else
        {
            // playerTransform이 직접 지정된 경우
            cachedPlayerController = playerTransform.GetComponent<PlayerController>();
        }
        
        // 표시할 오브젝트가 지정되지 않았으면 경고
        if (objectToShow == null)
        {
            Debug.LogWarning($"[sh] {gameObject.name}: 표시할 오브젝트가 지정되지 않았습니다!");
        }
        else
        {
            // 시작 시 오브젝트 숨기기
            objectToShow.SetActive(false);
            wasObjectVisible = false;
        }
        
        // UI 캔버스 초기화
        if (uiCanvas != null)
        {
            // UIAnimator가 있는지 확인
            if (useUIAnimator && uiCanvas.GetComponent<UIAnimator>() == null)
            {
                useUIAnimator = false;
            }
            
            // 시작 시 UI 숨기기
            if (useUIAnimator)
            {
                var animator = uiCanvas.GetComponent<UIAnimator>();
                if (animator != null)
                {
                    uiCanvas.SetActive(false);
                }
            }
            else
            {
                uiCanvas.SetActive(false);
            }
            isUIOpen = false;
            
            // 버튼 및 패널 초기화
            SetupButtonsAndPanels();
        }
    }

    void Update()
    {
        if (playerTransform == null || objectToShow == null)
            return;
        
        // 플레이어와의 거리 계산
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerNearby = distance <= detectionDistance;
        
        // 플레이어가 근처에 있고 오브젝트가 숨겨져 있으면 표시
        if (isPlayerNearby && !wasObjectVisible)
        {
            objectToShow.SetActive(true);
            wasObjectVisible = true;
        }
        // 플레이어가 멀어지고 오브젝트가 보이고 있으면 숨기기
        else if (!isPlayerNearby && wasObjectVisible)
        {
            objectToShow.SetActive(false);
            wasObjectVisible = false;
            
            // 오브젝트가 숨겨질 때 UI도 함께 닫기
            if (isUIOpen && uiCanvas != null)
            {
                CloseUI();
            }
        }
        
        // F키 입력 처리 (오브젝트가 보일 때만 작동)
        if (wasObjectVisible && Input.GetKeyDown(openKey))
        {
            if (uiCanvas != null)
            {
                if (isUIOpen)
                {
                    CloseUI();
                }
                else
                {
                    OpenUI();
                }
            }
        }
    }
    
    // UI 열기
    void OpenUI()
    {
        if (uiCanvas == null) return;
        
        // 게임 일시정지
        if (pauseGameOnOpen)
        {
            Time.timeScale = 0f;
        }
        
        // 플레이어 입력 비활성화
        if (disablePlayerInputOnOpen && cachedPlayerController != null)
        {
            cachedPlayerController.enabled = false;
        }
        
        if (useUIAnimator)
        {
            var animator = uiCanvas.GetComponent<UIAnimator>();
            if (animator != null)
            {
                animator.Show();
                isUIOpen = true;
                
                // 기본 패널들 열기
                OpenDefaultPanels();
                return;
            }
        }
        
        // UIAnimator가 없거나 사용하지 않는 경우
        uiCanvas.SetActive(true);
        isUIOpen = true;
        
        // 기본 패널들 열기
        OpenDefaultPanels();
    }
    
    // 기본 패널들 열기
    void OpenDefaultPanels()
    {
        if (defaultPanels == null) return;
        
        foreach (var panel in defaultPanels)
        {
            if (panel != null)
            {
                OpenPanel(panel);
            }
        }
    }
    
    // UI 닫기
    void CloseUI()
    {
        if (uiCanvas == null) return;
        
        // 열려있는 패널 모두 닫기
        if (currentOpenPanel != null)
        {
            ClosePanel(currentOpenPanel);
            currentOpenPanel = null;
        }
        
        // 모든 패널 닫기 (안전장치)
        if (panels != null)
        {
            foreach (var panel in panels)
            {
                if (panel != null && panel.activeSelf)
                {
                    panel.SetActive(false);
                }
            }
        }
        
        if (useUIAnimator)
        {
            var animator = uiCanvas.GetComponent<UIAnimator>();
            if (animator != null)
            {
                animator.Hide();
                isUIOpen = false;
                
                // 애니메이션 완료 후 게임 재개 (UIAnimator는 Time.unscaledDeltaTime 사용)
                // 애니메이션 시간 계산: 1 / animationSpeed (대략)
                float animDuration = 1f / Mathf.Max(0.1f, animator.animationSpeed);
                StartCoroutine(ResumeGameAfterDelay(animDuration));
                return;
            }
        }
        
        // UIAnimator가 없거나 사용하지 않는 경우
        uiCanvas.SetActive(false);
        isUIOpen = false;
        
        // 게임 재개
        ResumeGame();
    }
    
    // 애니메이션 완료 후 게임 재개
    IEnumerator ResumeGameAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ResumeGame();
    }
    
    // 게임 재개
    void ResumeGame()
    {
        // 게임 재개
        if (pauseGameOnOpen)
        {
            Time.timeScale = 1f;
        }
        
        // 플레이어 입력 활성화
        if (disablePlayerInputOnOpen && cachedPlayerController != null)
        {
            cachedPlayerController.enabled = true;
        }
    }
    
    // 버튼 및 패널 설정
    void SetupButtonsAndPanels()
    {
        // 버튼 자동 찾기
        if (autoFindButtons && (buttons == null || buttons.Length == 0))
        {
            if (uiCanvas != null)
            {
                buttons = uiCanvas.GetComponentsInChildren<Button>(true);
                if (buttons.Length > 0)
                {
                    Debug.Log($"[sh] {gameObject.name}: {buttons.Length}개의 버튼을 자동으로 찾았습니다.");
                }
            }
        }
        
        // 패널 초기화 (모두 숨기기)
        if (panels != null)
        {
            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }
        
        // 버튼과 패널 연결
        if (buttons != null && panels != null)
        {
            int buttonCount = Mathf.Min(buttons.Length, panels.Length);
            
            for (int i = 0; i < buttonCount; i++)
            {
                if (buttons[i] != null && panels[i] != null)
                {
                    int index = i; // 클로저를 위한 로컬 변수
                    buttons[i].onClick.RemoveAllListeners();
                    buttons[i].onClick.AddListener(() => OnButtonClicked(index));
                    Debug.Log($"[sh] 버튼 '{buttons[i].name}'을 패널 '{panels[i].name}'에 연결했습니다.");
                }
            }
            
            if (buttonCount < buttons.Length)
            {
                Debug.LogWarning($"[sh] 버튼이 {buttons.Length}개인데 패널이 {panels.Length}개입니다. 일부 버튼이 연결되지 않았습니다.");
            }
        }
        else
        {
            if (buttons == null || buttons.Length == 0)
            {
                Debug.LogWarning($"[sh] {gameObject.name}: 버튼을 찾을 수 없습니다. 수동으로 지정하거나 autoFindButtons를 체크하세요.");
            }
            if (panels == null || panels.Length == 0)
            {
                Debug.LogWarning($"[sh] {gameObject.name}: 패널이 지정되지 않았습니다.");
            }
        }
    }
    
    // 버튼 클릭 이벤트
    void OnButtonClicked(int panelIndex)
    {
        if (panels == null || panelIndex < 0 || panelIndex >= panels.Length)
        {
            Debug.LogWarning($"[sh] 잘못된 패널 인덱스: {panelIndex}");
            return;
        }
        
        GameObject targetPanel = panels[panelIndex];
        if (targetPanel == null)
        {
            Debug.LogWarning($"[sh] 패널 인덱스 {panelIndex}의 패널이 null입니다.");
            return;
        }
        
        // 패널이 이미 열려있는지 확인
        bool isTargetPanelOpen = targetPanel.activeSelf;
        
        // 단일 패널 모드: 다른 모든 패널 닫기
        if (singlePanelMode)
        {
            // 모든 패널을 순회하면서 열려있는 패널 닫기
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i] != targetPanel && panels[i].activeSelf)
                {
                    ClosePanel(panels[i]);
                }
            }
        }
        
        // 패널 토글
        if (isTargetPanelOpen)
        {
            ClosePanel(targetPanel);
        }
        else
        {
            OpenPanel(targetPanel);
        }
    }
    
    // 패널 열기
    void OpenPanel(GameObject panel)
    {
        if (panel == null) return;
        
        // UIAnimator가 있으면 사용 (Show()가 SetActive를 처리함)
        var animator = panel.GetComponent<UIAnimator>();
        if (animator != null)
        {
            animator.Show();
        }
        else
        {
            panel.SetActive(true);
        }
        
        currentOpenPanel = panel;
    }
    
    // 패널 닫기
    void ClosePanel(GameObject panel)
    {
        if (panel == null) return;
        
        // UIAnimator가 있으면 사용
        var animator = panel.GetComponent<UIAnimator>();
        if (animator != null)
        {
            animator.Hide();
        }
        else
        {
            panel.SetActive(false);
        }
        
        if (currentOpenPanel == panel)
        {
            currentOpenPanel = null;
        }
    }
    
    // 디버그용: Scene 뷰에서 감지 범위 표시
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
    }
}
