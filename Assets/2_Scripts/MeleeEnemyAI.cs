using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MeleeEnemyAI : MonoBehaviour
{
    [Header("플레이어 감지")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private string playerTag = "Player";

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stopDistance = 1.5f;
    [SerializeField] private bool facePlayer = true;

    [Header("지면 감지")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.4f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.5f);
    [SerializeField] private float groundCheckDistance = 0.8f;
    [SerializeField] private bool useMultipleRaycasts = true;

    [Header("벽 감지")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.3f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.3f, 0f);

    [Header("절벽 감지")]
    [SerializeField] private bool checkForCliff = true;
    [SerializeField] private float cliffCheckDistance = 0.5f;

    [Header("공격 설정")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private Vector2 attackOffset = Vector2.zero;
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f);

    [Header("애니메이션")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkTrigger = "Walk";
    [SerializeField] private string idleTrigger = "Idle";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string hurtTrigger = "Hurt";

    [Header("시각 효과")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool flipSprite = true;

    [Header("사운드")]
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private AudioClip detectSFX;

    [Header("디버그")]
    [SerializeField] private bool showGizmos = true;

    private Transform playerTransform;
    private Rigidbody2D rb;
    private EnemyHealth enemyHealth;
    private bool isPlayerDetected = false;
    private bool isAttacking = false;
    private bool canAttack = true;
    private float lastAttackTime = 0f;
    private Vector2 moveDirection = Vector2.zero;
    private bool isDead = false;
    private bool isGrounded = false;
    private bool isWallInFront = false;
    private bool isCliffAhead = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<EnemyHealth>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (playerLayer == 0)
            playerLayer = LayerMask.GetMask("Player");

        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
            if (groundLayer == 0)
            {
                groundLayer = LayerMask.GetMask("Default");
                Debug.LogWarning("[MeleeEnemyAI] Ground 레이어를 찾을 수 없습니다. Default 레이어를 사용합니다.");
            }
        }

        if (wallLayer == 0)
            wallLayer = LayerMask.GetMask("Ground", "Default", "Wall");

        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = groundCheckOffset;
            groundCheck = groundCheckObj.transform;
        }

        if (groundCheck != null)
        {
            Debug.Log($"[MeleeEnemyAI] Ground Check 생성됨: {groundCheck.position}, Offset: {groundCheckOffset}, Radius: {groundCheckRadius}, Layer: {groundLayer.value}");
        }

        if (rb != null)
        {
            rb.freezeRotation = true;
            if (rb.bodyType == RigidbodyType2D.Static)
                rb.bodyType = RigidbodyType2D.Dynamic;
        }

        if (wallCheck == null)
        {
            GameObject wallCheckObj = new GameObject("WallCheck");
            wallCheckObj.transform.SetParent(transform);
            wallCheckObj.transform.localPosition = wallCheckOffset;
            wallCheck = wallCheckObj.transform;
        }
    }

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        if (isDead) return;

        if (enemyHealth != null && enemyHealth.currentHp <= 0)
        {
            isDead = true;
            return;
        }

        CheckGround();
        CheckWall();
        if (checkForCliff)
            CheckCliff();

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        isPlayerDetected = distanceToPlayer <= detectionRange;

        if (isPlayerDetected && !isAttacking)
        {
            if (distanceToPlayer <= attackRange && canAttack)
            {
                StartCoroutine(Attack());
            }
            else if (distanceToPlayer > stopDistance && isGrounded && !isWallInFront && !isCliffAhead)
            {
                MoveTowardsPlayer();
            }
            else
            {
                StopMoving();
            }

            if (facePlayer)
            {
                FacePlayer();
            }
        }
        else if (!isPlayerDetected)
        {
            StopMoving();
        }
    }

    void FixedUpdate()
    {
        if (isDead || isAttacking) return;

        if (isGrounded)
        {
            Vector2 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.gravityScale = 0f;
        }
        else
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.gravityScale = 2.5f;
            }
            else
            {
                rb.gravityScale = 1f;
            }
        }

        if (isPlayerDetected && moveDirection != Vector2.zero && isGrounded && !isWallInFront && !isCliffAhead)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, 0f);
        }
        else if (isGrounded)
        {
            rb.linearVelocity = new Vector2(0f, 0f);
        }
    }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerTransform = player.transform;
            
            if (detectSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(detectSFX);
        }
    }

    void CheckGround()
    {
        if (groundCheck == null) return;

        Vector2 checkPos = groundCheck.position;
        
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);

        if (!isGrounded)
        {
            RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, groundCheckDistance, groundLayer);
            if (hit.collider != null)
            {
                isGrounded = true;
            }
        }

        if (!isGrounded)
        {
            RaycastHit2D hit1 = Physics2D.Raycast(checkPos + Vector2.left * 0.1f, Vector2.down, groundCheckDistance, groundLayer);
            RaycastHit2D hit2 = Physics2D.Raycast(checkPos + Vector2.right * 0.1f, Vector2.down, groundCheckDistance, groundLayer);
            if (hit1.collider != null || hit2.collider != null)
            {
                isGrounded = true;
            }
        }

        if (!isGrounded)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius * 1.5f);
            foreach (Collider2D col in colliders)
            {
                if (col == null) continue;
                if ((groundLayer.value & (1 << col.gameObject.layer)) != 0)
                {
                    isGrounded = true;
                    break;
                }
            }
        }
    }

    void CheckWall()
    {
        if (wallCheck == null) return;

        Vector2 checkPosition = (Vector2)wallCheck.position;
        float direction = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        Vector2 checkDirection = new Vector2(direction, 0f);

        isWallInFront = Physics2D.Raycast(checkPosition, checkDirection, wallCheckDistance, wallLayer);
    }

    void CheckCliff()
    {
        if (groundCheck == null) return;

        Vector2 checkPosition = (Vector2)groundCheck.position;
        float direction = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        Vector2 cliffCheckPos = checkPosition + new Vector2(direction * cliffCheckDistance, 0f);

        isCliffAhead = !Physics2D.Raycast(cliffCheckPos, Vector2.down, groundCheckDistance, groundLayer);
    }

    void MoveTowardsPlayer()
    {
        if (playerTransform == null) return;

        Vector2 direction = (playerTransform.position - transform.position).normalized;
        moveDirection = direction;

        if (animator != null && !string.IsNullOrEmpty(walkTrigger))
            animator.SetTrigger(walkTrigger);
    }

    void StopMoving()
    {
        moveDirection = Vector2.zero;

        if (animator != null && !string.IsNullOrEmpty(idleTrigger))
            animator.SetTrigger(idleTrigger);
    }

    void FacePlayer()
    {
        if (playerTransform == null) return;

        if (flipSprite && spriteRenderer != null)
        {
            float direction = playerTransform.position.x - transform.position.x;
            spriteRenderer.flipX = direction < 0f;
        }
        else
        {
            float direction = playerTransform.position.x - transform.position.x;
            if (direction > 0f)
                transform.localScale = new Vector3(1f, 1f, 1f);
            else if (direction < 0f)
                transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    IEnumerator Attack()
    {
        if (isAttacking || !canAttack) yield break;

        isAttacking = true;
        canAttack = false;
        lastAttackTime = Time.time;

        moveDirection = Vector2.zero;

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        if (attackSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(attackSFX);

        yield return new WaitForSeconds(attackDuration * 0.3f);

        PerformAttack();

        yield return new WaitForSeconds(attackDuration * 0.7f);

        isAttacking = false;

        yield return new WaitForSeconds(attackCooldown - attackDuration);

        canAttack = true;
    }

    void PerformAttack()
    {
        if (playerTransform == null) return;

        Vector2 attackCenter = (Vector2)transform.position + attackOffset;
        if (spriteRenderer != null && spriteRenderer.flipX)
            attackCenter.x -= attackOffset.x * 2f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, attackSize, 0f, playerLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            PlayerController pc = hit.GetComponent<PlayerController>() ??
                                 hit.GetComponentInParent<PlayerController>() ??
                                 hit.GetComponentInChildren<PlayerController>();

            if (pc != null && pc.IsParrying && pc.ConsumeHitboxIfParrying(hit))
                continue;

            IDamageable damageable = hit.GetComponent<IDamageable>() ??
                                    hit.GetComponentInParent<IDamageable>() ??
                                    hit.GetComponentInChildren<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage);
                break;
            }
        }
    }

    public void OnHurt()
    {
        if (animator != null && !string.IsNullOrEmpty(hurtTrigger))
            animator.SetTrigger(hurtTrigger);
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        if (Application.isPlaying && playerTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }

        Vector2 attackCenter = (Vector2)transform.position + attackOffset;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(attackCenter, attackSize);

        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (wallCheck != null)
        {
            float direction = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
            Vector2 wallCheckPos = (Vector2)wallCheck.position;
            Vector2 wallCheckEnd = wallCheckPos + new Vector2(direction * wallCheckDistance, 0f);
            
            Gizmos.color = isWallInFront ? Color.red : Color.white;
            Gizmos.DrawLine(wallCheckPos, wallCheckEnd);
        }

        if (checkForCliff && groundCheck != null)
        {
            float direction = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
            Vector2 groundCheckPos2 = (Vector2)groundCheck.position;
            Vector2 cliffCheckPos = groundCheckPos2 + new Vector2(direction * cliffCheckDistance, 0f);
            Vector2 cliffCheckEnd = cliffCheckPos + Vector2.down * groundCheckDistance;
            
            Gizmos.color = isCliffAhead ? Color.red : Color.blue;
            Gizmos.DrawLine(cliffCheckPos, cliffCheckEnd);
        }
    }
}
