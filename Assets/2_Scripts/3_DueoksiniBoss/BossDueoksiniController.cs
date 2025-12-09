using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        // ---------- Projectile (event-driven) ----------
        [Header("Move During Attack (optional)")]
        public float moveDistance = 0f;
        public float moveTime = 0f;
        public AnimationCurve moveCurve;

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
    }

    void OnEnable()
    {
        if (!_inRoutine)
            StartCoroutine(MainLoop());
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

            var prep = PickPrep();
            yield return DoAttackPrep(prep);
            yield return DoAttack(prep);
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

        // 이동
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
        IsChargingNow = false;

        // TreeWall 충돌 처리
        if (hitTree != null)
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
        if (preps == null || preps.Count == 0) return new PrepOption();

        float total = 0f;
        for (int i = 0; i < preps.Count; i++)
        {
            if (i == _lastPrepIndex) continue;
            total += Mathf.Max(0f, preps[i].weight);
        }

        float r = Random.value * (total <= 0f ? 1f : total);
        for (int i = 0; i < preps.Count; i++)
        {
            if (i == _lastPrepIndex) continue;
            float w = Mathf.Max(0f, preps[i].weight);
            if (r < w)
            {
                _lastPrepIndex = i;
                return preps[i];
            }
            r -= w;
        }

        _lastPrepIndex = 0;
        return preps[0];
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
                    yield return DoSimpleAttack(simpleAttacks[idx], opt.attackTriggerOverride);
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
        if (opt.simpleChoices != null && opt.simpleChoices.Count > 0)
        {
            float total = 0f;
            foreach (var c in opt.simpleChoices)
                total += Mathf.Max(0f, c.weight);

            if (total <= 0f)
                return Mathf.Clamp(opt.simpleChoices[0].simpleIndex, 0, simpleAttacks.Count - 1);

            float r = Random.value * total;
            foreach (var c in opt.simpleChoices)
            {
                float w = Mathf.Max(0f, c.weight);
                if (r < w)
                    return Mathf.Clamp(c.simpleIndex, 0, simpleAttacks.Count - 1);
                r -= w;
            }
        }

        return Mathf.Clamp(opt.simpleIndex, 0, simpleAttacks.Count - 1);
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
            TryFireSimpleProjectile(sa);

        Coroutine moveCR = null;
        if (sa.moveTime > 0f && Mathf.Abs(sa.moveDistance) > 0f)
            moveCR = StartCoroutine(CoAdvance(sa.moveDistance, sa.moveTime, sa.moveCurve));

        var hbList = GetSimpleHitboxList(sa);

        if (sa.useAnimEvent)
        {
            // 애니메이션 이벤트(AnimEvent_GenericHitOn/Off)에서 켜고 끄는 모드
            _currentHitboxes = hbList;
            yield return new WaitForSeconds(Mathf.Max(0.01f, sa.activeTime));
        }
        else
        {
            // activeTime 동안 자동 On → Off
            ToggleColliders(hbList, true);
            yield return new WaitForSeconds(Mathf.Max(0.01f, sa.activeTime));
            ToggleColliders(hbList, false);
        }

        if (moveCR != null)
            yield return moveCR;

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

        // 좌/우가 비어 있으면 공통 hitboxes 사용
        return sa.hitboxes;
    }

    // ───────────────── Projectile (Simple) ─────────────────
    void TryFireSimpleProjectile(SimpleAttack src)
    {
        var sa = src ?? _playingSimpleAttack;
        if (sa == null || !sa.projectilePrefab) return;
        if (_autoProjFiredThisAttack) return;

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

        _autoProjFiredThisAttack = true;
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
    public void AnimEvent_FireSimpleProjectile() { TryFireSimpleProjectile(_playingSimpleAttack); }

    // ★★★ 여기부터: 심플 어택 대미지 애니메이션 이벤트 ★★★

    /// <summary>
    /// 현재 재생 중인 SimpleAttack의 baseDamage로 히트박스 안의 Player(IDamageable)에 대미지
    /// </summary>
    public void AnimEvent_SimpleDamage()
    {
        if (_playingSimpleAttack == null) return;
        int dmg = (_playingSimpleAttack.baseDamage > 0) ? _playingSimpleAttack.baseDamage : 1;
        ApplySimpleDamage(dmg);
    }

    /// <summary>
    /// 애니메이션 이벤트 인자로 들어온 damage 값으로 히트 처리
    /// </summary>
    public void AnimEvent_SimpleDamageInt(int damage)
    {
        if (damage <= 0)
        {
            if (_playingSimpleAttack == null) return;
            damage = (_playingSimpleAttack.baseDamage > 0) ? _playingSimpleAttack.baseDamage : 1;
        }
        ApplySimpleDamage(damage);
    }

    // 히트박스 안에 들어있는 Player(IDamageable)에게 한 번씩 대미지 적용
    static readonly List<Collider2D> _hitOverlapBuffer = new List<Collider2D>(8);
    static readonly HashSet<IDamageable> _damagedCache = new HashSet<IDamageable>();

    void ApplySimpleDamage(int amount)
    {
        if (amount <= 0) return;
        if (_playingSimpleAttack == null) return;

        var hbList = GetSimpleHitboxList(_playingSimpleAttack);
        if (hbList == null || hbList.Count == 0) return;

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

                // 플레이어만 맞게 태그 필터
                if (!col.CompareTag("Player")) continue;

                var dmg = col.GetComponent<IDamageable>()
                        ?? col.GetComponentInParent<IDamageable>()
                        ?? col.GetComponentInChildren<IDamageable>();
                if (dmg == null || _damagedCache.Contains(dmg)) continue;

                _damagedCache.Add(dmg);
                dmg.TakeDamage(amount);
            }
        }
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
