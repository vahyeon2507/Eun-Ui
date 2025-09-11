using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // --- Movement / Jump ---
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isFacingRight = true;

    // --- Attack (existing) ---
    [Header("Attack")]
    public Transform attackPoint;
    public float attackRange = 0.5f;
    public LayerMask enemyLayer;
    public int attackDamage = 1;
    public float attackCooldown = 0.5f;
    private float lastAttackTime;

    // --- Gravity tweak (keeps your earlier logic) ---
    private float defaultGravityScale;

    // --- Dash ---
    [Header("Dash")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashSpeed = 18f;          // ���� �ӵ�
    public float dashDuration = 0.12f;     // dash ���ӽð�
    public float dashCooldown = 0.6f;
    private bool isDashing = false;
    private float lastDashTime = -99f;
    private float dashStartTime;
    private float savedGravityScaleForDash;

    // --- Parry ---
    [Header("Parry")]
    public KeyCode parryKey = KeyCode.LeftControl;
    public float parryWindow = 0.18f;      // �и� ������(ª��)
    public float parryCooldown = 0.6f;
    private bool isParrying = false;
    private float lastParryTime = -99f;

    // --- Public getters so other scripts (Enemy/Hitbox/Health) can check ---
    public bool IsDashing => isDashing;
    public bool IsParrying => isParrying;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();

        defaultGravityScale = rb.gravityScale;
    }

    void Update()
    {
        HandleInputs();
        UpdateAnimationParams();
    }

    void FixedUpdate()
    {
        // movement only applied in FixedUpdate for physics stability
        HandleMovement(); // this will skip horizontal control when dashing
        HandleGravity();
    }

    void HandleInputs()
    {
        // Attack (existing)
        if (Input.GetKeyDown(KeyCode.X) && Time.time >= lastAttackTime + attackCooldown)
        {
            animator.SetTrigger("Attack");
            lastAttackTime = Time.time;
        }

        // DASH - start only if not currently dashing and off cooldown
        if (Input.GetKeyDown(dashKey) && Time.time >= lastDashTime + dashCooldown && !isDashing && !isParrying)
        {
            StartCoroutine(DashRoutine());
        }

        // PARRY - start only if not currently parrying and off cooldown
        if (Input.GetKeyDown(parryKey) && Time.time >= lastParryTime + parryCooldown && !isParrying && !isDashing)
        {
            StartCoroutine(ParryRoutine());
        }
    }

    void HandleMovement()
    {
        // while dashing we fully control velocity in DashRoutine
        if (isDashing) return;

        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        if (moveInput > 0 && !isFacingRight) Flip();
        else if (moveInput < 0 && isFacingRight) Flip();

        // jump (original)
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.12f, groundLayer);
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    IEnumerator DashRoutine()
    {
        isDashing = true;
        lastDashTime = Time.time;
        dashStartTime = Time.time;

        // save gravity and temporarily reduce/zero to keep dash flat (tweak as you like)
        savedGravityScaleForDash = rb.gravityScale;
        rb.gravityScale = 0f;

        // lock vertical velocity to 0 for the dash (optional)
        rb.linearVelocity = new Vector2((isFacingRight ? 1f : -1f) * dashSpeed, 0f);

        // animator
        if (animator != null) animator.SetBool("isDashing", true);

        // wait for dash duration
        yield return new WaitForSeconds(dashDuration);

        // restore
        rb.gravityScale = savedGravityScaleForDash;
        isDashing = false;
        if (animator != null) animator.SetBool("isDashing", false);
    }

    IEnumerator ParryRoutine()
    {
        isParrying = true;
        lastParryTime = Time.time;

        if (animator != null) animator.SetBool("isParrying", true);

        // parry input gives very small window where incoming hits should be ignored/reflected
        yield return new WaitForSeconds(parryWindow);

        isParrying = false;
        if (animator != null) animator.SetBool("isParrying", false);
    }

    void HandleGravity()
    {
        // keep your original gravity tweaks (if you had specifics)
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = Mathf.Max(rb.gravityScale, 2.5f); // falling faster (if not dashing)
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.gravityScale = Mathf.Max(rb.gravityScale, 2f);
        }
        else
        {
            // only reset if not dashing
            if (!isDashing)
                rb.gravityScale = defaultGravityScale;
        }
    }

    void UpdateAnimationParams()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.12f, groundLayer);
        if (animator != null)
        {
            animator.SetBool("isGrounded", isGrounded);
            animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
            animator.SetBool("isDashing", isDashing);   // keep animator synced
            animator.SetBool("isParrying", isParrying); // keep animator synced
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        // DO NOT flip localScale X, use spriteRenderer.flipX to avoid physics issues
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.flipX = !isFacingRight;
    }

    // ���� ���� ó�� (�ִϸ��̼� �̺�Ʈ���� ȣ�� �Ǵ� ���� ���)
    void PerformAttack()
    {
        Vector2 attackPos = attackPoint.position;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPos, attackRange, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            // ���: ���� IDamageable�� ����
            var dmg = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(attackDamage);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
        if (groundCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, 0.12f);
        }
    }
}
