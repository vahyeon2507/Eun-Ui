using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TuboPointAttack : MonoBehaviour
{
    [Header("Owner")]
    public BossTuboController owner;

    [Header("Hitbox")]
    public Collider2D hitbox;      // isTrigger 권장
    public LayerMask playerLayer;

    [Header("Damage")]
    public int damage = 1;
    public float activeTime = 0.15f;
    public float extraParryGrace = 0.15f;

    [Header("FX/SFX (optional)")]
    public string spawnFxId;
    public string hitFxId;

    bool _active = false;
    bool _consumed = false;

    void Reset()
    {
        hitbox = GetComponent<Collider2D>();
        if (hitbox) hitbox.isTrigger = true;
    }

    void OnEnable()
    {
        // 그로기 중이면 스폰 즉시 무시
        if (owner && !owner.AttacksEnabled) { Destroy(gameObject); return; }
        StartCoroutine(CoLife());
    }

    IEnumerator CoLife()
    {
        _active = true;
        if (owner) owner.NotifyAttackWindow(true, activeTime + extraParryGrace); // 열림
        yield return new WaitForSeconds(activeTime);
        _active = false;
        if (owner) owner.NotifyAttackWindow(false); // 닫힘
        Destroy(gameObject, 0.02f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_active || _consumed) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
        if (!pc) return;
        Debug.Log($"[TPA] active={_active} consumed={_consumed} isParry={pc?.IsParrying}");

        // --- 패링 창일 때: 소비 + 튜보에게 '패링 성공' 알림 ---
        if (pc.IsParrying)
        {
            bool consumed = pc.ConsumeHitboxIfParrying(other); // 플레이어 쪽 패링 소비
            if (consumed)
            {
                _consumed = true;
                if (hitbox) hitbox.enabled = false;
                owner?.NotifyParrySuccessDirect();  // ★ 이 한 줄이 튜토 진행의 열쇠
                return;
            }
            // consume 못했으면 일반 피격으로 계속 진행
        }

        // 기존 OnTriggerEnter2D는 유지하고, 아래 Stay 추가
        void OnTriggerStay2D(Collider2D other)
        {
            if (!_active || _consumed) return;
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
            if (!pc) return;

            // 재개 후에라도 패링이면 소비 + 알림
            if (pc.IsParrying)
            {
                if (pc.ConsumeHitboxIfParrying(other))
                {
                    _consumed = true;
                    if (hitbox) hitbox.enabled = false;
                    owner?.NotifyParrySuccessDirect();   // ★ 튜토 진행 신호
                }
            }
        }




        // --- 일반 피격 ---
        _consumed = true;
        pc.TakeDamage(damage);
        if (hitbox) hitbox.enabled = false;
    }
}
