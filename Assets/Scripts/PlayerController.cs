// PlayerController.cs
using System.Collections;
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
    public LayerMask enemyLayer; // set in Inspector (Enemy)
    public int attackDamage = 1;
    public float attackCooldown = 0.5f;
    private float lastAttackTime;

    [Header("Dash")]
    public float dashSpeed = 12f;        // absolute speed during dash
    public float dashDuration = 0.15f;   // how long the dash lasts
    public float dashCooldown = 0.8f;
    private bool isDashing = false;
    private float lastDashTime = -99f;

    [Header("Parry")]
    public KeyCode parryKey = KeyCode.C; // default parry key
    public float parryWindow = 0.2f;     // active parry time
    public float parryCooldown = 1.0f;
    public string reflectTargetTag = "Boss"; // when reflected, hitbox.targetTag will be set to this
    private bool isParrying = false;
    private float lastParryTime = -99f;

    [Header("References")]
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
        // Inputs handled here, but movement respects dash state
        if (!isDashing)
        {
            HandleMovement();
        }

        HandleJump();
        HandleGravity();
        UpdateAnimationParams();

        // Attack (X)
        if (Input.GetKeyDown(KeyCode.X) && Time.time >= lastAttackTime)
        {
            animator?.SetTrigger("Attack");
            PerformAttack();
            lastAttackTime = Time.time + attackCooldown;
        }

        // Dash (Left Shift)
        if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= lastDashTime)
        {
            StartCoroutine(DoDash());
        }

        // Parry (C or configured key)
        if (Input.GetKeyDown(parryKey) && Time.time >= lastParryTime)
        {
            StartCoroutine(DoParry());
        }
    }

    #region Movement / Jump / Gravity
    void HandleMovement()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");

        // Don't change horizontal velocity if dashing
        if (!isDashing)
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        if (moveInput > 0 && !isFacingRight) Flip();
        else if (moveInput < 0 && isFacingRight) Flip();
    }

    void HandleJump()
    {
        if (groundCheck == null) return;
        bool isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            animator?.SetTrigger("Jump");
        }
    }

    void HandleGravity()
    {
        if (rb.linearVelocity.y < 0) rb.gravityScale = 2.5f;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump")) rb.gravityScale = 2.0f;
        else rb.gravityScale = 1.0f;
    }
    #endregion

    void UpdateAnimationParams()
    {
        if (animator == null) return;
        bool isGrounded = (groundCheck != null) ? Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer) : false;
        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        animator.SetBool("IsDashing", isDashing);
        animator.SetBool("IsParrying", isParrying);
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;

        // Keep attackPoint in front if it's a child transform
        if (attackPoint != null)
        {
            Vector3 lp = attackPoint.localPosition;
            lp.x = Mathf.Abs(lp.x) * (isFacingRight ? 1f : -1f);
            attackPoint.localPosition = lp;
        }
    }

    #region Attack
    void PerformAttack()
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("PerformAttack: attackPoint is null!");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);

        // If layer filtering fails (artist/dev accidentally set wrong layer), fall back to global and try to resolve IDamageable
        if (hits.Length == 0)
        {
            hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange);
        }

        foreach (Collider2D c in hits)
        {
            if (c == null) continue;
            if (c.gameObject == this.gameObject) continue;

            // Find IDamageable on collider, parent or child
            IDamageable dmg = c.GetComponent<IDamageable>() ?? c.GetComponentInParent<IDamageable>() ?? c.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(attackDamage);
                Debug.Log($"Player attack: hit {c.name} for {attackDamage} dmg.");
            }
            else
            {
                Debug.Log($"Player attack: {c.name} had no IDamageable (layer:{LayerMask.LayerToName(c.gameObject.layer)})");
            }
        }
    }
    #endregion

    #region Dash & Parry Coroutines
    IEnumerator DoDash()
    {
        if (isDashing) yield break;
        isDashing = true;
        lastDashTime = Time.time + dashCooldown;
        animator?.SetTrigger("Dash");

        // Freeze vertical velocity? Here we preserve vertical motion but set horizontal dash velocity
        float dir = isFacingRight ? 1f : -1f;
        float startTime = Time.time;
        float originalGravity = rb.gravityScale;

        // Optionally reduce gravity while dashing (comment/uncomment as desired)
        // rb.gravityScale = 0f;

        while (Time.time < startTime + dashDuration)
        {
            rb.linearVelocity = new Vector2(dir * dashSpeed, rb.linearVelocity.y);
            yield return null;
        }

        // restore gravity if changed
        rb.gravityScale = originalGravity;
        isDashing = false;
    }

    IEnumerator DoParry()
    {
        // mark parry state
        isParrying = true;
        lastParryTime = Time.time + parryCooldown;
        animator?.SetTrigger("Parry");
        // small window where parry is active and will reflect incoming hitboxes
        yield return new WaitForSeconds(parryWindow);
        isParrying = false;
    }
    #endregion

    #region Parry Reflection / Hit Handling
    // Called when something enters player's trigger (e.g., enemy hitbox). For parry to work:
    // - The enemy's Hitbox prefab must have a component named "Hitbox" (or similar) and expose 'owner' and 'targetTag' as writable fields.
    // - This method will reassign owner->player and change targetTag to reflectTargetTag so the hitbox can damage enemies instead.
    // If your Hitbox script is different, adapt this code to call the correct API on the hit object.
    void OnTriggerEnter2D(Collider2D other)
    {
        // ignore if not a hitbox
        var hb = other.GetComponent<Hitbox>();
        if (hb == null) return;

        // If player is parrying and hitbox belongs to an enemy, reflect it
        if (isParrying)
        {
            // sanity: avoid reflecting your own hitboxes (owner may be null)
            if (hb.owner != null && hb.owner != this.gameObject)
            {
                // reflect: change owner so it won't hit the player, and retarget it to enemies
                hb.owner = this.gameObject;

                // If Hitbox exposes a targetTag or layer, set it to the boss/enemy value.
                // We try both properties in case your Hitbox uses one or the other.
                try
                {
                    hb.targetTag = reflectTargetTag; // if Hitbox has targetTag string
                }
                catch { /* ignore if no such field */ }

                // optional: if hitbox has Rigidbody2D, flip its velocity to visually reflect
                var rbOther = other.attachedRigidbody;
                if (rbOther != null)
                {
                    rbOther.linearVelocity = new Vector2(-rbOther.linearVelocity.x, rbOther.linearVelocity.y);
                }

                Debug.Log("Parry: reflected a hitbox back at enemies.");
            }
        }
    }
    #endregion

    #region Gizmos & Utilities
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
            Gizmos.DrawWireSphere(groundCheck.position, 0.1f);
        }
    }

    // optional helper to set attack range from other scripts or editor buttons
    public void DEBUG_SetAttackRange(float r) { attackRange = r; }
    #endregion
}
