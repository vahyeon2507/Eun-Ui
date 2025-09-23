using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIAnimator : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;

    [Header("슬라이드 위치 설정")]
    [SerializeField] private Vector2 startOffset = new Vector2(0, -200f); // 시작 오프셋
    [SerializeField] private Vector2 endPosition = Vector2.zero;         // 최종 도착 위치

    private Vector2 startPosition;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panel == null) panel = GetComponent<RectTransform>();

        // 패널 기본 위치를 endPosition으로 저장 (Inspector에서 원하는 좌표 지정)
        if (endPosition == Vector2.zero)
            endPosition = panel.anchoredPosition;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(PlayShowAnim());
    }

    public void Hide()
    {
        StartCoroutine(PlayHideAnim());
    }

    IEnumerator PlayShowAnim()
    {
        // 초기 위치 = 최종 위치 + 오프셋
        startPosition = endPosition + startOffset;

        panel.localScale = Vector3.one * 0.9f;
        panel.anchoredPosition = startPosition;
        canvasGroup.alpha = 0f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 3f;
            float eased = Mathf.SmoothStep(0, 1, t);

            panel.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one * 1.05f, eased);
            panel.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);
            canvasGroup.alpha = eased;

            yield return null;
        }

        panel.localScale = Vector3.one;
    }

    IEnumerator PlayHideAnim()
    {
        float t = 0f;
        Vector3 startScale = panel.localScale;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 3f;
            float eased = Mathf.SmoothStep(0, 1, t);

            panel.localScale = Vector3.Lerp(startScale, Vector3.one * 0.9f, eased);
            canvasGroup.alpha = 1f - eased;

            yield return null;
        }

        gameObject.SetActive(false);
    }
}


