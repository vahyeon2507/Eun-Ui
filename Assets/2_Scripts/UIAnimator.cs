using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIAnimator : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;

    [Header("�����̵� ��ġ ����")]
    [SerializeField] private Vector2 startOffset = new Vector2(0, -200f); // ���� ������
    [SerializeField] private Vector2 endPosition = Vector2.zero;         // ���� ���� ��ġ

    private Vector2 startPosition;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panel == null) panel = GetComponent<RectTransform>();

        // �г� �⺻ ��ġ�� endPosition���� ���� (Inspector���� ���ϴ� ��ǥ ����)
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
        // �ʱ� ��ġ = ���� ��ġ + ������
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


