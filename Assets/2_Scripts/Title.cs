using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class Title : MonoBehaviour
{
    [SerializeField] private Transform imageParent; // Canvas > ImageBackGround ������Ʈ�� �Ҵ�
    [SerializeField] private string nextSceneName = "GameScene";
    [SerializeField] private float fadeDuration = 1.0f;

    private Image[] images;
    private bool isWaitingForInput = false;

    void Start()
    {
        // ImageBackGround�� �ڽ� Image ������Ʈ ��� ��������
        images = imageParent.GetComponentsInChildren<Image>(true);

        // ��� �̹��� �����ϰ� �ʱ�ȭ
        foreach (var img in images)
            img.color = new Color(1, 1, 1, 0);

        StartCoroutine(FadeAllImages(0f, 1f, () => isWaitingForInput = true));
    }

    void Update()
    {
        if (isWaitingForInput && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            isWaitingForInput = false;
            StartCoroutine(FadeAllImages(1f, 0f, () => SceneManager.LoadScene(nextSceneName)));
        }
    }

    IEnumerator FadeAllImages(float from, float to, System.Action onComplete)
    {
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, timer / fadeDuration);
            foreach (var img in images)
            {
                var color = img.color;
                color.a = alpha;
                img.color = color;
            }
            yield return null;
        }
        foreach (var img in images)
        {
            var color = img.color;
            color.a = to;
            img.color = color;
        }
        onComplete?.Invoke();
    }
}