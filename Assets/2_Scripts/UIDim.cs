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
        // �ʱ� ���� �����ϰ� ����
        if (dimImage != null)
        {
            dimImage.canvasRenderer.SetAlpha(0f); // ��� ���� 0����
            dimImage.raycastTarget = false;      // Ŭ���� ��� (�ð�ȿ����)
        }
        gameObject.SetActive(false);
    }

    public void ShowDim()
    {
        gameObject.SetActive(true);
        if (dimImage != null)
        {
            // Time.timeScale�� ������� ���̵�ǰ� true�� ����
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

