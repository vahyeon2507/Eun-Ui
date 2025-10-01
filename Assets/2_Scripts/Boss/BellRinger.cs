using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BellRinger : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    [Tooltip("벨을 울리는 트리거 이름(애니메이터 트리거)")]
    public string ringTrigger = "Ring";
    [Tooltip("벨이 울릴 때의 상태 이름(선택). 지정하면 이 상태가 끝날 때까지 ringing 유지")]
    public string ringStateName = "";
    [Tooltip("최소 차단 시간(초). 너무 짧으면 감지 어려움")]
    public float minBlockDuration = 0.1f;
    [Tooltip("최대 차단 시간(초). 안전장치")]
    public float maxBlockDuration = 1.0f;

    public bool IsRinging { get; private set; }

    Coroutine _co;

    public bool TryRing(float leadTime)
    {
        if (IsRinging) return false;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator) { animator.ResetTrigger(ringTrigger); animator.SetTrigger(ringTrigger); }
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Run(leadTime));
        return true;
    }

    IEnumerator Run(float leadTime)
    {
        IsRinging = true;

        // 최소 리드타임만큼은 반드시 유지
        float t = 0f;
        float minTime = Mathf.Max(minBlockDuration, leadTime);
        while (t < minTime) { t += Time.deltaTime; yield return null; }

        // ringStateName을 지정했다면, 해당 상태가 끝날 때까지(또는 최대 시간까지) 유지
        if (!string.IsNullOrEmpty(ringStateName) && animator != null)
        {
            float t2 = 0f;
            while (t2 < maxBlockDuration)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (!st.IsName(ringStateName)) break;
                t2 += Time.deltaTime;
                yield return null;
            }
        }

        IsRinging = false;
        _co = null;
    }
}
