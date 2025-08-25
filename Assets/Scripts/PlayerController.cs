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
    public void PerformAttack()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            enemy.GetComponent<Enemy>()?.TakeDamage(attackDamage);
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
