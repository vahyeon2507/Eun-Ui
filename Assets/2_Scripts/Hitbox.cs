using System.Collections.Generic;
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    public int damage = 1;
    public string targetTag = "Player";          // 기본값 Player
    public LayerMask targetLayerMask;           // optional, 태그 대신 레이어로 검사하려면 사용
    public GameObject owner;                    // 소유자(생성자) - self-hit 방지

    public bool destroyOnHit = false;
    public bool singleUse = true;
    private HashSet<Collider2D> hitSet = new HashSet<Collider2D>();
    private Collider2D myCollider;

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null)
            Debug.LogWarning($"[Hitbox] {name} has no Collider2D!");
        else if (!myCollider.isTrigger)
            Debug.LogWarning($"[Hitbox] {name} Collider2D.isTrigger is false. Set it to true for hit detection.");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    void TryHit(Collider2D other)
    {
        if (other == null) return;
        if (owner != null)
        {
            // 같은 오브젝트이거나 owner의 자식이면 무시
            if (other.gameObject == owner) return;
            if (other.transform.IsChildOf(owner.transform)) return;
        }

        // 태그 검사 우선(설정되어 있으면)
        if (!string.IsNullOrEmpty(targetTag))
        {
            if (!other.CompareTag(targetTag)) return;
        }
        else
        {
            // 태그 비어있으면 레이어마스크 검사
            if (((1 << other.gameObject.layer) & targetLayerMask.value) == 0) return;
        }

        // 중복 히트 방지
        if (hitSet.Contains(other)) return;

        // IDamageable 확인 (self/parent/child)
        IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>() ?? other.GetComponentInChildren<IDamageable>();
        if (dmg != null)
        {
            Debug.Log($"[Hitbox] Hitting {other.name} with {damage} dmg (owner: {owner?.name})");
            dmg.TakeDamage(damage);
            hitSet.Add(other);
            if (destroyOnHit || singleUse) Destroy(gameObject);
        }
        else
        {
            Debug.Log($"[Hitbox] {other.name} matched target but has no IDamageable.");
        }
    }
}
