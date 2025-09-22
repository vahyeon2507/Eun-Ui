using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
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

        // self/owner 무시
        if (owner != null)
        {
            if (other.gameObject == owner) return;
            if (other.transform.IsChildOf(owner.transform)) return;
        }

        // 중복 처리 방지
        if (hitSet.Contains(other))
            return;

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

        // 이제 "플레이어인지" 우선 확인해서 ConsumeHitboxIfParrying 호출 시도
        bool consumed = false;
        PlayerController player = null;
        try
        {
            // try get player in several ways (child/parent)
            player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>() ?? other.GetComponentInChildren<PlayerController>();
            if (player != null)
            {
                // PlayerController에 ConsumeHitboxIfParrying가 있으면 호출해서 consumed 여부 확인
                // 안전하게 호출(try/catch) — 만약 메서드 이름을 바꾸면 false 반환으로 fallthrough
                try
                {
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Hitbox] Exception calling ConsumeHitboxIfParrying on {player.name}: {ex.Message}");
                    consumed = false;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Hitbox] Exception while locating PlayerController on {other.name}: {ex.Message}");
        }

        // 로그: 디버그를 위해 항상 찍는다 (실전에선 레벨 낮춰도 됨)
        int id = gameObject.GetInstanceID();
        Debug.Log($"[Hitbox] TryHit: hitbox='{name}'(id={id}) collided with '{other.name}' consumed={consumed} (playerFound={(player != null)})");

        // 마친 후 중복 히트 방지용으로 처리
        hitSet.Add(other);

        if (consumed)
        {
            if (destroyOnHit || singleUse) Destroy(gameObject);
            if (player != null) Debug.Log($"[Hitbox] {name} consumed by player parry (player: {player.name}).");
            return; // 소비되었으니 데미지 처리 건너뜀
        }

        // 타깃이 IDamageable을 갖고 있으면 데미지 적용
        IDamageable dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>() ?? other.GetComponentInChildren<IDamageable>();
        if (dmg != null)
        {
            Debug.Log($"[Hitbox] Hitting {other.name} with {damage} dmg (owner: {owner?.name})");
            dmg.TakeDamage(damage);
            if (destroyOnHit || singleUse) Destroy(gameObject);
            return;
        }

        // 예: 기존 EnemyHealth 체계
        var enemyHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>() ?? other.GetComponentInChildren<EnemyHealth>();
        if (enemyHealth != null)
        {
            Debug.Log($"[Hitbox] Hitting EnemyHealth {other.name} with {damage} dmg (owner: {owner?.name})");
            enemyHealth.TakeDamage(damage);
            if (destroyOnHit || singleUse) Destroy(gameObject);
            return;
        }

        // 타깃이 조건에 맞지만 데미지 인터페이스 없음
        Debug.Log($"[Hitbox] {other.name} matched target but has no IDamageable/EnemyHealth.");
    }
}
