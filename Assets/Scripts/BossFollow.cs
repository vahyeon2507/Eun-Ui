// BossFollow_Modified.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossFollow_Modified : MonoBehaviour
{
    public Transform player;
    public float chaseRange = 12f;
    public float stopDistance = 2f;
    public float followSpeed = 2f;
    public float smoothTime = 0.08f;
    public bool faceTarget = true;
    public Animator animator;
    public string speedParamName = "Speed";

    Rigidbody2D rb;
    Vector2 currentVelocity = Vector2.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (player == null && GameObject.FindWithTag("Player") != null)
            player = GameObject.FindWithTag("Player").transform;

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        Vector2 pos = rb.position;
        // 플레이어의 Y를 따라가지 않도록 Y를 보스 자신의 Y로 고정
        Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
        float dist = Vector2.Distance(pos, targetPos);

        if (dist > chaseRange)
        {
            SetAnimatorSpeed(0f);
            return;
        }

        if (dist <= stopDistance)
        {
            SetAnimatorSpeed(0f);
            return;
        }

        Vector2 desiredPos = Vector2.MoveTowards(pos, targetPos, followSpeed * Time.fixedDeltaTime);
        Vector2 newPos = Vector2.SmoothDamp(pos, desiredPos, ref currentVelocity, smoothTime, Mathf.Infinity, Time.fixedDeltaTime);

        rb.MovePosition(newPos);

        if (faceTarget)
        {
            float dirX = player.position.x - transform.position.x;
            if (Mathf.Abs(dirX) > 0.01f)
                ApplyFacing(dirX > 0f);
        }

        SetAnimatorSpeed(currentVelocity.magnitude);
    }

    void ApplyFacing(bool faceRight)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (faceRight ? 1f : -1f);
        transform.localScale = s;
    }

    void SetAnimatorSpeed(float v)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetFloat(speedParamName, v);
    }
}
