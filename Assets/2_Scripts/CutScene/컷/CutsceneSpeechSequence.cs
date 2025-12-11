using UnityEngine;

/// <summary>
/// 컷씬 애니메이터 + 말풍선 시퀀스 컨트롤러
/// - 애니메이션 이벤트에서 AnimEvent_ShowSpeech(index) 호출
/// - 해당 인덱스의 프리팹을 인스턴스화해서 사용
/// - 말풍선이 닫히면 애니메이션 다시 재생
/// </summary>
public class CutsceneSpeechSequence : MonoBehaviour
{
    [Header("컷씬 애니메이터")]
    public Animator targetAnimator;
    [Tooltip("true면 말풍선 동안 Animator.speed=0으로 멈춤")]
    public bool pauseAnimatorBySpeed = true;

    [Header("말풍선 부모 (Canvas 안에 빈 오브젝트 추천)")]
    public Transform bubbleParent;

    [System.Serializable]
    public class SpeechEntry
    {
        [Header("이 대사에서 사용할 말풍선 프리팹")]
        public CutsceneSpeechBubble bubblePrefab;

        [Header("텍스트 설정")]
        [Tooltip("체크하면 아래 textOverride 사용, 아니면 프리팹 안의 TMP 텍스트 그대로 사용")]
        public bool overrideText = false;

        [TextArea(2, 5)]
        public string textOverride;

        [Header("넘기는 방식")]
        [Tooltip("true → 클릭/스페이스로 넘김\nfalse → 자동으로 넘어감")]
        public bool waitForClick = true;

        [Tooltip("자동으로 넘기는 경우, 모든 텍스트 출력 후 대기 시간(초)\n(waitForClick=false에서만 사용)")]
        public float autoCloseDelay = 1.0f;
    }

    [Header("대사 리스트 (0,1,2... 순서대로)")]
    public SpeechEntry[] speeches;

    bool _bubblePlaying = false;

    // ───────────────────────────────────────
    // Y 바운스 연출 (애니메이션 이벤트용)
    // ───────────────────────────────────────
    [Header("Y 바운스 연출 (AnimEvent_YBump)")]
    [Tooltip("Y값을 흔들 대상. 비워두면 targetAnimator.transform 사용")]
    public Transform yBumpTarget;

    [Tooltip("위/아래로 튈 거리 (양수면 위로 튀었다가 제자리)")]
    public float yBumpOffset = 0.2f;

    [Tooltip("전체 왕복 시간(초). 0.15~0.25 정도 추천")]
    public float yBumpDuration = 0.18f;

    [Tooltip("0→1→0 형태의 곡선. 없으면 sin(πt) 사용")]
    public AnimationCurve yBumpCurve;

    Coroutine _yBumpRoutine;

    void Awake()
    {
        // yBumpTarget이 비어 있으면 Animator 기준으로
        if (!yBumpTarget && targetAnimator != null)
            yBumpTarget = targetAnimator.transform;
    }

    // ───────────────────────────────────────
    // 애니메이션 이벤트용
    // ───────────────────────────────────────
    public void AnimEvent_ShowSpeech(int index)
    {
        ShowSpeech(index);
    }

    public void ShowSpeech(int index)
    {
        if (_bubblePlaying)
        {
            Debug.LogWarning("[CutsceneSpeechSequence] 이미 말풍선 재생 중인데 또 호출됨.");
            return;
        }

        if (speeches == null || speeches.Length == 0)
        {
            Debug.LogWarning("[CutsceneSpeechSequence] speeches가 비어있음.");
            return;
        }

        if (index < 0 || index >= speeches.Length)
        {
            Debug.LogWarning($"[CutsceneSpeechSequence] 잘못된 인덱스 {index}");
            index = Mathf.Clamp(index, 0, speeches.Length - 1);
        }

        var entry = speeches[index];
        if (!entry.bubblePrefab)
        {
            Debug.LogError($"[CutsceneSpeechSequence] {index}번 SpeechEntry에 bubblePrefab이 비어 있음!");
            return;
        }

        // 애니메이터 멈추기
        if (targetAnimator && pauseAnimatorBySpeed)
            targetAnimator.speed = 0f;

        _bubblePlaying = true;

        // 말풍선 생성 위치
        Transform parent = bubbleParent ? bubbleParent : transform;
        var bubble = Instantiate(entry.bubblePrefab, parent);
        bubble.transform.localPosition = Vector3.zero;

        // overrideText가 false면 null/빈 문자열을 넘겨서 프리팹 기본 텍스트를 사용하게 함
        string textToUse = entry.overrideText ? entry.textOverride : null;

        // 말풍선 표시 + 옵션 적용
        bubble.Show(
            textToUse,
            requireClick: entry.waitForClick,
            autoCloseDelay: entry.waitForClick ? 0f : entry.autoCloseDelay,
            onClosed: () =>
            {
                _bubblePlaying = false;

                if (targetAnimator && pauseAnimatorBySpeed)
                    targetAnimator.speed = 1f;
            }
        );
    }

    // ───────────────────────────────────────
    // Y 바운스 애니메이션 이벤트
    // ───────────────────────────────────────
    /// <summary>
    /// 애니메이션 이벤트에서 호출:
    /// - 호출 시점에 yBumpTarget.localPosition.y를 살짝 튀겼다가
    ///   yBumpDuration 안에 다시 원위치로 되돌린다.
    /// - Animator.speed가 0이어도 Time.deltaTime 기준으로 동작함.
    /// </summary>
    public void AnimEvent_YBump()
    {
        if (!yBumpTarget)
        {
            if (targetAnimator != null)
                yBumpTarget = targetAnimator.transform;
        }
        if (!yBumpTarget) return;

        if (_yBumpRoutine != null)
            StopCoroutine(_yBumpRoutine);

        _yBumpRoutine = StartCoroutine(CoYBump());
    }

    System.Collections.IEnumerator CoYBump()
    {
        Vector3 startPos = yBumpTarget.localPosition;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, yBumpDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);

            float curveValue;
            if (yBumpCurve != null && yBumpCurve.keys != null && yBumpCurve.keys.Length > 0)
            {
                curveValue = yBumpCurve.Evaluate(p); // 0~1~0 형태로 세팅해두면 좋음
            }
            else
            {
                // 기본: 0→1→0으로 튀는 sin 곡선
                curveValue = Mathf.Sin(p * Mathf.PI); // 0,↑1,↓0
            }

            float offsetY = curveValue * yBumpOffset;
            yBumpTarget.localPosition = new Vector3(
                startPos.x,
                startPos.y + offsetY,
                startPos.z
            );

            yield return null;
        }

        // 정확히 원위치 복구
        yBumpTarget.localPosition = startPos;
        _yBumpRoutine = null;
    }
}
