using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // 씬 로딩을 위해 추가

public class CutsceneManager : MonoBehaviour
{
    // --- 인스펙터에서 할당할 UI 컴포넌트 ---
    [Header("UI & Component References")]
    public Image illustrationImage;
    public TMP_Text subtitleText;
    public PlayableDirector timelineDirector;
    public CanvasGroup cutsceneCanvasGroup;

    [Tooltip("애니메이션 프리팹이 생성될 부모 Transform. Canvas 내의 Image 위치에 대응되는 자식 오브젝트를 할당하세요.")]
    public Transform animationContainer; //  애니메이션 생성 위치 추가

    // --- 자막 설정 (속도 조절 기능) ---
    [Header("Subtitle Settings")]
    [Tooltip("자막이 한 글자씩 출력될 때의 딜레이 시간 (초). 값이 낮을수록 빠르게 출력됩니다.")]
    public float typingSpeedDelay = 0.05f;

    // --- 인스펙터에서 할당할 데이터 ---
    [Header("Cutscene Data")]
    public List<CutsceneFrame> cutsceneFrames;

    private int currentFrameIndex = 0;
    private bool isWaitingForInput = false;

    void Start()
    {
        if (cutsceneFrames == null || cutsceneFrames.Count == 0) return;

        illustrationImage.enabled = false;
        subtitleText.text = "";

        StartCoroutine(StartCutsceneFlow());
    }

    void Update()
    {
        // Space/ESC로 스킵 처리
        if (isWaitingForInput && Input.GetKeyDown(KeyCode.Space))
        {
            isWaitingForInput = false;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopAllCoroutines();
            Debug.Log("컷씬 전체 건너뛰기.");
            LoadNextScene("MainMenuOrGameplay");
        }
    }

    IEnumerator StartCutsceneFlow()
    {
        yield return FadeCanvas(cutsceneCanvasGroup, 0f, 1f, 1f);

        while (currentFrameIndex < cutsceneFrames.Count)
        {
            CutsceneFrame currentFrame = cutsceneFrames[currentFrameIndex];

            // 이전 프레임 클리어
            illustrationImage.enabled = false;
            subtitleText.text = "";

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

        yield return FadeCanvas(cutsceneCanvasGroup, 1f, 0f, 1f);
        LoadNextScene("MainMenuOrGameplay");
    }

    // --- 콘텐츠 핸들러 ---

    IEnumerator HandleStaticImage(CutsceneFrame frame)
    {
        illustrationImage.sprite = frame.illustrationSprite;
        illustrationImage.enabled = true;
        yield return FadeImage(illustrationImage, 0f, 1f, 0.5f);
        yield return ShowSubtitleAndAwaitInput(frame);
        yield return FadeImage(illustrationImage, 1f, 0f, 0.5f);
    }

    //  오류 해결: 애니메이션 프리팹 인스턴스화 및 길이 검색
    IEnumerator HandleSpriteAnimation(CutsceneFrame frame)
    {
        if (frame.animatedPrefab == null || animationContainer == null)
        {
            Debug.LogError("Sprite Animation에 할당된 Prefab 또는 Animation Container가 없습니다.");
            yield break;
        }

        // 1. 애니메이션 오브젝트 생성
        GameObject animInstance = Instantiate(frame.animatedPrefab, animationContainer);
        // RectTransform을 사용하여 UI 위치를 맞춥니다.
        RectTransform rt = animInstance.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = Vector2.zero;

        Animator animator = animInstance.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("할당된 프리팹에 Animator 컴포넌트가 없습니다.");
            Destroy(animInstance);
            yield break;
        }

        // 2. 애니메이션 클립 길이 검색 (오류 해결 핵심)
        //  주의: "Default_Clip_Name" 부분을 유니티 에디터에서 생성된 실제 클립 이름으로 반드시 변경하세요!
        float clipLength = FindClipLength(animator, "Cutscene_Idle");

        if (clipLength == 0f)
        {
            Debug.LogWarning("애니메이션 클립 길이를 찾을 수 없습니다. 최소 대기 시간만 적용됩니다.");
            clipLength = 0f;
        }

        // 3. 자막 타이핑 시작
        Coroutine subtitleRoutine = StartCoroutine(TypeSentence(frame.subtitleText));

        // 4. 대기 시간 계산 및 대기
        float totalWaitTime = clipLength + frame.minDuration;
        float startTime = Time.time;

        while (Time.time < startTime + totalWaitTime)
        {
            if (isWaitingForInput && Input.GetKeyDown(KeyCode.Space))
            {
                isWaitingForInput = false;
                break;
            }
            yield return null;
        }

        // 5. 정리 및 제거
        StopCoroutine(subtitleRoutine);
        subtitleText.text = "";
        Destroy(animInstance);
    }

    IEnumerator HandleTimelineSequence(CutsceneFrame frame)
    {
        if (frame.timelineClip == null || timelineDirector == null) yield break;

        timelineDirector.playableAsset = frame.timelineClip;
        timelineDirector.Play();

        yield return TypeSentence(frame.subtitleText);

        yield return new WaitUntil(() => timelineDirector.state != PlayState.Playing);

        float startTime = Time.time;
        while (Time.time < startTime + frame.minDuration)
        {
            yield return null;
        }
    }

    // --- 유틸리티 함수 ---

    IEnumerator ShowSubtitleAndAwaitInput(CutsceneFrame frame)
    {
        yield return TypeSentence(frame.subtitleText);

        float startTime = Time.time;
        while (Time.time < startTime + frame.minDuration)
        {
            yield return null;
        }

        isWaitingForInput = true;
        yield return new WaitUntil(() => !isWaitingForInput);

        subtitleText.text = "";
    }

    //  자막 속도 조절 기능 적용
    IEnumerator TypeSentence(string sentence)
    {
        subtitleText.text = "";

        if (typingSpeedDelay <= 0)
        {
            subtitleText.text = sentence;
            yield break;
        }

        foreach (char letter in sentence.ToCharArray())
        {
            subtitleText.text += letter;
            yield return new WaitForSeconds(typingSpeedDelay);
        }
        yield return null;
    }

    //  애니메이션 클립 길이 검색 헬퍼 함수
    float FindClipLength(Animator animator, string clipName)
    {
        if (animator.runtimeAnimatorController == null) return 0f;

        // Animator Controller의 모든 클립을 검색하여 길이를 찾습니다.
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip.length;
            }
        }
        // 클립 이름을 찾지 못했을 때 경고
        Debug.LogWarning($"클립 이름을 찾을 수 없습니다. 인스펙터의 프리팹을 만들 때 생성된 애니메이션 클립의 실제 이름을 사용하세요. (현재 검색 이름: '{clipName}')");
        return 0f;
    }

    //  누락된 FadeImage 함수 구현
    IEnumerator FadeImage(Image image, float startAlpha, float endAlpha, float duration)
    {
        Color color = image.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            color.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            image.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }
        color.a = endAlpha;
        image.color = color;
    }

    //  누락된 FadeCanvas 함수 구현
    IEnumerator FadeCanvas(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = endAlpha;
    }

    void LoadNextScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.Log($"씬 '{sceneName}'을(를) 로드하지 못했습니다. 다음 씬 이름을 확인해 주세요.");
        }
    }
}