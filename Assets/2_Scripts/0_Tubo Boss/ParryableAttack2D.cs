using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ParryableAttack2D : MonoBehaviour
{
    [Header("Owner (Tubo)")]
    public BossTuboController owner;

    [Header("Detection")]
    [Tooltip("플레이어 패링 히트박스의 태그")]
    public string parryTag = "Parry";
    [Tooltip("패링 성공 시 이 공격을 바로 없앨지")]
    public bool destroyOnParry = true;

    [Header("Events")]
    public UnityEvent onParried;

    bool _consumed = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed) return;
        if (!other || !other.CompareTag(parryTag)) return;

        _consumed = true;

        // 보스에 바로 알림 → 즉시 그로기 진입
        if (owner) owner.OnParried();
        onParried?.Invoke();

        // 이 공격을 더 이상 위험하지 않게 정지
        foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
        var rb = GetComponent<Rigidbody2D>(); if (rb) rb.linearVelocity = Vector2.zero;

        if (destroyOnParry) Destroy(gameObject, 0.02f);
    }
}
