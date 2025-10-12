using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIAnimator : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;

    [Header("슬라이드 위치 설정")]
    [SerializeField] private Vector2 startOffset = new Vector2(0, -200f); // 시작 오프셋
    [SerializeField] private Vector2 endPosition = Vector2.zero;         // 최종 목표 위치

    [Header("애니메이션 설정")]
    public float animationSpeed = 3f;
    public bool debugLog = false;

    private Vector2 startPosition;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panel == null) panel = GetComponent<RectTransform>();

        // 패널 기본 위치를 endPosition으로 설정 (Inspector에서 원하는 좌표 설정)
        if (endPosition == Vector2.zero)
            endPosition = panel.anchoredPosition;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(PlayShowAnim());
        if (debugLog) Debug.Log($"[UIAnimator] {gameObject.name} 표시 시작");
    }

    public void Hide()
    {
        StartCoroutine(PlayHideAnim());
        if (debugLog) Debug.Log($"[UIAnimator] {gameObject.name} 숨김 시작");
    }

    IEnumerator PlayShowAnim()
    {
        // 초기 위치 = 최종 위치 + 오프셋
        startPosition = endPosition + startOffset;

        panel.localScale = Vector3.one * 0.9f;
        panel.anchoredPosition = startPosition;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false; // 애니메이션 중 상호작용 방지

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * animationSpeed;
            float eased = Mathf.SmoothStep(0, 1, t);

            panel.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one * 1.05f, eased);
            panel.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);
            canvasGroup.alpha = eased;

            yield return null;
        }

        panel.localScale = Vector3.one;
        canvasGroup.interactable = true; // 애니메이션 완료 후 상호작용 허용
        
        if (debugLog) Debug.Log($"[UIAnimator] {gameObject.name} 표시 애니메이션 완료");
    }

    IEnumerator PlayHideAnim()
    {
        canvasGroup.interactable = false; // 애니메이션 중 상호작용 방지
        
        float t = 0f;
        Vector3 startScale = panel.localScale;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * animationSpeed;
            float eased = Mathf.SmoothStep(0, 1, t);

            panel.localScale = Vector3.Lerp(startScale, Vector3.one * 0.9f, eased);
            canvasGroup.alpha = 1f - eased;

            yield return null;
        }

        gameObject.SetActive(false);
        
        if (debugLog) Debug.Log($"[UIAnimator] {gameObject.name} 숨김 애니메이션 완료");
    }
    
    [ContextMenu("표시 테스트")]
    public void TestShow()
    {
        Show();
    }
    
    [ContextMenu("숨김 테스트")]
    public void TestHide()
    {
        Hide();
    }
    
    [ContextMenu("즉시 표시")]
    public void ShowInstant()
    {
        gameObject.SetActive(true);
        panel.localScale = Vector3.one;
        panel.anchoredPosition = endPosition;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }
    
    [ContextMenu("즉시 숨김")]
    public void HideInstant()
    {
        gameObject.SetActive(false);
    }
}