using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TuboStrikeProjectile2D : MonoBehaviour

{
    [Header("Hit")]
    public int damage = 1;
    public LayerMask hitMask;          // Player(Ȥ�� PlayerHurtbox) ���̾� ����
    public bool destroyOnHit = false;
    public float activeTime = 0.10f;   // ���� ������ ����ִ� �ð�
    public float lifeTime = 0.9f;      // �ִ� ������ ����� �ð�

    [Header("Anim (optional)")]
    public Animator animator;
    public string playTrigger = "Play";

    Collider2D _col;
    bool _didDamage;
    ContactFilter2D _filter;
    static readonly List<Collider2D> _buf = new(8);

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col) _col.isTrigger = true; // Ʈ���� ����
        _filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        _filter.SetLayerMask(hitMask);
    }

    void OnEnable()
    {
        _didDamage = false;
        if (animator && !string.IsNullOrEmpty(playTrigger))
            animator.SetTrigger(playTrigger);

        StartCoroutine(CoLife());
    }

    public BossTuboController owner;   // ★ 추가
    IEnumerator CoLife()
    {
        float t = 0f, alive = 0f;

        while (t < lifeTime)
        {
            // Ȱ�� â ������ �� ������ ���� ������ �˻� (���̾� ��Ʈ���� ����)
            if (alive < activeTime)
            {
                if (TryDamageOnce())
                {
                    if (destroyOnHit) break;
                }
                alive += Time.deltaTime;
            }

            t += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    bool TryDamageOnce()
    {
        if (_didDamage || !_col) return false;

        _buf.Clear();
        int count = _col.Overlap(_filter, _buf);
        for (int i = 0; i < count; i++)
        {

            var c = _buf[i];
            if (!c) continue;

            // �и� ���̸� �÷��̾ �Һ��ϵ���
            var pc = c.GetComponentInParent<PlayerController>() ?? c.GetComponent<PlayerController>();
            if (pc != null && pc.ConsumeHitboxIfParrying(c))

            {
                owner?.OnParried(c);       // ★ 보스에 직접 알림
                // ��Ʈ�ѷ�(�Ǵ� ����)�� �˷��� �׷α� ó��(������)
                SendMessageUpwards("OnParried", c, SendMessageOptions.DontRequireReceiver);
                _didDamage = true;
                return true;
            }

            var dmg = c.GetComponent<IDamageable>()
                   ?? c.GetComponentInParent<IDamageable>()
                   ?? c.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(damage);
                _didDamage = true;
                return true;
            }
        }
        return false;
    }

    // ���Ž�/������: ��Ʈ������ �¾� ������ ���� ����
    void OnTriggerEnter2D(Collider2D other)
    {
        if (_didDamage) return;
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
        if (pc != null && pc.ConsumeHitboxIfParrying(other))
        {
            owner?.OnParried(other);   // ★ 여기서도 보스에 직접 알림
            SendMessageUpwards("OnParried", other, SendMessageOptions.DontRequireReceiver);
            _didDamage = true;
            if (destroyOnHit) Destroy(gameObject);
            return;
        }

        var dmg = other.GetComponent<IDamageable>()
               ?? other.GetComponentInParent<IDamageable>()
               ?? other.GetComponentInChildren<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damage);
            _didDamage = true;
            if (destroyOnHit) Destroy(gameObject);
        }
    }
}
