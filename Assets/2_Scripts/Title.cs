using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class Title : MonoBehaviour
{
    [SerializeField] private Transform imageParent; // ImageBackGround 오브젝트
    [SerializeField] private TMP_Text titleText;    // TitleText 오브젝트
    [SerializeField] private TMP_Text openingText;  // OpeningText 오브젝트
    [SerializeField] private string nextSceneName = "GameScene";
    [SerializeField] private float fadeDuration = 1.0f;

    private Image[] images;
    private bool isWaitingForInput = false;

    void Start()
    {
        // 필수 참조 체크
        if (!imageParent)
        {
            Debug.LogError("[Title] imageParent가 할당되지 않았습니다!");
            return;
        }
        if (!titleText || !openingText)
        {
            Debug.LogError("[Title] titleText 또는 openingText가 할당되지 않았습니다!");
            return;
        }

        images = imageParent.GetComponentsInChildren<Image>(true);

        // 모든 이미지/텍스트 투명으로 초기화
        foreach (var img in images) if (img) img.color = new Color(1, 1, 1, 0);
        SetTextAlpha(titleText, 0f);
        SetTextAlpha(openingText, 0f);

        // 타이틀 BGM 재생(있으면)
        if (AudioManager.Instance != null)
        {
            Debug.Log("[Title] Playing title BGM");
            AudioManager.Instance.PlayTitleBGM();
        }

        // 페이드 인 후 입력 대기
        StartCoroutine(FadeAll(0f, 1f, () => isWaitingForInput = true));
    }

    void Update()
    {
        if (!isWaitingForInput) return;

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
        {
            isWaitingForInput = false;

            // 페이드 아웃하면서 BGM 전환 및 씬 로드
            StartCoroutine(FadeAll(1f, 0f, () =>
            {
                if (AudioManager.Instance != null)
                {
                    Debug.Log("[Title] Switching to gameplay BGM");
                    AudioManager.Instance.PlayGameplayBGM();
                }

                try
                {
                    SceneManager.LoadScene(nextSceneName);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Title] 씬 로딩 실패: {e.Message}");
                }
            }));
        }
    }

    IEnumerator FadeAll(float from, float to, System.Action onComplete)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);

            SetImagesAlpha(a);
            SetTextAlpha(titleText, a);
            SetTextAlpha(openingText, a);

            yield return null;
        }

        // 마지막 값 보정
        SetImagesAlpha(to);
        SetTextAlpha(titleText, to);
        SetTextAlpha(openingText, to);

        onComplete?.Invoke();
    }

    void SetImagesAlpha(float alpha)
    {
        if (images == null) return;
        foreach (var img in images)
        {
            if (!img) continue;
            var c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (!text) return;
        var c = text.color;
        c.a = alpha;
        text.color = c;
    }
}
