using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 플레이어 머리 위에 표시되는 부적 선택 UI
/// Tab키로 토글, Q/E키로 부적 변경
/// </summary>
public class TalismanSelectionUI : MonoBehaviour
{
    [Header("UI 참조")]
    public Canvas uiCanvas;
    public GameObject uiPanel;
    public Image leftTalisman;    // 이전 부적 (75% 투명도, 작게)
    public Image centerTalisman;  // 현재 부적 (100% 투명도, 정상 크기)
    public Image rightTalisman;   // 다음 부적 (75% 투명도, 작게)
    
    [Header("부적 스프라이트")]
    public Sprite[] talismanSprites = new Sprite[5]; // Fire, Earth, Water, Metal, Wood 순서
    
    [Header("UI 설정")]
    public Vector3 offsetFromPlayer = new Vector3(0, 2f, 0); // 플레이어로부터의 오프셋
    public float sideScale = 0.7f; // 좌우 부적 크기 비율
    public float sideAlpha = 0.75f; // 좌우 부적 투명도
    public float animationSpeed = 5f; // 부적 변경 애니메이션 속도
    
    [Header("입력 키")]
    public KeyCode toggleKey = KeyCode.Tab;
    public KeyCode previousKey = KeyCode.Q;
    public KeyCode nextKey = KeyCode.E;
    
    // 내부 변수
    private PlayerTalismanUnified talismanSystem;
    private Transform playerTransform;
    private Camera mainCamera;
    private bool isUIVisible = false;
    private int currentIndex = 0; // 현재 선택된 부적 인덱스
    private bool isAnimating = false;
    
    // 애니메이션용 코루틴
    private Coroutine changeAnimation;
    
    void Start()
    {
        InitializeUI();
        FindReferences();
        SetupUI();
    }
    
    void InitializeUI()
    {
        // UI가 없으면 자동 생성
        if (uiCanvas == null)
        {
            CreateUICanvas();
        }
        
        if (uiPanel == null)
        {
            CreateUIPanel();
        }
        
        // 초기에는 UI 숨김
        SetUIVisibility(false);
    }
    
    void CreateUICanvas()
    {
        GameObject canvasObj = new GameObject("TalismanSelectionCanvas");
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.WorldSpace;
        uiCanvas.worldCamera = Camera.main;
        
        // CanvasScaler 추가
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        
        // GraphicRaycaster 추가 (상호작용이 필요하면)
        canvasObj.AddComponent<GraphicRaycaster>();
        
        Debug.Log("[TalismanUI] Canvas 자동 생성 완료");
    }
    
    void CreateUIPanel()
    {
        GameObject panelObj = new GameObject("TalismanPanel");
        panelObj.transform.SetParent(uiCanvas.transform);
        
        uiPanel = panelObj;
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(300, 100);
        
        // 좌측 부적
        leftTalisman = CreateTalismanImage("LeftTalisman", new Vector3(-100, 0, 0), sideScale, sideAlpha);
        
        // 중앙 부적
        centerTalisman = CreateTalismanImage("CenterTalisman", Vector3.zero, 1f, 1f);
        
        // 우측 부적
        rightTalisman = CreateTalismanImage("RightTalisman", new Vector3(100, 0, 0), sideScale, sideAlpha);
        
        Debug.Log("[TalismanUI] UI 패널 자동 생성 완료");
    }
    
    Image CreateTalismanImage(string name, Vector3 localPos, float scale, float alpha)
    {
        GameObject imgObj = new GameObject(name);
        imgObj.transform.SetParent(uiPanel.transform);
        
        RectTransform rect = imgObj.AddComponent<RectTransform>();
        rect.anchoredPosition = localPos;
        rect.sizeDelta = new Vector2(80, 80);
        rect.localScale = Vector3.one * scale;
        
        Image img = imgObj.AddComponent<Image>();
        Color color = img.color;
        color.a = alpha;
        img.color = color;
        
        return img;
    }
    
    void FindReferences()
    {
        // PlayerTalismanUnified 찾기
        if (talismanSystem == null)
        {
            talismanSystem = FindObjectOfType<PlayerTalismanUnified>();
            if (talismanSystem == null)
            {
                Debug.LogError("[TalismanUI] PlayerTalismanUnified를 찾을 수 없습니다!");
                return;
            }
        }
        
        // 플레이어 Transform 찾기
        if (playerTransform == null && talismanSystem != null)
        {
            playerTransform = talismanSystem.transform;
        }
        
        // 메인 카메라 찾기
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        Debug.Log($"[TalismanUI] 참조 찾기 완료 - Player: {playerTransform != null}, Camera: {mainCamera != null}");
    }
    
    void SetupUI()
    {
        if (talismanSystem != null)
        {
            currentIndex = (int)talismanSystem.current;
        }
        
        UpdateTalismanDisplay();
    }
    
