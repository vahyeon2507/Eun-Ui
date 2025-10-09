using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BulgasariAttackAI : MonoBehaviour
{
    public enum FireMode
    {
        AnimatorTrigger,  // 애니메이터 트리거를 쏴서 애니메이션+이벤트로 공격
        DirectExec        // 애니 없이 훅스에서 바로 히트 박스 실행
    }

    [Header("Refs")]
    public BulgasariAttackHooks hooks;   // 어택 테이블/실행 담당
    public Animator animator;            // AnimatorTrigger 모드일 때 사용
    public Transform player;             // (선택) 사거리 조건에 쓸 플레이어

    [Header("Loop / Cadence")]
    public bool runOnStart = true;
    public Vector2 intervalRange = new Vector2(1.2f, 2.0f); // 공격 간 최소/최대 대기
    public float thinkTick = 0.1f;                          // 후보가 없을 때 폴링 주기
    public FireMode fireMode = FireMode.AnimatorTrigger;

    [Header("Distance Gate (optional)")]
    public bool gateByDistance = false;
    public float minDistance = 0f;    // 이보다 가까워야/멀어야 등의 용도
    public float maxDistance = 999f;

    [System.Serializable]
    public class AttackSlot
    {
        [Tooltip("hooks의 Attack Table에 등록된 ID와 같아야 함")]
        public string id = "Clap";

        [Tooltip("AnimatorTrigger 모드일 때 쏠 트리거 이름")]
        public string animTrigger = "Clap";

        [Tooltip("랜덤 선택 가중치")]
        public float weight = 1f;

        [Tooltip("해당 공격의 개별 쿨다운(초)")]
        public float cooldown = 2.0f;

        [Tooltip("애니메이션이 끝나기 전 다음 공격을 막고 싶으면 대략의 바쁜 시간(초)")]
        public float busyTime = 0.8f;

        [HideInInspector] public float _cdTimer;
    }

    [Header("Attack List (랜덤 풀)")]
    public List<AttackSlot> attacks = new();

    bool _running;
    Coroutine _loop;

    void Reset()
    {
        if (!hooks) hooks = GetComponent<BulgasariAttackHooks>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!player) { var p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.transform; }
    }

    void Start()
    {
        if (runOnStart) StartAI();
    }

    public void StartAI()
    {
        if (_running) return;
        _running = true;
        _loop = StartCoroutine(Loop());
    }

    public void StopAI()
    {
        _running = false;
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator Loop()
    {
        yield return null;

        while (_running)
        {
            // 후보 준비
            int picked = PickIndex();
            if (picked < 0)
            {
                TickCooldowns(thinkTick);
                yield return new WaitForSeconds(thinkTick);
                continue;
            }

            // 실행
            var slot = attacks[picked];
            DoFire(slot);

            // 쿨다운/바쁜시간 처리
            slot._cdTimer = slot.cooldown;
            float wait = Mathf.Max(0f, Random.Range(intervalRange.x, intervalRange.y));
            float remain = Mathf.Max(wait, slot.busyTime);
            while (remain > 0f)
            {
                float dt = Time.deltaTime;
                TickCooldowns(dt);
                remain -= dt;
                yield return null;
            }
        }
    }

    int PickIndex()
    {
        if (attacks == null || attacks.Count == 0 || hooks == null) return -1;

        // 사거리 조건
        bool distanceOK = true;
        if (gateByDistance && player)
        {
            float d = Mathf.Abs(player.position.x - transform.position.x);
            distanceOK = (d >= minDistance && d <= maxDistance);
        }
        if (!distanceOK) return -1;

        // 사용 가능 + 가중치 목록
        float totalW = 0f;
        List<int> candidates = new();
        List<float> weights = new();

        for (int i = 0; i < attacks.Count; i++)
        {
            var s = attacks[i];
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            if (s._cdTimer > 0f) continue;
            // hooks에 ID가 존재하는지도 체크
            if (!hooks.HasAttackId(s.id)) continue;

            candidates.Add(i);
            float w = Mathf.Max(0.0001f, s.weight);
            weights.Add(w);
            totalW += w;
        }

        if (candidates.Count == 0) return -1;

        // 가중 랜덤
        float r = Random.value * totalW;
        float acc = 0f;
        for (int k = 0; k < candidates.Count; k++)
        {
            acc += weights[k];
            if (r <= acc) return candidates[k];
        }
        return candidates[candidates.Count - 1];
    }

    void DoFire(AttackSlot s)
    {
        if (fireMode == FireMode.DirectExec)
        {
            hooks.ExecById(s.id); // 애니 없이 바로 히트
            return;
        }

        if (animator && !string.IsNullOrEmpty(s.animTrigger))
            animator.SetTrigger(s.animTrigger);
        else
            hooks.ExecById(s.id); // 안전망
    }

    void TickCooldowns(float dt)
    {
        for (int i = 0; i < attacks.Count; i++)
            if (attacks[i] != null && attacks[i]._cdTimer > 0f)
                attacks[i]._cdTimer -= dt;
    }

    // === 편의 기능 ===
#if UNITY_EDITOR
    [ContextMenu("Import IDs from Hooks")]
    void ImportFromHooks()
    {
        if (hooks == null) return;
        var ids = hooks.GetAttackIds();
        attacks = new List<AttackSlot>();
        foreach (var id in ids)
        {
            attacks.Add(new AttackSlot
            {
                id = id,
                animTrigger = id, // 트리거 이름을 ID와 같게 시작(원하면 바꿔)
                weight = 1f,
                cooldown = 1.5f,
                busyTime = 0.7f
            });
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
