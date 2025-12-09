using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BulgasariAttackAI : MonoBehaviour
{
    [Header("Refs")]
    public BulgasariAttackHooks hooks;   // 필요없어도 둠(훅스에서 히트 실행)
    public Animator animator;
    public Transform player;

    [Header("Run")]
    public bool runOnStart = true;
    public int layerIndex = 0;                  // 공격이 있는 레이어
    [Tooltip("공격 스테이트 공통 태그명 (Animator State Tag)")]
    public string attackTagName = "Attack";

    [Header("Idle pause (seconds)")]
    public Vector2 idlePauseRange = new Vector2(0.35f, 0.75f);

    [Header("Time guard (seconds)")]
    [Tooltip("공격으로 진입을 기다리는 최대 시간")]
    public float enterTimeout = 0.5f;
    [Tooltip("공격이 끝나길 기다리는 최대 시간(넘으면 강제 Idle)")]
    public float attackTimeout = 6f;

    [Header("Idle return (fallback only)")]
    [Tooltip("타임아웃으로 막혔을 때만 강제 Idle로 넘김")]
    public bool forceIdleOnTimeout = true;
    public string idleStateName = "Idle";
    public float crossFadeIdleDuration = 0.06f;

    [System.Serializable]
    public class AttackEntry
    {
        public string id = "clap";         // 참고용
        public string animTrigger = "clap";// AnyState → 이 트리거 1개로 전이
        public float cooldown = 0.8f;      // 같은 기술 재사용 대기
        public bool useDistanceGate = false;
        public float minDistance = 0f;
        public float maxDistance = 999f;
        [HideInInspector] public float nextReadyTime;
    }

    [Header("Attacks")]
    public List<AttackEntry> attacks = new();

    [Header("Global distance gate (optional)")]
    public bool gateByDistance = false;
    public float globalMinDistance = 0f;
    public float globalMaxDistance = 999f;

    // === Heat System ===
    [Header("Floor Heat")]
    [Tooltip("불가사리 열기 시스템 (바닥용)")]
    public BulgasariFloorHeatSystem floorHeat;

    [Tooltip("공격 1회당 올릴 열기량")]
    public int heatPerAttack = 1;

    Coroutine _loop;
    int _attackTagHash;

    void Reset()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!hooks) hooks = GetComponentInChildren<BulgasariAttackHooks>();
        if (!floorHeat) floorHeat = FindObjectOfType<BulgasariFloorHeatSystem>();
    }

    void Awake()
    {
        _attackTagHash = Animator.StringToHash(attackTagName);
    }

    void OnEnable()
    {
        if (runOnStart && _loop == null) _loop = StartCoroutine(AILoop());
    }
    void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    IEnumerator AILoop()
    {
        if (!animator || attacks.Count == 0) yield break;

        while (true)
        {
            // 0) Idle에서 잠깐 쉬기
            float idlePause = Random.Range(Mathf.Min(idlePauseRange.x, idlePauseRange.y),
                                           Mathf.Max(idlePauseRange.x, idlePauseRange.y));
            yield return new WaitForSeconds(idlePause);

            // 1) 공격 하나 선택
            var pick = PickOne(Time.time);
            if (pick == null) continue;

            // 2) 모든 트리거 클리어 후, 선택 트리거만 쏨
            ClearAllTriggers();
            animator.SetTrigger(pick.animTrigger);

            // 👉 여기서 열기 스택 올려준다
            if (floorHeat != null && heatPerAttack > 0)
                floorHeat.AddHeat(heatPerAttack);

            // 3) 공격 태그에 "들어갈 때"까지 대기 (enterTimeout)
            float t = 0f;
            while (!IsInAttackTag() && t < enterTimeout)
            {
                yield return null; t += Time.deltaTime;
            }

            // 4) 공격 태그에 "있는 동안" 대기 (attackTimeout)
            t = 0f;
            while (IsInAttackTag() && t < attackTimeout)
            {
                // 전이 중이어도 next/current 중 하나가 Attack 태그면 계속 기다림
                yield return null; t += Time.deltaTime;
            }

            // 5) 타임아웃이면 막힌 상태 — 필요 시 강제로 Idle
            if (t >= attackTimeout && forceIdleOnTimeout && !string.IsNullOrEmpty(idleStateName))
                animator.CrossFade(idleStateName, crossFadeIdleDuration, layerIndex, 0f);

            // 6) 쿨다운
            pick.nextReadyTime = Time.time + pick.cooldown;
        }
    }

    AttackEntry PickOne(float now)
    {
        float dist = player ? Mathf.Abs(player.position.x - transform.position.x) : 0f;

        // 전역 거리 게이트
        if (gateByDistance && (dist < globalMinDistance || dist > globalMaxDistance))
            return null;

        // 쿨타임/거리 충족하는 후보 수집
        var pool = new List<AttackEntry>();
        foreach (var a in attacks)
        {
            if (a == null) continue;
            if (a.nextReadyTime > now) continue;
            if (a.useDistanceGate && (dist < a.minDistance || dist > a.maxDistance)) continue;
            pool.Add(a);
        }
        if (pool.Count == 0) return null;

        // 아주 단순하게 랜덤
        return pool[Random.Range(0, pool.Count)];
    }

    bool IsInAttackTag()
    {
        if (!animator) return false;

        // 현재
        var cur = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (cur.tagHash == _attackTagHash) return true;

        // 전이 중이면 다음 상태도 체크
        if (animator.IsInTransition(layerIndex))
        {
            var next = animator.GetNextAnimatorStateInfo(layerIndex);
            if (next.tagHash == _attackTagHash) return true;
        }
        return false;
    }

    void ClearAllTriggers()
    {
        if (!animator) return;
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(p.name);
    }
}
