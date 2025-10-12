using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI 버튼이 안 눌리는 문제를 진단하는 스크립트
/// </summary>
public class UIButtonDiagnostic : MonoBehaviour
{
    [Header("진단할 버튼")]
    public Button targetButton;
    
    [Header("자동 진단")]
    public bool runDiagnosticOnStart = true;
    
    void Start()
    {
        if (runDiagnosticOnStart)
        {
            DiagnoseUIButtons();
        }
    }
    
    [ContextMenu("UI 버튼 진단")]
    public void DiagnoseUIButtons()
    {
        Debug.Log("=== UI 버튼 진단 시작 ===");
        
        // 1. EventSystem 확인
        CheckEventSystem();
        
        // 2. Canvas 확인
        CheckCanvas();
        
        // 3. 특정 버튼 확인 (설정된 경우)
        if (targetButton != null)
        {
            CheckSpecificButton(targetButton);
        }
        
        // 4. 모든 버튼 확인
        CheckAllButtons();
        
        Debug.Log("=== UI 버튼 진단 완료 ===");
    }
    
    void CheckEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogError("[UI진단] EventSystem이 씬에 없습니다! EventSystem을 추가하세요.");
            return;
        }
        
        if (!eventSystem.enabled)
        {
            Debug.LogError("[UI진단] EventSystem이 비활성화되어 있습니다!");
            return;
        }
        
        Debug.Log("[UI진단] EventSystem: 정상");
    }
    
    void CheckCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        if (canvases.Length == 0)
        {
            Debug.LogError("[UI진단] 씬에 Canvas가 없습니다!");
            return;
        }
        
        foreach (Canvas canvas in canvases)
        {
            Debug.Log($"[UI진단] Canvas '{canvas.name}' 확인:");
            
            // GraphicRaycaster 확인
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Debug.LogError($"  - GraphicRaycaster가 없습니다!");
            }
            else if (!raycaster.enabled)
            {
                Debug.LogError($"  - GraphicRaycaster가 비활성화되어 있습니다!");
            }
            else
            {
                Debug.Log($"  - GraphicRaycaster: 정상");
            }
            
            // CanvasGroup 확인
            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                if (!canvasGroup.interactable)
                {
                    Debug.LogWarning($"  - CanvasGroup.interactable이 false입니다!");
                }
                if (!canvasGroup.blocksRaycasts)
                {
                    Debug.LogWarning($"  - CanvasGroup.blocksRaycasts가 false입니다!");
                }
            }
        }
    }
    
    void CheckSpecificButton(Button button)
    {
        Debug.Log($"[UI진단] 버튼 '{button.name}' 상세 확인:");
        
        // 기본 상태 확인
        if (!button.gameObject.activeInHierarchy)
        {
            Debug.LogError($"  - 버튼이 비활성화되어 있습니다!");
            return;
        }
        
        if (!button.interactable)
        {
            Debug.LogError($"  - 버튼의 Interactable이 false입니다!");
        }
        
        // 이벤트 확인
        if (button.onClick.GetPersistentEventCount() == 0)
        {
            Debug.LogWarning($"  - OnClick 이벤트가 설정되지 않았습니다!");
        }
        else
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                var target = button.onClick.GetPersistentTarget(i);
                var methodName = button.onClick.GetPersistentMethodName(i);
                
                if (target == null)
                {
                    Debug.LogError($"  - OnClick 이벤트 {i}: Target이 NULL입니다! (Missing Reference)");
                }
                else
                {
                    Debug.Log($"  - OnClick 이벤트 {i}: {target.name}.{methodName}()");
                }
            }
        }
        
        // RectTransform 확인
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0)
            {
                Debug.LogWarning($"  - 버튼 크기가 0입니다! (Width: {rect.width}, Height: {rect.height})");
            }
        }
        
        // 부모 CanvasGroup 확인
        CanvasGroup[] parentGroups = button.GetComponentsInParent<CanvasGroup>();
        foreach (CanvasGroup group in parentGroups)
        {
            if (!group.interactable)
            {
                Debug.LogWarning($"  - 부모 CanvasGroup '{group.name}'의 interactable이 false입니다!");
            }
            if (!group.blocksRaycasts)
            {
                Debug.LogWarning($"  - 부모 CanvasGroup '{group.name}'의 blocksRaycasts가 false입니다!");
            }
        }
    }
    
    void CheckAllButtons()
    {
        Button[] allButtons = FindObjectsOfType<Button>();
        
        Debug.Log($"[UI진단] 전체 버튼 개수: {allButtons.Length}");
        
        int activeButtons = 0;
        int interactableButtons = 0;
        int buttonsWithEvents = 0;
        int buttonsWithMissingTargets = 0;
        
        foreach (Button button in allButtons)
        {
            if (button.gameObject.activeInHierarchy)
            {
                activeButtons++;
                
                if (button.interactable)
                {
                    interactableButtons++;
                }
                
                if (button.onClick.GetPersistentEventCount() > 0)
                {
                    buttonsWithEvents++;
                    
                    // Missing Target 확인
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentTarget(i) == null)
                        {
                            buttonsWithMissingTargets++;
                            Debug.LogError($"[UI진단] 버튼 '{button.name}'의 OnClick 이벤트에 Missing Target이 있습니다!");
                            break;
                        }
                    }
                }
            }
        }
        
        Debug.Log($"[UI진단] 활성 버튼: {activeButtons}");
        Debug.Log($"[UI진단] 상호작용 가능한 버튼: {interactableButtons}");
        Debug.Log($"[UI진단] 이벤트가 있는 버튼: {buttonsWithEvents}");
        
        if (buttonsWithMissingTargets > 0)
        {
            Debug.LogError($"[UI진단] Missing Target이 있는 버튼: {buttonsWithMissingTargets}개");
        }
    }
    
    void Update()
    {
        // 마우스 클릭 시 UI 요소 확인
        if (Input.GetMouseButtonDown(0))
        {
            CheckMouseClick();
        }
    }
    
    void CheckMouseClick()
    {
        Debug.Log($"[UI진단] 마우스 클릭 감지 - 위치: {Input.mousePosition}");
        
        if (EventSystem.current == null)
        {
            Debug.LogError("[UI진단] EventSystem이 없습니다!");
            return;
        }
        
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        if (results.Count == 0)
        {
            Debug.LogWarning("[UI진단] 마우스 클릭: UI 요소가 감지되지 않았습니다!");
            Debug.LogWarning("[UI진단] 가능한 원인: Canvas가 Screen Space - Camera인데 Camera가 설정되지 않음, 또는 UI가 화면 밖에 있음");
        }
        else
        {
            Debug.Log($"[UI진단] 마우스 클릭: {results.Count}개의 UI 요소 감지:");
            foreach (var result in results)
            {
                GameObject obj = result.gameObject;
                Debug.Log($"  - {obj.name} ({obj.GetComponent<Graphic>()?.GetType().Name})");
                
                // 버튼인지 확인
                Button button = obj.GetComponent<Button>();
                if (button != null)
                {
                    Debug.Log($"    → 버튼 발견! Interactable: {button.interactable}");
                    
                    // 버튼 클릭 강제 실행
                    if (button.interactable)
                    {
                        Debug.Log($"    → 버튼 '{button.name}' 클릭 이벤트 강제 실행");
                        button.onClick.Invoke();
                    }
                    else
                    {
                        Debug.LogWarning($"    → 버튼 '{button.name}'이 비활성화되어 있습니다!");
                    }
                }
                
                // 부모에서 버튼 찾기
                Button parentButton = obj.GetComponentInParent<Button>();
                if (parentButton != null && parentButton != button)
                {
                    Debug.Log($"    → 부모 버튼 발견: {parentButton.name}, Interactable: {parentButton.interactable}");
                    
                    if (parentButton.interactable)
                    {
                        Debug.Log($"    → 부모 버튼 '{parentButton.name}' 클릭 이벤트 강제 실행");
                        parentButton.onClick.Invoke();
                    }
                }
            }
        }
        
        // Canvas 설정도 확인
        CheckCanvasSettings();
    }
    
    void CheckCanvasSettings()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            if (!canvas.gameObject.activeInHierarchy) continue;
            
            Debug.Log($"[UI진단] Canvas '{canvas.name}' 설정:");
            Debug.Log($"  - Render Mode: {canvas.renderMode}");
            Debug.Log($"  - Sort Order: {canvas.sortingOrder}");
            
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (canvas.worldCamera == null)
                {
                    Debug.LogError($"  - Screen Space - Camera 모드인데 World Camera가 설정되지 않았습니다!");
                }
                else
                {
                    Debug.Log($"  - World Camera: {canvas.worldCamera.name}");
                }
            }
            
            // Canvas Group 확인
            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                Debug.Log($"  - CanvasGroup - Interactable: {canvasGroup.interactable}, BlocksRaycasts: {canvasGroup.blocksRaycasts}, Alpha: {canvasGroup.alpha}");
                
                if (!canvasGroup.interactable || !canvasGroup.blocksRaycasts)
                {
                    Debug.LogWarning($"  - CanvasGroup 설정 문제 발견!");
                }
            }
        }
    }
}
