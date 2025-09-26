// JangsanbeomBoss.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomBoss : MonoBehaviour
{
    [Serializable]
    public class AttackData
    {
        [Header("Basics")]
        public string Name = "ClawExecute";
        public Vector2 Offset = new Vector2(1.5f, 0);
        public Vector2 Size = new Vector2(3f, 1.5f);
        public float Angle = 0f;
        public int Damage = 2;
        public float Lifetime = 0.12f;

        [Header("Editor Visuals")]
        public GameObject TelegraphPrefab;
        public float TelegraphTime = 0f;
        public float cloneSpawnOffsetX = 3.0f;
        public float cloneSpawnOffsetY = 0.71f;
        public bool DefaultFake = false;
        public Sprite FakeSprite;
        public float FakeSpriteDuration = 0.18f;
        public Transform OriginOverride;

        [Header("Phase Usage")]
        public bool AllowInPhase1 = true;
        public bool AllowInPhase2 = true;

        [Header("Phase2: Fake → Real")]
        public bool Phase2_FakeDealsDamage = true;
    }

    // ---------- Refs ----------
    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    // ---------- Health / Phase ----------
    [Header("Health / Phase")]
    public MonoBehaviour bossHealthComponent;
    public float MaxHealth = 100f;
    public float CurrentHealth = 100f;
    [Range(0f, 1f)] public float Phase2HealthThreshold = 0.66f;

    [Header("Map Roots")]
    public GameObject Phase1MapRoot;
    public GameObject Phase2IntroMapRoot;
    public GameObject Phase2MapRoot;

    [Header("Phase 2 Positioning")]
    public Transform phase2CenterPoint;
    public Vector2 phase2CenterFallback = Vector2.zero;
    public bool teleportToCenterOnPhase2 = true;
    public bool lockMovementInPhase2 = true;

    [Header("Player detection (no hitbox prefab)")]
    public LayerMask playerLayer;
    public string playerTag = "Player";

    // ---------- Flip ----------
    [Header("Visual & Flip")]
    public Transform graphicsRoot;          // ← 애니메이션은 여기에 키 찍는 걸 권장
    public BoxCollider2D flipTrigger;
    public Transform flipPivot;
    public float flipPivotDeadzone = 0.6f;

    [Header("Flip tuning")]
    public float flipTriggerHysteresis = 0.05f;
    public float flipCooldown = 0.12f;
    [HideInInspector] public bool facingRight = true;
    public bool enforceVisualFlip = true;
    public bool debugFlip = false;

    [Header("Facing (bounds-based)")]
    [Tooltip("이 콜라이더의 가로 범위를 기준으로, 플레이어가 범위 바깥에 있으면 강제 플립")]
    public Collider2D bodyCollider;
    [Tooltip("바깥 판정을 느슨하게 하는 마진(월드 단위)")]
    public float faceOutsideMarginX = 0.2f;

    // ---------- Movement / AI ----------
    [Header("AI / Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Animator movement params")]
    public string animParam_MoveBool = "Move";
    public string animParam_MoveSpeed = "Speed";

    [Header("Behavior")]
    public bool useAnimationEvents = true;

    [Header("Fake options")]
    public bool enableFakePhase1 = false;
    public float fakeChancePhase1 = 0f;
    public bool enableFakePhase2 = true;
    public float fakeChancePhase2 = 0.25f;

    [Header("Attacks (per phase list)")]
    public List<AttackData> attacksPhase1 = new();
    public List<AttackData> attacksPhase2 = new();

    [Header("Gizmos")]
    public bool showAttackGizmos = true;
    public Color gizmoRealColor = new(1f, 0.2f, 0.2f, 0.18f);
    public Color gizmoFakeColor = new(1f, 0.85f, 0.2f, 0.12f);

    [Header("Animator param names (attack)")]
    public string trig_ClawTelegraph = "ClawTelegraph";
    public string trig_ClawExecute = "ClawExecute";
    public string trig_ClawFakeExecute = "ClawFakeExecute";
    public string trig_EnterPhase2 = "EnterPhase2";

    [Header("Phase2 Intro")]
    public bool phase2IntroInvulnerable = true;
    public float phase2IntroFallbackDuration = 1.5f;

    // ---------- Dash / Animation Locks ----------
    [Header("Dash / Animation Locks")]
    public string trig_Dash = "Dash";
    bool _dashActive = false;
    bool _animLockMove = false;
    bool _animLockFlip = false;

    // internals
    Vector3 _rootOriginalScale, _graphicsOriginalScale;
    float _lastFlipTime = -10f;
    int _prevTriggerSide = 0;
    Coroutine aiCoroutine;
    bool busy = false;

    float _flipTriggerOriginalLocalX = 0f;
    bool _flipTriggerIsChild = false;

    List<PolygonCollider2D> _polyColliders = new();
    List<Vector2[][]> _originalPolyPaths = new();
    Vector3 _lastPosition;

    // phase state
    bool _inPhase2 = false;
    bool _phase2IntroPlaying = false;
    bool _invulnerable = false;

    // health poll
    float _lastKnownHp = -1f, _lastKnownMaxHp = -1f;
    float _healthPollInterval = 0.2f, _healthPollTimer = 0f;

    // ▼ Phase2 Clones
    [Header("Phase2 Clones")]
    public bool lockCloneY = true;
    public float cloneFixedY = 0.71f;
    public JangsanbeomClone clonePrefab;   // 분신 프리팹
    public int maxClones = 1;              // 최대 동시 분신 수
    public float cloneSpawnCooldown = 6f;  // 분신 소환 쿨타임(초)
    public float cloneSpawnRadius = 6f;    // 본체 기준 랜덤 스폰 반경
    public int cloneInitialHp = 5;         // 분신 체력
    public bool cloneInheritFlip = true;   // 스폰 시 본체 바라보는 방향 따라갈지

    float _cloneCooldownTimer = 0f;
    readonly List<JangsanbeomClone> _clones = new();

    // ===== [ADD] Phase2 Clones: methods =====

    // 분신 스폰 시도
    void TrySpawnCloneIfNeeded()
    {
        // null 정리
        for (int i = _clones.Count - 1; i >= 0; i--)
            if (_clones[i] == null) _clones.RemoveAt(i);

        if (_clones.Count >= maxClones) return;
        if (_cloneCooldownTimer > 0f) return;
        if (clonePrefab == null) return;

        // 스폰 위치: 본체 주변 원형 랜덤
        Vector2 basePos = transform.position;
        Vector2 spawnPos = basePos + UnityEngine.Random.insideUnitCircle.normalized * cloneSpawnRadius;
        if (lockCloneY) spawnPos.y = cloneFixedY;

        var clone = Instantiate(clonePrefab, spawnPos, Quaternion.identity);

        // 초기 세팅 복사
        clone.owner = this;
        clone.player = this.player;
        clone.enableFakePhase2 = this.enableFakePhase2;
        clone.fakeChancePhase2 = this.fakeChancePhase2;
        clone.attacks = BuildPhase2AttackPoolForClone();
        clone.maxHp = Mathf.Max(1, cloneInitialHp);
        clone.currentHp = clone.maxHp;
        clone.allowDropRewards = false;
        if (cloneInheritFlip) clone.FaceRight(this.facingRight);

        _clones.Add(clone);
        _cloneCooldownTimer = cloneSpawnCooldown;
    }

    // 분신이 사용할 공격 풀(2페이즈 허용된 것만)
    List<AttackData> BuildPhase2AttackPoolForClone()
    {
        var list = new List<AttackData>(8);
        foreach (var a in attacksPhase2) if (a != null && a.AllowInPhase2) list.Add(a);
        foreach (var a in attacksPhase1) if (a != null && a.AllowInPhase2) list.Add(a);
        return list;
    }

    // 분신 사망 콜백 (Clone.Die에서 호출)
    public void OnCloneDied(JangsanbeomClone who)
    {
        _clones.Remove(who);
    }

    void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb) { rb.bodyType = RigidbodyType2D.Kinematic; rb.simulated = true; }

        _rootOriginalScale = transform.localScale;
        _rootOriginalScale.x = Mathf.Abs(_rootOriginalScale.x);
        transform.localScale = _rootOriginalScale;

        if (graphicsRoot)
        {
            _graphicsOriginalScale = graphicsRoot.localScale;
            _graphicsOriginalScale.x = Mathf.Abs(_graphicsOriginalScale.x);
            var s = graphicsRoot.localScale; s.x = Mathf.Abs(s.x); graphicsRoot.localScale = s;
        }

        if (flipTrigger)
        {
            _flipTriggerIsChild = flipTrigger.transform.IsChildOf(transform);
            _flipTriggerOriginalLocalX = _flipTriggerIsChild ? flipTrigger.transform.localPosition.x : 0f;
        }

        CachePolygonColliderPaths();
    }

    void Start()
    {
        if (player == null) Debug.LogWarning("[Boss] player reference not set!");

        aiCoroutine = StartCoroutine(AIBehavior());
        _lastPosition = transform.position;

        UpdateHealthCacheImmediate();
        UpdatePhaseFlagsFromHealth();
        ApplyPhaseMapRoots(false);

        if (player != null)
        {
            float rel = (flipTrigger != null) ? player.position.x - GetFlipTriggerWorldX()
                       : (flipPivot ? player.position.x - flipPivot.position.x
                                    : player.position.x - transform.position.x);
            _prevTriggerSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
        }
    }

    void Update()
    {
        if (player == null) return;

        ForceFaceWhenPlayerOutsideBounds();

        // follow
        float dx = player.position.x - transform.position.x;
        bool isMoving = false;
        bool blockMove = busy || _phase2IntroPlaying || (_inPhase2 && lockMovementInPhase2) || _animLockMove;

        if (!blockMove && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            float prevX = transform.position.x;
            var p = transform.position;
            p.x = Mathf.MoveTowards(transform.position.x,
                player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = p;
            isMoving = Mathf.Abs(p.x - prevX) > 0.0001f;
        }

        if (_inPhase2 && !_phase2IntroPlaying)
        {
            if (_cloneCooldownTimer > 0f) _cloneCooldownTimer -= Time.deltaTime;
            TrySpawnCloneIfNeeded();
        }

        if (animator && !string.IsNullOrEmpty(animParam_MoveBool))
            SafeSetBool(animParam_MoveBool, isMoving);

        // ▼ 바운즈 기반 강제 바라보기(콜라이더 바깥에 있을 때 즉시 플립)
        ForceFaceWhenPlayerOutsideBounds();

        if (!_animLockFlip && (Time.time - _lastFlipTime > flipCooldown))
        {
            if (player != null && bodyCollider != null)
            {
                var b = bodyCollider.bounds;
                b.Expand(new Vector3(faceOutsideMarginX * 2f, 0f, 0f));
                bool insideBodyX = (player.position.x >= b.min.x && player.position.x <= b.max.x);

                if (insideBodyX) // 스프라이트가 겹칠 정도로 가까움 → 안쪽 레일 사용
                {
                    if (flipTrigger) // 전용 자식 박스(뒤로 약간 물린)를 기준으로 스위칭
                    {
                        float triggerX = GetFlipTriggerWorldX();
                        float rel = player.position.x - triggerX;
                        int cur = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
                        if (cur != 0 && cur != _prevTriggerSide)
                        {
                            FlipTo(rel > 0f);
                            _lastFlipTime = Time.time;
                        }
                    }
                    else if (flipPivot) // 전용 박스가 없다면 보조 피벗 사용(옵션)
                    {
                        float rel = player.position.x - flipPivot.position.x;
                        int cur = (rel > flipPivotDeadzone) ? 1 : (rel < -flipPivotDeadzone ? -1 : 0);
                        if (cur != 0 && cur != _prevTriggerSide)
                        {
                            FlipTo(rel > 0f);
                            _lastFlipTime = Time.time;
                        }
                    }
                    else
                    {
                        // 최후의 보루: 자기 중심 기준(덜 자연스러움)
                        float rel = player.position.x - transform.position.x;
                        int cur = (rel > 0f) ? 1 : (rel < 0f ? -1 : 0);
                        if (cur != _prevTriggerSide)
                        {
                            FlipTo(rel > 0f);
                            _lastFlipTime = Time.time;
                        }
                    }
                }
                // insideBodyX가 아니면 바깥 레일이 이미 처리함(ForceFaceWhenPlayerOutsideBounds에서 즉시 플립)
            }
        }

        // health poll
        _healthPollTimer -= Time.deltaTime;
        if (_healthPollTimer <= 0f)
        {
            _healthPollTimer = _healthPollInterval;
            UpdateHealthCacheImmediate();
            UpdatePhaseFlagsFromHealth();
        }
    }

    void LateUpdate()
    {
        // ❌ 루트 X 스케일 강제 복구 제거 (애니메이션 스케일 보존)
        // if (transform.localScale.x != _rootOriginalScale.x)
        // { var s = transform.localScale; s.x = _rootOriginalScale.x; transform.localScale = s; }

        if (enforceVisualFlip) ApplyVisualFlipNow();

        bool blockMove = busy || _phase2IntroPlaying || (_inPhase2 && lockMovementInPhase2) || _animLockMove;

        if (animator && !string.IsNullOrEmpty(animParam_MoveSpeed))
        {
            float dt = Time.deltaTime;
            float vx = (!blockMove && dt > 0f) ? (transform.position.x - _lastPosition.x) / dt : 0f;
            SafeSetFloat(animParam_MoveSpeed, Mathf.Abs(vx));
        }
        _lastPosition = transform.position;
    }

    // ---------- Phase ----------
    void UpdateHealthCacheImmediate()
    {
        if (bossHealthComponent != null)
        {
            float c = TryGetFloatMember(bossHealthComponent, "CurrentHp", "currentHp", "CurrentHP", "currentHP", "hp", "HP", "cur", "current");
            float m = TryGetFloatMember(bossHealthComponent, "MaxHp", "maxHp", "MaxHP", "maxHP", "maxHealth", "MaxHealth", "HPMax");
            if (!float.IsNaN(c)) CurrentHealth = c;
            if (!float.IsNaN(m) && m > 0f) MaxHealth = m;
        }
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, Mathf.Max(1f, MaxHealth));
        _lastKnownHp = CurrentHealth; _lastKnownMaxHp = MaxHealth;
    }
    float TryGetFloatMember(object obj, params string[] names)
    {
        if (obj == null) return float.NaN;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var pi = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null && (pi.PropertyType == typeof(float) || pi.PropertyType == typeof(double) || pi.PropertyType == typeof(int)))
            {
                var v = pi.GetValue(obj);
                if (v is float f) return f; if (v is double d) return (float)d; if (v is int i) return i;
            }
            var fi = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && (fi.FieldType == typeof(float) || fi.FieldType == typeof(double) || fi.FieldType == typeof(int)))
            {
                var v = fi.GetValue(obj);
                if (v is float f2) return f2; if (v is double d2) return (float)d2; if (v is int i2) return i2;
            }
        }
        return float.NaN;
    }

    void UpdatePhaseFlagsFromHealth()
    {
        if (MaxHealth <= 0f) return;
        float perc = CurrentHealth / MaxHealth;
        bool goPhase2 = perc <= Phase2HealthThreshold;
        if (goPhase2 && !_inPhase2) EnterPhase2();
        _inPhase2 = goPhase2;
    }

    void EnterPhase2()
    {
        if (debugFlip) Debug.Log("[Boss] Entering Phase 2!");
        _inPhase2 = true;

        if (teleportToCenterOnPhase2)
        {
            Vector3 target = phase2CenterPoint ? phase2CenterPoint.position
                                               : new Vector3(phase2CenterFallback.x, phase2CenterFallback.y, transform.position.z);
            transform.position = target;
            if (rb != null)
            {
#if UNITY_2022_3_OR_NEWER
                rb.linearVelocity = Vector2.zero;
#else
                rb.velocity = Vector2.zero;
#endif
            }
        }

        _phase2IntroPlaying = true;
        if (phase2IntroInvulnerable) _invulnerable = true;
        ApplyPhaseMapRoots(true);
        SafeSetTrigger(trig_EnterPhase2);

        if (phase2IntroFallbackDuration > 0f)
            StartCoroutine(Phase2IntroFallback(phase2IntroFallbackDuration));
    }

    IEnumerator Phase2IntroFallback(float t)
    {
        yield return new WaitForSeconds(t);
        EndPhase2Intro();
    }

    public void OnPhase2IntroStart()
    {
        _inPhase2 = true;
        _phase2IntroPlaying = true;
        if (phase2IntroInvulnerable) _invulnerable = true;
        ApplyPhaseMapRoots(true);
    }
    public void OnPhase2IntroEnd() { EndPhase2Intro(); }
    void EndPhase2Intro()
    {
        _phase2IntroPlaying = false;
        _invulnerable = false;
        ApplyPhaseMapRoots(false);
    }

    void ApplyPhaseMapRoots(bool intro)
    {
        if (Phase1MapRoot) Phase1MapRoot.SetActive(!_inPhase2);
        if (Phase2IntroMapRoot) Phase2IntroMapRoot.SetActive(_inPhase2 && intro);
        if (Phase2MapRoot) Phase2MapRoot.SetActive(_inPhase2 && !intro);
    }

    // ---------- Flip ----------
    public void FlipTo(bool faceRight)
    {
        facingRight = faceRight;
        // 루트 스케일은 그대로 유지(애니값 보존)

        if (graphicsRoot)
        {
            // ✅ 현재 크기(절대값)는 유지하고, 부호만 설정
            var s = graphicsRoot.localScale;
            s.x = Mathf.Abs(s.x) * (faceRight ? 1f : -1f);
            graphicsRoot.localScale = s;
            if (spriteRenderer) spriteRenderer.flipX = false;
        }
        else
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer) spriteRenderer.flipX = !faceRight;
            else FlipAllSpriteRenderers(faceRight);
        }

        ApplyFlipToPolygonColliders(faceRight);

        if (flipTrigger && _flipTriggerIsChild)
        {
            var lp = flipTrigger.transform.localPosition;
            lp.x = _flipTriggerOriginalLocalX * (faceRight ? 1f : -1f);
            flipTrigger.transform.localPosition = lp;
        }

        float refX = (flipTrigger ? GetFlipTriggerWorldX() : (flipPivot ? flipPivot.position.x : transform.position.x));
        float relAfter = player ? player.position.x - refX : 0f;
        _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 : (relAfter < -flipTriggerHysteresis ? -1 : 0);
    }

    void ApplyVisualFlipNow()
    {
        if (graphicsRoot)
        {
            // ✅ 절대값 유지, 부호만 강제
            var s = graphicsRoot.localScale;
            float wantSign = facingRight ? 1f : -1f;
            if (Mathf.Sign(s.x) != wantSign) { s.x = Mathf.Abs(s.x) * wantSign; graphicsRoot.localScale = s; }
        }
        else FlipAllSpriteRenderers(facingRight);
    }
    void FlipAllSpriteRenderers(bool faceRight)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.flipX = !faceRight;
    }
    float GetFlipTriggerWorldX()
    {
        if (!flipTrigger) return transform.position.x;
        if (_flipTriggerIsChild)
        {
            float localX = _flipTriggerOriginalLocalX * (facingRight ? 1f : -1f);
            Vector3 world = transform.TransformPoint(new Vector3(localX, flipTrigger.transform.localPosition.y, 0f));
            return world.x;
        }
        return flipTrigger.bounds.center.x;
    }

    // ---------- AI / Attacks ----------
    IEnumerator AIBehavior()
    {
        while (true)
        {
            if (!busy && !_phase2IntroPlaying && player)
            {
                var pool = BuildCurrentAttackPool();
                float d = Mathf.Abs(player.position.x - transform.position.x);
                if (d <= aggroRange && pool.Count > 0)
                {
                    if (UnityEngine.Random.Range(0, 100) < 60)
                        StartAttackByIndex(0, pool);
                }
            }
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.6f, 1.5f));
        }
    }

    List<AttackData> BuildCurrentAttackPool()
    {
        var list = new List<AttackData>(8);
        if (!_inPhase2)
        {
            foreach (var a in attacksPhase1) if (a != null && a.AllowInPhase1) list.Add(a);
        }
        else
        {
            foreach (var a in attacksPhase2) if (a != null && a.AllowInPhase2) list.Add(a);
            foreach (var a in attacksPhase1) if (a != null && a.AllowInPhase2) list.Add(a);
        }
        return list;
    }

    public void StartAttackByIndex(int index, List<AttackData> pool = null)
    {
        if (busy) return;
        pool ??= BuildCurrentAttackPool();

        AttackData atk = null;
        if (pool != null && index >= 0 && index < pool.Count) atk = pool[index];
        if (atk == null) atk = new AttackData();

        StartCoroutine(ClawRoutine_Generic(atk));
    }

    IEnumerator ClawRoutine_Generic(AttackData atk)
    {
        busy = true;

        bool isFakeVisual = false;
        bool applyDamage = true;

        if (!_inPhase2)
        {
            if (enableFakePhase1)
            {
                isFakeVisual = UnityEngine.Random.value < fakeChancePhase1 || atk.DefaultFake;
                if (isFakeVisual) applyDamage = false;
            }
        }
        else
        {
            if (enableFakePhase2)
            {
                isFakeVisual = UnityEngine.Random.value < fakeChancePhase2 || atk.DefaultFake;
                applyDamage = isFakeVisual ? atk.Phase2_FakeDealsDamage : true;
            }
        }

        if (animator) SafeSetTrigger(isFakeVisual ? trig_ClawFakeExecute : trig_ClawExecute);

        if (!useAnimationEvents)
        {
            if (isFakeVisual && atk.FakeSprite && spriteRenderer)
                StartCoroutine(TemporarilyShow(atk.FakeSprite, atk.FakeSpriteDuration));

            yield return new WaitForSeconds(isFakeVisual ? 0.06f : 0.08f);
            PerformAttackOnce(atk, applyDamage);
        }

        if (atk.TelegraphTime > 0f) yield return new WaitForSeconds(atk.TelegraphTime);
        yield return new WaitForSeconds(0.01f);

        busy = false;
    }

    public void OnClawHitFrame()
    {
        var pool = BuildCurrentAttackPool(); if (pool.Count > 0) PerformAttackOnce(pool[0], true);
    }
    public void OnClawFakeHitFrame()
    {
        var pool = BuildCurrentAttackPool(); if (pool.Count > 0) { var a = pool[0]; bool dmg = _inPhase2 ? a.Phase2_FakeDealsDamage : false; PerformAttackOnce(a, dmg); }
    }

    void PerformAttackOnce(AttackData atk, bool applyDamage)
    {
        if (!applyDamage) return;
        if (_invulnerable) return;

        if (playerLayer == 0) { Debug.LogWarning("[Boss] playerLayer not set."); return; }

        Vector2 center = GetAttackWorldPos(atk);
        float dirSign = facingRight ? 1f : -1f;
        float worldAngle = atk.Angle * dirSign;

        var hits = Physics2D.OverlapBoxAll(center, atk.Size, worldAngle, playerLayer);
        var set = new HashSet<Collider2D>();

        foreach (var col in hits)
        {
            if (!col || set.Contains(col)) continue;
            set.Add(col);
            if (!string.IsNullOrEmpty(playerTag) && !col.CompareTag(playerTag)) continue;

            var pc = col.GetComponent<PlayerController>() ?? col.GetComponentInParent<PlayerController>() ?? col.GetComponentInChildren<PlayerController>();
            if (pc != null)
            {
                if (pc.IsParrying)
                {
                    bool consumed = TryAskPlayerToConsumeParry(pc, atk.Damage);
                    if (consumed) continue;
                }

                var dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>() ?? col.GetComponentInChildren<IDamageable>();
                if (dmg != null) { dmg.TakeDamage(atk.Damage); continue; }

                var pHealth = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
                if (pHealth != null) { pHealth.TakeDamage(atk.Damage); continue; }

                var mb = col.GetComponent<MonoBehaviour>() ?? col.GetComponentInParent<MonoBehaviour>();
                if (mb != null)
                {
                    MethodInfo mi = mb.GetType().GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null) { mi.Invoke(mb, new object[] { atk.Damage }); continue; }
                }
            }
        }
    }

    bool TryAskPlayerToConsumeParry(PlayerController pc, int incomingDamage)
    {
        if (pc == null) return false;
        string[] methods = { "ConsumeHitboxIfParrying", "ConsumeParryHit", "ConsumeParry", "OnParriedByHitbox", "ConsumeHitIfParry" };
        var t = pc.GetType();
        foreach (var name in methods)
        {
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) continue;
            var ps = m.GetParameters();
            try
            {
                object r = null;
                if (ps.Length == 0) r = m.Invoke(pc, null);
                else if (ps.Length == 1) r = m.Invoke(pc, new object[] { incomingDamage });
                else if (ps.Length >= 2) r = m.Invoke(pc, new object[] { incomingDamage, this });
                if (r is bool b) return b;
                return true;
            }
            catch { }
        }
        return false;
    }

    // ---------- Utils ----------
    Vector2 GetAttackWorldPos(AttackData atk)
    {
        float dir = facingRight ? 1f : -1f;
        Vector2 origin = atk.OriginOverride ? (Vector2)atk.OriginOverride.position : (Vector2)transform.position;
        Vector2 off = atk.Offset; off.x *= dir;
        float ang = (atk.Angle * dir) * Mathf.Deg2Rad;
        float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
        Vector2 rot = new(off.x * c - off.y * s, off.x * s + off.y * c);
        return origin + rot;
    }

    IEnumerator TemporarilyShow(Sprite s, float dur)
    {
        if (!spriteRenderer) yield break;
        var prev = spriteRenderer.sprite; spriteRenderer.sprite = s;
        yield return new WaitForSeconds(dur);
        if (spriteRenderer) spriteRenderer.sprite = prev;
    }

    void CachePolygonColliderPaths()
    {
        _polyColliders.Clear(); _originalPolyPaths.Clear();
        foreach (var p in GetComponentsInChildren<PolygonCollider2D>(true))
        {
            if (graphicsRoot && p.transform.IsChildOf(graphicsRoot)) continue;
            _polyColliders.Add(p);
            int pc = p.pathCount;
            Vector2[][] paths = new Vector2[pc][];
            for (int i = 0; i < pc; i++)
            {
                var path = p.GetPath(i);
                var copy = new Vector2[path.Length];
                Array.Copy(path, copy, path.Length);
                paths[i] = copy;
            }
            _originalPolyPaths.Add(paths);
        }
    }

    void ApplyFlipToPolygonColliders(bool faceRight)
    {
        for (int k = 0; k < _polyColliders.Count; k++)
        {
            var poly = _polyColliders[k];
            if (!poly) continue;
            var orig = _originalPolyPaths[k];
            poly.pathCount = orig.Length;
            bool mirror = !faceRight;
            for (int i = 0; i < orig.Length; i++)
            {
                var src = orig[i];
                var dst = new Vector2[src.Length];
                if (!mirror) Array.Copy(src, dst, src.Length);
                else { for (int j = 0; j < src.Length; j++) dst[j] = new Vector2(-src[j].x, src[j].y); Array.Reverse(dst); }
                poly.SetPath(i, dst);
            }
        }
    }

    // ---------- Safe Animator helpers ----------
    void SafeSetTrigger(string name)
    {
        if (!animator || string.IsNullOrEmpty(name)) return;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) { animator.SetTrigger(name); return; }
    }
    void SafeSetBool(string name, bool val)
    {
        if (!animator || string.IsNullOrEmpty(name)) return;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool) { animator.SetBool(name, val); return; }
    }
    void SafeSetFloat(string name, float v)
    {
        if (!animator || string.IsNullOrEmpty(name)) return;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Float) { animator.SetFloat(name, v); return; }
    }

    // ===== Animation Events: Dash / Locks / I-Frames / Relative motion =====
    public void Anim_DashStart() { _dashActive = true; _animLockMove = true; _animLockFlip = true; busy = true; }
    public void Anim_DashEnd() { _dashActive = false; _animLockMove = false; _animLockFlip = false; busy = false; }

    public void Anim_SetMoveLock(int on) { _animLockMove = (on != 0); }
    public void Anim_SetFlipLock(int on) { _animLockFlip = (on != 0); }

    public void Anim_InvulnOn() { _invulnerable = true; }
    public void Anim_InvulnOff() { _invulnerable = false; }

    // spec: "dx,dy[,local|world][,face]"
    public void Anim_MoveBy(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return;
        string[] p = spec.Split(',');
        if (p.Length < 2) return;

        float dx = 0f, dy = 0f;
        float.TryParse(p[0], out dx);
        float.TryParse(p[1], out dy);

        bool useLocal = false;
        bool useFacingSign = false;

        for (int i = 2; i < p.Length; i++)
        {
            string s = p[i].Trim().ToLowerInvariant();
            if (s == "local") useLocal = true;
            else if (s == "world") useLocal = false;
            else if (s == "face" || s == "signed") useFacingSign = true;
        }

        if (useFacingSign) dx *= (facingRight ? 1f : -1f);

        Vector3 delta = new Vector3(dx, dy, 0f);
        if (useLocal) transform.position = transform.TransformPoint(delta);
        else transform.position += delta;
    }

    // -------- Bounds-based facing helper --------
    // -------- 바깥 레일: 본체 바운즈 밖이면 즉시 바라보기 --------
    void ForceFaceWhenPlayerOutsideBounds()
    {
        if (_animLockFlip) return;
        if (player == null || bodyCollider == null) return;

        var b = bodyCollider.bounds;
        b.Expand(new Vector3(faceOutsideMarginX * 2f, 0f, 0f)); // 약간 여유

        float px = player.position.x;
        bool outsideX = (px < b.min.x || px > b.max.x);

        if (outsideX)
        {
            bool wantRight = px >= b.center.x;
            if (wantRight != facingRight)
            {
                FlipTo(wantRight);
                _lastFlipTime = Time.time;
            }
            // 히스테리시스 기준 업데이트
            float refX = (flipTrigger ? GetFlipTriggerWorldX() :
                         (flipPivot ? flipPivot.position.x : transform.position.x));
            float relAfter = player.position.x - refX;
            _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 :
                               (relAfter < -flipTriggerHysteresis ? -1 : 0);
        }
    }


    void OnDrawGizmos()
    {
        if (!showAttackGizmos) return;
        var list = Application.isPlaying ? BuildCurrentAttackPool() : attacksPhase1;
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            Vector2 pos; float angleDeg;
            if (Application.isPlaying) { pos = GetAttackWorldPos(a); angleDeg = a.Angle * (facingRight ? 1f : -1f); }
            else
            {
                float dir = (facingRight ? 1f : -1f);
                Vector2 origin = a.OriginOverride ? (Vector2)a.OriginOverride.position : (Vector2)transform.position;
                Vector2 off = a.Offset; off.x *= dir;
                float ang = (a.Angle * dir) * Mathf.Deg2Rad; float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                Vector2 rot = new(off.x * c - off.y * s, off.x * s + off.y * c);
                pos = origin + rot; angleDeg = a.Angle * dir;
            }
            Color fill = a.DefaultFake ? gizmoFakeColor : gizmoRealColor;
            Color wire = new(fill.r, fill.g, fill.b, Mathf.Min(1f, fill.a * 2f));

            var prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(new Vector3(pos.x, pos.y, 0), Quaternion.Euler(0, 0, angleDeg), Vector3.one);
            Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, new Vector3(a.Size.x, a.Size.y, 0.01f));
            Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, new Vector3(a.Size.x, a.Size.y, 0.01f) * 1.001f);
            Gizmos.matrix = prev;
        }
    }
}
