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

    [SerializeField] private DirectionalAnimPairSync pairSync; // GfxRoot 쪽 컴포넌트 연결
    public bool FacingRight { get; private set; } = true;

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
    [Tooltip("돌진 애니 베이스 이름(예: Charge → _R/_L 상태 사용)")]
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
    [Tooltip("좌우 페어의 베이스 이름(예: Prep_Charge → _R/_L 상태 사용)")]
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
        [Tooltip("애니 이벤트(AnimEvent_GenericHitOn/Off)로 히트창을 제어할지")]
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
        public int simpleIndex;
        public float weight;
    }

    [System.Serializable]
    public class PrepOption
    {
        [Header("Prep")]
        public string prepTrigger = "Atk_Swipe";
        public float prepHoldTime = 0.5f;
        public float weight = 1f;
        public bool useAnimEventEnd = false;

        [Header("Attack mapping")]
        public AttackKind attack = AttackKind.Simple;

        // 레거시 단일
        public int simpleIndex = 0;

        // 다중 랜덤 선택
        public List<SimpleChoice> simpleChoices = new List<SimpleChoice>();

        // 이 준비에서만 트리거 오버라이드
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

        if (!pairSync) pairSync = GetComponentInChildren<DirectionalAnimPairSync>(true);

        _anim = gfxAnimator;
        _rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (facePlayer) FaceTowardPlayer(); // FacingRight 초기화
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

            // 2) 멀리면 점프 → 루프, 아니면 돌진+즉시 공격 시퀀스
            bool far = IsPlayerFar(farDistanceThreshold);
            if (far)
            {
                yield return DoJumpAttack();
                continue;
            }
            else
            {
                // ✅ 돌진이 끝나자마자 공격 준비/공격으로 체인
                yield return DoCharge(chainToAttack: true);
                continue;
            }
        }
    }

    // =========================
    // Steps
    // =========================
    IEnumerator DoChargePrep()
    {
        if (_anim && !string.IsNullOrEmpty(chargePrepTrigger))
            PlayDirectionalState(chargePrepTrigger, 0f);

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

    // chainToAttack: 도착 즉시 Prep→Attack으로 연결
    IEnumerator DoCharge(bool chainToAttack = false)
    {
        if (_anim && !string.IsNullOrEmpty(chargeTrigger))
            PlayDirectionalState(chargeTrigger, 0f);

        if (_anim && !string.IsNullOrEmpty(isChargingBool))
            _anim.SetBool(isChargingBool, true);

        int dir = DirToPlayer();
        float startX = transform.position.x;
        float targetX = startX + dir * Mathf.Abs(chargeDistance);

        // 벽 체크
        float rayLen = Mathf.Abs(chargeDistance) + wallSkin;
        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(dir, 0f),
            rayLen,
            environmentMask
        );
        if (hit.collider)
            targetX = hit.point.x - dir * wallSkin;

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

        if (_anim && !string.IsNullOrEmpty(isChargingBool))
            _anim.SetBool(isChargingBool, false);

        // ✅ 도착 즉시 공격 준비/공격으로 체인
        if (chainToAttack)
        {
            if (facePlayer) FaceTowardPlayer();
            var prep = PickPrep();
            yield return DoAttackPrep(prep);
            yield return DoAttack(prep);
        }
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
        float dirSign = FacingRight ? 1f : -1f;
        Vector3 p0 = transform.position;
        Vector3 p1 = p0 + Vector3.right * (distance * dirSign);

        float t = 0f;
        while (t < time)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            float k = (curve != null && curve.keys != null && curve.length > 0) ? curve.Evaluate(a) : EaseInOut(a);
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
        if (!player) return FacingRight ? 1 : -1;
        return (player.position.x - transform.position.x) >= 0f ? 1 : -1;
    }

    void SetFacing(bool right)
    {
        FacingRight = right;

        if (pairSync) pairSync.facingRight = right;

        if (gfxFlipRoot)
        {
            var s = gfxFlipRoot.localScale;
            s.x = Mathf.Abs(s.x); // 스케일 뒤집기 금지(좌/우 별도 애니 사용)
            gfxFlipRoot.localScale = s;
        }

        if (gfxRenderer) gfxRenderer.flipX = false;
    }

    public void FaceRight() => SetFacing(true);
    public void FaceLeft() => SetFacing(false);

    void FaceTowardPlayer()
    {
        if (!player) return;
        bool right = (player.position.x - transform.position.x) >= 0f;
        SetFacing(right);
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

    public void AnimEvent_GenericHitOn() { if (_currentHitboxes != null) ToggleColliders(_currentHitboxes, true); }
    public void AnimEvent_GenericHitOff() { if (_currentHitboxes != null) ToggleColliders(_currentHitboxes, false); }

    // 레거시 이름 유지 → 범용으로 위임
    public void AnimEvent_SwipeHitOn() { AnimEvent_GenericHitOn(); }
    public void AnimEvent_SwipeHitOff() { AnimEvent_GenericHitOff(); }

    public void AnimEvent_SlamHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SlamHitOff() { ToggleColliders(slamHitboxes, false); }

    // =========================
    // Gizmos
    // =========================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 a = transform.position;
        float sign = FacingRight ? 1f : -1f;
        Vector3 b = a + Vector3.right * sign * chargeDistance;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(b, 0.08f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, Mathf.Abs(farDistanceThreshold));
    }

    // =========================
    // Directional play helper
    // =========================
    bool PlayDirectionalState(string baseName, float crossFade = 0f)
    {
        if (string.IsNullOrEmpty(baseName) || !_anim) return false;

        // 1) pairSync가 있으면 우선 사용
        if (pairSync != null && pairSync.PlayByBase(baseName, crossFade))
            return true;

        // 2) 폴백: 접미사(_R/_L)로 직접 크로스페이드
        string suffix = (FacingRight ? pairSync?.rightSuffix : pairSync?.leftSuffix) ?? (FacingRight ? "_R" : "_L");
        string state = baseName + suffix;
        _anim.CrossFadeInFixedTime(state, crossFade);
        return true;
    }
}
