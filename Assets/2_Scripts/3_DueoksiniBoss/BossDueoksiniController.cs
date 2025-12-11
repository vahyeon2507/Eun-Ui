using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // ★ 분노 게이지 UI용

[DisallowMultipleComponent]
public class BossDueoksiniController : MonoBehaviour
{
    // 내부 상태
    public bool IsChargingNow { get; private set; } = false;
    bool _isStunned = false;

    // ===== Refs =====
    [Header("Refs")]
    public Transform player;

    [Header("Graphics (optional)")]
    public Animator gfxAnimator;
    public SpriteRenderer gfxRenderer;
    public Transform gfxFlipRoot;

    [SerializeField] private DirectionalAnimPairSync pairSync;
    public bool FacingRight { get; private set; } = true;

    // ===== Charge =====
    [Header("Charge")]
    public float chargeDistance = 6f;
    public float chargeTime = 0.6f;
    public LayerMask environmentMask;
    public float wallSkin = 0.1f;
    public string chargeTrigger = "Charge";
    public string isChargingBool = "isCharging";

    [Header("Charge ▸ Contact Damage")]
    [Tooltip("돌진 중 본인 콜라이더에 플레이어가 닿았을 때 줄 대미지 (0 이하면 비활성화)")]
    public int chargeContactDamage = 0;

    [Tooltip("접촉 판정에 사용할 플레이어 레이어 마스크")]
    public LayerMask chargePlayerLayer;

    [Tooltip("접촉 판정에 사용할 콜라이더 (비워두면 이 오브젝트의 Collider2D 사용)")]
    public Collider2D chargeDamageCollider;

    [Header("Charge ▸ Cancel When Far")]
    [Tooltip("돌진 중 플레이어와의 거리가 이 값보다 커지면 돌진을 조기 종료 (0 이하면 비활성화)")]
    public float chargeCancelDistance = 0f;

    [Tooltip("true면 2D 거리(Vector2.Distance), false면 X축 거리만 사용")]
    public bool chargeCancelUse2DDistance = true;

    [Header("Face/Move")]
    public bool facePlayer = true;

    // ===== Charge-Prep =====
    [Header("Charge ▸ Prep (single)")]
    public string chargePrepTrigger = "Prep_Charge";
    public bool chargePrepEndByAnimEvent = false;
    public float chargePrepHoldTime = 0.5f;

    [Header("Charge ▸ Far → Jump branch")]
    public float farDistanceThreshold = 10f;

    // ===== Jump =====
    [Header("Jump Attack (when far at Charge-Prep)")]
    public float jumpDuration = 0.8f;
    public float jumpArcHeight = 3.5f;
    public string jumpTrigger = "JumpAtk";
    public bool jumpEndsWithSlam = true;

    // ===== Simple Attacks =====
    [System.Serializable]
    public class SimpleAttack
    {
        [Header("Animator")]
        public string name = "SimpleAttack";
        public string attackTrigger;

        [Header("Hit Window")]
        [Tooltip("true면 히트박스 On/Off를 애니메이션 이벤트(AnimEvent_GenericHitOn/Off)로 제어")]
        public bool useAnimEvent = false;

        [Tooltip("useAnimEvent=false일 때 자동으로 켜져있는 시간(초)")]
        public float activeTime = 0.12f;

        [Tooltip("방향과 상관없이 공통으로 쓸 히트박스(선택). 좌/우 리스트가 비어 있을 때 폴백으로 사용")]
        public List<Collider2D> hitboxes = new List<Collider2D>();

        [Tooltip("오른쪽을 바라볼 때 우선 사용할 히트박스들")]
        public List<Collider2D> rightHitboxes = new List<Collider2D>();

        [Tooltip("왼쪽을 바라볼 때 우선 사용할 히트박스들")]
        public List<Collider2D> leftHitboxes = new List<Collider2D>();

        [Header("Damage")]
        [Tooltip("AnimEvent_SimpleDamage() 호출 시 기본으로 사용할 대미지")]
        public int baseDamage = 1;

        [Header("Strong Attack")]
        [Tooltip("체크 시 이 SimpleAttack은 '분노 강공격'으로 취급된다.")]
        public bool isStrongAttack = false;

        [Header("Move During Attack (optional)")]
        public float moveDistance = 0f;
        public float moveTime = 0f;
        public AnimationCurve moveCurve;

        // ---------- AnimEvent Move ----------
        [Header("AnimEvent Move (optional)")]
        [Tooltip("true면 이 공격은 AnimEvent_MoveStart/MoveStop 이벤트로 이동을 제어한다. CoAdvance는 사용하지 않음.")]
        public bool useAnimEventMove = false;

