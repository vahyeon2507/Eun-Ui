using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerController — 패링/스페셜/호환성 보강판 (디버그 UI 포함)
// 변경: 패링 성공 카운트는 시간 경과로 자동 초기화되지 않음.
//       오직 ParrySpecial 발동(소모) 시에만 parrySuccessCount를 0으로 초기화.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour, IDamageable
{
    [HideInInspector] public bool ExternalRangedOverride;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Gravity tuning")]
    public float fallGravity = 2.5f;
    public float lowJumpGravity = 2f;
    public float normalGravity = 1f;

    [Header("Attack - general")]
    public Transform attackPoint;
    public LayerMask enemyLayer;
    public float inputBufferWindow = 0.45f;

    [Header("Combo (3 steps)")]
    public int maxCombo = 3;
    public float[] comboRanges = new float[3] { 0.6f, 0.8f, 1.0f };
    public int[] comboDamages = new int[3] { 1, 1, 2 };
    // <-- 배열명 정확히: comboLockDurations
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
    public float parryWindow = 0.2f;   // 패링 유효 시간 (버튼 누른 후)
    public float parryCooldown = 1.0f; // 패링 자체 재사용 대기
    public string animParryTrigger = "Parry";
    public string animIsParryingBool = "isParrying";

    [Header("Parry → Special")]
    public int parrySuccessNeeded = 4;
    public string animParrySpecialTrigger = "ParrySpecial";
    public float parrySpecialInvulDuration = 0.3f;
    [Tooltip("(더 이상 자동 리셋을 원하지 않는 경우 무시) 패링 성공 후 카운트 리셋까지의 허용 시간(초). 현재는 사용하지 않습니다.")]
    public float parrySuccessResetDelay = 3.0f; // **이 값은 더 이상 자동 리셋에 사용되지 않음**

    // === NEW: 패링 스페셜 타격 설정 ===
    [Header("Parry Special Attack (hit settings)")]
    public bool parrySpecialUseCircle = true;           // 원형 or 박스형
    public float parrySpecialRadius = 1.6f;             // 원형 반경
    public Vector2 parrySpecialBoxSize = new(3.2f, 1.6f); // 박스 크기
    public Vector2 parrySpecialOffset = new(1.0f, 0.1f);  // 캐릭터 기준 오프셋 (우측이 +)
    public int parrySpecialDamage = 3;                  // 대미지
    public LayerMask parrySpecialEnemyMaskOverride;     // 비워두면 enemyLayer 사용

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
    int comboIndex = 0;
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

        if (attackPoint == null) Debug.LogWarning("[PlayerController] attackPoint not assigned!");
        if (groundCheck == null) Debug.LogWarning("[PlayerController] groundCheck not assigned!");

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

        // (자동 리셋 제거)
    }

    void HandleInputs()
    {
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

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            if (canDash && !isDashing && !isParrying && !isAttacking && attackLockTimer <= 0f)
            {
                // 대시 사운드 재생
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayPlayerDash();
                StartCoroutine(DoDash());
            }
        }

        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (canParry && !isParrying && !isDashing && attackLockTimer <= 0f)
            {
                // 패링 사운드 재생
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayPlayerParry();
                StartCoroutine(DoParry());
            }
        }

        if (Input.GetButtonDown("Jump"))
        {
            bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            if (grounded && !isDashing && !isParrying)
            {
                // 점프 사운드 재생
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayPlayerJump();
                Vector2 v = rb.linearVelocity;
                v.y = jumpForce;
                rb.linearVelocity = v;
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
        bool groundedNow = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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
    // Combo / Attack functions
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

    public void OnAttackHit()
    {
        if (comboIndex <= 0 || comboIndex > maxCombo) return;

        float range = (comboRanges != null && comboRanges.Length >= comboIndex) ? comboRanges[comboIndex - 1] : 0.6f;
        int dmg = (comboDamages != null && comboDamages.Length >= comboIndex) ? comboDamages[comboIndex - 1] : 1;

        if (attackPoint == null) { Debug.LogWarning("[PlayerController] attackPoint null"); return; }

        // 공격 사운드 재생
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPlayerAttack();

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, range, enemyLayer);
        foreach (var col in hits)
        {
            if (col == null) continue;
            var dmgComp = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>() ?? col.GetComponentInChildren<IDamageable>();
            if (dmgComp != null) dmgComp.TakeDamage(dmg);
        }
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
        canDash = false;
        isDashing = true;
        if (animator != null) 
        {
            // 안전한 트리거 설정
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
            // 안전한 트리거 설정
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
                    Debug.Log($"[Player] Hit already consumed (id={hitId})");
                    return true;
                }

                _recentlyConsumedHitIds.Add(hitId);
                StartCoroutine(ClearConsumedHitAfter(hitId, _recentlyConsumedClearDelay));
            }
            else
            {
                // ID가 없을 때의 하드 체크 (짧은 시간 내 재호출 방지)
                if (Time.time - lastParrySuccessTime < 0.06f) return true;
            }

            // 실제 패링 성공 처리 (카운트 증가 등)
            parrySuccessCount++;
            lastParrySuccessTime = Time.time;
            Debug.Log($"[Player] Parry success #{parrySuccessCount}");

            // 잠깐 무적(중복 히트 방지)
            StartCoroutine(TemporaryInvul(0.06f));

            // 스페셜 조건 만족 시 트리거
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
    // IDamageable implementation (defensive: check parry here too)
    // --------------------------------------------------------
    public void TakeDamage(int amount)
    {
        if (isInvulnerable) return;

        if (isParrying)
        {
            bool consumed = ConsumeHitboxIfParrying(null);
            if (consumed) return;
        }

        // 피격 사운드 재생
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPlayerHurt();

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
    // Parry special logic
    // --------------------------------------------------------
    IEnumerator TriggerParrySpecial()
    {
        parrySpecialLocked = true;
        Debug.Log("[Player] ParrySpecial triggered!");
        if (animator != null) animator.SetTrigger(animParrySpecialTrigger);

        // 스페셜 타격은 애니메이션 이벤트(OnParrySpecialHit)에서 처리
        yield return TemporaryInvul(parrySpecialInvulDuration);

        // **스페셜을 소비했을 때만 카운트 초기화**
        parrySuccessCount = 0;
        lastParrySuccessTime = -999f;

        // 잠깐 유지 후 잠금 해제
        yield return new WaitForSeconds(0.25f);
        parrySpecialLocked = false;
    }

    // === NEW: 패링 스페셜 타격 함수(애니메이션 이벤트로 호출) ===
    public void OnParrySpecialHit()
    {
        // 사용할 레이어 마스크 결정
        var mask = (parrySpecialEnemyMaskOverride.value != 0) ? parrySpecialEnemyMaskOverride : enemyLayer;

        // 기준점: attackPoint가 있으면 사용, 없으면 본인 위치
        Vector2 originBase = (attackPoint != null) ? (Vector2)attackPoint.position : (Vector2)transform.position;

        // 좌우 방향(스케일 x 기준) 반영
        float dir = (transform.localScale.x >= 0f) ? 1f : -1f;

        // 오프셋 적용
        Vector2 origin = originBase + new Vector2(parrySpecialOffset.x * dir, parrySpecialOffset.y);

        // 충돌 수집
        Collider2D[] hits;
        if (parrySpecialUseCircle)
        {
            hits = Physics2D.OverlapCircleAll(origin, parrySpecialRadius, mask);
#if UNITY_EDITOR
            Debug.DrawLine(origin + Vector2.right * parrySpecialRadius, origin - Vector2.right * parrySpecialRadius, Color.red, 0.25f);
            Debug.DrawLine(origin + Vector2.up * parrySpecialRadius, origin - Vector2.up * parrySpecialRadius, Color.red, 0.25f);
#endif
        }
        else
        {
            float ang = 0f; // 필요하면 회전값 사용
            // 박스의 가로는 좌우에 맞게 생각했지만 OverlapBoxAll은 크기를 절대값으로 받으므로 그대로 전달
            hits = Physics2D.OverlapBoxAll(origin, parrySpecialBoxSize, ang, mask);
#if UNITY_EDITOR
            Vector2 hx = new(parrySpecialBoxSize.x * 0.5f * dir, parrySpecialBoxSize.y * 0.5f);
            Debug.DrawLine(origin - hx, origin + hx, Color.red, 0.25f);
            Debug.DrawLine(new(origin.x - hx.x, origin.y + hx.y), new(origin.x + hx.x, origin.y - hx.y), Color.red, 0.25f);
#endif
        }

        var touched = new HashSet<Collider2D>();
        foreach (var col in hits)
        {
            if (col == null || touched.Contains(col)) continue;
            touched.Add(col);

            var dmg = col.GetComponent<IDamageable>()
                   ?? col.GetComponentInParent<IDamageable>()
                   ?? col.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(parrySpecialDamage);
                continue;
            }

            // 구형 EnemyHealth 지원
            var enemyHealth = col.GetComponent<EnemyHealth>() ?? col.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(parrySpecialDamage);
                continue;
            }
        }
    }

    IEnumerator TemporaryInvul(float dur)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(dur);
        isInvulnerable = false;
    }

    // --------------------------------------------------------
    // Gizmos
    // --------------------------------------------------------
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (attackPoint != null && comboRanges != null && comboRanges.Length > 0)
        {
            int idx = Mathf.Clamp(comboIndex - 1, 0, comboRanges.Length - 1);
            float r = (comboRanges != null && comboRanges.Length > 0) ? comboRanges[idx] : comboRanges[0];
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, r);
        }

#if UNITY_EDITOR
        if (isParrying)
        {
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawWireDisc(transform.position + Vector3.up * 1.2f, Vector3.forward, 0.4f);
        }
#endif
    }

    // --------------------------------------------------------
    // Simple in-game debug UI for parry status/count
    // --------------------------------------------------------
    void OnGUI()
    {
        if (!showParryDebugUI) return;

        string text = $"Parry: {(isParrying ? "ON" : "off")}  |  SuccessCount: {parrySuccessCount} / {parrySuccessNeeded}\n" +
                      $"ParrySpecialLocked: {parrySpecialLocked}  |  Invul: {isInvulnerable}\n" +
                      $"LastParryTimeAgo: {(lastParrySuccessTime < -900f ? "n/a" : (Time.time - lastParrySuccessTime).ToString("F2") + "s")}";

        Rect r = new Rect(parryDebugPosition.x, parryDebugPosition.y, 360, 60);
        GUI.Box(r, GUIContent.none);
        GUI.Label(new Rect(r.x + 6, r.y + 4, r.width - 10, r.height - 8), text, parryDebugStyle);
    }
}
