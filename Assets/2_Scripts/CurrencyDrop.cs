using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CurrencyDrop : MonoBehaviour
{
    [Header("재화 설정")]
    [SerializeField] private int currencyAmount = 10;

    [Header("물리 설정")]
    [SerializeField] private float dropForce = 3f;
    [SerializeField] private float randomForceRange = 2f;

    [Header("자동 획득 설정")]
    [SerializeField] private bool autoCollect = true;
    [SerializeField] private float collectRange = 2f;
    [SerializeField] private float collectSpeed = 5f;

    [Header("시각 효과")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float floatAmount = 0.2f;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("사운드")]
    [SerializeField] private AudioClip collectSFX;
    [SerializeField] private AudioClip dropSFX;

    [Header("이펙트")]
    [SerializeField] private GameObject collectEffect;

    [Header("지면 감지")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float groundCheckDistance = 0.3f;

    private Rigidbody2D rb;
    private bool isCollected = false;
    private bool isInRange = false;
    private Vector3 startPosition;
    private float floatTimer = 0f;
    private bool hasLanded = false;
    private bool isGrounded = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("[CurrencyDrop] Collider2D가 Trigger로 설정되어 있지 않습니다!");
        }

        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
            if (groundLayer == 0)
            {
                groundLayer = LayerMask.GetMask("Default");
            }
        }
    }

    void Start()
    {
        startPosition = transform.position;
        Drop();

        if (dropSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(dropSFX);
    }

    void Update()
    {
        if (isCollected) return;

        CheckGround();

        if (!hasLanded && isGrounded)
        {
            hasLanded = true;
            startPosition = transform.position;
        }

        if (hasLanded)
        {
            FloatAnimation();
        }

        RotateAnimation();

        if (autoCollect)
        {
            CheckPlayerInRange();
        }
    }

    void FixedUpdate()
    {
        if (isCollected) return;

        if (isGrounded)
        {
            Vector2 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.gravityScale = 0f;
        }

        if (hasLanded && isInRange && autoCollect)
        {
            MoveTowardsPlayer();
        }
    }

    void Drop()
    {
        if (rb == null) return;

        Vector2 randomDirection = new Vector2(
            Random.Range(-1f, 1f),
            Random.Range(0.5f, 1f)
        ).normalized;

        float force = dropForce + Random.Range(-randomForceRange, randomForceRange);
        rb.AddForce(randomDirection * force, ForceMode2D.Impulse);
        rb.AddTorque(Random.Range(-5f, 5f), ForceMode2D.Impulse);
    }

    void FloatAnimation()
    {
        if (isInRange && autoCollect) return;

        floatTimer += Time.deltaTime * floatSpeed;
        float offset = Mathf.Sin(floatTimer) * floatAmount;

        if (rb != null)
        {
            Vector2 currentVel = rb.linearVelocity;
            float targetY = startPosition.y + offset;
            float currentY = transform.position.y;
            float yVelocity = (targetY - currentY) * 10f;
            rb.linearVelocity = new Vector2(currentVel.x, yVelocity);
        }
        else
        {
            Vector3 currentPos = transform.position;
            currentPos.y = startPosition.y + offset;
            transform.position = currentPos;
        }
    }

    void CheckGround()
    {
        Vector2 checkPosition = transform.position;
        isGrounded = Physics2D.OverlapCircle(checkPosition, groundCheckRadius, groundLayer);

        if (!isGrounded)
        {
            RaycastHit2D hit = Physics2D.Raycast(checkPosition, Vector2.down, groundCheckDistance, groundLayer);
            if (hit.collider != null)
            {
                isGrounded = true;
            }
        }
    }

    void RotateAnimation()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }

    void CheckPlayerInRange()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.transform.position);
        isInRange = distance <= collectRange;
    }

    void MoveTowardsPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Vector2 direction = (player.transform.position - transform.position).normalized;
        rb.linearVelocity = direction * collectSpeed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }

    void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.AddCurrency(currencyAmount);
        }

        if (collectSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(collectSFX);

        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    public void SetCurrencyAmount(int amount)
    {
        currencyAmount = Mathf.Max(1, amount);
    }

    public int GetCurrencyAmount()
    {
        return currencyAmount;
    }

    void OnDrawGizmosSelected()
    {
        if (autoCollect)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, collectRange);
        }
    }
}
