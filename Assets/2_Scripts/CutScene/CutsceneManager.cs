using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CutsceneManager : MonoBehaviour
{
    // --- 인스펙터 연결 ---
    [Header("UI & Component References")]
    public Image illustrationImage;
    public TMP_Text subtitleText;
    public PlayableDirector timelineDirector;
    public CanvasGroup cutsceneCanvasGroup;

    [Tooltip("애니메이션 프리팹이 생성될 부모 Transform (Canvas 내부).")]
    public Transform animationContainer;

    // --- 자막 ---
    [Header("Subtitle Settings")]
    [Tooltip("자막 타이핑 속도(초/글자). 0 이하면 즉시 출력.")]
    public float typingSpeedDelay = 0.05f;

    // --- 컷신 데이터 ---
    [Header("Cutscene Data")]
    public List<CutsceneFrame> cutsceneFrames;

    // --- 씬 전환 옵션 ---
    [Header("Scene Transition")]
    [Tooltip("컷신이 끝나면 이동할 씬 이름. 비어있으면 현재 씬의 다음 빌드 인덱스를 시도.")]
    public string nextSceneName = "MainMenuOrGameplay";
    [Tooltip("nextSceneName이 로드 불가할 때, 빌드 인덱스 +1 로 폴백할지 여부.")]
    public bool fallbackToNextBuildIndex = true;
    [Tooltip("씬 전환을 비동기로 수행.")]
    public bool loadAsync = true;
    [Tooltip("씬 로드 모드 (대부분 Single).")]
    public LoadSceneMode loadMode = LoadSceneMode.Single;

    int currentFrameIndex = 0;
    bool isWaitingForInput = false;
    bool _isLoadingScene = false;

    void Start()
    {
        if (cutsceneFrames == null || cutsceneFrames.Count == 0) return;

        if (illustrationImage) illustrationImage.enabled = false;
        if (subtitleText) subtitleText.text = "";

        StartCoroutine(StartCutsceneFlow());
    }

    void Update()
    {
        // Space: 다음으로
        if (isWaitingForInput && Input.GetKeyDown(KeyCode.Space))
            isWaitingForInput = false;

        // ESC: 전체 스킵
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopAllCoroutines();
            Debug.Log("컷씬 전체 건너뛰기.");
            LoadNextScene("MainMenuOrGameplay");    // 기존 호출 보존
        }
    }

    IEnumerator StartCutsceneFlow()
    {
        if (cutsceneCanvasGroup) yield return FadeCanvas(cutsceneCanvasGroup, 0f, 1f, 1f);

        while (currentFrameIndex < cutsceneFrames.Count)
        {
            var currentFrame = cutsceneFrames[currentFrameIndex];

            // 클리어
            if (illustrationImage) illustrationImage.enabled = false;
            if (subtitleText) subtitleText.text = "";

            switch (currentFrame.contentType)
            {
                case FrameContentType.StaticImage:
                    yield return HandleStaticImage(currentFrame);
                    break;
                case FrameContentType.SpriteAnimation:
                    yield return HandleSpriteAnimation(currentFrame);
                    break;
                case FrameContentType.TimelineSequence:
                    yield return HandleTimelineSequence(currentFrame);
                    break;
            }

            currentFrameIndex++;
        }

        if (cutsceneCanvasGroup) yield return FadeCanvas(cutsceneCanvasGroup, 1f, 0f, 1f);
        LoadNextScene("MainMenuOrGameplay");        // 기존 호출 보존(아래 메서드가 인스펙터 값/폴백 처리)
    }

    // --- 콘텐츠 핸들러 ---

    IEnumerator HandleStaticImage(CutsceneFrame frame)
    {
        if (illustrationImage)
        {
            illustrationImage.sprite = frame.illustrationSprite;
            illustrationImage.enabled = true;
            yield return FadeImage(illustrationImage, 0f, 1f, 0.5f);
        }

        yield return ShowSubtitleAndAwaitInput(frame);

        if (illustrationImage)
            yield return FadeImage(illustrationImage, 1f, 0f, 0.5f);
    }

    IEnumerator HandleSpriteAnimation(CutsceneFrame frame)
    {
        if (frame.animatedPrefab == null || animationContainer == null)
        {
            Debug.LogError("Sprite Animation 프리팹 또는 Animation Container 미지정.");
            yield break;
        }

        GameObject animInstance = Instantiate(frame.animatedPrefab, animationContainer);
        var rt = animInstance.GetComponent<RectTransform>();
        if (rt) rt.anchoredPosition = Vector2.zero;

        var animator = animInstance.GetComponent<Animator>();
        if (!animator)
        {
            Debug.LogError("애니 프리팹에 Animator 없음.");
            Destroy(animInstance);
            yield break;
        }

        float clipLength = FindClipLength(animator, "Cutscene_Idle");
        if (clipLength == 0f) Debug.LogWarning("클립 길이 못찾음. minDuration만 적용.");

        var subtitleRoutine = StartCoroutine(TypeSentence(frame.subtitleText));

        float totalWait = clipLength + frame.minDuration;
        float start = Time.time;
        while (Time.time < start + totalWait)
        {
            if (isWaitingForInput && Input.GetKeyDown(KeyCode.Space))
            {
                isWaitingForInput = false;
                break;
            }
            yield return null;
        }

        if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
        if (subtitleText) subtitleText.text = "";
        Destroy(animInstance);
    }

    IEnumerator HandleTimelineSequence(CutsceneFrame frame)
    {
        if (frame.timelineClip == null || timelineDirector == null) yield break;

        timelineDirector.playableAsset = frame.timelineClip;
        timelineDirector.Play();

        yield return TypeSentence(frame.subtitleText);
        yield return new WaitUntil(() => timelineDirector.state != PlayState.Playing);

        float start = Time.time;
        while (Time.time < start + frame.minDuration) yield return null;
    }

    // --- 유틸 ---

    IEnumerator ShowSubtitleAndAwaitInput(CutsceneFrame frame)
    {
        yield return TypeSentence(frame.subtitleText);

        float start = Time.time;
        while (Time.time < start + frame.minDuration) yield return null;

        isWaitingForInput = true;
        yield return new WaitUntil(() => !isWaitingForInput);

        if (subtitleText) subtitleText.text = "";
    }

    IEnumerator TypeSentence(string sentence)
    {
        if (!subtitleText) yield break;

        subtitleText.text = "";
        if (typingSpeedDelay <= 0f)
        {
            subtitleText.text = sentence;
            yield break;
        }

        foreach (char ch in sentence)
        {
            subtitleText.text += ch;
            yield return new WaitForSeconds(typingSpeedDelay);
        }
    }

    float FindClipLength(Animator animator, string clipName)
    {
        if (!animator || animator.runtimeAnimatorController == null) return 0f;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip && clip.name == clipName) return clip.length;

        Debug.LogWarning($"애니메이션 클립 '{clipName}'을 찾지 못했습니다. 프리팹의 실제 클립명을 확인하세요.");
        return 0f;
    }

    IEnumerator FadeImage(Image image, float a0, float a1, float dur)
    {
        if (!image) yield break;
        Color c = image.color;
        float t = 0f;
        while (t < dur)
        {
            c.a = Mathf.Lerp(a0, a1, t / dur);
            image.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        c.a = a1; image.color = c;
    }

    IEnumerator FadeCanvas(CanvasGroup cg, float a0, float a1, float dur)
    {
        if (!cg) yield break;
        float t = 0f;
        while (t < dur)
        {
            cg.alpha = Mathf.Lerp(a0, a1, t / dur);
            t += Time.deltaTime;
            yield return null;
        }
        cg.alpha = a1;
    }

    // ===== 씬 전환 =====

    // 기존 호출 보존용 오버로드
    void LoadNextScene(string sceneNameFromCall)
    {
        if (_isLoadingScene) return;

        // 1) 호출자가 준 값 우선, 비정상이면 인스펙터 값, 그것도 없으면 빌드 인덱스 폴백
        string target = !string.IsNullOrEmpty(sceneNameFromCall) ? sceneNameFromCall : nextSceneName;

        // 이름으로 가능하면 이름 우선
        if (!string.IsNullOrEmpty(target) && Application.CanStreamedLevelBeLoaded(target))
        {
            if (loadAsync) StartCoroutine(LoadSceneAsyncByName(target));
            else SceneManager.LoadScene(target, loadMode);
            return;
        }

        // 이름이 비었거나 로드 불가 → 빌드 인덱스 +1 시도
        if (fallbackToNextBuildIndex)
        {
            int cur = SceneManager.GetActiveScene().buildIndex;
            int next = cur + 1;
            int count = SceneManager.sceneCountInBuildSettings;
            if (next >= 0 && next < count)
            {
                if (loadAsync) StartCoroutine(LoadSceneAsyncByIndex(next));
                else SceneManager.LoadScene(next, loadMode);
                return;
            }
        }

        Debug.LogWarning($"씬 전환 실패. 이름 '{target}' 로드 불가, 빌드 인덱스 폴백도 사용 불가.");
    }

    IEnumerator LoadSceneAsyncByName(string sceneName)
    {
        _isLoadingScene = true;
        var op = SceneManager.LoadSceneAsync(sceneName, loadMode);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
    }

    IEnumerator LoadSceneAsyncByIndex(int buildIndex)
    {
        _isLoadingScene = true;
        var op = SceneManager.LoadSceneAsync(buildIndex, loadMode);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
    }
}
