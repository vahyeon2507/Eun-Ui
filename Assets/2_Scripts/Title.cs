using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class Title : MonoBehaviour
{
    [SerializeField] private Transform imageParent; // ImageBackGround ������Ʈ
    [SerializeField] private TMP_Text titleText;    // TitleText ������Ʈ
    [SerializeField] private TMP_Text openingText;  // OpeningText ������Ʈ
    [SerializeField] private string nextSceneName = "GameScene";
    [SerializeField] private float fadeDuration = 1.0f;

    private Image[] images;
    private bool isWaitingForInput = false;

    void Start()
    {
        images = imageParent.GetComponentsInChildren<Image>(true);

        // ��� �̹����� �ؽ�Ʈ �����ϰ� �ʱ�ȭ
        foreach (var img in images)
            img.color = new Color(1, 1, 1, 0);
        SetTextAlpha(titleText, 0f);
        SetTextAlpha(openingText, 0f);

        // 타이틀 BGM 재생
        if (AudioManager.Instance != null)
        {
            Debug.Log("[Title] Playing title BGM");
            AudioManager.Instance.PlayTitleBGM();
        }
        else
        {
            Debug.LogError("[Title] AudioManager.Instance is null!");
        }

        StartCoroutine(FadeAll(0f, 1f, () => isWaitingForInput = true));
    }

    void Update()
    {
        if (isWaitingForInput && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            isWaitingForInput = false;
            // 게임플레이 BGM으로 전환
            if (AudioManager.Instance != null)
            {
                Debug.Log("[Title] Switching to gameplay BGM");
                AudioManager.Instance.PlayGameplayBGM();
            }
            else
            {
                Debug.LogError("[Title] AudioManager.Instance is null when switching BGM!");
            }
            StartCoroutine(FadeAll(1f, 0f, () => SceneManager.LoadScene(nextSceneName)));
        }
    }

    IEnumerator FadeAll(float from, float to, System.Action onComplete)
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
            SetTextAlpha(titleText, alpha);
            SetTextAlpha(openingText, alpha);

            yield return null;
        }
        foreach (var img in images)
        {
            var color = img.color;
            color.a = to;
            img.color = color;
        }
        SetTextAlpha(titleText, to);
        SetTextAlpha(openingText, to);

        onComplete?.Invoke();
    }

    void SetTextAlpha(TMP_Text text, float alpha)
    {
        var color = text.color;
        color.a = alpha;
        text.color = color;
    }
}