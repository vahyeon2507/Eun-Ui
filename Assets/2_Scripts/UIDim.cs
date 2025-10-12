using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIDim : MonoBehaviour
{
    [SerializeField] private Image dimImage;
    [SerializeField] private float targetAlpha = 0.5f;
    [SerializeField] private float fadeDuration = 0.25f;

    void Awake()
    {
        // 초기 상태 안전하게 설정
        if (dimImage != null)
        {
            dimImage.canvasRenderer.SetAlpha(0f); // 즉시 알파 0으로
            dimImage.raycastTarget = false;      // 클릭은 통과 (시각효과만)
        }
        gameObject.SetActive(false);
    }

    public void ShowDim()
    {
        gameObject.SetActive(true);
        if (dimImage != null)
        {
            // Time.timeScale에 상관없이 페이드되게 true로 설정
            dimImage.CrossFadeAlpha(targetAlpha, fadeDuration, true);
        }
    }

    public void HideDim()
    {
        if (dimImage != null)
        {
            dimImage.CrossFadeAlpha(0f, fadeDuration, true);
        }
        StopAllCoroutines();
        StartCoroutine(DisableAfterUnscaled(fadeDuration + 0.05f));
    }

    private IEnumerator DisableAfterUnscaled(float waitSeconds)
    {
        float t = 0f;
        while (t < waitSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        gameObject.SetActive(false);
    }
}