        [Tooltip("AnimEvent_MoveStart가 호출된 순간부터 목표 지점까지 도달하는데 걸리는 시간(초). " +
                 "이 값을 애니에서 Start~Stop 사이 시간과 맞추면 Stop 프레임에 딱 도착.")]
        public float animEventMoveDuration = 0.2f;

        // ---------- Projectile (event-driven) ----------
        [Header("Projectile (optional)")]
        [Tooltip("체크 시, 어택 시작 시 자동 1회 발사(이벤트와 중복 방지).")]
        public bool spawnProjectileAuto = false;

        [Tooltip("SlashProjectile2D 붙은 프리팹")]
        public GameObject projectilePrefab;

        [Tooltip("오른쪽을 볼 때 쓸 머즐(없으면 공용/오프셋 사용)")]
        public Transform projectileMuzzleRight;

        [Tooltip("왼쪽을 볼 때 쓸 머즐(없으면 공용/오프셋 사용)")]
        public Transform projectileMuzzleLeft;

        [Tooltip("공용 머즐(좌우 공통). 비어있으면 오프셋 사용")]
        public Transform projectileMuzzle;

        [Tooltip("머즐이 없으면 사용하는 오프셋(보스 기준). X는 좌/우에 따라 ± 적용")]
        public Vector2 projectileMuzzleOffset = new Vector2(0.6f, 0.9f);

        [Tooltip("플레이어 쪽으로 조준(켜면 좌/우 대신 목표 방향 사용)")]
        public bool projAimAtPlayer = false;
    }

    [Header("Attack ▸ Simple (recommended)")]
    public List<SimpleAttack> simpleAttacks = new List<SimpleAttack>();

    // ===== 분노 / 강공격 게이지 (Fill UI) =====
    [Header("Rage / Strong Attack")]
    [Tooltip("분노 게이지 최대 스택 수 (칸 수)")]
    public int rageMaxStacks = 3;

    [Tooltip("자동으로 분노 1스택이 차오르는 시간(초). 6초면 6초마다 한 칸 분량이 찬다.")]
    public float rageAutoInterval = 6f;

    [Tooltip("각 분노 칸의 Image (왼쪽→오른쪽). Image.type=Filled 로 설정해야 함.")]
    public Image[] rageFillImages;

    [Tooltip("각 분노 칸이 '막 가득' 찼을 때 재생할 FX Animator (선택)")]
    public Animator[] rageSegmentFxAnimators;

    [Tooltip("위 Animator들에 보낼 트리거 이름")]
    public string rageSegmentFxTrigger = "Play";

    [Tooltip("현재 분노 값 (0 ~ rageMaxStacks, 소수 포함)")]
    public float rageValue = 0f;

    int RageStacks => Mathf.FloorToInt(rageValue);      // 0,1,2,3...
    bool RageIsFull => RageStacks >= rageMaxStacks;

    // ===== Prep & Attack mapping =====
    public enum AttackKind { Simple, ProjectileBurst, GroundSlam }
    [System.Serializable] public struct SimpleChoice { public int simpleIndex; public float weight; }

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
        public int simpleIndex = 0;
        public List<SimpleChoice> simpleChoices = new List<SimpleChoice>();
        public string attackTriggerOverride = null;

        [Header("Prep Directional (optional)")]
        public string prepDirectionalBase;
        public float prepCrossFade = 0.05f;

