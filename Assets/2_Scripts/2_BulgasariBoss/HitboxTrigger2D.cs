using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HitboxTrigger2D : MonoBehaviour
{
    public int damage = 2;
    public LayerMask targetMask;      // Player만
    public bool oneHitPerTarget = true;
    public bool activeOnEnable = false;

    HashSet<Collider2D> _seen = new();
    bool _active;
    float _ttl = -1f;

    void Awake() { GetComponent<Collider2D>().isTrigger = true; if (activeOnEnable) _active = true; }

    public void Activate(float duration, int dmg = -1)
    {
        if (dmg > -1) damage = dmg;
        _active = true; _ttl = duration; _seen.Clear(); gameObject.SetActive(true);
    }
    public void Deactivate() { _active = false; _ttl = -1f; }

    void Update()
    {
        if (_ttl > 0f) { _ttl -= Time.deltaTime; if (_ttl <= 0f) Deactivate(); }
    }

    void OnTriggerEnter2D(Collider2D other) { TryHit(other); }
    void OnTriggerStay2D(Collider2D other) { TryHit(other); } // “붙어있는 동안”도 허용

    void TryHit(Collider2D other)
    {
        if (!_active) return;
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;
        if (oneHitPerTarget && !_seen.Add(other)) return;

        // 패링 우선 소비
        var pc = other.GetComponent<PlayerController>() ??
                 other.GetComponentInParent<PlayerController>() ??
                 other.GetComponentInChildren<PlayerController>();
        if (pc != null && pc.IsParrying && pc.ConsumeHitboxIfParrying(other)) return;

        var dmgComp = other.GetComponent<IDamageable>() ??
                      other.GetComponentInParent<IDamageable>() ??
                      other.GetComponentInChildren<IDamageable>();
        if (dmgComp != null) dmgComp.TakeDamage(damage);
    }
}
