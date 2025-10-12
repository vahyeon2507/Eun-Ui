using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BulgasariSimpleAttackLooper : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public int layerIndex = 0;

    [Header("Idle 대기 시간(초)")]
    public Vector2 idleDelayRange = new Vector2(0.6f, 1.2f);

    [Header("트리거 목록")]
    public List<string> triggers = new(); // 예: "Sweep", "Clap", "Crosscut", "Thorn"

    [Header("선택 방식")]
    public bool randomOrder = true; // false면 순차

    [Header("시작 옵션")]
    public bool runOnStart = true;

    int _idx;
    int _attackTagHash;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        _attackTagHash = Animator.StringToHash("Attack");
    }

    void Start()
    {
        if (runOnStart) StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        if (animator == null || triggers.Count == 0) yield break;

        while (true)
        {
            // 1) Idle 대기
            float idle = Random.Range(idleDelayRange.x, idleDelayRange.y);
            yield return new WaitForSeconds(idle);

            // 2) 트리거 선택
            string trig = PickTrigger();
            if (!string.IsNullOrEmpty(trig))
            {
                animator.ResetTrigger(trig); // 혹시 남아있을 수 있는 잔여 제거
                animator.SetTrigger(trig);
            }

            // 3) 공격 상태 진입을 기다림(최대 0.5초 타임아웃)
            float t = 0f;
            while (!IsInAttack() && t < 0.5f)
            {
                yield return null; t += Time.deltaTime;
            }

            // 4) 공격이 끝날 때까지 대기
            //  - Attack 태그의 상태가 더 이상 아니거나
            //  - (전이 중이 아니고) normalizedTime >= 0.99
            while (true)
            {
                var st = animator.GetCurrentAnimatorStateInfo(layerIndex);

                bool finished =
                    (!IsInAttack()) ||
                    (!animator.IsInTransition(layerIndex) && st.normalizedTime >= 0.99f);

                if (finished) break;
                yield return null;
            }

            // 이제 Idle로 자동 복귀(애니메이터 전이 설정) → 다음 루프에서 Idle 대기 후 다음 공격
        }
    }

    string PickTrigger()
    {
        if (triggers.Count == 0) return null;

        if (randomOrder)
        {
            int i = Random.Range(0, triggers.Count);
            return triggers[i];
        }
        else
        {
            var s = triggers[_idx];
            _idx = (_idx + 1) % triggers.Count;
            return s;
        }
    }

    bool IsInAttack()
    {
        var st = animator.GetCurrentAnimatorStateInfo(layerIndex);
        return st.tagHash == _attackTagHash;
    }
}
