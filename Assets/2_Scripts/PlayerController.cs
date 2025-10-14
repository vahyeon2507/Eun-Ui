using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerController — 패링/스페셜/히트박스 자식 콜라이더판 (디버그 UI 포함)
// - 3연속 기본공격과 스페셜 공격 모두 "자식 Collider2D"로만 히트 판정.
// - (호환) attackPoint는 원거리/탈리스만 등의 발사 기준점으로만 사용.
// - 스페셜 적중 시 BulgasariBoss.OnParrySpecialLanded(col)로 그로기 트리거 신호.
// - IParryStreakProvider 유지.  AudioManager 즉시 재생 로직 병합.

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour, IDamageable, IParryStreakProvider
{
    [HideInInspector] public bool ExternalRangedOverride;

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

    // IParryStreakProvider
    public int CurrentParryStreak => parrySuccessCount;
    public event Action<int> OnParryStreakChanged;

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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (!groundCheck) Debug.LogWarning("[PlayerController] groundCheck not assigned!");

        if (parryDebugStyle == null)
        {
            parryDebugStyle = new GUIStyle();
            parryDebugStyle.fontSize = 14;
            parryDebugStyle.normal.textColor = Color.white;
        }
    }

    void Update()
    {
        HandleInputs();
        HandleMovement();
        HandleGravity();
        UpdateGrounded();
        UpdateAnimationParams();

        if (attackLockTimer > 0f) attackLockTimer -= Time.deltaTime;
    }

    void HandleInputs()
    {
        // 기본 공격 입력(좌클릭)
        if (Input.GetMouseButtonDown(0))
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
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            if (canDash && !isDashing && !isParrying && !isAttacking && attackLockTimer <= 0f)
            {
                // 대시 사운드 즉시 재생
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerDashSFX);
                StartCoroutine(DoDash());
            }
        }

        // 패링
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (canParry && !isParrying && !isDashing && attackLockTimer <= 0f)
            {
                // 패링 사운드 즉시 재생
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerParrySFX);
                StartCoroutine(DoParry());
            }
        }

        // 점프
        if (Input.GetButtonDown("Jump"))
        {
            bool grounded = groundCheck
                ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
                : false;

            if (grounded && !isDashing && !isParrying)
            {
                // 점프 사운드 즉시 재생
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
        bool locked = attackLockTimer > 0f || isParrying;
        if (isDashing) locked = true;

        float moveInput = locked ? 0f : Input.GetAxisRaw("Horizontal");

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
    // Combo / Attack functions (자식 히트박스 사용)
    // --------------------------------------------------------
    void StartAttack(int attackStep)
    {
        if (attackStep < 1 || attackStep > maxCombo) return;
        if (isDashing || isParrying) return;

        isAttacking = true;
        comboIndex = attackStep;
        queuedNext = false;

        int idx = Mathf.Clamp(comboIndex - 1, 0, comboLockDurations.Length - 1);
        attackLockTimer = comboLockDurations[idx];

        // 공격 사운드 즉시 재생 (애니 이벤트보다 먼저)
        if (AudioManager.Instance != null)
        {
            if (AudioManager.Instance.playerAttackSFX != null)
            {
                AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerAttackSFX);
            }
            else
            {
                // 폴백
                AudioManager.Instance.PlayPlayerAttack();
            }
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
        if (group == null || group.Length == 0) { Debug.LogWarning($"[Player] Attack{comboIndex} hitboxes not assigned."); return; }

        // ① 대미지 적용 + ② 실제 맞은 콜라이더에만 이펙트
        //    (여러 적을 동시에 치면 각 적마다 1번씩 FX)
        var spawnedOn = new HashSet<Transform>();
        ApplyHitboxGroup(group, enemyLayer, dmg, (col) =>
        {
            var root = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform.root;
            if (spawnedOn.Add(root))
                fx?.PlayFxOnCollider("AttackSpark", col);
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
    }

    // --------------------------------------------------------
    // Dash coroutine
    // --------------------------------------------------------
    IEnumerator DoDash()
    {
        if (fx) fx.PlayFx("DashStart", "Body");

        canDash = false;
        isDashing = true;
        if (animator != null)
        {
            // 안전한 트리거 확인 후 설정
            foreach (var param in animator.parameters)
            {
                if (param.name == animDashTrigger && param.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(animDashTrigger);
                    break;
                }
            }
        }

        float elapsed = 0f;
        float dir = isFacingRight ? 1f : -1f;

        float prevGravity = rb.gravityScale;
        float originalYVel = rb.linearVelocity.y;

        float prevLock = attackLockTimer;
        attackLockTimer = dashDuration;

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(dir * dashSpeed, originalYVel);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.gravityScale = prevGravity;
        isDashing = false;
        attackLockTimer = prevLock;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --------------------------------------------------------
    // Parry coroutine (패링 윈도우 ON)
    // --------------------------------------------------------
    IEnumerator DoParry()
    {
        canParry = false;
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

    // --------------------------------------------------------
    // Parry consumption API — 보스에서 호출할 수 있도록 public
    // --------------------------------------------------------
    public bool ConsumeHitboxIfParrying(object hitInfo = null)
    {
        // 디버그 로그 - 호출 및 현재 상태 확인
        Debug.Log($"[Player] ConsumeHitboxIfParrying called. isParrying={isParrying}, isInvulnerable={isInvulnerable}, hitInfo={hitInfo}");

        if (isInvulnerable) return true;

        if (isParrying)
        {
            int hitId = 0;
            if (hitInfo is Collider2D col) hitId = col.GetInstanceID();
            else if (hitInfo is GameObject go) hitId = go.GetInstanceID();
            else if (hitInfo is int i) hitId = i;

            // 중복 소비 방지
            if (hitId != 0)
            {
                if (_recentlyConsumedHitIds.Contains(hitId))
                {
                    return true;
                }

                _recentlyConsumedHitIds.Add(hitId);
                StartCoroutine(ClearConsumedHitAfter(hitId, _recentlyConsumedClearDelay));
            }
            else
            {
                if (Time.time - lastParrySuccessTime < 0.06f) return true;
            }

            // 성공 처리
            parrySuccessCount++;
            OnParryStreakChanged?.Invoke(parrySuccessCount);

            lastParrySuccessTime = Time.time;

            // PlayerHealth의 패링바 업데이트(있으면)
            if (healthComponent != null)
            {
                var addParryMethod = healthComponent.GetType().GetMethod("AddParryCount");
                if (addParryMethod != null) addParryMethod.Invoke(healthComponent, null);
            }

            // 잠깐 무적
            StartCoroutine(TemporaryInvul(0.06f));

            // 스페셜 조건 달성
            if (!parrySpecialLocked && parrySuccessCount >= parrySuccessNeeded)
            {
                StartCoroutine(TriggerParrySpecial());
            }

            return true;
        }

        return false;
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
        if (isInvulnerable) return;

        if (isParrying)
        {
            bool consumed = ConsumeHitboxIfParrying(null);
            if (consumed) return;
        }

        Debug.Log($"[Player] Took {amount} damage.");
        if (healthComponent != null)
        {
            var mi = healthComponent.GetType().GetMethod("TakeDamage");
            if (mi != null) mi.Invoke(healthComponent, new object[] { amount });
            else Debug.LogWarning("[Player] healthComponent provided but no TakeDamage(int) found.");
            return;
        }
    }

    // --------------------------------------------------------
    // Parry special logic (자식 히트박스 사용)
    // --------------------------------------------------------
    IEnumerator TriggerParrySpecial()
    {
        parrySpecialLocked = true;
        if (animator != null) animator.SetTrigger(animParrySpecialTrigger);

        // 스페셜 타격은 애니메이션 이벤트(OnParrySpecialHit)에서 처리
        yield return TemporaryInvul(parrySpecialInvulDuration);

        // **스페셜을 소비했을 때만 카운트 초기화**
        parrySuccessCount = 0;
        lastParrySuccessTime = -999f;
        OnParryStreakChanged?.Invoke(parrySuccessCount);

        yield return new WaitForSeconds(0.25f);
        parrySpecialLocked = false;
    }

    // 애니메이션 이벤트로 1틱(프레임) 스페셜 히트
    public void OnParrySpecialHit()
    {
        Debug.Log("[PS] OnParrySpecialHit fired");

        // UI2 쪽 로직 유지: PlayerHealth 강화 공격 사용 시도
        if (healthComponent != null)
        {
            var useEnhancedMethod = healthComponent.GetType().GetMethod("UseEnhancedAttack");
            if (useEnhancedMethod != null)
                useEnhancedMethod.Invoke(healthComponent, null);
        }

        // 사용할 레이어 마스크 결정
        var mask = (parrySpecialEnemyMaskOverride.value != 0) ? parrySpecialEnemyMaskOverride : enemyLayer;

        if (hitboxesParrySpecial == null || hitboxesParrySpecial.Length == 0)
        {
            Debug.LogWarning("[Player] hitboxesParrySpecial not assigned.");
            return;
        }

        ApplyHitboxGroup(
            hitboxesParrySpecial,
            mask,
            damage: 3,    // 필요 시 프로젝트 규칙에 맞게 조정
            onTouch: (col) =>
            {
                Debug.Log($"[PS] hit {col.name}");

                // 보스 그로기 트리거 알림
                var boss = col.GetComponentInParent<BulgasariBoss>();
                if (boss != null) boss.OnParrySpecialLanded(col);

                var grog = col.GetComponent<BulgasariGroggyTarget>() ?? col.GetComponentInParent<BulgasariGroggyTarget>();
                if (grog != null) grog.NotifyParrySpecialHit(this);
            });
    }

    IEnumerator TemporaryInvul(float dur)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(dur);
        isInvulnerable = false;
    }

    // --------------------------------------------------------
    // Hitbox group executor (공통 루틴)
    // --------------------------------------------------------
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

                var dmg = c.GetComponent<IDamageable>()
                       ?? c.GetComponentInParent<IDamageable>()
                       ?? c.GetComponentInChildren<IDamageable>();
                if (dmg != null) dmg.TakeDamage(damage);

                onTouch?.Invoke(c);
            }
        }
    }

    // (선택) 지속 판정 — 필요 시 애니 이벤트에서 호출
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
