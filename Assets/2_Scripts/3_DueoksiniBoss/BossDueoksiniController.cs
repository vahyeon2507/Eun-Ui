using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossDueoksiniController : MonoBehaviour
{
    // =========================
    // Refs
    // =========================
    [Header("Refs")]
    public Transform player; // (비워두면 Tag=Player 자동 탐색)

    // === Graphics/Animator (자식으로 분리돼도 OK) ===
    [Header("Graphics (optional)")]
    [Tooltip("보스의 애니메이터(보통 GfxRoot). 비워두면 자식에서 자동 탐색")]
    public Animator gfxAnimator;
    [Tooltip("보스의 스프라이트 렌더러(자식). 비워두면 자식에서 자동 탐색")]
    public SpriteRenderer gfxRenderer;
    [Tooltip("좌우 반전을 적용할 트랜스폼(보통 FacingPivot). 비워두면 렌더러/애니메이터 Transform → 없으면 루트")]
    public Transform gfxFlipRoot;

    // =========================
    // Charge (dash) flow
    // =========================
    [Header("Charge")]
    [Tooltip("돌진 총 가로 거리(+) = 우 / (–) = 좌. 실제로는 플레이어 방향을 보고 부호를 정함")]
    public float chargeDistance = 6f;
    [Tooltip("돌진에 걸리는 시간(초)")]
    public float chargeTime = 0.6f;
    [Tooltip("벽/바닥 레이어(여기에 맞으면 앞에서 정지)")]
    public LayerMask environmentMask;
    [Tooltip("벽 앞 세이프 간격")]
    public float wallSkin = 0.1f;
    [Tooltip("돌진 시작 애니 트리거(선택)")]
    public string chargeTrigger = "Charge";
    [Tooltip("돌진 중 true로 올릴 애니 Bool(선택)")]
    public string isChargingBool = "isCharging";

    [Header("Face/Move")]
    [Tooltip("행동 진입 시 플레이어를 바라보게 좌우 반전")]
    public bool facePlayer = true;

    // =========================
    // Charge-Prep (single)
    // =========================
    [Header("Charge ▸ Prep (single)")]
    [Tooltip("돌진 직전 준비 모션(하나만). 비우면 준비 없이 바로 돌진/점프 판정")]
    public string chargePrepTrigger = "Prep_Charge";
    [Tooltip("애니메이션 이벤트로 종료할지 여부 (AnimEvent_PrepReady 호출)")]
    public bool chargePrepEndByAnimEvent = false;
    [Tooltip("이벤트를 쓰지 않는다면 대기 시간(초)")]
    public float chargePrepHoldTime = 0.5f;

    [Header("Charge ▸ Far → Jump branch")]
    [Tooltip("돌진 준비 끝 시점에 플레이어와 X거리 절대값이 이 값보다 크면, 돌진 대신 점프 공격으로 분기")]
    public float farDistanceThreshold = 10f;

    // =========================
    // Jump attack (when far)
    // =========================
    [Header("Jump Attack (when far at Charge-Prep)")]
    public float jumpDuration = 0.8f;
    public float jumpArcHeight = 3.5f;
    public string jumpTrigger = "JumpAtk";
    public bool jumpEndsWithSlam = true;

    // =========================
    // Simple Attacks (커스텀 공격 프리셋)
    // =========================
    [System.Serializable]
    public class SimpleAttack
    {
        [Header("Animator")]
        public string name = "SimpleAttack";
        [Tooltip("공격 시 쏠 애니 트리거(비우면 트리거 안 보냄)")]
        public string attackTrigger;

        [Header("Hit Window")]
        [Tooltip("애니메이션 이벤트(AnimEvent_GenericHitOn/Off)로 히트창을 제어할지")]
        public bool useAnimEvent = false;
        [Tooltip("useAnimEvent=false일 때, 히트 활성 지속시간")]
        public float activeTime = 0.12f;
        [Tooltip("이 공격에서 켜고 끌 히트박스들")]
        public List<Collider2D> hitboxes = new List<Collider2D>();

        [Header("Move During Attack (optional)")]
        [Tooltip("공격 중 전진/후퇴 거리(+전진, -후퇴). 좌우는 바라보는 방향 기준")]
        public float moveDistance = 0f;
        [Tooltip("이동에 걸리는 시간(초). 0이면 이동 안 함")]
        public float moveTime = 0f;
        [Tooltip("이동 보간 커브(0→1). 비우면 EaseInOut")]
        public AnimationCurve moveCurve;
    }

    [Header("Attack ▸ Simple (recommended)")]
    public List<SimpleAttack> simpleAttacks = new List<SimpleAttack>();

    // =========================
    // Prep & Attack mapping
    // =========================
    public enum AttackKind { Simple, ProjectileBurst, GroundSlam }

    [System.Serializable]
    public struct SimpleChoice
    {
        [Tooltip("위 Simple Attacks 리스트의 인덱스")]
        public int simpleIndex;
        [Tooltip("가중치(랜덤 선택용)")]
        public float weight;
    }

    [System.Serializable]
    public class PrepOption
    {
        [Header("Prep")]
        public string prepTrigger = "Atk_Swipe";
        public float prepHoldTime = 0.5f;
        public float weight = 1f;
        [Tooltip("애니 이벤트 AnimEvent_PrepReady 로 종료할지")]
        public bool useAnimEventEnd = false;

        [Header("Attack mapping")]
        public AttackKind attack = AttackKind.Simple;

        [Tooltip("단일 Simple 공격으로 매핑하고 싶을 때(레거시).")]
        public int simpleIndex = 0;

        [Tooltip("하나의 준비에서 여러 Simple 공격을 번갈아/랜덤으로 쓰고 싶을 때")]
        public List<SimpleChoice> simpleChoices = new List<SimpleChoice>();

        [Tooltip("선택) 이 준비에서만 공격 트리거를 오버라이드하고 싶다면 입력")]
        public string attackTriggerOverride = null;
    }

    [Header("Prep & Attack")]
    public List<PrepOption> preps = new List<PrepOption>();
    [Tooltip("Attack-Prep 전체에 대해 애니 이벤트 종료를 강제. (PrepOption.useAnimEventEnd 가 개별 우선)")]
    public bool attackPrepEndByAnimEvent = false;

    // =========================
    // Legacy Attacks (유지)
    // =========================
    [Header("Attack ▸ Projectile Burst")]
    public GameObject projectilePrefab;
    public List<Transform> projectileMuzzles = new List<Transform>();
    public int projectileCount = 6;
    public float projectileInterval = 0.07f;
    public float projectileSpeed = 12f;

    [Header("Attack ▸ Ground Slam")]
    public List<Collider2D> slamHitboxes = new List<Collider2D>();
    public float slamActiveTime = 0.15f;
    public float slamShakeAmp = 0.8f;
    public float slamShakeDur = 0.2f;
    public float slamShakeFreq = 22f;

    // =========================
    // Internals
    // =========================
    Animator _anim;
    Rigidbody2D _rb;
    int _lastPrepIndex = -1;
    bool _waitingPrepEvent = false;
    bool _inRoutine = false;

    // 애니 이벤트에서 토글할 현재 히트박스 묶음(심플 공격용)
    List<Collider2D> _currentHitboxes;

    void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        if (!gfxAnimator) gfxAnimator = GetComponentInChildren<Animator>(true);
        if (!gfxRenderer) gfxRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (!gfxFlipRoot) gfxFlipRoot = gfxRenderer ? gfxRenderer.transform
                              : (gfxAnimator ? gfxAnimator.transform : transform);

        _anim = gfxAnimator;
        _rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (!_inRoutine) StartCoroutine(MainLoop());
    }

    // =========================
    // Main FSM
    // =========================
    IEnumerator MainLoop()
    {
        _inRoutine = true;

        while (true)
        {
            if (facePlayer) FaceTowardPlayer();

            // 1) Charge-Prep
            yield return DoChargePrep();

            // 멀리면 점프, 아니면 돌진
            bool far = IsPlayerFar(farDistanceThreshold);
            if (far) { yield return DoJumpAttack(); continue; }
            else { yield return DoCharge(); }

            if (facePlayer) FaceTowardPlayer();

            // 2) Attack Prep pick → 3) Attack
            var prep = PickPrep();
            yield return DoAttackPrep(prep);
            yield return DoAttack(prep);
        }
    }

    // =========================
    // Steps
    // =========================
    IEnumerator DoChargePrep()
    {
        if (!string.IsNullOrEmpty(chargePrepTrigger) && _anim)
            _anim.SetTrigger(chargePrepTrigger);

        if (chargePrepEndByAnimEvent)
        {
            _waitingPrepEvent = true;
            while (_waitingPrepEvent) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(chargePrepHoldTime);
        }
    }

    IEnumerator DoCharge()
    {
        if (_anim && !string.IsNullOrEmpty(chargeTrigger)) _anim.SetTrigger(chargeTrigger);
        if (_anim && !string.IsNullOrEmpty(isChargingBool)) _anim.SetBool(isChargingBool, true);

        int dir = DirToPlayer();
        float startX = transform.position.x;
        float targetX = startX + dir * Mathf.Abs(chargeDistance);

        // 벽 체크
        float rayLen = Mathf.Abs(chargeDistance) + wallSkin;
        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(dir, 0f), rayLen, environmentMask);
        if (hit.collider) targetX = hit.point.x - dir * wallSkin;

        // 시간 보간 (Y 고정)
        float t = 0f;
        Vector3 p0 = transform.position;
        Vector3 p1 = new Vector3(targetX, p0.y, p0.z);

        while (t < chargeTime)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, chargeTime));
            float k = EaseInOut(a);
            Vector3 pos = Vector3.Lerp(p0, p1, k);
            pos.y = p0.y;
            transform.position = pos;
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = p1;

        if (_anim && !string.IsNullOrEmpty(isChargingBool)) _anim.SetBool(isChargingBool, false);
    }

    IEnumerator DoJumpAttack()
    {
        if (_anim && !string.IsNullOrEmpty(jumpTrigger))
            _anim.SetTrigger(jumpTrigger);

        Vector3 start = transform.position;
        Vector3 end = new Vector3(player ? player.position.x : start.x, start.y, start.z);

        if (facePlayer) FaceTowardPlayer();

        float t = 0f;
        while (t < jumpDuration)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, jumpDuration));
            float yOffset = 4f * jumpArcHeight * a * (1f - a);
            float x = Mathf.Lerp(start.x, end.x, a);
            float y = start.y + yOffset;
            transform.position = new Vector3(x, y, start.z);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = new Vector3(end.x, start.y, start.z);

        if (jumpEndsWithSlam) yield return DoSlamOnce();
    }

    PrepOption PickPrep()
    {
        if (preps == null || preps.Count == 0) return new PrepOption();

        // 직전 동일 회피 + 가중치
        int n = preps.Count;
        float total = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i == _lastPrepIndex) continue;
            total += Mathf.Max(0f, preps[i].weight);
        }
        float r = Random.value * (total <= 0f ? 1f : total);
        for (int i = 0; i < n; i++)
        {
            if (i == _lastPrepIndex) continue;
            float w = Mathf.Max(0f, preps[i].weight);
            if (r < w) { _lastPrepIndex = i; return preps[i]; }
            r -= w;
        }
        _lastPrepIndex = 0;
        return preps[0];
    }

    IEnumerator DoAttackPrep(PrepOption opt)
    {
        if (_anim && !string.IsNullOrEmpty(opt.prepTrigger))
            _anim.SetTrigger(opt.prepTrigger);

        bool useEvent = attackPrepEndByAnimEvent || opt.useAnimEventEnd;

        if (useEvent)
        {
            _waitingPrepEvent = true;
            while (_waitingPrepEvent) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0f, opt.prepHoldTime));
        }
    }

    IEnumerator DoAttack(PrepOption opt)
    {
        switch (opt.attack)
        {
            case AttackKind.Simple:
                {
                    int idx = PickSimpleIndex(opt);
                    if (idx >= 0 && idx < simpleAttacks.Count)
                        yield return DoSimpleAttack(simpleAttacks[idx], opt.attackTriggerOverride);
                    break;
                }
            case AttackKind.ProjectileBurst:
                yield return DoProjectileBurst();
                break;

            case AttackKind.GroundSlam:
                yield return DoSlamOnce();
                break;
        }
    }

    int PickSimpleIndex(PrepOption opt)
    {
        if (opt.simpleChoices != null && opt.simpleChoices.Count > 0)
        {
            float total = 0f;
            foreach (var c in opt.simpleChoices) total += Mathf.Max(0f, c.weight);
            if (total <= 0f) return Mathf.Clamp(opt.simpleChoices[0].simpleIndex, 0, simpleAttacks.Count - 1);

            float r = Random.value * total;
            foreach (var c in opt.simpleChoices)
            {
                float w = Mathf.Max(0f, c.weight);
                if (r < w) return Mathf.Clamp(c.simpleIndex, 0, simpleAttacks.Count - 1);
                r -= w;
            }
        }
        return Mathf.Clamp(opt.simpleIndex, 0, simpleAttacks.Count - 1);
    }

    // =========================
    // Simple Attack executor
    // =========================
    IEnumerator DoSimpleAttack(SimpleAttack sa, string triggerOverride)
    {
        // 1) 애니 트리거
        string trig = string.IsNullOrEmpty(triggerOverride) ? sa.attackTrigger : triggerOverride;
        if (_anim && !string.IsNullOrEmpty(trig))
            _anim.SetTrigger(trig);

        // 2) 이동(선택)
        Coroutine moveCR = null;
        if (sa.moveTime > 0f && Mathf.Abs(sa.moveDistance) > 0f)
            moveCR = StartCoroutine(CoAdvance(sa.moveDistance, sa.moveTime, sa.moveCurve));

        // 3) 히트창
        if (sa.useAnimEvent)
        {
            _currentHitboxes = sa.hitboxes; // 애니 이벤트가 On/Off 토글
            // 애니 길이를 모르면 안전하게 activeTime 만큼만 대기(0이면 한 프레임)
            float wait = Mathf.Max(0.01f, sa.activeTime);
            yield return new WaitForSeconds(wait);
        }
        else
        {
            ToggleColliders(sa.hitboxes, true);
            yield return new WaitForSeconds(Mathf.Max(0.01f, sa.activeTime));
            ToggleColliders(sa.hitboxes, false);
        }

        if (moveCR != null) yield return moveCR;
        _currentHitboxes = null;
    }

    IEnumerator CoAdvance(float distance, float time, AnimationCurve curve)
    {
        float dirSign = Mathf.Sign((gfxFlipRoot ? gfxFlipRoot.localScale.x : transform.localScale.x));
        Vector3 p0 = transform.position;
        Vector3 p1 = p0 + Vector3.right * (distance * dirSign);

        float t = 0f;
        while (t < time)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            float k = curve != null && curve.keys != null && curve.length > 0 ? curve.Evaluate(a) : EaseInOut(a);
            transform.position = Vector3.Lerp(p0, p1, k);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = p1;
    }

    // =========================
    // Legacy Attacks
    // =========================
    IEnumerator DoProjectileBurst()
    {
        if (!projectilePrefab || projectileMuzzles.Count == 0) yield break;

        for (int i = 0; i < projectileCount; i++)
        {
            foreach (var m in projectileMuzzles)
            {
                if (!m) continue;
                var go = GameObject.Instantiate(projectilePrefab, m.position, m.rotation);
                var rb = go.GetComponent<Rigidbody2D>();
                Vector2 dir = Vector2.right * DirToPlayer();
                if (player) dir = (player.position - m.position).normalized;
                if (rb) rb.linearVelocity = dir * projectileSpeed;
            }
            if (projectileInterval > 0f) yield return new WaitForSeconds(projectileInterval);
            else yield return null;
        }
    }

    IEnumerator DoSlamOnce()
    {
        var cam = Camera.main ? Camera.main.GetComponent<CameraSimple2D>() : null;
        if (cam) cam.Shake(slamShakeAmp, slamShakeDur, slamShakeFreq);

        ToggleColliders(slamHitboxes, true);
        yield return new WaitForSeconds(slamActiveTime);
        ToggleColliders(slamHitboxes, false);
    }

    // =========================
    // Helpers
    // =========================
    bool IsPlayerFar(float threshold)
    {
        if (!player) return false;
        return Mathf.Abs(player.position.x - transform.position.x) > Mathf.Abs(threshold);
    }

    int DirToPlayer()
    {
        if (!player) return transform.localScale.x >= 0f ? 1 : -1;
        return (player.position.x - transform.position.x) >= 0f ? 1 : -1;
    }

    void FaceTowardPlayer()
    {
        int dir = DirToPlayer();
        var target = gfxFlipRoot ? gfxFlipRoot : transform;
        Vector3 s = target.localScale;
        s.x = Mathf.Abs(s.x) * (dir >= 0 ? 1f : -1f);
        target.localScale = s;
    }

    static void ToggleColliders(List<Collider2D> list, bool on)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            if (list[i]) list[i].enabled = on;
    }

    static float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // =========================
    // Animation Events
    // =========================
    public void AnimEvent_PrepReady() { _waitingPrepEvent = false; }

    // 심플 공격용 범용 On/Off (현재 선택된 _currentHitboxes 토글)
    public void AnimEvent_GenericHitOn() { if (_currentHitboxes != null) ToggleColliders(_currentHitboxes, true); }
    public void AnimEvent_GenericHitOff() { if (_currentHitboxes != null) ToggleColliders(_currentHitboxes, false); }

    // 기존 이벤트 유지(과거 애니들이 참조할 수 있으니)
    public void AnimEvent_SwipeHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SwipeHitOff() { ToggleColliders(slamHitboxes, false); }
    public void AnimEvent_SlamHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SlamHitOff() { ToggleColliders(slamHitboxes, false); }

    // =========================
    // Gizmos
    // =========================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 a = transform.position;
        float sign = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
        Vector3 b = a + Vector3.right * sign * chargeDistance;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(b, 0.08f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, Mathf.Abs(farDistanceThreshold));
    }
}
