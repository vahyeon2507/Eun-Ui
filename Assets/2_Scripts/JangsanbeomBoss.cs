// JangsanbeomBoss.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomBoss : MonoBehaviour
{
    // ====== Clone / Light Mode (이중 가드용) ======
    [Header("Clone / Light Mode")]
    [Tooltip("원본 보스만 true. 클론은 Awake에서 자동으로 false로 전환됩니다.")]
    public bool allowCloneSpawns = true;     // 호출부 1차 가드
    [Tooltip("디버그 표기용. Awake에서 BossCloneConfig 감지되면 true.")]
    public bool isCloneInstance = false;

    // ====== Attack Data ======
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
        public float cloneSpawnOffsetX = 3.0f;   // (미사용) 유지
        public float cloneSpawnOffsetY = 0.71f;  // (미사용) 유지
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

    // ====== Dash Attack ======
    [Header("Dash Attack")]
    public AttackData dashAttack;

    public void OnDashHitFrame()
    {
        if (debugDamage) Debug.Log("[Boss] OnDashHitFrame()");
        if (dashAttack != null) { PerformAttackOnce(dashAttack, true); return; }
        var pool = BuildCurrentAttackPool();
        if (pool.Count > 0) PerformAttackOnce(pool[0], true);
    }

    // ====== Refs ======
    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    // ====== Health / Phase ======
    [Header("Health / Phase")]
    public MonoBehaviour bossHealthComponent;
    public float MaxHealth = 100f;
    public float CurrentHealth = 100f;
    [Range(0f, 1f)] public float Phase2HealthThreshold = 0.66f;

    [Header("Damage Gates")]
    public bool requirePlayerTag = false;           // (현재 로직은 태그 미사용)
    public bool blockOutgoingDamageWhileInvuln = false;
    public bool debugDamage = false;

    [Header("Map Roots")]
    public GameObject Phase1MapRoot, Phase2IntroMapRoot, Phase2MapRoot;

    [Header("Phase 2 Positioning")]
    public Transform phase2CenterPoint;
    public Vector2 phase2CenterFallback = Vector2.zero;
    public bool teleportToCenterOnPhase2 = true;
    public bool lockMovementInPhase2 = true;

    [Header("Player detection (no hitbox prefab)")]
    [Tooltip("현재 히트는 OverlapBoxAll(마스크 없이)로 처리함. 이 필드는 사용하지 않음.")]
    public LayerMask playerLayer;  // 참고용
    public string playerTag = "Player";

    // ====== Flip ======
    [Header("Visual & Flip")]
    public Transform graphicsRoot;     // 애니메이션 루트(권장)
    public BoxCollider2D flipTrigger;  // 스프라이트와 겹칠 때 기준선으로 사용 (뒤로 약간 물려 배치)
    public Transform flipPivot;        // 보조 피벗
    public float flipPivotDeadzone = 0.6f;

    [Header("Flip tuning")]
    public float flipTriggerHysteresis = 0.05f;
    public float flipCooldown = 0.12f;
    [HideInInspector] public bool facingRight = true;
    public bool enforceVisualFlip = true;
    public bool debugFlip = false;

    [Header("Orientation")]
    [Tooltip("원본 스프라이트 기본이 오른쪽이면 true, 왼쪽이면 false")]
    public bool spriteFacesRight = true;

    [Header("Facing (bounds-based)")]
    public Collider2D bodyCollider;
    public float faceOutsideMarginX = 0.2f;

    // ====== Movement / AI ======
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

    // ====== Dash / Animation Locks ======
    [Header("Dash / Animation Locks")]
<<<<<<< HEAD
    public string trig_Dash = "Dash";   // 애니메이터에 동일 이름 트리거 생성해서 사용
    // bool _dashActive = false; // 사용하지 않는 변수 - 주석 처리
    bool _animLockMove = false;         // 애니메이션으로 이동 잠금
    bool _animLockFlip = false;         // 애니메이션으로 플립 잠금
=======
    public string trig_Dash = "Dash";
    bool _dashActive = false;
    bool _animLockMove = false;
    bool _animLockFlip = false;
>>>>>>> main

    // ====== Internals ======
    Vector3 _rootOriginalScale, _graphicsOriginalScale;
    float _lastFlipTime = -10f;
    int _prevTriggerSide = 0;
    Coroutine aiCoroutine;
    bool busy = false;

    float _flipTriggerOriginalLocalX = 0f;
    bool _flipTriggerIsChild = false;

    readonly List<PolygonCollider2D> _polyColliders = new();
    readonly List<Vector2[][]> _originalPolyPaths = new();
    Vector3 _lastPosition;

    // phase state
    bool _inPhase2 = false;
    bool _phase2IntroPlaying = false;
    bool _invulnerable = false;

    // health poll
    float _lastKnownHp = -1f, _lastKnownMaxHp = -1f;
    float _healthPollInterval = 0.2f, _healthPollTimer = 0f;

    // ====== Phase2 Clones ======
    [Header("Phase2 Clones")]
    public bool lockCloneY = true;
    public float cloneFixedY = 0.71f;
    public GameObject clonePrefab;
    public int maxClones = 1;
    public float cloneSpawnCooldown = 6f;
    public float cloneSpawnRadius = 6f; // (원랜덤 스폰에서 쓰던 값, 유지)
    public int cloneInitialHp = 5;
    public bool cloneInheritFlip = true;

    [Header("Bell Signal (Phase2)")]
    public List<BellRinger> bellRingers = new();   // 맵의 종들 등록
    [Tooltip("종이 하나라도 울리는 동안은 분신 공격/스폰 금지")]
    public bool blockAttacksWhileAnyBellRinging = true;
    [Tooltip("종이 먼저 울리고 난 뒤 분신 스폰까지의 리드 타임")]
    public float bellLeadTime = 0.08f;

    // 내부 상태
    Coroutine _pendingBellSpawn;

    // ====== One-Shot Clone 추가 설정 ======
    [Header("One-Shot Clone Config")]
    [Tooltip("원샷 분신 사용 여부(페이드인→한 번 공격→페이드아웃)")]
    public bool useOneShotClones = true;
    [Tooltip("플레이어 바로 앞(보스 쪽)으로 띄울 X 오프셋")]
    public float oneShotFrontOffsetX = 1.2f;
    [Tooltip("등장 페이드 인 시간")]
    public float oneShotFadeIn = 0.12f;
    [Tooltip("공격 전 아주 짧은 딜레이(텔레그래프 대용)")]
    public float oneShotPreDelay = 0.06f;
    [Tooltip("공격 후 제거까지 대기")]
    public float oneShotDespawnDelay = 0.05f;
    [Tooltip("사라질 때 페이드 아웃 시간")]
    public float oneShotFadeOut = 0.18f;
    [Tooltip("스폰 시 원보스의 바라보는 방향을 상속")]
    public bool oneShotInheritFlip = true;

    float _cloneCooldownTimer = 0f;
    readonly List<JangsanbeomClone> _clones = new();   // 프로젝트에 존재하는 타입 그대로 유지

    // ====== Unity ======
    void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    // 외부에서 임시로 원점만 바꿔 한 번 쏘고 싶은 경우용(유틸)
    public void PerformAttackOnceFrom(AttackData atk, Transform originOverride, bool applyDamage)
    {
        if (atk == null) return;
        var bak = atk.OriginOverride;
        atk.OriginOverride = originOverride;
        PerformAttackOnce(atk, applyDamage);
        atk.OriginOverride = bak;
    }

    void Awake()
    {
        // 분신 마커 감지 → 라이트 모드로 전환
        var cloneCfg = GetComponent<BossCloneConfig>();
        if (cloneCfg != null)
        {
            isCloneInstance = true;

            // 스폰 루프 근본 차단
            allowCloneSpawns = !cloneCfg.disableCloneSpawns;
            maxClones = 0;             // 안전망
            clonePrefab = null;        // 안전망

            // 2페이즈 자체 방지
            if (cloneCfg.disablePhase2) Phase2HealthThreshold = -1f;
            if (cloneCfg.disablePhase2Intro) { phase2IntroInvulnerable = false; phase2IntroFallbackDuration = 0f; }

            // HP 경량화
            MaxHealth = Mathf.Max(1, cloneCfg.cloneMaxHp);
            CurrentHealth = MaxHealth;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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

            // 시작 시 플레이어 쪽을 보도록 보정
            FlipTo(player.position.x >= transform.position.x);
        }
    }

    void Update()
    {
        if (player == null) return;

        // 1차 가드: 바깥 레일 기준 정면 유지
        ForceFaceWhenPlayerOutsideBounds();

        // Follow
        float dx = player.position.x - transform.position.x;
        bool isMoving = false;
        bool blockMove = busy || _phase2IntroPlaying || (_inPhase2 && lockMovementInPhase2) || _animLockMove;

        if (!blockMove && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            float prevX = transform.position.x;
            var p = transform.position;
            p.x = Mathf.MoveTowards(transform.position.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = p;
            isMoving = Mathf.Abs(p.x - prevX) > 0.0001f;
        }

        // 스폰 호출부 1차 가드
        if (_inPhase2 && !_phase2IntroPlaying
            && allowCloneSpawns
            && maxClones > 0
            && clonePrefab != null)
        {
            if (_cloneCooldownTimer > 0f) _cloneCooldownTimer -= Time.deltaTime;
            TrySpawnCloneIfNeeded();
        }

        if (animator && !string.IsNullOrEmpty(animParam_MoveBool))
            SafeSetBool(animParam_MoveBool, isMoving);

        // 안쪽 레일(겹침) 히스테리시스 플립
        if (!_animLockFlip && (Time.time - _lastFlipTime > flipCooldown))
        {
            if (player != null && bodyCollider != null)
            {
                var b = bodyCollider.bounds;
                b.Expand(new Vector3(faceOutsideMarginX * 2f, 0f, 0f));
                bool insideBodyX = (player.position.x >= b.min.x && player.position.x <= b.max.x);

                if (insideBodyX)
                {
                    if (flipTrigger)
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
                    else if (flipPivot)
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
                        float rel = player.position.x - transform.position.x;
                        int cur = (rel > 0f) ? 1 : (rel < 0f ? -1 : 0);
                        if (cur != _prevTriggerSide)
                        {
                            FlipTo(rel > 0f);
                            _lastFlipTime = Time.time;
                        }
                    }
                }
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

    // ====== Phase ======
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
<<<<<<< HEAD

        // 2페이즈 BGM 전환
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPhase2BGM();
        }

        // **핵심**: 인트로 맵을 켜기 전에 Phase2 플래그를 true로 먼저 설정
=======
>>>>>>> main
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

    // ====== Flip / Visual ======
    float FacingSign() => facingRight ? 1f : -1f;
    float VisualSign() => (facingRight ? 1f : -1f) * (spriteFacesRight ? 1f : -1f);

    float VisualSignForGizmos()
    {
        if (Application.isPlaying) return VisualSign();
        if (graphicsRoot) { float sx = graphicsRoot.localScale.x; return Mathf.Approximately(sx, 0f) ? 1f : Mathf.Sign(sx); }
        if (spriteRenderer)
        {
            int srFlipSign = spriteRenderer.flipX ? -1 : 1;
            int baseSign = spriteFacesRight ? 1 : -1;
            return srFlipSign * baseSign;
        }
        return 1f;
    }

    public void FlipTo(bool faceRight)
    {
        facingRight = faceRight;

        if (graphicsRoot)
        {
            var s = graphicsRoot.localScale;
            float sign = faceRight ? 1f : -1f;
            s.x = Mathf.Abs(s.x) * (spriteFacesRight ? sign : -sign);
            graphicsRoot.localScale = s;
            if (spriteRenderer) spriteRenderer.flipX = false;
        }
        else
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer) spriteRenderer.flipX = spriteFacesRight ? !faceRight : faceRight;
            else FlipAllSpriteRenderers(faceRight);
        }

        ApplyFlipToPolygonColliders(faceRight);

        if (flipTrigger && _flipTriggerIsChild)
        {
            var lp = flipTrigger.transform.localPosition;
            lp.x = _flipTriggerOriginalLocalX * VisualSign();
            flipTrigger.transform.localPosition = lp;
        }

        float refX = (flipTrigger ? GetFlipTriggerWorldX()
                                  : (flipPivot ? flipPivot.position.x : transform.position.x));
        float relAfter = player ? player.position.x - refX : 0f;
        _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 : (relAfter < -flipTriggerHysteresis ? -1 : 0);
    }

    void ApplyVisualFlipNow()
    {
        if (graphicsRoot)
        {
            var s = graphicsRoot.localScale;
            float sign = facingRight ? 1f : -1f;
            float want = Mathf.Abs(s.x) * (spriteFacesRight ? sign : -sign);
            if (!Mathf.Approximately(s.x, want)) { s.x = want; graphicsRoot.localScale = s; }
        }
        else
        {
            if (spriteRenderer) spriteRenderer.flipX = spriteFacesRight ? !facingRight : facingRight;
            else foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                    sr.flipX = spriteFacesRight ? !facingRight : facingRight;
        }
    }

    void FlipAllSpriteRenderers(bool faceRight)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.flipX = spriteFacesRight ? !faceRight : faceRight;
    }

    float GetFlipTriggerWorldX()
    {
        if (!flipTrigger) return transform.position.x;
        if (_flipTriggerIsChild)
        {
            float localX = _flipTriggerOriginalLocalX * VisualSign();
            Vector3 world = transform.TransformPoint(new Vector3(localX, flipTrigger.transform.localPosition.y, 0f));
            return world.x;
        }
        return flipTrigger.bounds.center.x;
    }

    // ====== AI / Attacks ======
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

    // ====== 공통 히트 처리(마스크/태그 의존 X) ======
    void PerformAttackOnce(AttackData atk, bool applyDamage)
    {
        if (!applyDamage) return;
<<<<<<< HEAD
        if (_invulnerable) return; // 변신/무적 중엔 공격 무시(원하면 제거)

        // 보스 공격 사운드 재생
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBossAttack();

        if (playerLayer == 0) { Debug.LogWarning("[Boss] playerLayer not set."); return; }
=======
>>>>>>> main

        Vector2 center = GetAttackWorldPos(atk);
        float worldAngle = atk.Angle * VisualSign();

        // 마스크 없이 전부 겹침 검사
        var hits = Physics2D.OverlapBoxAll(center, atk.Size, worldAngle);

        var seen = new HashSet<Collider2D>();
        foreach (var col in hits)
        {
            if (!col || seen.Contains(col)) continue;
            seen.Add(col);

            // 플레이어 컴포넌트가 있어야만 대미지
            var pc = col.GetComponent<PlayerController>() ??
                     col.GetComponentInParent<PlayerController>() ??
                     col.GetComponentInChildren<PlayerController>();
            if (pc == null) continue;

            if (pc.IsParrying && TryAskPlayerToConsumeParry(pc, atk.Damage)) continue;

            var dmg = col.GetComponent<IDamageable>() ??
                      col.GetComponentInParent<IDamageable>() ??
                      col.GetComponentInChildren<IDamageable>();
            if (dmg != null) { dmg.TakeDamage(atk.Damage); continue; }

            var pHealth = col.GetComponent<PlayerHealth>() ??
                          col.GetComponentInParent<PlayerHealth>() ??
                          col.GetComponentInChildren<PlayerHealth>();
            if (pHealth != null) { pHealth.TakeDamage(atk.Damage); continue; }

            var mb = col.GetComponent<MonoBehaviour>() ?? col.GetComponentInParent<MonoBehaviour>();
            if (mb != null)
            {
                MethodInfo mi = mb.GetType().GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(mb, new object[] { atk.Damage });
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

    // ====== Utils ======
    Vector2 GetAttackWorldPos(AttackData atk)
    {
        float sign = VisualSign();
        Vector2 origin = atk.OriginOverride ? (Vector2)atk.OriginOverride.position : (Vector2)transform.position;
        Vector2 off = atk.Offset; off.x *= sign;
        float ang = (atk.Angle * sign) * Mathf.Deg2Rad;
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
        bool mirror = VisualSign() < 0f;
        for (int k = 0; k < _polyColliders.Count; k++)
        {
            var poly = _polyColliders[k];
            if (!poly) continue;
            var orig = _originalPolyPaths[k];
            poly.pathCount = orig.Length;

            for (int i = 0; i < orig.Length; i++)
            {
                var src = orig[i];
                var dst = new Vector2[src.Length];
                if (!mirror) Array.Copy(src, dst, src.Length);
                else
                {
                    for (int j = 0; j < src.Length; j++) dst[j] = new Vector2(-src[j].x, src[j].y);
                    Array.Reverse(dst);
                }
                poly.SetPath(i, dst);
            }
        }
    }

    // ====== Safe Animator helpers ======
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

<<<<<<< HEAD
    // ===== Animation Events: Dash / Locks / I-Frames / Relative motion =====
    public void Anim_DashStart() { /* _dashActive = true; */ _animLockMove = true; _animLockFlip = true; busy = true; }
    public void Anim_DashEnd() { /* _dashActive = false; */ _animLockMove = false; _animLockFlip = false; busy = false; }

=======
    // ====== Animation Events ======
    public void Anim_DashStart() { _dashActive = true; _animLockMove = true; _animLockFlip = true; busy = true; }
    public void Anim_DashEnd() { _dashActive = false; _animLockMove = false; _animLockFlip = false; busy = false; }
>>>>>>> main
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

    // ====== Bounds-based facing helper ======
    void ForceFaceWhenPlayerOutsideBounds()
    {
        if (_animLockFlip) return;
        if (player == null || bodyCollider == null) return;

        var b = bodyCollider.bounds;
        b.Expand(new Vector3(faceOutsideMarginX * 2f, 0f, 0f));

        float px = player.position.x;
        bool outsideX = (px < b.min.x || px > b.max.x);

        if (outsideX)
        {
            bool wantRight = px >= b.center.x;
            if (wantRight == facingRight) return; // 이미 정면이면 유지

            FlipTo(wantRight);
            _lastFlipTime = Time.time;

            float refX = (flipTrigger ? GetFlipTriggerWorldX()
                         : (flipPivot ? flipPivot.position.x : transform.position.x));
            float relAfter = player.position.x - refX;
            _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 :
                               (relAfter < -flipTriggerHysteresis ? -1 : 0);
        }
    }

    // ====== Gizmos ======
    void OnDrawGizmos()
    {
        if (!showAttackGizmos) return;

        // 현재 풀
        var list = Application.isPlaying ? BuildCurrentAttackPool() : attacksPhase1;
        if (list != null)
        {
            float sign = VisualSignForGizmos();
            for (int i = 0; i < list.Count; i++)
                DrawGizmoForAttack(list[i], sign, gizmoRealColor, new Color(gizmoRealColor.r, gizmoRealColor.g, gizmoRealColor.b, Mathf.Min(1f, gizmoRealColor.a * 2f)));
        }

        // 대쉬 기즈모(파란색 톤)
        if (dashAttack != null)
        {
            float sign = VisualSignForGizmos();
            Color dashFill = new Color(0.2f, 0.6f, 1f, 0.18f);
            Color dashWire = new Color(0.2f, 0.6f, 1f, 0.36f);
            DrawGizmoForAttack(dashAttack, sign, dashFill, dashWire);
        }
    }

    void DrawGizmoForAttack(AttackData a, float sign, Color fill, Color wire)
    {
        if (a == null) return;

        Vector2 origin = a.OriginOverride ? (Vector2)a.OriginOverride.position : (Vector2)transform.position;
        Vector2 off = a.Offset; off.x *= sign;

        float angRad = (a.Angle * sign) * Mathf.Deg2Rad;
        float c = Mathf.Cos(angRad), s = Mathf.Sin(angRad);
        Vector2 rot = new(off.x * c - off.y * s, off.x * s + off.y * c);

        Vector2 pos = origin + rot;
        float angleDeg = a.Angle * sign;

        var prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(new Vector3(pos.x, pos.y, 0), Quaternion.Euler(0, 0, angleDeg), Vector3.one);
        Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, new Vector3(a.Size.x, a.Size.y, 0.01f));
        Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, new Vector3(a.Size.x, a.Size.y, 0.01f) * 1.001f);
        Gizmos.matrix = prev;
    }

    // ====== Clones (Double-Guard) ======
    void TrySpawnCloneIfNeeded()
    {
        // ★ 하드 게이트
        if (!allowCloneSpawns || maxClones <= 0 || clonePrefab == null) return;

        // null 정리
        for (int i = _clones.Count - 1; i >= 0; i--)
            if (_clones[i] == null) _clones.RemoveAt(i);

        // 상한/쿨다운/예약 중복 가드
        if (_clones.Count >= maxClones) return;
        if (_cloneCooldownTimer > 0f) return;
        if (_pendingBellSpawn != null) return; // 이미 종 울리고 스폰 예약됨

        // === 스폰 위치: 플레이어 "바로 앞"(보스 방향 쪽) ===
        Vector2 basePos = transform.position;
        float dirToBoss = Mathf.Sign(transform.position.x - player.position.x); // 플레이어→보스 방향(+/-1)
        Vector2 spawnPos = new Vector2(
            player.position.x + dirToBoss * oneShotFrontOffsetX,
            lockCloneY ? cloneFixedY : basePos.y
        );

        // === 종 울림 선행 & 스폰 예약 ===
        if (!PreSignalBellAndSchedule(spawnPos))
        {
            // 종이 울리는 중이면 스폰 시도하지 않음(혼동 금지)
            return;
        }

        // 리드타임 동안 추가 시도를 막기 위해 바로 쿨다운 시작
        if (_cloneCooldownTimer <= 0f) _cloneCooldownTimer = cloneSpawnCooldown;
    }

    bool PreSignalBellAndSchedule(Vector2 spawnPos)
    {
        // 종이 하나라도 울리는 중이면 금지
        if (blockAttacksWhileAnyBellRinging && bellRingers != null)
        {
            for (int i = 0; i < bellRingers.Count; i++)
            {
                var r = bellRingers[i];
                if (r && r.IsRinging) return false;
            }
        }

        // 가까운 "대기 중" 종을 선택해 울린다
        BellRinger best = null;
        float bestDist = float.MaxValue;

        if (bellRingers != null && bellRingers.Count > 0)
        {
            for (int i = 0; i < bellRingers.Count; i++)
            {
                var r = bellRingers[i];
                if (r == null || r.IsRinging) continue;
                float dx = r.transform.position.x - spawnPos.x;
                float dy = r.transform.position.y - spawnPos.y;
                float d = Mathf.Abs(dx) + Mathf.Abs(dy); // L1 거리(가볍게)
                if (d < bestDist) { best = r; bestDist = d; }
            }

            // 쓸 수 있는 종이 없다면 공격 명령을 내리지 않는다(혼동 방지)
            if (best == null) return false;

            // 종 울리기 시도 (실패 시 스폰 안 함)
            if (!best.TryRing(Mathf.Max(0.01f, bellLeadTime))) return false;

            // 리드타임 뒤 스폰 예약
            if (_pendingBellSpawn != null) StopCoroutine(_pendingBellSpawn);
            _pendingBellSpawn = StartCoroutine(SpawnAfterBellDelay(spawnPos, bellLeadTime));
            return true;
        }
        else
        {
            // 종을 쓰지 않는 씬이라면 바로 스폰
            SpawnCloneNowAt(spawnPos);
            return true;
        }
    }

    IEnumerator SpawnAfterBellDelay(Vector2 spawnPos, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        SpawnCloneNowAt(spawnPos);
        _pendingBellSpawn = null;
    }

    void SpawnCloneNowAt(Vector2 spawnPos)
    {
        // 생성
        var go = Instantiate(clonePrefab, spawnPos, Quaternion.identity);

        // 라이트 모드 설정
        var cloneBoss = go.GetComponent<JangsanbeomBoss>();
        if (cloneBoss != null)
        {
            cloneBoss.player = this.player;

            var cfg = go.GetComponent<BossCloneConfig>();
            if (cfg == null) cfg = go.AddComponent<BossCloneConfig>();
            cfg.cloneMaxHp = Mathf.Max(1, cloneInitialHp);
            cfg.disablePhase2 = true;
            cfg.disableCloneSpawns = true;     // 클론이 또 스폰 못 하게
            cfg.disablePhase2Intro = true;
            cfg.disableRewardsOnDeath = true;
            cfg.clearMapRoots = true;
            cfg.hideGizmosInPlay = true;

            if (cloneInheritFlip) cloneBoss.FlipTo(this.facingRight);
        }

        var jsbClone = go.GetComponent<JangsanbeomClone>();
        if (jsbClone != null) _clones.Add(jsbClone);

        // 원샷 분신 모드(이미 구현해둔 스크립트)
        if (useOneShotClones)
        {
            var oneShot = go.GetComponent<JangsanbeomOneShotClone>();
            if (!oneShot) oneShot = go.AddComponent<JangsanbeomOneShotClone>();
            oneShot.Init(
                owner: this,
                selfBoss: cloneBoss,
                player: this.player,
                preDelay: oneShotPreDelay,
                fadeIn: oneShotFadeIn,
                fadeOut: oneShotFadeOut,
                despawnDelay: oneShotDespawnDelay,
                inheritFlip: oneShotInheritFlip
            );
        }

        // 쿨다운(이미 걸려있으면 덮어쓰지 않음)
        if (_cloneCooldownTimer <= 0f) _cloneCooldownTimer = cloneSpawnCooldown;
    }


    List<AttackData> BuildPhase2AttackPoolForClone()
    {
        var list = new List<AttackData>(8);
        foreach (var a in attacksPhase2) if (a != null && a.AllowInPhase2) list.Add(a);
        foreach (var a in attacksPhase1) if (a != null && a.AllowInPhase2) list.Add(a);
        return list;
    }

    public void OnCloneDied(JangsanbeomClone who) { _clones.Remove(who); }
}
