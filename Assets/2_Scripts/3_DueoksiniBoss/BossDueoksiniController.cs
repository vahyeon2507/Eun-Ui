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
    public Transform player;

    [Header("Graphics (optional)")]
    public Animator gfxAnimator;
    public SpriteRenderer gfxRenderer;
    public Transform gfxFlipRoot;

    [SerializeField] private DirectionalAnimPairSync pairSync;
    public bool FacingRight { get; private set; } = true;

    // =========================
    // Charge (dash) flow
    // =========================
    [Header("Charge")]
    public float chargeDistance = 6f;
    public float chargeTime = 0.6f;
    public LayerMask environmentMask;
    public float wallSkin = 0.1f;
    public string chargeTrigger = "Charge";
    public string isChargingBool = "isCharging";

    [Header("Face/Move")]
    public bool facePlayer = true;

    // =========================
    // Charge-Prep (single)
    // =========================
    [Header("Charge ▸ Prep (single)")]
    public string chargePrepTrigger = "Prep_Charge";
    public bool chargePrepEndByAnimEvent = false;
    public float chargePrepHoldTime = 0.5f;

    [Header("Charge ▸ Far → Jump branch")]
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
    // Simple Attacks
    // =========================
    [System.Serializable]
    public class SimpleAttack
    {
        [Header("Animator")]
        public string name = "SimpleAttack";
        public string attackTrigger;

        [Header("Hit Window")]
        public bool useAnimEvent = false;
        public float activeTime = 0.12f;
        public List<Collider2D> hitboxes = new List<Collider2D>();

        [Header("Move During Attack (optional)")]
        public float moveDistance = 0f;
        public float moveTime = 0f;
        public AnimationCurve moveCurve;

        // ===== NEW: Projectile (optional) =====
        [Header("Projectile (optional)")]
        public bool spawnProjectile = false;
        public GameObject projectilePrefab;        // 반드시 SlashProjectile2D 붙은 프리팹
        public Transform projectileMuzzle;         // 없으면 보스 위치
        public int projectileDamage = 1;

        [Tooltip("발사 직후 속도")]
        public float projStartSpeed = 3f;

        [Tooltip("가속 종료 시 목표 속도")]
        public float projFastSpeed = 14f;

        [Tooltip("이 시간 동안은 느리게(지연)")]
        public float projAccelDelay = 0.12f;

        [Tooltip("지연 이후 fastSpeed까지 도달하는 데 걸리는 시간")]
        public float projAccelTime = 0.35f;

        [Tooltip("0~1 속도 보간 커브(없으면 EaseInOut)")]
        public AnimationCurve projSpeedCurve;

        [Tooltip("투사체 생존 시간")]
        public float projLifeTime = 5f;

        public LayerMask projHitMask = ~0;
        public bool projDestroyOnHit = true;

        [Tooltip("플레이어 쪽으로 방향을 조준할지(켜면 좌우 대신 목표 방향 사용)")]
        public bool projAimAtPlayer = false;
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

        public int simpleIndex = 0;
        public List<SimpleChoice> simpleChoices = new List<SimpleChoice>();

        public string attackTriggerOverride = null;

        // ===== NEW: Prep Directional (optional) =====
        [Header("Prep Directional (optional)")]
        [Tooltip("좌우 페어 베이스 이름(예: Ready_Kwon Geuk)")]
        public string prepDirectionalBase;
        public float prepCrossFade = 0.05f;
    }

    [Header("Prep & Attack")]
    public List<PrepOption> preps = new List<PrepOption>();
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
    SimpleAttack _playingSimpleAttack;        // NEW: 현재 재생 중 심플어택

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

    void OnEnable()
    {
        if (!_inRoutine) StartCoroutine(MainLoop());
    }

    IEnumerator MainLoop()
    {
        _inRoutine = true;
        while (true)
        {
            if (facePlayer) FaceTowardPlayer();

            // 1) Charge-Prep
            yield return DoChargePrep();

            // 2) 분기
            bool far = IsPlayerFar(farDistanceThreshold);
            if (far) { yield return DoJumpAttack(); continue; }
            else { yield return DoCharge(); }

            if (facePlayer) FaceTowardPlayer();

            // 3) Prep → 4) Attack
            var prep = PickPrep();
            yield return DoAttackPrep(prep);
            yield return DoAttack(prep);
        }
    }

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

    IEnumerator DoCharge()
    {
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
        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(dir, 0f), rayLen, environmentMask);
        if (hit.collider) targetX = hit.point.x - dir * wallSkin;

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
            if (r < w) { _lastPrepIndex = i; return preps[i]; }
            r -= w;
        }
        _lastPrepIndex = 0;
        return preps[0];
    }

    IEnumerator DoAttackPrep(PrepOption opt)
    {
        // 좌우 페어 베이스가 있으면 그걸 우선
        if (!string.IsNullOrEmpty(opt.prepDirectionalBase))
            PlayDirectionalState(opt.prepDirectionalBase, opt.prepCrossFade);
        else if (_anim && !string.IsNullOrEmpty(opt.prepTrigger))
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

    IEnumerator DoSimpleAttack(SimpleAttack sa, string triggerOverride)
    {
        _playingSimpleAttack = sa; // NEW: 현재 어택 기억

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
            _currentHitboxes = sa.hitboxes;
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
        _playingSimpleAttack = null; // NEW: 종료
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

    public void AnimEvent_SlamHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SlamHitOff() { ToggleColliders(slamHitboxes, false); }

    // ===== NEW: Slash projectile spawn (animation event) =====
    public void AnimEvent_FireSimpleProjectile()
    {
        var sa = _playingSimpleAttack;
        if (sa == null || !sa.spawnProjectile || !sa.projectilePrefab) return;

        Vector3 muzzlePos = transform.position;
        if (sa.projectileMuzzle) muzzlePos = sa.projectileMuzzle.position;

        // 방향 결정
        Vector2 dir = FacingRight ? Vector2.right : Vector2.left;
        if (sa.projAimAtPlayer && player)
        {
            dir = (player.position - muzzlePos).normalized;
            if (dir == Vector2.zero) dir = FacingRight ? Vector2.right : Vector2.left;
        }

        var go = Instantiate(sa.projectilePrefab, muzzlePos, Quaternion.identity);
        var pr = go.GetComponent<SlashProjectile2D>();
        if (pr)
        {
            pr.Launch(
                dir,
                this.transform,               // ownerRoot(피격 무시)
                sa.projectileDamage,
                sa.projStartSpeed,
                sa.projFastSpeed,
                sa.projAccelDelay,
                sa.projAccelTime,
                sa.projSpeedCurve,
                sa.projLifeTime,
                sa.projHitMask,
                sa.projDestroyOnHit
            );
        }
        else
        {
            // 폴백: RB2D 있으면 단순 속도
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb) rb.linearVelocity = dir * sa.projStartSpeed;
        }
    }

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

    // ===== Directional state helper =====
    bool PlayDirectionalState(string baseName, float crossFade = 0f)
    {
        if (pairSync && pairSync.PlayByBase(baseName, crossFade))
            return true;

        if (!_anim) return false;
        string suffix = (FacingRight ? pairSync?.rightSuffix : pairSync?.leftSuffix) ?? (FacingRight ? "_R" : "_L");
        string state = baseName + suffix;
        _anim.CrossFadeInFixedTime(state, crossFade);
        return true;
    }

    void Start()
    {
        if (facePlayer) FaceTowardPlayer();
    }
}
