using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlashProjectile2D : MonoBehaviour
{
    public int damage = 1;
    public float startSpeed = 3f;
    public float fastSpeed = 14f;
    public float accelDelay = 0.12f;
    public float accelTime = 0.35f;
    public AnimationCurve speedCurve;  // 0~1
    public float lifeTime = 5f;
    public LayerMask hitMask = ~0;
    public bool destroyOnHit = true;

    Rigidbody2D rb;
    Vector2 dir = Vector2.right;
    float timer;
    Transform ownerRoot;
    bool launched;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (speedCurve == null || speedCurve.length == 0)
            speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    public void Launch(
        Vector2 direction,
        Transform ownerRoot,
        int damage,
        float startSpeed,
        float fastSpeed,
        float accelDelay,
        float accelTime,
        AnimationCurve curve,
        float lifeTime,
        LayerMask hitMask,
        bool destroyOnHit)
    {
        this.dir = direction.normalized;
        this.ownerRoot = ownerRoot;
        this.damage = damage;
        this.startSpeed = startSpeed;
        this.fastSpeed = fastSpeed;
        this.accelDelay = Mathf.Max(0f, accelDelay);
        this.accelTime = Mathf.Max(0.0001f, accelTime);
        if (curve != null && curve.length > 0) this.speedCurve = curve;
        this.lifeTime = lifeTime;
        this.hitMask = hitMask;
        this.destroyOnHit = destroyOnHit;

        launched = true;
        timer = 0f;

        // �ð� ���� ����(����)
        if (dir != Vector2.zero)
            transform.right = dir;

        // ���� Ÿ�̸�
        if (lifeTime > 0f) Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (!launched) return;

        timer += Time.deltaTime;

        float speed = startSpeed;
        if (timer > accelDelay)
        {
            float t = Mathf.Clamp01((timer - accelDelay) / accelTime);
            float k = speedCurve != null ? speedCurve.Evaluate(t) : t;
            speed = Mathf.Lerp(startSpeed, fastSpeed, k);
        }

        Vector2 v = dir * speed;

        if (rb)
        {
            // Rigidbody2D�� ������ velocity ���(Continuous collision ����)
            rb.linearVelocity = v;
        }
        else
        {
            transform.position += (Vector3)(v * Time.deltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!launched) return;

        // �ڽ�/������ �浹 ����
        if (ownerRoot && (other.transform == ownerRoot || other.transform.IsChildOf(ownerRoot))) return;

        // ���̾� ����
        if (((1 << other.gameObject.layer) & hitMask.value) == 0) return;

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null) dmg.TakeDamage(damage);

        if (destroyOnHit) Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // ���� Ʈ���Ű� �ƴ϶� �ݸ������� ���ٸ� ���� ó��
        OnTriggerEnter2D(collision.collider);
    }
}
