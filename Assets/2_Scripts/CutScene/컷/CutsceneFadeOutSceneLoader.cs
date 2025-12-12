using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 컷씬용 페이드 인 / 아웃 + 씬 전환 컨트롤러
/// - Start에서 옵션에 따라 페이드 인 후 컷씬 애니메이션 시작
/// - 애니메이션 이벤트에서 AnimEvent_FadeOutAndLoad() 호출 시
///   검은 화면으로 페이드 아웃 후 씬 전환
/// </summary>
[DisallowMultipleComponent]
public class CutsceneFadeInOutSceneLoader : MonoBehaviour
{
    [Header("Fade Target")]
    [Tooltip("화면을 덮는 검은 패널에 붙은 CanvasGroup")]
    public CanvasGroup blackCanvasGroup;

    [Tooltip("비워두면 자식에서 자동으로 CanvasGroup 하나 찾아옴")]
    public bool autoFindCanvasGroupInChildren = true;

    // ──────────────────────────────
    // 페이드 인 + 컷씬 시작
    // ──────────────────────────────
    [Header("Fade-In at Scene Start")]
    [Tooltip("씬 시작 시 자동으로 페이드 인을 실행할지 여부")]
    public bool playFadeInOnStart = true;

    [Tooltip("검은 화면에서 투명해지기까지 걸리는 시간(초)")]
    public float fadeInDuration = 1.0f;

    [Tooltip("페이드 인 곡선 (없으면 기본 EaseInOut)")]
    public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Cutscene Animator")]
    [Tooltip("페이드 인 후 재생할 컷씬 애니메이터 (선택)")]
    public Animator cutsceneAnimator;

    [Tooltip("true면 페이드 인 동안 cutsceneAnimator.speed=0, 끝나면 1로 돌림")]
    public bool controlCutsceneAnimator = true;

    [Tooltip("페이드 인이 끝난 후 애니메이션 재생을 시작하기까지 딜레이(초)")]
    public float cutsceneStartDelay = 0.5f;

    // ──────────────────────────────
    // 페이드 아웃 + 씬 전환
    // ──────────────────────────────
    [Header("Fade-Out + Scene Load")]
    [Tooltip("검은 화면으로 완전히 닫히는 데 걸리는 시간(초)")]
    public float fadeOutDuration = 1.0f;

    [Tooltip("페이드 아웃 곡선 (없으면 기본 EaseInOut)")]
    public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Scene Settings")]
    [Tooltip("로드할 씬 이름 (우선순위 1). 비우면 buildIndex 사용")]
    public string nextSceneName;

    [Tooltip("씬 빌드 인덱스 (nextSceneName 비었을 때만 사용)")]
    public int nextSceneBuildIndex = -1;

    [Tooltip("Additive 모드로 로드할지 여부 (필요 없으면 false 유지)")]
    public bool loadAdditive = false;

    bool _fadeInRunning = false;
    bool _fadeOutRunning = false;

