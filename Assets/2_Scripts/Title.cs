using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Title : MonoBehaviour
{
    [SerializeField] private Transform imageParent; // ImageBackGround 오브젝트
    [SerializeField] private TMP_Text titleText;    // TitleText 오브젝트
    [SerializeField] private TMP_Text openingText;  // OpeningText 오브젝트 ("아무 키나 누르세요")
    [SerializeField] private string nextSceneName = "GameScene";
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float blinkSpeed = 1.5f; // 깜빡이는 속도 (조정 가능)

    private Image[] images;
    private bool isWaitingForInput = false;
    private Coroutine blinkCoroutine; // 깜빡임 코루틴 저장용

    void Start()
    {
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
        StartCoroutine(FadeAll(0f, 1f, () =>
        {
            isWaitingForInput = true;
            // 🔥 깜빡이기 시작
            blinkCoroutine = StartCoroutine(BlinkText(openingText));
        }));
    }

    void Update()
    {
        if (!isWaitingForInput) return;

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
        {
            isWaitingForInput = false;

            // 깜빡임 중단
            if (blinkCoroutine != null)
                StopCoroutine(blinkCoroutine);
            SetTextAlpha(openingText, 1f);

            // 페이드 아웃하면서 씬 로드
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

    // 🔥 추가된 부분: 텍스트 깜빡이기 코루틴
    IEnumerator BlinkText(TMP_Text text)
    {
        while (true)
        {
            // Mathf.PingPong으로 0~1 사이 알파 반복
            float alpha = Mathf.PingPong(Time.time * blinkSpeed, 1f);
            SetTextAlpha(text, alpha);
            yield return null;
        }
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
