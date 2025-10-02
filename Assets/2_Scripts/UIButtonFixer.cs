using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI 버튼 문제를 자동으로 수정하는 스크립트
/// </summary>
public class UIButtonFixer : MonoBehaviour
{
    [Header("자동 수정")]
    public bool fixOnStart = true;
    
    void Start()
    {
        if (fixOnStart)
        {
            FixUIButtons();
        }
    }
    
    [ContextMenu("UI 버튼 문제 수정")]
    public void FixUIButtons()
    {
        Debug.Log("=== UI 버튼 문제 수정 시작 ===");
        
        // 1. EventSystem 확인 및 생성
        EnsureEventSystem();
        
        // 2. Canvas GraphicRaycaster 확인
        EnsureGraphicRaycasters();
        
        // 3. Canvas 설정 수정
        FixCanvasSettings();
        
        // 4. 버튼 설정 수정
        FixButtonSettings();
        
        // 5. GameManager 연결
        ReconnectGameManager();
        
        Debug.Log("=== UI 버튼 문제 수정 완료 ===");
    }
    
    void FixCanvasSettings()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            Debug.Log($"[UI수정] Canvas '{canvas.name}' 설정 확인 중...");
            
            // Screen Space - Camera 모드에서 카메라 설정 확인
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    canvas.worldCamera = mainCamera;
                    Debug.Log($"[UI수정] Canvas '{canvas.name}'에 Main Camera를 설정했습니다.");
                }
                else
                {
                    // Screen Space - Overlay로 변경
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Debug.Log($"[UI수정] Canvas '{canvas.name}'을 Screen Space - Overlay로 변경했습니다.");
                }
            }
            
            // CanvasGroup 문제 수정
            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                if (!canvasGroup.interactable)
                {
                    canvasGroup.interactable = true;
                    Debug.Log($"[UI수정] Canvas '{canvas.name}'의 CanvasGroup interactable을 활성화했습니다.");
                }
                if (!canvasGroup.blocksRaycasts)
                {
                    canvasGroup.blocksRaycasts = true;
                    Debug.Log($"[UI수정] Canvas '{canvas.name}'의 CanvasGroup blocksRaycasts를 활성화했습니다.");
                }
                if (canvasGroup.alpha < 0.1f)
                {
                    canvasGroup.alpha = 1f;
                    Debug.Log($"[UI수정] Canvas '{canvas.name}'의 CanvasGroup alpha를 1로 설정했습니다.");
                }
            }
        }
    }
    
    void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystem = eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
            Debug.Log("[UI수정] EventSystem을 생성했습니다.");
        }
        else if (!eventSystem.enabled)
        {
            eventSystem.enabled = true;
            Debug.Log("[UI수정] EventSystem을 활성화했습니다.");
        }
        else
        {
            Debug.Log("[UI수정] EventSystem: 정상");
        }
    }
    
    void EnsureGraphicRaycasters()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"[UI수정] Canvas '{canvas.name}'에 GraphicRaycaster를 추가했습니다.");
            }
            else if (!raycaster.enabled)
            {
                raycaster.enabled = true;
                Debug.Log($"[UI수정] Canvas '{canvas.name}'의 GraphicRaycaster를 활성화했습니다.");
            }
        }
    }
    
    void FixButtonSettings()
    {
        Button[] allButtons = FindObjectsOfType<Button>();
        
        int fixedButtons = 0;
        
        foreach (Button button in allButtons)
        {
            bool wasFixed = false;
            
            // Interactable 확인
            if (!button.interactable)
            {
                button.interactable = true;
                Debug.Log($"[UI수정] 버튼 '{button.name}'의 Interactable을 활성화했습니다.");
                wasFixed = true;
            }
            
            // CanvasGroup 확인
            CanvasGroup[] parentGroups = button.GetComponentsInParent<CanvasGroup>();
            foreach (CanvasGroup group in parentGroups)
            {
                if (!group.interactable)
                {
                    group.interactable = true;
                    Debug.Log($"[UI수정] CanvasGroup '{group.name}'의 interactable을 활성화했습니다.");
                    wasFixed = true;
                }
                if (!group.blocksRaycasts)
                {
                    group.blocksRaycasts = true;
                    Debug.Log($"[UI수정] CanvasGroup '{group.name}'의 blocksRaycasts를 활성화했습니다.");
                    wasFixed = true;
                }
            }
            
            if (wasFixed)
            {
                fixedButtons++;
            }
        }
        
        Debug.Log($"[UI수정] {fixedButtons}개의 버튼을 수정했습니다.");
    }
    
    [ContextMenu("GameManager 참조 다시 연결")]
    public void ReconnectGameManager()
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("[UI수정] GameManager를 찾을 수 없습니다!");
            return;
        }
        
        Debug.Log("[UI수정] GameManager 참조를 다시 연결합니다...");
        
        // 버튼들을 찾아서 수동으로 이벤트 연결
        Button[] buttons = FindObjectsOfType<Button>();
        
        foreach (Button button in buttons)
        {
            Debug.Log($"[UI수정] 버튼 '{button.name}' 확인 중...");
            
            // 버튼 이름에 따라 적절한 메서드 연결
            if (button.name.Contains("Retry") || button.name.Contains("retry"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.SendMessage("OnRestart"));
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OnRestart에 연결했습니다.");
            }
            else if (button.name.Contains("Keep") || button.name.Contains("keep") || button.name.Contains("Continue") || button.name.Contains("continue"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.SendMessage("OnContinue"));
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OnContinue에 연결했습니다.");
            }
            else if (button.name.Contains("Exit") || button.name.Contains("exit") || button.name.Contains("Quit") || button.name.Contains("quit"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.SendMessage("OnQuit"));
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OnQuit에 연결했습니다.");
            }
            else if (button.name.Contains("Setting") || button.name.Contains("setting"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.SendMessage("OpenSettings"));
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OpenSettings에 연결했습니다.");
            }
            // 설정 패널 내부 버튼들
            else if (button.name.Contains("Panel") && button.name.Contains("Button"))
            {
                // Panel0Button, Panel1Button 등의 패턴 감지
                string numberStr = button.name.Replace("Panel", "").Replace("Button", "");
                if (int.TryParse(numberStr, out int panelIndex))
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => gameManager.OpenPanel(panelIndex));
                    Debug.Log($"[UI수정] '{button.name}' 버튼을 OpenPanel({panelIndex})에 연결했습니다.");
                }
                else
                {
                    Debug.LogWarning($"[UI수정] '{button.name}' 버튼의 패널 인덱스를 파싱할 수 없습니다.");
                }
            }
            // 일반적인 설정 버튼 패턴들
            else if (button.name.ToLower().Contains("audio") || button.name.ToLower().Contains("sound"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.OpenPanel(0)); // 보통 오디오는 첫 번째 패널
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OpenPanel(0)에 연결했습니다.");
            }
            else if (button.name.ToLower().Contains("video") || button.name.ToLower().Contains("graphic"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.OpenPanel(1)); // 보통 그래픽은 두 번째 패널
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OpenPanel(1)에 연결했습니다.");
            }
            else if (button.name.ToLower().Contains("control") || button.name.ToLower().Contains("key"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.OpenPanel(2)); // 보통 컨트롤은 세 번째 패널
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OpenPanel(2)에 연결했습니다.");
            }
            else if (button.name.ToLower().Contains("gameplay") || button.name.ToLower().Contains("game"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.OpenPanel(3)); // 보통 게임플레이는 네 번째 패널
                Debug.Log($"[UI수정] '{button.name}' 버튼을 OpenPanel(3)에 연결했습니다.");
            }
            else if (button.name.ToLower().Contains("back") || button.name.ToLower().Contains("close"))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => gameManager.BackToMenu());
                Debug.Log($"[UI수정] '{button.name}' 버튼을 BackToMenu에 연결했습니다.");
            }
            else
            {
                Debug.LogWarning($"[UI수정] '{button.name}' 버튼의 기능을 알 수 없습니다. 수동으로 연결하세요.");
            }
        }
    }
    
    [ContextMenu("모든 버튼 강제 연결")]
    public void ForceConnectAllButtons()
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("[UI수정] GameManager를 찾을 수 없습니다!");
            return;
        }
        
        Debug.Log("[UI수정] 모든 버튼을 강제로 연결합니다...");
        
        // 특정 버튼 이름으로 직접 찾기
        ConnectButtonByName("RetryButton", () => gameManager.SendMessage("OnRestart"), gameManager);
        ConnectButtonByName("KeepButton", () => gameManager.SendMessage("OnContinue"), gameManager);
        ConnectButtonByName("ExitButton", () => gameManager.SendMessage("OnQuit"), gameManager);
        ConnectButtonByName("SettingButton", () => gameManager.SendMessage("OpenSettings"), gameManager);
    }
    
    void ConnectButtonByName(string buttonName, System.Action action, GameManager gameManager)
    {
        GameObject buttonObj = GameObject.Find(buttonName);
        if (buttonObj != null)
        {
            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action.Invoke());
                Debug.Log($"[UI수정] '{buttonName}' 버튼을 연결했습니다.");
            }
            else
            {
                Debug.LogWarning($"[UI수정] '{buttonName}' 오브젝트에 Button 컴포넌트가 없습니다.");
            }
        }
        else
        {
            Debug.LogWarning($"[UI수정] '{buttonName}' 버튼을 찾을 수 없습니다.");
        }
    }
}