    void Awake()
    {
        if (!blackCanvasGroup && autoFindCanvasGroupInChildren)
            blackCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

        // 컷씬 애니메이터 제어 옵션
        if (cutsceneAnimator && controlCutsceneAnimator && playFadeInOnStart)
        {
            // 처음엔 정지 상태에서 대기
            cutsceneAnimator.speed = 0f;
        }

        if (blackCanvasGroup)
        {
            if (playFadeInOnStart)
            {
                // 씬 시작 시: 까만 화면에서 시작해서 페이드 인
                blackCanvasGroup.gameObject.SetActive(true);
                blackCanvasGroup.alpha = 1f;
                blackCanvasGroup.blocksRaycasts = true;
            }
            else
            {
                // 페이드 인 안 쓸 경우: 기본은 투명 & 비활성
                blackCanvasGroup.alpha = 0f;
                blackCanvasGroup.gameObject.SetActive(false);
                blackCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    void Start()
    {
        if (playFadeInOnStart && blackCanvasGroup)
        {
            StartFadeInAndPlayCutscene();
        }
    }

    // ──────────────────────────────
    // 페이드 인 + 컷씬 시작
    // ──────────────────────────────

    /// <summary>
    /// 코드에서 직접 호출해도 되는 페이드 인 + 컷씬 시작 함수
    /// </summary>
    public void StartFadeInAndPlayCutscene()
    {
        if (_fadeInRunning) return;
        StartCoroutine(CoFadeInThenStartCutscene());
    }

    IEnumerator CoFadeInThenStartCutscene()
    {
        _fadeInRunning = true;

        if (!blackCanvasGroup)
        {
            Debug.LogWarning("[CutsceneFadeInOutSceneLoader] CanvasGroup이 없어 페이드 인 없이 컷씬만 재생합니다.");

            // 그냥 컷씬만 시작
            yield return new WaitForSecondsRealtime(cutsceneStartDelay);
            if (cutsceneAnimator && controlCutsceneAnimator)
                cutsceneAnimator.speed = 1f;

            _fadeInRunning = false;
            yield break;
        }

        blackCanvasGroup.gameObject.SetActive(true);
        blackCanvasGroup.blocksRaycasts = true;

        float duration = Mathf.Max(0.01f, fadeInDuration);
        float t = 0f;
        float start = 1f;  // 까만 화면에서 시작
        float end = 0f;    // 투명해질 때까지

        // 혹시 에디터에서 알파를 건드렸으면 강제로 1로 맞추기
        blackCanvasGroup.alpha = start;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            if (fadeInCurve != null && fadeInCurve.keys != null && fadeInCurve.length > 0)
                p = fadeInCurve.Evaluate(p);

            blackCanvasGroup.alpha = Mathf.Lerp(start, end, p);
            yield return null;
        }

        blackCanvasGroup.alpha = end;
        blackCanvasGroup.blocksRaycasts = false;
        blackCanvasGroup.gameObject.SetActive(false);

        // 페이드 인 끝난 후 약간 기다렸다가 컷씬 재생
        if (cutsceneStartDelay > 0f)
            yield return new WaitForSecondsRealtime(cutsceneStartDelay);

        if (cutsceneAnimator && controlCutsceneAnimator)
            cutsceneAnimator.speed = 1f;

        _fadeInRunning = false;
    }

    // ──────────────────────────────
    // 페이드 아웃 + 씬 전환
    // ──────────────────────────────

    /// <summary>
    /// 애니메이션 이벤트에서 호출할 함수
    /// (페이드 아웃 후 씬 전환)
    /// </summary>
    public void AnimEvent_FadeOutAndLoad()
    {
        StartFadeOutAndLoad();
    }

    /// <summary>
    /// 코드에서 직접 호출 가능
    /// </summary>
    public void StartFadeOutAndLoad()
    {
        if (_fadeOutRunning) return;
        StartCoroutine(CoFadeOutAndLoad());
    }

    IEnumerator CoFadeOutAndLoad()
    {
        _fadeOutRunning = true;

        if (!blackCanvasGroup)
        {
            Debug.LogWarning("[CutsceneFadeInOutSceneLoader] CanvasGroup이 없어 페이드 없이 바로 씬 전환합니다.");
            LoadSceneNow();
            yield break;
        }

        blackCanvasGroup.gameObject.SetActive(true);
        blackCanvasGroup.blocksRaycasts = true;

        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float t = 0f;
        float start = blackCanvasGroup.alpha; // 현재 알파에서 시작(보통 0)
        float end = 1f;                       // 완전히 까맣게

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            if (fadeOutCurve != null && fadeOutCurve.keys != null && fadeOutCurve.length > 0)
                p = fadeOutCurve.Evaluate(p);

            blackCanvasGroup.alpha = Mathf.Lerp(start, end, p);
            yield return null;
        }

        blackCanvasGroup.alpha = end;

        LoadSceneNow();
    }

    void LoadSceneNow()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(
                nextSceneName,
                loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single
            );
        }
        else if (nextSceneBuildIndex >= 0)
        {
            SceneManager.LoadScene(
                nextSceneBuildIndex,
                loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single
            );
        }
        else
        {
            Debug.LogError("[CutsceneFadeInOutSceneLoader] 로드할 씬이 설정되지 않았습니다. nextSceneName 또는 nextSceneBuildIndex를 세팅하세요.");
        }
    }
}
