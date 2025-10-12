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
        // Null 체크 추가
        if (imageParent == null)
        {
            Debug.LogError("[Title] imageParent가 할당되지 않았습니다!");
            return;
        }
        
        if (titleText == null || openingText == null)
        {
            Debug.LogError("[Title] titleText 또는 openingText가 할당되지 않았습니다!");
            return;
        }

        images = imageParent.GetComponentsInChildren<Image>(true);

        // 모든 이미지와 텍스트 투명하게 초기화
        foreach (var img in images)
            img.color = new Color(1, 1, 1, 0);
        SetTextAlpha(titleText, 0f);
        SetTextAlpha(openingText, 0f);

        StartCoroutine(FadeAll(0f, 1f, () => isWaitingForInput = true));
    }

    void Update()
    {
        if (isWaitingForInput && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            isWaitingForInput = false;
            StartCoroutine(FadeAll(1f, 0f, () => {
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
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, timer / fadeDuration);

            // 이미지 알파 설정
            SetImagesAlpha(alpha);
            // 텍스트 알파 설정
            SetTextAlpha(titleText, alpha);
            SetTextAlpha(openingText, alpha);

            yield return null;
        }
        
        // 최종 알파 값 설정
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
            if (img != null)
            {
                var color = img.color;
                color.a = alpha;
                img.color = color;
            }
        }
    }

    void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;
        var color = text.color;
        color.a = alpha;
        text.color = color;
    }
}