using System.Collections.Generic;
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    [Header("Hitbox")]
    public int damage = 1;
    public string targetTag = "Player";
    public bool destroyOnHit = false;
    public bool singleUse = false;
    public bool allowMultipleHitsPerTarget = false;

    [Header("Center Safe")]
    public bool allowCenterSafe = false;
    public float centerSafeRadius = 0.5f;

    [Header("Knockback")]
    public float knockbackForce = 0f;

    // internal
    private HashSet<Collider2D> hitSet = new HashSet<Collider2D>();
    private Collider2D myCollider;

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null)
        {
            Debug.LogWarning($"[Hitbox] {name} has no Collider2D! Hit detection won't work.");
        }
        else if (!myCollider.isTrigger)
        {
            Debug.LogWarning($"[Hitbox] {name} Collider2D.isTrigger is false. Set it to true for hit detection without physics push.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    // (선택) OnTriggerStay2D를 쓰려면 cooldown 로직 추가
    private void TryHit(Collider2D other)
    {
        if (myCollider == null) return;
        if (other == null) return;

        // 태그 검사 (빠르게 실패)
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            // 태그 불일치면 무시
            // Debug.Log($"[Hitbox] Ignored {other.name} (tag {other.tag})");
            return;
        }

        // 중복 히트 방지
        if (!allowMultipleHitsPerTarget && hitSet.Contains(other)) return;

        // 중앙 안전영역 검사
        if (allowCenterSafe)
        {
            Vector2 closestPoint = myCollider.ClosestPoint(other.transform.position);
            Vector2 center = myCollider.bounds.center;
            float dist = Vector2.Distance(center, closestPoint);
            if (dist <= centerSafeRadius)
            {
                Debug.Log($"[Hitbox] Ignored {other.name} due to center-safe (dist {dist:F2} <= {centerSafeRadius})");
                return;
            }
        }

        // IDamageable 찾기 (우직하게 부모/자식까지 확인)
        IDamageable dmg = other.GetComponent<IDamageable>();
        if (dmg == null)
        {
            dmg = other.GetComponentInParent<IDamageable>();
            if (dmg != null) Debug.Log($"[Hitbox] Found IDamageable on parent of {other.name}");
        }
        if (dmg == null)
        {
            dmg = other.GetComponentInChildren<IDamageable>();
            if (dmg != null) Debug.Log($"[Hitbox] Found IDamageable on child of {other.name}");
        }

        if (dmg != null)
        {
            Debug.Log($"[Hitbox] Hitting {other.name} with {damage} dmg (hitbox {name})");
            dmg.TakeDamage(damage);
            hitSet.Add(other);

            // 넉백
            if (knockbackForce > 0f)
            {
                Rigidbody2D rb = other.attachedRigidbody;
                if (rb != null)
                {
                    Vector2 dir = (other.transform.position - transform.position).normalized;
                    rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
                }
            }

            if (destroyOnHit || singleUse)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"[Hitbox] {other.name} matched tag but has no IDamageable component (searched self/parent/children).");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (myCollider != null && allowCenterSafe)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            Gizmos.DrawWireSphere(myCollider.bounds.center, centerSafeRadius);
        }
    }
}