    void Update()
    {
        HandleInput();
        UpdateUIPosition();
    }
    
    void HandleInput()
    {
        // Tab키로 UI 토글
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUI();
        }
        
        // UI가 보일 때만 부적 변경 가능
        if (isUIVisible && !isAnimating)
        {
            if (Input.GetKeyDown(previousKey))
            {
                ChangeTalisman(-1);
            }
            else if (Input.GetKeyDown(nextKey))
            {
                ChangeTalisman(1);
            }
        }
        
        // PlayerTalismanUnified의 현재 부적과 동기화
        if (talismanSystem != null && (int)talismanSystem.current != currentIndex)
        {
            currentIndex = (int)talismanSystem.current;
            UpdateTalismanDisplay();
        }
    }
    
    void UpdateUIPosition()
    {
        if (playerTransform != null && uiCanvas != null && isUIVisible)
        {
            Vector3 worldPos = playerTransform.position + offsetFromPlayer;
            uiCanvas.transform.position = worldPos;
            
            // 카메라를 향해 회전 (빌보드 효과)
            if (mainCamera != null)
            {
                uiCanvas.transform.LookAt(mainCamera.transform);
                uiCanvas.transform.Rotate(0, 180, 0); // UI가 뒤집히지 않도록
            }
        }
    }
    
    public void ToggleUI()
    {
        isUIVisible = !isUIVisible;
        SetUIVisibility(isUIVisible);
        
        Debug.Log($"[TalismanUI] UI {(isUIVisible ? "표시" : "숨김")}");
    }
    
    void SetUIVisibility(bool visible)
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(visible);
        }
    }
    
    void ChangeTalisman(int direction)
    {
        if (isAnimating) return;
        
        int newIndex = (currentIndex + direction + 5) % 5; // 5개 부적 순환
        
        if (newIndex != currentIndex)
        {
            currentIndex = newIndex;
            
            // PlayerTalismanUnified에 변경 사항 반영
            if (talismanSystem != null)
            {
                talismanSystem.current = (TalismanType)currentIndex;
                Debug.Log($"[TalismanUI] 부적 변경: {(TalismanType)currentIndex}");
            }
            
            // 애니메이션과 함께 UI 업데이트
            if (changeAnimation != null)
            {
                StopCoroutine(changeAnimation);
            }
            changeAnimation = StartCoroutine(AnimateTalismanChange());
        }
    }
    
    IEnumerator AnimateTalismanChange()
    {
        isAnimating = true;
        
        // 간단한 스케일 애니메이션
        float duration = 1f / animationSpeed;
        float elapsed = 0f;
        
        Vector3 originalScale = centerTalisman.transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;
        
        // 확대
        while (elapsed < duration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (duration / 2f);
            centerTalisman.transform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            yield return null;
        }
        
        // 스프라이트 변경
        UpdateTalismanDisplay();
        
        elapsed = 0f;
        // 축소
        while (elapsed < duration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (duration / 2f);
            centerTalisman.transform.localScale = Vector3.Lerp(targetScale, originalScale, progress);
            yield return null;
        }
        
        centerTalisman.transform.localScale = originalScale;
        isAnimating = false;
    }
    
    void UpdateTalismanDisplay()
    {
        if (talismanSprites == null || talismanSprites.Length < 5) return;
        
        // 이전, 현재, 다음 부적 인덱스 계산
        int prevIndex = (currentIndex - 1 + 5) % 5;
        int nextIndex = (currentIndex + 1) % 5;
        
        // 스프라이트 할당
        if (leftTalisman != null && talismanSprites[prevIndex] != null)
            leftTalisman.sprite = talismanSprites[prevIndex];
            
        if (centerTalisman != null && talismanSprites[currentIndex] != null)
            centerTalisman.sprite = talismanSprites[currentIndex];
            
        if (rightTalisman != null && talismanSprites[nextIndex] != null)
            rightTalisman.sprite = talismanSprites[nextIndex];
    }
    
    // 외부에서 현재 부적 인덱스를 설정할 때 사용
    public void SetCurrentTalisman(int index)
    {
        if (index >= 0 && index < 5)
        {
            currentIndex = index;
            UpdateTalismanDisplay();
        }
    }
    
    // 현재 부적 타입 반환
    public TalismanType GetCurrentTalisman()
    {
        return (TalismanType)currentIndex;
    }
    
    [ContextMenu("UI 토글 테스트")]
    public void TestToggleUI()
    {
        ToggleUI();
    }
    
    [ContextMenu("다음 부적 테스트")]
    public void TestNextTalisman()
    {
        if (isUIVisible)
        {
            ChangeTalisman(1);
        }
    }
    
    [ContextMenu("이전 부적 테스트")]
    public void TestPreviousTalisman()
    {
        if (isUIVisible)
        {
            ChangeTalisman(-1);
        }
    }
}