        [Header("Strong Prep")]
        [Tooltip("체크 시 이 Prep은 '분노 강공격'용 준비자세로 취급된다.")]
        public bool isStrongPrep = false;
    }

    [Header("Prep & Attack")]
    public List<PrepOption> preps = new List<PrepOption>();
    public bool attackPrepEndByAnimEvent = false;

    // ===== Legacy projectile burst / slam =====
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

    // ===== Tree Wall stun =====
    [Header("Tree Wall Stun")]
    [Tooltip("TreeWall에 박았을 때 기절 시간")]
    public float wallStunDuration = 1.2f;
    [Tooltip("TreeWall에 박은 뒤 받뎀 배율")]
    public float wallVulnMultiplier = 2f;
    [Tooltip("부딪히면 나무를 즉시 부순다")]
    public bool breakTreeOnHit = true;

    // ===== Internals =====
    Animator _anim;
    Rigidbody2D _rb;
    BossHealth _health;

    int _lastPrepIndex = -1;
    bool _waitingPrepEvent = false;
    bool _inRoutine = false;

    List<Collider2D> _currentHitboxes;
    SimpleAttack _playingSimpleAttack;
    bool _autoProjFiredThisAttack = false;

    // Rage strong attack queue
    bool _strongAttackQueued = false;   // 분노 가득 찼을 때 "강공격 1회 예약"
    bool _useStrongThisCycle = false;   // 이번 공격 사이클에서 강공격을 쓸지 여부

    // ── AnimEvent-driven move state ──
    bool _animMoveActive = false;
    Vector3 _animMoveStartPos;
    Vector3 _animMoveTargetPos;
    float _animMoveElapsed = 0f;
    float _animMoveDuration = 0.2f;

    // ─────────────────────────────────────────────
    // 외부 스턴
    // ─────────────────────────────────────────────
    public void ExternalStun(float duration)
    {
        if (_isStunned) return;
        StartCoroutine(CoExternalStun(duration));
    }

    IEnumerator CoExternalStun(float duration)
    {
        _isStunned = true;
        // 돌진 플래그 해제
        IsChargingNow = false;
        if (_anim && !string.IsNullOrEmpty(isChargingBool)) _anim.SetBool(isChargingBool, false);
        // 기절 애니
        if (_anim)
        {
            foreach (var p in _anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Stun")
                { _anim.SetTrigger("Stun"); break; }
        }
        float t = 0f;
        while (t < duration) { t += Time.deltaTime; yield return null; }
        _isStunned = false;
    }

    void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        if (!gfxAnimator) gfxAnimator = GetComponentInChildren<Animator>(true);
        if (!gfxRenderer) gfxRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (!gfxFlipRoot)
            gfxFlipRoot = gfxRenderer ? gfxRenderer.transform :
                           (gfxAnimator ? gfxAnimator.transform : transform);
        if (!pairSync) pairSync = GetComponentInChildren<DirectionalAnimPairSync>(true);

        _anim = gfxAnimator;
        _rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<BossHealth>();

        UpdateRageUI(); // ★ 시작 시 분노 UI 초기화
    }

    void OnEnable()
    {
        if (!_inRoutine)
            StartCoroutine(MainLoop());
    }

    void Update()
    {
        UpdateAnimEventMove();

        // ★ 분노 자동 채우기 (6초당 1칸 분량)
        AddRageByTime(Time.deltaTime);
    }

    IEnumerator MainLoop()
    {
        _inRoutine = true;
        while (true)
        {
            // 기절 중에는 완전 정지
            while (_isStunned) yield return null;

            if (facePlayer) FaceTowardPlayer();
            yield return DoChargePrep();

            bool far = IsPlayerFar(farDistanceThreshold);
            if (far)
            {
                yield return DoJumpAttack();
                continue;
            }
            else
            {
                yield return DoCharge();
            }

            if (facePlayer) FaceTowardPlayer();

            // 이번 공격 사이클에서 강공격 쓸지 결정
            _useStrongThisCycle = _strongAttackQueued;

            var prep = PickPrep();
            yield return DoAttackPrep(prep);
            yield return DoAttack(prep);

            // 한 사이클 종료 → 플래그만 리셋 (실제 게이지 소모는 강공격 때)
            _useStrongThisCycle = false;
        }
    }

    // ───────────────── Charge Prep ─────────────────
    IEnumerator DoChargePrep()
    {
        if (_anim) PlayDirectionalState(chargePrepTrigger, 0f);

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

    // ───────────────── Charge ─────────────────
    IEnumerator DoCharge()
    {
        IsChargingNow = true;
        if (_anim)
        {
            PlayDirectionalState(chargeTrigger);
            if (!string.IsNullOrEmpty(isChargingBool))
                _anim.SetBool(isChargingBool, true);
        }

        int dir = DirToPlayer();
        float startX = transform.position.x;
        float targetX = startX + dir * Mathf.Abs(chargeDistance);

        float rayLen = Mathf.Abs(chargeDistance) + wallSkin;
        RaycastHit2D hit = Physics2D.Raycast((Vector2)transform.position, new Vector2(dir, 0f), rayLen, environmentMask);

        // TreeWall 맞았는지 확인
        TreeWall hitTree = null;
        if (hit.collider)
        {
            targetX = hit.point.x - dir * wallSkin;
            hitTree = hit.collider.GetComponent<TreeWall>() ?? hit.collider.GetComponentInParent<TreeWall>();
        }

        float t = 0f;
        Vector3 p0 = transform.position;
        Vector3 p1 = new Vector3(targetX, p0.y, p0.z);

        // 접촉 판정용 콜라이더
        Collider2D damageCol = chargeDamageCollider;
        if (!damageCol)
            damageCol = GetComponent<Collider2D>();

        bool didContactDamage = false;
        bool earlyCanceled = false;

        while (t < chargeTime)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, chargeTime));
            float k = EaseInOut(a);
            Vector3 pos = Vector3.Lerp(p0, p1, k);
            pos.y = p0.y;
            transform.position = pos;
            t += Time.deltaTime;

            // 1) 돌진 중 접촉 대미지 (한 번만)
            if (!didContactDamage && chargeContactDamage > 0 && damageCol && chargePlayerLayer.value != 0)
            {
                Bounds b = damageCol.bounds;
                Vector2 center = b.center;
                Vector2 size = b.size;

                Collider2D hitCol = Physics2D.OverlapBox(center, size, 0f, chargePlayerLayer);
                if (hitCol != null)
                {
                    var dmg = hitCol.GetComponent<IDamageable>()
                           ?? hitCol.GetComponentInParent<IDamageable>()
                           ?? hitCol.GetComponentInChildren<IDamageable>();

                    if (dmg is PlayerHealth ph)
                        ph.TakeDamageFromHitbox(chargeContactDamage, hitCol);
                    else if (dmg != null)
                        dmg.TakeDamage(chargeContactDamage);

                    didContactDamage = true;
                }
            }

            // 2) 플레이어와 너무 멀어지면 돌진 조기 종료
            if (chargeCancelDistance > 0f && player)
            {
                float dist = chargeCancelUse2DDistance
                    ? Vector2.Distance(transform.position, player.position)
                    : Mathf.Abs(player.position.x - transform.position.x);

                if (dist > chargeCancelDistance)
                {
                    earlyCanceled = true;
                    break;
                }
            }

            yield return null;
        }

        // 정상 종료면 목표 지점까지 이동
        if (!earlyCanceled)
            transform.position = p1;

        if (_anim && !string.IsNullOrEmpty(isChargingBool))
            _anim.SetBool(isChargingBool, false);
        IsChargingNow = false;

        // TreeWall 충돌 처리 (조기 캔슬이 아닐 때만)
        if (hitTree != null && !earlyCanceled)
        {
            if (breakTreeOnHit) hitTree.BreakAndDestroy();
            if (_health) _health.ApplyVulnerability(wallVulnMultiplier, wallStunDuration);
            ExternalStun(wallStunDuration);
            while (_isStunned) yield return null;
        }
    }

    // ───────────────── Jump Attack ─────────────────
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

        if (jumpEndsWithSlam)
            yield return DoSlamOnce();
    }

    // ───────────────── Attack Prep & 선택 ─────────────────
    PrepOption PickPrep()
    {
        if (preps == null || preps.Count == 0)
            return new PrepOption();

        // 분노가 꽉 찼는지가 아니라, 이번 사이클에 강공격을 쓸 예정인지로 판단
        bool wantStrongPrep = _useStrongThisCycle;

        // 강 Prep 존재 여부 확인
        bool hasStrongPrep = false;
        for (int i = 0; i < preps.Count; i++)
        {
            if (preps[i] != null && preps[i].isStrongPrep)
            {
                hasStrongPrep = true;
                break;
            }
        }

        // 어떤 Prep들이 후보가 될지 결정하는 필터
        System.Predicate<int> isCandidate;

        if (hasStrongPrep)
        {
            if (wantStrongPrep)
            {
                // ★ 이번 사이클 강공격 → 강 Prep만 사용
                isCandidate = idx => preps[idx].isStrongPrep;
            }
            else
            {
                // ★ 이번 사이클 일반공격 → 일반 Prep만 사용
                isCandidate = idx => !preps[idx].isStrongPrep;
            }
        }
        else
        {
            // 강 Prep이 하나도 없으면 기존 로직 유지
            isCandidate = idx => true;
        }

        // 1차 후보 리스트: 직전 Prep은 제외
        List<int> candidates = new List<int>();
        for (int i = 0; i < preps.Count; i++)
        {
            if (i == _lastPrepIndex) continue;
            if (!isCandidate(i)) continue;

            float w = Mathf.Max(0f, preps[i].weight);
            if (w <= 0f) continue;

            candidates.Add(i);
        }

        // 후보 0이면 직전 것도 허용해서 다시 구성
        if (candidates.Count == 0)
        {
            for (int i = 0; i < preps.Count; i++)
            {
                if (!isCandidate(i)) continue;

                float w = Mathf.Max(0f, preps[i].weight);
                if (w <= 0f) continue;

                candidates.Add(i);
            }
        }

        // 여전히 없으면 그냥 0번
        if (candidates.Count == 0)
        {
            _lastPrepIndex = 0;
            return preps[0];
        }

        // 가중치 합
        float total = 0f;
        foreach (int idx in candidates)
            total += Mathf.Max(0f, preps[idx].weight);

        if (total <= 0f)
        {
            int chosen = candidates[Random.Range(0, candidates.Count)];
            _lastPrepIndex = chosen;
            return preps[chosen];
        }

        // 가중치 랜덤 선택
        float r = Random.value * total;
        foreach (int idx in candidates)
        {
            float w = Mathf.Max(0f, preps[idx].weight);
            if (w <= 0f) continue;

            if (r < w)
            {
                _lastPrepIndex = idx;
                return preps[idx];
            }
            r -= w;
        }

        int lastIdx = candidates[candidates.Count - 1];
        _lastPrepIndex = lastIdx;
        return preps[lastIdx];
    }

    IEnumerator DoAttackPrep(PrepOption opt)
    {
        if (!string.IsNullOrEmpty(opt.prepDirectionalBase))
        {
            PlayDirectionalState(opt.prepDirectionalBase, opt.prepCrossFade);
        }
        else if (_anim && !string.IsNullOrEmpty(opt.prepTrigger))
        {
            _anim.SetTrigger(opt.prepTrigger);
        }

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
                int idx = PickSimpleIndex(opt);
                if (idx >= 0 && idx < simpleAttacks.Count)
                {
                    var sa = simpleAttacks[idx];
                    bool isStrong = sa != null && sa.isStrongAttack;

                    // 이번 사이클이 강공격이고, 실제로 강 SimpleAttack이 선택됐으면 분노 소모
                    if (isStrong && _useStrongThisCycle && _strongAttackQueued)
                    {
                        ClearRage();
                    }

                    yield return DoSimpleAttack(sa, opt.attackTriggerOverride);
                }
                break;

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
        if (simpleAttacks == null || simpleAttacks.Count == 0) return -1;

        // 분노 상태가 아니라, 이번 사이클이 강공격 모드인지 기준
        bool wantStrongOnly = _useStrongThisCycle;

        // 1) simpleChoices 우선 (강/일반 필터 적용)
        if (opt.simpleChoices != null && opt.simpleChoices.Count > 0)
        {
            float total = 0f;
            foreach (var c in opt.simpleChoices)
            {
                int idx = Mathf.Clamp(c.simpleIndex, 0, simpleAttacks.Count - 1);
                bool isStrong = simpleAttacks[idx].isStrongAttack;

                if (wantStrongOnly && !isStrong) continue;
                if (!wantStrongOnly && isStrong) continue;

                float w = Mathf.Max(0f, c.weight);
                if (w <= 0f) continue;
                total += w;
            }

            if (total > 0f)
            {
                float r = Random.value * total;
                foreach (var c in opt.simpleChoices)
                {
                    int idx = Mathf.Clamp(c.simpleIndex, 0, simpleAttacks.Count - 1);
                    bool isStrong = simpleAttacks[idx].isStrongAttack;

                    if (wantStrongOnly && !isStrong) continue;
                    if (!wantStrongOnly && isStrong) continue;

                    float w = Mathf.Max(0f, c.weight);
                    if (w <= 0f) continue;

                    if (r < w) return idx;
                    r -= w;
                }
            }
        }

        // 2) 폴백: opt.simpleIndex 또는 첫 유효 강/일반 공격
        int fallback = Mathf.Clamp(opt.simpleIndex, 0, simpleAttacks.Count - 1);
        bool fallbackStrong = simpleAttacks[fallback].isStrongAttack;

        if (wantStrongOnly && fallbackStrong) return fallback;
        if (!wantStrongOnly && !fallbackStrong) return fallback;

        for (int i = 0; i < simpleAttacks.Count; i++)
        {
            bool isStrong = simpleAttacks[i].isStrongAttack;
            if (wantStrongOnly && isStrong) return i;
            if (!wantStrongOnly && !isStrong) return i;
        }

        return fallback;
    }

    // ───────────────── Simple Attack 본체 ─────────────────
    IEnumerator DoSimpleAttack(SimpleAttack sa, string triggerOverride)
    {
        _playingSimpleAttack = sa;
        _autoProjFiredThisAttack = false;

        string trig = string.IsNullOrEmpty(triggerOverride) ? sa.attackTrigger : triggerOverride;
        if (_anim && !string.IsNullOrEmpty(trig))
            _anim.SetTrigger(trig);

        if (sa.spawnProjectileAuto)
            TryFireSimpleProjectile(sa, true);   // 이건 공격당 1번만

        Coroutine moveCR = null;

        if (!sa.useAnimEventMove && sa.moveTime > 0f && Mathf.Abs(sa.moveDistance) > 0f)
            moveCR = StartCoroutine(CoAdvance(sa.moveDistance, sa.moveTime, sa.moveCurve));

        var hbList = GetSimpleHitboxList(sa);

        if (sa.useAnimEvent)
        {
            _currentHitboxes = hbList;
            yield return new WaitForSeconds(Mathf.Max(0.01f, sa.activeTime));
        }
        else
        {
            ToggleColliders(hbList, true);
            yield return new WaitForSeconds(Mathf.Max(0.01f, sa.activeTime));
            ToggleColliders(hbList, false);
        }

        if (moveCR != null)
            yield return moveCR;

        StopAnimEventMove(false);

        _currentHitboxes = null;
        _playingSimpleAttack = null;
        _autoProjFiredThisAttack = false;
    }

    // 현재 바라보는 방향에 따라 적절한 히트박스 리스트 반환
    List<Collider2D> GetSimpleHitboxList(SimpleAttack sa)
    {
        if (sa == null) return null;

        if (FacingRight)
        {
            if (sa.rightHitboxes != null && sa.rightHitboxes.Count > 0)
                return sa.rightHitboxes;
        }
        else
        {
            if (sa.leftHitboxes != null && sa.leftHitboxes.Count > 0)
                return sa.leftHitboxes;
        }

        return sa.hitboxes;
    }

    // ───────────────── Projectile (Simple) ─────────────────
    // oncePerAttack = true 면 "이 공격 동안 딱 1번" 제한
    void TryFireSimpleProjectile(SimpleAttack src, bool oncePerAttack = true)
    {
        var sa = src ?? _playingSimpleAttack;
        if (sa == null || !sa.projectilePrefab) return;

        if (oncePerAttack)
        {
            if (_autoProjFiredThisAttack) return;
            _autoProjFiredThisAttack = true;
        }

        // 1) 발사지점
        Transform muzzle =
            (FacingRight
                ? (sa.projectileMuzzleRight ? sa.projectileMuzzleRight : sa.projectileMuzzle)
                : (sa.projectileMuzzleLeft ? sa.projectileMuzzleLeft : sa.projectileMuzzle));

        Vector3 spawnPos;
        if (muzzle)
        {
            spawnPos = muzzle.position;
        }
        else
        {
            float sign = FacingRight ? 1f : -1f;
            spawnPos = transform.position +
                       new Vector3(sa.projectileMuzzleOffset.x * sign, sa.projectileMuzzleOffset.y, 0f);
        }

        // 2) 방향
        Vector2 dir = FacingRight ? Vector2.right : Vector2.left;
        if (sa.projAimAtPlayer && player)
        {
            Vector2 d = (player.position - spawnPos);
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }
        bool dirRightForAnim = dir.x >= 0f;

        // 3) 인스턴스 & 런치
        var go = Instantiate(sa.projectilePrefab, spawnPos, Quaternion.identity);
        var pr = go.GetComponent<SlashProjectile2D>();
        if (pr)
        {
            pr.LaunchWithPrefabDefaults(dir, transform, dirRightForAnim);
        }
        else
        {
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb) rb.linearVelocity = dir * 6f;
        }
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
            float k = (curve != null && curve.keys != null && curve.length > 0) ? curve.Evaluate(a) : EaseInOut(a);
            transform.position = Vector3.Lerp(p0, p1, k);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = p1;
    }

    // ───────────────── Projectile Burst ─────────────────
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

            if (projectileInterval > 0f)
                yield return new WaitForSeconds(projectileInterval);
            else
                yield return null;
        }
    }

    // ───────────────── Ground Slam ─────────────────
    IEnumerator DoSlamOnce()
    {
        var cam = Camera.main ? Camera.main.GetComponent<CameraSimple2D>() : null;
        if (cam) cam.Shake(slamShakeAmp, slamShakeDur, slamShakeFreq);

        ToggleColliders(slamHitboxes, true);
        yield return new WaitForSeconds(slamActiveTime);
        ToggleColliders(slamHitboxes, false);
    }

    // ===== Helpers =====
    bool IsPlayerFar(float threshold)
    {
        if (!player) return false;
        return Mathf.Abs(player.position.x - transform.position.x) > Mathf.Abs(threshold);
    }

    int DirToPlayer()
    {
        if (!player)
            return transform.localScale.x >= 0f ? 1 : -1;
        return (player.position.x - transform.position.x) >= 0f ? 1 : -1;
    }

    void SetFacing(bool right)
    {
        FacingRight = right;
        if (pairSync) pairSync.facingRight = right;

        if (gfxFlipRoot)
        {
            var s = gfxFlipRoot.localScale;
            s.x = Mathf.Abs(s.x);
            gfxFlipRoot.localScale = s;
        }

        if (gfxRenderer)
            gfxRenderer.flipX = false;
    }

    public void FaceRight() => SetFacing(true);
    public void FaceLeft() => SetFacing(false);

    void FaceTowardPlayer()
    {
        if (!player) return;
        SetFacing((player.position.x - transform.position.x) >= 0f);
    }

    static void ToggleColliders(List<Collider2D> list, bool on)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i]) list[i].enabled = on;
        }
    }

    static float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ── AnimEvent Move 업데이트 ──
    void UpdateAnimEventMove()
    {
        if (!_animMoveActive) return;

        _animMoveElapsed += Time.deltaTime;

        float t = (_animMoveDuration <= 0f)
            ? 1f
            : Mathf.Clamp01(_animMoveElapsed / _animMoveDuration);

        float k = EaseInOut(t);
        Vector3 pos = Vector3.Lerp(_animMoveStartPos, _animMoveTargetPos, k);
        transform.position = pos;

        if (t >= 1f)
            _animMoveActive = false;
    }

    void StopAnimEventMove(bool snapToTarget)
    {
        if (!_animMoveActive) return;

        if (snapToTarget)
            transform.position = _animMoveTargetPos;

        _animMoveActive = false;
    }

    // ───────────────── Anim Events ─────────────────
    public void AnimEvent_PrepReady() { _waitingPrepEvent = false; }

    public void AnimEvent_GenericHitOn()
    {
        if (_currentHitboxes != null)
            ToggleColliders(_currentHitboxes, true);
    }

    public void AnimEvent_GenericHitOff()
    {
        if (_currentHitboxes != null)
            ToggleColliders(_currentHitboxes, false);
    }

    public void AnimEvent_SlamHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SlamHitOff() { ToggleColliders(slamHitboxes, false); }
    public void AnimEvent_FireSimpleProjectile()
    {
        // oncePerAttack = false → 이벤트 호출할 때마다 발사
        TryFireSimpleProjectile(_playingSimpleAttack, false);
    }

    // ★ AnimEvent 이동 시작/정지
    public void AnimEvent_MoveStart(float deltaX)
    {
        float dirSign = FacingRight ? 1f : -1f;
        float worldDeltaX = deltaX * dirSign;

        _animMoveStartPos = transform.position;
        _animMoveTargetPos = _animMoveStartPos + new Vector3(worldDeltaX, 0f, 0f);
        _animMoveElapsed = 0f;

        float duration = 0.2f;

        if (_playingSimpleAttack != null &&
            _playingSimpleAttack.useAnimEventMove &&
            _playingSimpleAttack.animEventMoveDuration > 0f)
        {
            duration = _playingSimpleAttack.animEventMoveDuration;
        }

        _animMoveDuration = duration;
        _animMoveActive = true;
    }

    public void AnimEvent_MoveStop()
    {
        StopAnimEventMove(true);
    }

    // ★★★ 여기부터: 심플 어택 대미지 애니메이션 이벤트 ★★★

    public void AnimEvent_SimpleDamage()
    {
        if (_playingSimpleAttack == null) return;
        int dmg = (_playingSimpleAttack.baseDamage > 0) ? _playingSimpleAttack.baseDamage : 1;
        ApplySimpleDamage(dmg);
    }

    public void AnimEvent_SimpleDamageInt(int damage)
    {
        if (damage <= 0)
        {
            if (_playingSimpleAttack == null) return;
            damage = (_playingSimpleAttack.baseDamage > 0) ? _playingSimpleAttack.baseDamage : 1;
        }
        ApplySimpleDamage(damage);
    }

    static readonly List<Collider2D> _hitOverlapBuffer = new List<Collider2D>(8);
    static readonly HashSet<IDamageable> _damagedCache = new HashSet<IDamageable>();

    void ApplySimpleDamage(int amount)
    {
        if (amount <= 0) return;
        if (_playingSimpleAttack == null) return;

        var hbList = GetSimpleHitboxList(_playingSimpleAttack);
        if (hbList == null || hbList.Count == 0) return;

        bool isStrong = _playingSimpleAttack.isStrongAttack;

        var filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        _damagedCache.Clear();

        for (int i = 0; i < hbList.Count; i++)
        {
            var hb = hbList[i];
            if (!hb || !hb.enabled) continue;

            _hitOverlapBuffer.Clear();
            int count = hb.Overlap(filter, _hitOverlapBuffer);
            for (int j = 0; j < count; j++)
            {
                var col = _hitOverlapBuffer[j];
                if (!col) continue;
                if (!col.CompareTag("Player")) continue;

                var dmg = col.GetComponent<IDamageable>()
                        ?? col.GetComponentInParent<IDamageable>()
                        ?? col.GetComponentInChildren<IDamageable>();
                if (dmg == null || _damagedCache.Contains(dmg)) continue;

                _damagedCache.Add(dmg);

                // PlayerController를 최대한 확실히 찾기
                var pc = col.GetComponentInParent<PlayerController>();
                if (pc == null)
                    pc = FindObjectOfType<PlayerController>();

                if (isStrong)
                {
                    bool consumed = false;
                    if (pc != null)
                        consumed = pc.HandleStrongAttackHit(col);

                    // 강패링 성공 → 대미지 / 분노 증가 모두 스킵
                    if (consumed) continue;

                    if (pc != null)
                    {
                        pc.TakeStrongDamage(amount);
                        AddRage(1f);   // 실제로 맞았으니 분노 +1칸
                    }
                    else
                    {
                        if (dmg is PlayerHealth phStrong)
                            phStrong.TakeDamageFromHitbox(amount, col);
                        else
                            dmg.TakeDamage(amount);

                        AddRage(1f);
                    }
                }
                else
                {
                    if (dmg is PlayerHealth ph)
                        ph.TakeDamageFromHitbox(amount, col);
                    else
                        dmg.TakeDamage(amount);

                    AddRage(1f);  // 일반 공격도 맞으면 분노 +1칸
                }
            }
        }
    }

    // ===== 분노 게이지 처리 (fill 방식) =====

    void AddRage(float amount)
    {
        if (amount <= 0f) return;
        if (rageMaxStacks <= 0) return;
        if (rageValue >= rageMaxStacks) return;

        float prev = rageValue;
        rageValue = Mathf.Clamp(rageValue + amount, 0f, rageMaxStacks);

        HandleRageThreshold(prev, rageValue);
        UpdateRageUI();
    }

    void AddRageByTime(float deltaTime)
    {
        if (rageMaxStacks <= 0) return;
        if (rageAutoInterval <= 0f) return;
        if (rageValue >= rageMaxStacks) return;

        float prev = rageValue;
        rageValue = Mathf.Clamp(rageValue + deltaTime / rageAutoInterval, 0f, rageMaxStacks);

        HandleRageThreshold(prev, rageValue);
        UpdateRageUI();
    }

    void HandleRageThreshold(float prev, float cur)
    {
        int prevStacks = Mathf.FloorToInt(prev);
        int newStacks = Mathf.FloorToInt(cur);

        for (int s = prevStacks + 1; s <= newStacks && s <= rageMaxStacks; s++)
        {
            OnRageSegmentFilled(s - 1);
        }

        // 분노가 이번에 처음 가득 찼다면 강공격 1회 예약
        if (!_strongAttackQueued && prev < rageMaxStacks && cur >= rageMaxStacks)
        {
            _strongAttackQueued = true;
        }
    }

    void OnRageSegmentFilled(int segmentIndex)
    {
        if (rageSegmentFxAnimators == null) return;
        if (segmentIndex < 0 || segmentIndex >= rageSegmentFxAnimators.Length) return;

        var anim = rageSegmentFxAnimators[segmentIndex];
        if (!anim) return;

        if (!string.IsNullOrEmpty(rageSegmentFxTrigger))
            anim.SetTrigger(rageSegmentFxTrigger);
        else
            anim.Play(0, -1, 0f);
    }

    void UpdateRageUI()
    {
        if (rageFillImages == null) return;

        for (int i = 0; i < rageFillImages.Length; i++)
        {
            var img = rageFillImages[i];
            if (!img) continue;

            float segmentValue = Mathf.Clamp01(rageValue - i); // i번째 칸의 fill (0~1)
            img.fillAmount = segmentValue;
        }
    }

    void ClearRage()
    {
        // 분노 값 + 강공격 예약 모두 초기화
        rageValue = 0f;
        _strongAttackQueued = false;
        UpdateRageUI();
    }

    // ===== Gizmos / Animator helper =====
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

        if (chargeCancelDistance > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Mathf.Abs(chargeCancelDistance));
        }
    }

    bool PlayDirectionalState(string baseName, float crossFade = 0f)
    {
        if (pairSync && pairSync.PlayByBase(baseName, crossFade)) return true;
        if (!_anim) return false;

        string suffix = (FacingRight ? pairSync?.rightSuffix : pairSync?.leftSuffix) ??
                        (FacingRight ? "_R" : "_L");
        string state = baseName + suffix;
        _anim.CrossFadeInFixedTime(state, crossFade);
        return true;
    }

    public void StunFromTreeWall(TreeWall wall)
    {
        if (_isStunned) return;
        if (breakTreeOnHit && wall != null) wall.BreakAndDestroy();
        if (_health) _health.ApplyVulnerability(wallVulnMultiplier, wallStunDuration);
        ExternalStun(wallStunDuration);
    }

    void Start()
    {
        if (facePlayer) FaceTowardPlayer();
    }
}
