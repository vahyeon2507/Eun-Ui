using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour, IDamageable, IParryStreakProvider
{
    [HideInInspector] public bool ExternalRangedOverride;

    // === Attack FX (맞췄을 때) ===
    [Header("Attack FX")]
    [Tooltip("기본 공격에 사용할 FX id (PlayerFxHooks.fxList의 id)")]
    public string hitFxId = "AttackSpark";

    [Tooltip("패링 스페셜 전용 FX id (PlayerFxHooks.fxList의 id)")]
    public string parrySpecialFxId = "ParryBurst";

    // === 일반 패링 성공 피드백(스페셜 전 아님) ===
    [Header("Parry Success (non-special)")]
    [Tooltip("일반 패링 성공 시 재생할 애니메이션 트리거명 (4번째=스페셜 때는 재생 안 됨)")]
    public string animParrySuccessTrigger = "ParrySuccess";
    [Tooltip("일반 패링 성공 시 뿌릴 FX id (PlayerFxHooks.fxList의 id)")]
    public string parrySuccessFxId = "ParrySpark";
    [Tooltip("일반 패링 성공 FX를 뿌릴 전용 범위(플레이어 자식, isTrigger 권장)")]
    public Collider2D[] parrySuccessFxAreas;

    // === 자식 히트박스 그룹 ===
    [Header("Hitboxes (children driven)")]
    [Tooltip("Attack1에 쓸 자식 Collider2D들 (isTrigger 권장)")]
    public Collider2D[] hitboxesAttack1;
    [Tooltip("Attack2에 쓸 자식 Collider2D들")]
    public Collider2D[] hitboxesAttack2;
    [Tooltip("Attack3에 쓸 자식 Collider2D들")]
    public Collider2D[] hitboxesAttack3;

    [Tooltip("패링 스페셜에 쓸 자식 Collider2D들 (여기에 뭔가라도 있으면 스페셜은 이걸 사용)")]
    public Collider2D[] hitboxesParrySpecial;

    // (호환) 탈리스만/원거리용 앵커
    [Header("Ability Anchor (for ranged/talisman)")]
    public Transform attackPoint;  // 없으면 transform 사용

    // === 입력 설정 ===
    [Header("Input")]
    [Tooltip("기본공격 추가 키(마우스 좌클릭과 병렬)")]
    public KeyCode extraAttackKey = KeyCode.X;

    public enum InputPreset
    {
        Preset1,
        Preset2
    }

    [Serializable]
    public struct PlayerInputConfig
    {
        public KeyCode LeftMove;
        public KeyCode RightMove;
        public KeyCode JumpKey;
        public KeyCode AttackKey;
        public KeyCode ParryKey;
        public KeyCode DashKey;
        public bool UseMouseAttack;
    }

    // 스윙/패링 중복 방지 플래그
    bool _attackHitFiredThisSwing = false;
    bool _fxSpawnedThisSwing = false;
    bool _specialHitFiredThisSwing = false;
    bool _specialFxSpawnedThisSwing = false;

    // 일반 패링 성공 피드백 중복 억제
    float _lastParryFeedbackTime = -999f;
    const float _parryFeedbackMinInterval = 0.04f;

    // IParryStreakProvider
    public int CurrentParryStreak => parrySuccessCount;
    public event Action<int> OnParryStreakChanged;

    public static System.Func<PlayerAction, bool> TutorialAllow = null;
    public enum PlayerAction { Attack, Parry, Dash, Jump, Move }

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;

    public PlayerFxHooks fx;   // 인스펙터에 Drag
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Gravity tuning")]
    public float fallGravity = 2.5f;
    public float lowJumpGravity = 2f;
    public float normalGravity = 1f;

    [Header("Attack - general")]
    [Tooltip("공격이 맞을 레이어(적/보스). 자식 히트박스가 이 레이어와 겹치는 상대만 집계")]
    public LayerMask enemyLayer;
    public float inputBufferWindow = 0.45f;

    [Header("Combo (3 steps)")]
    public int[] comboDamages = new int[3] { 1, 1, 2 };
    public float[] comboLockDurations = new float[3] { 0.25f, 0.3f, 0.4f };

    [Header("Animator parameter names (must match)")]
    public string animAttack1 = "Attack1";
    public string animAttack2 = "Attack2";
    public string animAttack3 = "Attack3";

    [Header("Dash")]
    public float dashSpeed = 16f;
    public float dashDuration = 0.16f;
    public float dashCooldown = 0.6f;
    public string animDashTrigger = "Dash";
    public string animIsDashingBool = "isDashing";

    [Header("Parry")]
    public float parryWindow = 0.2f;
    public float parryCooldown = 1.0f;
    public string animParryTrigger = "Parry";
    public string animIsParryingBool = "isParrying";

    [Header("Parry → Special")]
    public int parrySuccessNeeded = 4;
    public string animParrySpecialTrigger = "ParrySpecial";
    public float parrySpecialInvulDuration = 0.3f;
    [Tooltip("과거 호환용(사용 안 함)")]
    public float parrySuccessResetDelay = 3.0f;

    [Tooltip("스페셜만 별도 레이어마스크를 쓰고 싶다면 지정(비워두면 enemyLayer 사용)")]
    public LayerMask parrySpecialEnemyMaskOverride;

    [Tooltip("패링 스페셜 공격의 대미지(인스펙터 조절)")]
    public int parrySpecialDamage = 3;

    [Header("Strong Attack / Strong Parry")]
    [Tooltip("쇠+불 부적 조합 같은 효과로, 강공격도 패링 가능할 때 true로 켜줌")]
    public bool strongParryAssistActive = false;

    [Tooltip("강패링 성공 시 잠깐 들어갈 무적 시간")]
    public float strongParryInvulDuration = 0.12f;

    /// <summary>
    /// 강패링 성공 시(강공격을 패링으로 받아냈을 때) 호출되는 이벤트.
    /// 보스별 추가 연출이 필요하면 여기 구독해서 쓰면 됨.
    /// </summary>
    public event System.Action<BossHealth> OnStrongParrySuccess;

    [Header("Ground Check / Stability")]
    public float groundCheckRadius = 0.14f;
    public float groundRememberTime = 0.08f;

    [Header("Optional health forwarder (if you have a separate health component)")]
    public MonoBehaviour healthComponent; // optional

    [Header("Debug / UI")]
    public bool showParryDebugUI = true; // 인게임 OnGUI로 카운트/상태 표시
    public Vector2 parryDebugPosition = new Vector2(10, 10);
    public GUIStyle parryDebugStyle;

    // internals
    Rigidbody2D rb;
    Animator animator;
    bool isFacingRight = true;

    // combo internals
    int comboIndex = 0;        // 1..3
    int maxCombo => 3;
    bool isAttacking = false;
    bool queuedNext = false;
    float lastAttackButtonTime = -10f;
    float attackLockTimer = 0f;

    // dash/parry internals
    bool isDashing = false;
    bool canDash = true;
    bool isParrying = false;
    bool canParry = true;

    // === 대시 인터럽트용 ===
    Coroutine _dashRoutine;
    bool _dashInterrupting;

    // parry bookkeeping
    int parrySuccessCount = 0;
    float lastParrySuccessTime = -999f;
    bool parrySpecialLocked = false;

    // prevent multiple consumption of the same incoming hit
    HashSet<int> _recentlyConsumedHitIds = new HashSet<int>();
    float _recentlyConsumedClearDelay = 0.25f;

    // invulnerability
    bool isInvulnerable = false;

    // grounded stability
    float groundedRememberCounter = 0f;

    // public getters
    public bool IsDashing => isDashing;
    public bool IsParrying => isParrying;

    // 튜토리얼 전체 입력 락
    public static bool TutorialInputLocked = false;

    // ===== PATCH: PlayerHealth 강타입 캐시/자동 연결 =====
    PlayerHealth _hpCached;
    PlayerHealth HP
    {
        get
        {
            if (_hpCached) return _hpCached;
            _hpCached = healthComponent as PlayerHealth;
            if (_hpCached) return _hpCached;
            _hpCached = GetComponent<PlayerHealth>()
                     ?? GetComponentInChildren<PlayerHealth>()
                     ?? GetComponentInParent<PlayerHealth>();
            return _hpCached;
        }
    }

    // 로컬 헬퍼(폴백용): 콜라이더 내부 임의 포인트
    Vector3 RandomPointInsideCollider(Collider2D hb)
    {
        var b = hb.bounds;
        for (int i = 0; i < 8; i++)
        {
            float x = UnityEngine.Random.Range(b.min.x, b.max.x);
            float y = UnityEngine.Random.Range(b.min.y, b.max.y);
            var p = new Vector2(x, y);
            if (hb.OverlapPoint(p)) return p;
        }
        return hb.ClosestPoint(transform.position);
    }

    public void ForceParrySpecialFromStrongParry()
    {
        // 이미 스페셜 발동 중이면 무시
        if (parrySpecialLocked) return;

        // 내부 패링 스택을 최대치로 맞춰줌 (이벤트도 같이 쏴줌)
        parrySuccessCount = Mathf.Max(parrySuccessNeeded, parrySuccessCount);
        OnParryStreakChanged?.Invoke(parrySuccessCount);

        // 기존 스페셜 로직 그대로 사용
        StartCoroutine(TriggerParrySpecial());
    }

    // ====== 프리셋 관련 필드 ======
    public InputPreset inputPreset = InputPreset.Preset1;
    public PlayerInputConfig inputConfig;

    public void ApplyInputPreset(InputPreset preset)
    {
        switch (preset)
        {
            case InputPreset.Preset1:
                inputConfig.LeftMove = KeyCode.LeftArrow;
                inputConfig.RightMove = KeyCode.RightArrow;
                inputConfig.JumpKey = KeyCode.Space;
                inputConfig.AttackKey = KeyCode.X;
                inputConfig.ParryKey = KeyCode.C;
                inputConfig.DashKey = KeyCode.LeftShift;
                inputConfig.UseMouseAttack = false;
                break;
            case InputPreset.Preset2:
                inputConfig.LeftMove = KeyCode.A;
                inputConfig.RightMove = KeyCode.D;
                inputConfig.JumpKey = KeyCode.Space;
                inputConfig.AttackKey = KeyCode.Mouse0; // 마우스 좌클릭
                inputConfig.ParryKey = KeyCode.LeftControl;
                inputConfig.DashKey = KeyCode.LeftShift;
                inputConfig.UseMouseAttack = true;
                break;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // ★ 프리셋 적용
        ApplyInputPreset(inputPreset);

        if (!groundCheck) Debug.LogWarning("[PlayerController] groundCheck not assigned!");

        if (parryDebugStyle == null)
        {
            parryDebugStyle = new GUIStyle();
            parryDebugStyle.fontSize = 14;
            parryDebugStyle.normal.textColor = Color.white;
        }

        // ===== PATCH: healthComponent 자동 연결(누락 보호) =====
        if (healthComponent == null)
        {
            var hp = HP;
            if (hp != null) healthComponent = hp;
            else Debug.LogWarning("[PlayerController] healthComponent가 비어 있고 PlayerHealth도 찾지 못했습니다.");
        }
    }

    void Update()
    {
        // 튜토리얼 중이면 어떤 행동도 하지 않음
        if (TutorialInputLocked)
        {
            return;
        }

        HandleInputs();
        HandleMovement();
        HandleGravity();
        UpdateGrounded();
        UpdateAnimationParams();

        if (attackLockTimer > 0f) attackLockTimer -= Time.deltaTime;
    }

    bool Allow(PlayerAction a) => (TutorialAllow == null) || TutorialAllow(a);

    void HandleInputs()
    {
        // 기본 공격 입력
        bool attackPressed = false;
        if (inputConfig.UseMouseAttack)
            attackPressed = Input.GetMouseButtonDown(0);
        else
            attackPressed = Input.GetKeyDown(inputConfig.AttackKey);

        if (attackPressed && Allow(PlayerAction.Attack))
        {
            lastAttackButtonTime = Time.time;
            if (isAttacking)
            {
                if (comboIndex > 0 && comboIndex < maxCombo) queuedNext = true;
            }
            else
            {
                if (!isDashing && !isParrying) StartAttack(1);
            }
        }

        // 대쉬
        if (Input.GetKeyDown(inputConfig.DashKey))
        {
            if (canDash && !isDashing && !isParrying && !isAttacking && attackLockTimer <= 0f)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerDashSFX);
                StartCoroutine(DoDash());
            }
        }

        // 패링
        if (Input.GetKeyDown(inputConfig.ParryKey) && Allow(PlayerAction.Parry))
        {
            if (canParry && !isParrying)
            {
                bool groundedForAction = groundedRememberCounter > 0f;

                if (groundedForAction)
                {
                    // 지상에서는 어떤 애니메이션 중이든 즉시 패링으로 전환
                    InterruptToParryImmediately();
                }
                else
                {
                    // 공중에서는 기존 제한 유지
                    if (!isDashing && attackLockTimer <= 0f)
                    {
                        if (AudioManager.Instance != null)
                            AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerParrySFX);
                        StartCoroutine(DoParry());
                    }
                }
            }
        }

        // 점프
        if (Input.GetKeyDown(inputConfig.JumpKey))
        {
            bool grounded = groundCheck
                ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
                : false;

            if (grounded && !isDashing && !isParrying)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerJumpSFX);

                Vector2 v = rb.linearVelocity;
                v.y = jumpForce;
                rb.linearVelocity = v;

                if (fx) fx.PlayFx("JumpDust", "Feet");
            }
        }
    }

    void HandleMovement()
    {
        // ★ 예전 조작감 복원: 공격 락 / 패링 / 대시 중에는 이동 입력 막기
        bool locked = attackLockTimer > 0f || isParrying;
        if (isDashing) locked = true;

        float moveInput = 0f;

        if (!locked)
        {
            if (Input.GetKey(inputConfig.LeftMove)) moveInput -= 1f;
            if (Input.GetKey(inputConfig.RightMove)) moveInput += 1f;
        }

        Vector2 vel = rb.linearVelocity;
        vel.x = moveInput * moveSpeed;
        rb.linearVelocity = vel;

        if (!Mathf.Approximately(moveInput, 0f))
        {
            if (moveInput > 0 && !isFacingRight) Flip();
            else if (moveInput < 0 && isFacingRight) Flip();
        }
    }

    void HandleGravity()
    {
        if (rb.linearVelocity.y < 0)
            rb.gravityScale = fallGravity;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rb.gravityScale = lowJumpGravity;
        else
            rb.gravityScale = normalGravity;
    }

    void UpdateGrounded()
    {
        bool groundedNow = groundCheck
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : false;

        if (groundedNow) groundedRememberCounter = groundRememberTime;
        else groundedRememberCounter -= Time.deltaTime;

        bool groundedForChar = groundedRememberCounter > 0f;
        if (animator != null)
        {
            animator.SetBool("isGrounded", groundedForChar);
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        }
    }

    void UpdateAnimationParams()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool(animIsDashingBool, isDashing);
            animator.SetBool(animIsParryingBool, isParrying);
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    // --------------------------------------------------------
    // 즉시 패링 인터럽트
    // --------------------------------------------------------
    void InterruptToParryImmediately()
    {
        // 사운드 먼저
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerParrySFX);

        // 1) 대시 중이면 즉시 중단
        ForceStopDash();

        // 2) 공격 중이면 끊기
        if (isAttacking) ResetCombo();

        // 혹시 남은 트리거 정리(안전)
        if (animator != null)
        {
            animator.ResetTrigger(animAttack1);
            animator.ResetTrigger(animAttack2);
            animator.ResetTrigger(animAttack3);
        }

        // 3) 이동 락 해제
        attackLockTimer = 0f;

        // 4) 바로 패링 시작
        StartCoroutine(DoParry());
    }

    // --------------------------------------------------------
    // Combo / Attack functions (자식 히트박스 사용)
    // --------------------------------------------------------
    void StartAttack(int attackStep)
    {
        if (attackStep < 1 || attackStep > maxCombo) return;
        if (isDashing || isParrying) return;

        _attackHitFiredThisSwing = false;
        _fxSpawnedThisSwing = false;

        isAttacking = true;
        comboIndex = attackStep;
        queuedNext = false;

        int idx = Mathf.Clamp(comboIndex - 1, 0, comboLockDurations.Length - 1);
        attackLockTimer = comboLockDurations[idx];

        // 공격 사운드 즉시 재생
        if (AudioManager.Instance != null)
        {
            if (AudioManager.Instance.playerAttackSFX != null)
                AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerAttackSFX);
            else
                AudioManager.Instance.PlayPlayerAttack(); // 폴백
        }

        if (animator != null)
        {
            switch (comboIndex)
            {
                case 1: animator.SetTrigger(animAttack1); break;
                case 2: animator.SetTrigger(animAttack2); break;
                case 3: animator.SetTrigger(animAttack3); break;
            }
        }
    }

    // 애니메이션 이벤트에서 한 프레임 히트할 때 호출
    public void OnAttackHit()
    {
        // 애니메이션 이벤트가 여러 번 찍혀 있어도 스윙당 1번만 처리
        if (_attackHitFiredThisSwing) return;
        _attackHitFiredThisSwing = true;

        if (comboIndex <= 0 || comboIndex > maxCombo) return;

        int dmg = (comboDamages != null && comboDamages.Length >= comboIndex)
                    ? comboDamages[comboIndex - 1] : 1;

        Collider2D[] group = null;
        switch (comboIndex)
        {
            case 1: group = hitboxesAttack1; break;
            case 2: group = hitboxesAttack2; break;
            case 3: group = hitboxesAttack3; break;
        }
        if (group == null || group.Length == 0)
        {
            Debug.LogWarning($"[Player] Attack{comboIndex} hitboxes not assigned.");
            return;
        }

        // 실제 맞은 적에게 데미지 주고, FX는 "플레이어 히트박스 내부"에서 딱 1번만 스폰
        ApplyHitboxGroupWithSource(group, enemyLayer, dmg, (hitTarget, sourceHb) =>
        {
            if (!_fxSpawnedThisSwing)
            {
                _fxSpawnedThisSwing = true;
                if (fx != null && !string.IsNullOrEmpty(hitFxId))
                    fx.PlayFxOnCollider(hitFxId, sourceHb);  // ← 히트박스 내부에서 스폰
            }
        });
    }

    public void OnAttackAnimationEnd()
    {
        if (queuedNext && comboIndex < maxCombo)
            StartAttack(comboIndex + 1);
        else ResetCombo();
    }

    void ResetCombo()
    {
        comboIndex = 0;
        isAttacking = false;
        queuedNext = false;
        attackLockTimer = 0f;
        _attackHitFiredThisSwing = false;
        _fxSpawnedThisSwing = false;
    }

    // --------------------------------------------------------
    // Dash coroutine (중단 가능)
    // --------------------------------------------------------
    IEnumerator DoDash()
    {
        if (fx) fx.PlayFx("DashStart", "Body");

        canDash = false;
        _dashInterrupting = false;
        isDashing = true;

        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == animDashTrigger && param.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(animDashTrigger);
                    break;
                }
            }
        }

        _dashRoutine = StartCoroutine(DashRoutine());
        yield break; // 대시는 별도 루틴에서 처리
    }

    IEnumerator DashRoutine()
    {
        float elapsed = 0f;
        float dir = (transform.localScale.x >= 0f) ? 1f : -1f;

        float prevGravity = rb.gravityScale;
        float originalYVel = rb.linearVelocity.y;

        float prevLock = attackLockTimer;
        attackLockTimer = dashDuration;

        while (elapsed < dashDuration && !_dashInterrupting)
        {
            rb.linearVelocity = new Vector2(dir * dashSpeed, originalYVel);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 종료 정리
        rb.gravityScale = prevGravity;
        isDashing = false;
        attackLockTimer = _dashInterrupting ? 0f : prevLock;

        _dashRoutine = null;

        // 쿨다운
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void ForceStopDash()
    {
        if (!isDashing) return;

        _dashInterrupting = true;

        if (_dashRoutine != null)
        {
            StopCoroutine(_dashRoutine);
            _dashRoutine = null;
        }

        isDashing = false;
        rb.gravityScale = normalGravity;
        attackLockTimer = 0f;

        // 쿨다운은 유지
        StopCoroutine(nameof(DashCooldownOnly));
        StartCoroutine(DashCooldownOnly());
    }

    IEnumerator DashCooldownOnly()
    {
        canDash = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --------------------------------------------------------
    // Parry coroutine (패링 윈도우 ON)
    // --------------------------------------------------------
    IEnumerator DoParry()
    {
        canParry = false;
        _parryRewardedThisWindow = false;   // ★ 패링 윈도우 시작할 때 반드시 리셋
        isParrying = true;
        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == animParryTrigger && param.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(animParryTrigger);
                    break;
                }
            }
        }

        float elapsed = 0f;
        while (elapsed < parryWindow)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        isParrying = false;

        yield return new WaitForSeconds(parryCooldown);
        canParry = true;
    }

    public bool HandleStrongAttackHit(Collider2D sourceCollider)
    {
        // 1) 패링 중이 아니면 그냥 일반 히트
        if (!isParrying)
            return false;

        // 2) 강패링 보조가 꺼져 있으면, 이 공격은 '절대 패링 불가'
        if (!strongParryAssistActive)
        {
            return false;
        }

        BossHealth boss = null;
        if (sourceCollider != null)
        {
            boss = sourceCollider.GetComponentInParent<BossHealth>()
                ?? sourceCollider.GetComponent<BossHealth>()
                ?? sourceCollider.GetComponentInChildren<BossHealth>();
        }

        if (boss != null)
        {
            boss.ApplyStrongParryGroggy();
            OnStrongParrySuccess?.Invoke(boss);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerParrySFX);

        if (animator != null && !string.IsNullOrEmpty(animParrySuccessTrigger))
            animator.SetTrigger(animParrySuccessTrigger);

        StartCoroutine(TemporaryInvul(strongParryInvulDuration));
        return true;
    }

    public bool TryHandleProjectileHit(SlashProjectile2D projectile, Collider2D hitCollider, int damage)
    {
        bool consumed = ConsumeHitboxIfParrying(hitCollider);
        if (consumed)
        {
            Debug.Log($"[Player] Parried projectile '{(projectile ? projectile.name : "null")}' via collider '{(hitCollider ? hitCollider.name : "null")}'.");
        }
        return consumed;
    }

    // --------------------------------------------------------
    // Parry consumption API
    // --------------------------------------------------------
    public bool ConsumeHitboxIfParrying(object hitInfo = null)
    {
        if (!isParrying && isInvulnerable) return true;

        if (isParrying)
        {
            if (_parryRewardedThisWindow) return true;

            int hitId = 0;
            if (hitInfo is Collider2D col) hitId = col.GetInstanceID();
            else if (hitInfo is GameObject go) hitId = go.GetInstanceID();
            else if (hitInfo is int i) hitId = i;

            if (hitId != 0)
            {
                if (_recentlyConsumedHitIds.Contains(hitId)) return true;
                _recentlyConsumedHitIds.Add(hitId);
                StartCoroutine(ClearConsumedHitAfter(hitId, _recentlyConsumedClearDelay));
            }
            else
            {
                if (Time.time - lastParrySuccessTime < 0.06f) return true;
            }

            _parryRewardedThisWindow = true;

            parrySuccessCount++;
            OnParryStreakChanged?.Invoke(parrySuccessCount);
            lastParrySuccessTime = Time.time;

            var hp = HP;
            if (hp != null) hp.AddParryCount();
            else if (healthComponent != null)
            {
                var addParryMethod = healthComponent.GetType().GetMethod("AddParryCount");
                if (addParryMethod != null) addParryMethod.Invoke(healthComponent, null);
            }

            if (parrySuccessCount < parrySuccessNeeded)
            {
                if (Time.time - _lastParryFeedbackTime > _parryFeedbackMinInterval)
                {
                    _lastParryFeedbackTime = Time.time;
                    if (animator != null && !string.IsNullOrEmpty(animParrySuccessTrigger))
                        animator.SetTrigger(animParrySuccessTrigger);
                    if (fx != null && !string.IsNullOrEmpty(parrySuccessFxId))
                    {
                        var area = GetFirstEnabledCollider(parrySuccessFxAreas);
                        if (area != null) fx.PlayFxOnCollider(parrySuccessFxId, area);
                        else fx.PlayFx(parrySuccessFxId, null, transform);
                    }
                }
            }
            else if (!parrySpecialLocked && parrySuccessCount >= parrySuccessNeeded)
            {
                StartCoroutine(TriggerParrySpecial());
            }

            StartCoroutine(TemporaryInvul(0.06f));
            return true;
        }

        return false;
    }

    Collider2D GetFirstEnabledCollider(Collider2D[] arr)
    {
        if (arr == null) return null;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] && arr[i].enabled) return arr[i];
        }
        return null;
    }

    IEnumerator ClearConsumedHitAfter(int hitId, float delay)
    {
        yield return new WaitForSeconds(delay);
        _recentlyConsumedHitIds.Remove(hitId);
    }

    // --------------------------------------------------------
    // IDamageable
    // --------------------------------------------------------
    public void TakeDamage(int amount)
    {
        TakeDamageInternal(amount, allowParry: true);
    }

    public void TakeStrongDamage(int amount)
    {
        TakeDamageInternal(amount, allowParry: false);
    }

    void TakeDamageInternal(int amount, bool allowParry)
    {
        if (isInvulnerable) return;

        if (allowParry && isParrying)
        {
            bool consumed = ConsumeHitboxIfParrying(null);
            if (consumed) return;
        }

        Debug.Log($"[Player] Took {amount} damage. (allowParry={allowParry})");

        if (healthComponent != null)
        {
            var mi = healthComponent.GetType().GetMethod("TakeDamage");
            if (mi != null) mi.Invoke(healthComponent, new object[] { amount });
            else Debug.LogWarning("[Player] healthComponent provided but no TakeDamage(int) found.");
            return;
        }
    }

    // --------------------------------------------------------
    // Parry special logic
    // --------------------------------------------------------
    IEnumerator TriggerParrySpecial()
    {
        _specialHitFiredThisSwing = false;
        _specialFxSpawnedThisSwing = false;

        parrySpecialLocked = true;
        if (animator != null) animator.SetTrigger(animParrySpecialTrigger);

        var cam2D = Camera.main ? Camera.main.GetComponent<CameraSimple2D>() : null;
        if (cam2D) cam2D.PunchZoomParrySpecial();

        yield return TemporaryInvul(parrySpecialInvulDuration);
        parrySuccessCount = 0;
        lastParrySuccessTime = -999f;
        OnParryStreakChanged?.Invoke(parrySuccessCount);
        yield return new WaitForSeconds(0.25f);
        parrySpecialLocked = false;
    }

    public void OnParrySpecialHit()
    {
        if (_specialHitFiredThisSwing) return;
        _specialHitFiredThisSwing = true;

        if (healthComponent != null)
        {
            var useEnhancedMethod = healthComponent.GetType().GetMethod("UseEnhancedAttack");
            if (useEnhancedMethod != null)
                useEnhancedMethod.Invoke(healthComponent, null);
        }

        var mask = (parrySpecialEnemyMaskOverride.value != 0) ? parrySpecialEnemyMaskOverride : enemyLayer;

        if (hitboxesParrySpecial == null || hitboxesParrySpecial.Length == 0)
        {
            Debug.LogWarning("[Player] hitboxesParrySpecial not assigned.");
            return;
        }

        ApplyHitboxGroupWithSource(
            hitboxesParrySpecial,
            mask,
            damage: parrySpecialDamage,
            onTouch: (col, sourceHb) =>
            {
                if (!_specialFxSpawnedThisSwing && fx != null && !string.IsNullOrEmpty(parrySpecialFxId))
                {
                    _specialFxSpawnedThisSwing = true;
                    fx.PlayFxOnCollider(parrySpecialFxId, sourceHb);
                }

                var boss = col.GetComponentInParent<BulgasariBoss>();
                if (boss != null) boss.OnParrySpecialLanded(col);

                var grog = col.GetComponent<BulgasariGroggyTarget>() ?? col.GetComponentInParent<BulgasariGroggyTarget>();
                if (grog != null) grog.NotifyParrySpecialHit(this);
            });
    }

    void ApplyHitboxGroupWithSource(
        Collider2D[] group, LayerMask mask, int damage,
        System.Action<Collider2D, Collider2D> onTouch)
    {
        if (group == null || group.Length == 0) return;

        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(mask);

        var seen = new HashSet<Collider2D>();

        foreach (var hb in group)
        {
            if (!hb || !hb.enabled) continue;

            _overlapBuf.Clear();
            int count = hb.Overlap(filter, _overlapBuf);
            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuf[i];
                if (!c || seen.Contains(c)) continue;
                seen.Add(c);

                var dmg = c.GetComponent<IDamageable>()
                       ?? c.GetComponentInParent<IDamageable>()
                       ?? c.GetComponentInChildren<IDamageable>();
                if (dmg != null) dmg.TakeDamage(damage);

                onTouch?.Invoke(c, hb);
            }
        }
    }

    IEnumerator TemporaryInvul(float dur)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(dur);
        isInvulnerable = false;
    }

    static readonly List<Collider2D> _overlapBuf = new(16);

    void ApplyHitboxGroup(Collider2D[] group, LayerMask mask, int damage, Action<Collider2D> onTouch)
    {
        if (group == null || group.Length == 0) return;

        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true
        };
        filter.SetLayerMask(mask);

        var seen = new HashSet<Collider2D>();

        foreach (var hb in group)
        {
            if (!hb || !hb.enabled) continue;

            _overlapBuf.Clear();
            int count = hb.Overlap(filter, _overlapBuf);
            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuf[i];
                if (!c || seen.Contains(c)) continue;
                seen.Add(c);

                var target = c.GetComponent<IDamageable>()
                         ?? c.GetComponentInParent<IDamageable>()
                         ?? c.GetComponentInChildren<IDamageable>();
                if (target != null) target.TakeDamage(damage);

                onTouch?.Invoke(c);
            }
        }
    }

    IEnumerator ApplyHitboxGroupFor(Collider2D[] group, LayerMask mask, int damage, float duration, Action<Collider2D> onTouch = null)
    {
        float t = 0f;
        while (t < duration)
        {
            ApplyHitboxGroup(group, mask, damage, onTouch);
            yield return null;
            t += Time.deltaTime;
        }
    }

    bool _parryRewardedThisWindow = false;

    void OnGUI()
    {
        if (!showParryDebugUI) return;

        if (parryDebugStyle == null)
        {
            parryDebugStyle = new GUIStyle(GUI.skin.label);
            parryDebugStyle.fontSize = 14;
            parryDebugStyle.normal.textColor = Color.white;
        }

        string txt =
            $"Parry: {parrySuccessCount}/{parrySuccessNeeded}\n" +
            $"Parrying: {isParrying}\n" +
            $"Dashing: {isDashing}";

        GUI.Label(new Rect(parryDebugPosition.x, parryDebugPosition.y, 260f, 64f), txt, parryDebugStyle);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (isParrying)
        {
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawWireDisc(transform.position + Vector3.up * 1.2f, Vector3.forward, 0.4f);
        }
    }
#endif
}
