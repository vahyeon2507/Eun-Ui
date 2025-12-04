using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TuboStrikeProjectile2D : MonoBehaviour
{
    [Header("Hit")]
    public int damage = 1;
    public LayerMask hitMask;            // 보통 Player
    public bool destroyOnHit = true;
    public float activeTime = 0.08f;     // 히트 가능 시간
    public float lifeTime = 0.6f;        // 전체 생명

    [Header("Anim (optional)")]
    public Animator animator;
    public string playTrigger = "Play";  // 애니메이터 트리거명

    Collider2D _col;
    bool _playing;

    void Reset()
    {
        _col = GetComponent<Collider2D>();
        _col.isTrigger = true;
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col) _col.enabled = false;
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    public void Play()
    {
        if (_playing) return;
        StartCoroutine(CoPlay());
    }

    IEnumerator CoPlay()
    {
        _playing = true;

        if (animator && !string.IsNullOrEmpty(playTrigger))
            animator.SetTrigger(playTrigger);

        // 짧게 히트 가능
        if (_col) _col.enabled = true;
        yield return new WaitForSeconds(activeTime);
        if (_col) _col.enabled = false;

        // 잔상/연기 등 보이도록 여유
        yield return new WaitForSeconds(Mathf.Max(0f, lifeTime - activeTime));
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        // 플레이어에 피해
        var dmg = other.GetComponent<IDamageable>()
               ?? other.GetComponentInParent<IDamageable>()
               ?? other.GetComponentInChildren<IDamageable>();
        if (dmg != null) dmg.TakeDamage(damage);

        if (destroyOnHit) Destroy(gameObject);
    }
}
