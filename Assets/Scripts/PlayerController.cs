using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Attack")]
    public Transform attackPoint;
    public float attackRange = 0.5f;
    public LayerMask enemyLayer;
    public int attackDamage = 1;
    public float attackCooldown = 0.5f;
    private float lastAttackTime;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isFacingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        HandleMovement();
        HandleJump();
        HandleGravity();
        HandleAttack();
        UpdateAnimationParams();
    }

    void HandleMovement()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        if (moveInput > 0 && !isFacingRight)
            Flip();
        else if (moveInput < 0 && isFacingRight)
            Flip();
    }

    void HandleJump()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    void HandleGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = 2.5f;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.gravityScale = 2f;
        }
        else
        {
            rb.gravityScale = 1f;
        }
    }

    void HandleAttack()
    {
        if (Input.GetKeyDown(KeyCode.X) && Time.time >= lastAttackTime)
        {
            animator.SetTrigger("Attack");
            lastAttackTime = Time.time;
        }
    }

    // 애니메이션 이벤트에서 호출될 공격 로직
    void PerformAttack()
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("PerformAttack: attackPoint is null!");
            return;
        }

        Vector2 attackPos = attackPoint.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, attackRange, enemyLayer);

        if (hits.Length == 0)
        {
            // 히트가 아예 없었음 — 디버그용
            Debug.Log("PerformAttack: hit nothing.");
        }

        foreach (Collider2D c in hits)
        {
            if (c == null) continue;

            // IDamageable 가능한 대상을 부모/자식까지 찾아서 호출
            IDamageable dmg = c.GetComponent<IDamageable>()
                            ?? c.GetComponentInParent<IDamageable>()
                            ?? c.GetComponentInChildren<IDamageable>();

            if (dmg != null)
            {
                // 위치 기반으로 방향 계산 (플레이어에서 적으로 향하는 방향)
                Vector2 dir = (c.transform.position - transform.position).normalized;

                // 간단하게 데미지와 히트 방향 전달 (인터페이스 확장 버전 필요시)
                dmg.TakeDamage(attackDamage);

                // 피해 시 시각 이펙트(선택) — 이펙트가 있으면 Instantiate
                // Instantiate(hitEffectPrefab, c.transform.position, Quaternion.identity);

                Debug.Log($"PerformAttack: hit {c.name}");
            }
            else
            {
                Debug.LogWarning($"PerformAttack: {c.name} has no IDamageable component.");
            }
        }
    }

    void UpdateAnimationParams()
    {
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
