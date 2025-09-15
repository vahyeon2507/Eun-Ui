using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
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
    public float[] comboLockDurations = new float[3] { 0.25f, 0.3f, 0.4f };

    [Header("Animator parameter names (must match)")]
    public string animAttack1 = "Attack1";
    public string animAttack2 = "Attack2";
    public string animAttack3 = "Attack3";

    [Header("Dash")]
    public float dashSpeed = 16f;
    public float dashDuration = 0.16f;
    public float dashCooldown = 0.6f;
    public string animDashTrigger = "Dash";         // Animator에 트리거로 추가
    public string animIsDashingBool = "isDashing"; // Animator에 Bool로 추가

    [Header("Parry")]
    public float parryWindow = 0.2f;   // 패링 유효 시간
    public float parryCooldown = 1.0f;
    public string animParryTrigger = "Parry";         // 트리거
    public string animIsParryingBool = "isParrying"; // Bool

    [Header("Ground Check / Stability")]
    public float groundCheckRadius = 0.14f;        // 반지름 (조정 권장 0.12~0.18)
    public float groundRememberTime = 0.08f;       // 코요테 타임 (false flicker 방지)

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

    // grounded stability
    float groundedRememberCounter = 0f;

    // public getters for other systems (e.g. boss)
    public bool IsDashing => isDashing;
    public bool IsParrying => isParrying;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (attackPoint == null) Debug.LogWarning("[PlayerController] attackPoint not assigned!");
        if (groundCheck == null) Debug.LogWarning("[PlayerController] groundCheck not assigned!");
    }

    void Update()
    {
        HandleInputs();

        // movement uses physics velocity but input read here
        HandleMovement();

        HandleGravity();

        // 안정적인 ground 판정
        UpdateGrounded();

        UpdateAnimationParams();

        if (attackLockTimer > 0f) attackLockTimer -= Time.deltaTime;
    }

    void HandleInputs()
    {
        // ATTACK -> Left mouse button
        if (Input.GetMouseButtonDown(0))
        {
            lastAttackButtonTime = Time.time;
            if (isAttacking)
            {
                // 이미 공격 중이면 큐 넣기 허용 (다음 단계가 있으면)
                if (comboIndex > 0 && comboIndex < maxCombo) queuedNext = true;
            }
            else
            {
                // 공격 시작. 대쉬/패리 중일 때 공격 안 하도록
                if (!isDashing && !isParrying) StartAttack(1);
            }
        }

        // DASH (Left Shift or Right Shift)
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            if (canDash && !isDashing && !isParrying && !isAttacking && attackLockTimer <= 0f)
            {
                StartCoroutine(DoDash());
            }
        }

        // PARRY (C or Left Ctrl)
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (canParry && !isParrying && !isDashing && attackLockTimer <= 0f)
            {
                StartCoroutine(DoParry());
            }
        }

        // JUMP
        if (Input.GetButtonDown("Jump"))
        {
            bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            if (grounded && !isDashing && !isParrying)
            {
                Vector2 v = rb.linearVelocity;
                v.y = jumpForce;
                rb.linearVelocity = v;
            }
        }
    }

    void HandleMovement()
    {
        // locked only when attackLockTimer active or while parrying/dashing.
        // (이전처럼 isAttacking 전체를 막지 않음 — attackLockTimer로 잠금 제어)
        bool locked = attackLockTimer > 0f || isParrying;
        if (isDashing) locked = true;

        float moveInput = locked ? 0f : Input.GetAxisRaw("Horizontal");

        // apply horizontal velocity while preserving vertical velocity
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
        // current overlap test
        bool groundedNow = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (groundedNow)
            groundedRememberCounter = groundRememberTime;
        else
            groundedRememberCounter -= Time.deltaTime;

        bool groundedForChar = groundedRememberCounter > 0f;

        // animator 파라미터에 전달
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
            // Speed (move) — 애니메이터에서 달리기 전이에 사용
            animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));

            // dash/parry bools
            animator.SetBool(animIsDashingBool, isDashing);
            animator.SetBool(animIsParryingBool, isParrying);
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.localScale = new Vector3(transform.localScale.x * -1f, transform.localScale.y, transform.localScale.z);
    }

    // --------------------------------------------------------
    // Combo / Attack functions
    // --------------------------------------------------------
    void StartAttack(int attackStep)
    {
        if (attackStep < 1 || attackStep > maxCombo) return;
        if (isDashing || isParrying) return; // optional: block attacks while special states

        isAttacking = true;
        comboIndex = attackStep;
        queuedNext = false;

        // 안전하게 인덱스 체크
        int idx = Mathf.Clamp(comboIndex - 1, 0, comboLockDurations.Length - 1);
        attackLockTimer = comboLockDurations[idx];

        // animator 트리거
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

    // 이 함수는 애니메이션 이벤트로 호출되어야 함 (데미지 프레임)
    public void OnAttackHit()
    {
        if (comboIndex <= 0 || comboIndex > maxCombo) return;

        float range = (comboRanges != null && comboRanges.Length >= comboIndex) ? comboRanges[comboIndex - 1] : 0.6f;
        int dmg = (comboDamages != null && comboDamages.Length >= comboIndex) ? comboDamages[comboIndex - 1] : 1;

        if (attackPoint == null) { Debug.LogWarning("[PlayerController] attackPoint null"); return; }

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, range, enemyLayer);
        Debug.Log($"PerformAttack_DEBUG: OverlapCircleAll found {hits.Length} colliders (step {comboIndex})");

        foreach (Collider2D col in hits)
        {
            if (col == null) continue;
            // IDamageable 우선
            var dmgComp = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>() ?? col.GetComponentInChildren<IDamageable>();
            if (dmgComp != null) { dmgComp.TakeDamage(dmg); Debug.Log($"Hit {col.name} for {dmg}"); continue; }

            // 예: 기존 EnemyHealth 체계
            var enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null) { enemyHealth.TakeDamage(dmg); Debug.Log($"Hit EnemyHealth {col.name} for {dmg}"); }
        }
    }

    // 애니메이션 이벤트에서 공격 애니메이션이 끝날 때 호출
    public void OnAttackAnimationEnd()
    {
        if (queuedNext && comboIndex < maxCombo)
        {
            StartAttack(comboIndex + 1);
        }
        else
        {
            ResetCombo();
        }
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
        if (animator != null) animator.SetTrigger(animDashTrigger);

        float elapsed = 0f;
        float dir = isFacingRight ? 1f : -1f;

        float prevGravity = rb.gravityScale;
        float originalYVel = rb.linearVelocity.y;

        // lock movement for dash duration
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

        // cooldown
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --------------------------------------------------------
    // Parry coroutine (temporary parry window)
    // --------------------------------------------------------
    IEnumerator DoParry()
    {
        canParry = false;
        isParrying = true;
        if (animator != null) animator.SetTrigger(animParryTrigger);

        float elapsed = 0f;
        while (elapsed < parryWindow)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        isParrying = false;

        // cooldown
        yield return new WaitForSeconds(parryCooldown);
        canParry = true;
    }

    // --------------------------------------------------------
    // Gizmos for debug (ground check & attack range)
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
            // show current combo range when attacking (or first range if idle)
            int idx = Mathf.Clamp(comboIndex - 1, 0, comboRanges.Length - 1);
            float r = (comboRanges != null && comboRanges.Length > 0) ? comboRanges[idx] : comboRanges[0];
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, r);
        }
    }
}
