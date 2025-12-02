// HurtboxRelay2D.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HurtboxRelay2D : MonoBehaviour, IDamageable
{
    [Tooltip("데미지를 실제로 받을 주인 (대개 보스 루트에 있는 BossHealth)")]
    public BossHealth owner;
    [Range(0.01f, 5f)] public float damageMultiplier = 1f;
    [Tooltip("보스 무적(invuln) 중엔 무시")]
    public bool ignoreWhileOwnerInvuln = true;

    // 한 프레임에 같은 무기로 여러 콜라이더 히트 방지
    int _lastHitFrame = -1;

    void Reset()
    {
        // 기본 세팅 편의
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // 무기 Trigger 히트박스와 잘 맞음
        if (!owner) owner = GetComponentInParent<BossHealth>();
        // 선택: 허트박스 레이어 자동 지정 (없으면 주석)
        int layer = LayerMask.NameToLayer("Hurtbox");
        if (layer >= 0) gameObject.layer = layer;
    }

    public void TakeDamage(int amount)
    {
        if (Time.frameCount == _lastHitFrame) return;
        _lastHitFrame = Time.frameCount;

        if (!owner) owner = GetComponentInParent<BossHealth>();
        if (!owner) return;

        if (ignoreWhileOwnerInvuln && owner.IsInvulnerable()) return;

        int dmg = Mathf.Max(0, Mathf.RoundToInt(amount * damageMultiplier));
        if (dmg > 0) owner.TakeDamage(dmg);
    }
}
