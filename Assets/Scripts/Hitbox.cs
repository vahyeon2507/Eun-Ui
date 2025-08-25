using System.Collections.Generic;
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    [Header("Hitbox")]
    public int damage = 1;                       // 입히는 데미지
    public string targetTag = "Player";          // 공격 대상 태그
    public bool destroyOnHit = false;            // 적중 시 히트박스 파괴 여부
    public bool singleUse = false;               // 한 번이라도 맞으면 파괴
    public bool allowMultipleHitsPerTarget = false; // 같은 대상 여러번 맞힐지

    [Header("Center Safe (for BodySlam)")]
    public bool allowCenterSafe = false;         // 중앙 안전영역 사용 여부
    public float centerSafeRadius = 0.5f;        // 중앙 안전 반지름 (월드 단위)

    [Header("Knockback")]
    public float knockbackForce = 0f;            // 0이면 넉백 없음

    // 내부
    private HashSet<Collider2D> hitSet = new HashSet<Collider2D>();
    private Collider2D myCollider;

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null)
            Debug.LogWarning($"[Hitbox] {name} has no Collider2D! Hit detection won't work.");
        // 반드시 isTrigger 체크된 Collider2D 필요
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    // (선택) If you want continuous hits, you could call TryHit from OnTriggerStay2D with a cooldown
    // void OnTriggerStay2D(Collider2D other) { TryHit(other); }

    private void TryHit(Collider2D other)
    {
        if (myCollider == null) return;
        if (other == null) return;
        if (!other.CompareTag(targetTag)) return;

        // 중복 히트 체크
        if (!allowMultipleHitsPerTarget && hitSet.Contains(other)) return;

        // 중앙 안전영역 검사 (충돌 지점 기준)
        if (allowCenterSafe)
        {
            // 충돌 대상에서 히트박스까지의 가장 가까운 포인트를 구하고,
            // 그 포인트가 히트박스 중심으로부터 centerSafeRadius 이내면 무시한다.
            Vector2 closestPointOnHitbox = myCollider.ClosestPoint(other.transform.position);
            Vector2 hitboxCenter = myCollider.bounds.center;
            float dist = Vector2.Distance(hitboxCenter, closestPointOnHitbox);

            if (dist <= centerSafeRadius)
            {
                // 중앙 안전 영역에 걸림 -> 무시
                // (디버그 로그 원하면 활성화)
                // Debug.Log($"[Hitbox] collision ignored due to center-safe (dist={dist})");
                return;
            }
        }

        // 실제 데미지 전달
        IDamageable dmg = other.GetComponent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damage);

            // 넉백
            if (knockbackForce > 0f)
            {
                Rigidbody2D rb = other.attachedRigidbody;
                if (rb != null)
                {
                    // 넉백 방향: 충돌 대상에서 히트박스 중심 방향 (플레이어가 바깥으로 날아가게)
                    Vector2 dir = (other.transform.position - transform.position).normalized;
                    rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
                }
            }

            // 기록 및 자기 파괴
            hitSet.Add(other);

            if (destroyOnHit || singleUse)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            // IDamageable이 붙어있지 않은 경우도 처리하고 싶다면 여기서 확장
            // Debug.Log("[Hitbox] target has no IDamageable: " + other.name);
        }
    }

    // 편의: 시각 디버그(설정값을 씬 뷰에 보여줌)
    void OnDrawGizmosSelected()
    {
        if (myCollider != null && allowCenterSafe)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            Gizmos.DrawWireSphere(myCollider.bounds.center, centerSafeRadius);
        }
    }
}
