using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DirectionalAnimPairSync : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;          // 비우면 자동 연결

    [System.Serializable]
    public class Pair
    {
        [Tooltip("베이스 이름(예: Charge, Prep_Charge 등).")]
        public string baseName;
        [Tooltip("우측을 볼 때 재생할 상태 이름")]
        public string rightState;
        [Tooltip("좌측을 볼 때 재생할 상태 이름")]
        public string leftState;
    }

    [Header("Pairs (Base → Right/Left)")]
    public List<Pair> pairs = new List<Pair>();

    [Header("Options")]
    [Tooltip("PlayByBase 호출 시 기본 CrossFade(초). 0이면 즉시 Play")]
    public float defaultCrossFadeSeconds = 0f;
    [Tooltip("현재 바라보는 방향 (true=오른쪽)")]
    public bool facingRight = true;

    [Header("Flip support (선택)")]
    [Tooltip("방향 전환 시 flipRoot의 X 스케일을 ±로 바꿀지 여부")]
    public bool mirrorXOnFlip = false;
    [Tooltip("스케일 반전을 적용할 트랜스폼(예: FacingPivot)")]
    public Transform flipRoot;

    [Header("Suffix helper (PlayByBase fallback)")]
    [Tooltip("Pairs 매핑이 없을 때 사용할 우측 접미사")]
    public string rightSuffix = "_R";
    [Tooltip("Pairs 매핑이 없을 때 사용할 좌측 접미사")]
    public string leftSuffix = "_L";

    void Reset()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!flipRoot && animator) flipRoot = animator.transform;
    }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!flipRoot && animator) flipRoot = animator.transform;
    }

    Pair FindPair(string baseName)
    {
        if (string.IsNullOrEmpty(baseName) || pairs == null) return null;
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i] != null && pairs[i].baseName == baseName)
                return pairs[i];
        }
        return null;
    }

    public void SetFacing(bool right)
    {
        facingRight = right;

        if (mirrorXOnFlip && flipRoot)
        {
            var s = flipRoot.localScale;
            s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
            flipRoot.localScale = s;
        }
    }

    /// <summary>
    /// 베이스 이름(예: "Charge", "Prep_Charge")으로 우/좌 상태를 골라 재생.
    /// 매핑이 없으면 접미사(right/leftSuffix)를 붙여 시도.
    /// 찾으면 true, 못 찾으면 false.
    /// </summary>
    public bool PlayByBase(string baseName, float crossFade = -1f)
    {
        if (!animator || string.IsNullOrEmpty(baseName)) return false;

        string state = null;
        var p = FindPair(baseName);
        if (p != null)
        {
            state = facingRight ? p.rightState : p.leftState;
        }
        else
        {
            // 매핑 없으면 접미사로 시도
            state = baseName + (facingRight ? rightSuffix : leftSuffix);
        }

        if (string.IsNullOrEmpty(state)) return false;

        float t = (crossFade >= 0f) ? crossFade : defaultCrossFadeSeconds;
        if (t > 0f) animator.CrossFadeInFixedTime(state, t);
        else animator.Play(state, 0, 0f);

        return true;
    }
}
