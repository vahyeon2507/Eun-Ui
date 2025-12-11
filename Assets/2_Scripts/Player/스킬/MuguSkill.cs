using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ���� ��ų: �ݶ��̴� ��� �״�� ���� ���� ���.
/// - Hit On/Off�� �ִϸ��̼� �̺�Ʈ�� ����
/// - �ݶ��̴��� isTrigger ����
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MuguSkill : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("�� �� ���� �� �� �����")]
    public int damage = 3;

    [Tooltip("���� ��� ���̾�(��/����)")]
    public LayerMask enemyMask = ~0;

    [Header("Hitbox")]
    [Tooltip("���� ������ ��Ÿ���� �ݶ��̴�. ���� �ڱ� �ݶ��̴� ���")]
    public Collider2D hitbox;

    [Tooltip("����� �����찡 �ƴ� �� �ݶ��̴��� ������ ����")]
    public bool disableColliderOutsideHit = true;

    [Header("Lifetime")]
    [Tooltip("������ �ڵ� �ı� �ð�(��). �ִ� �̺�Ʈ�� ���� �ı��Ǹ� �̰� ���õ�")]
    public float autoDestroyTime = 5f;

    [Header("Owner (����)")]
    [Tooltip("��ȯ�� ��(�÷��̾�). �ʿ������ ����� ��")]
    public Transform owner;

    // ���� ����
    bool _damageActive = false;

    // �ߺ� Ÿ�� ������
    static readonly List<Collider2D> _overlapBuf = new List<Collider2D>(8);
    static readonly HashSet<IDamageable> _damagedCache = new HashSet<IDamageable>();

    void Awake()
    {
        if (!hitbox) hitbox = GetComponent<Collider2D>();

        if (hitbox)
        {
            hitbox.isTrigger = true;
            if (disableColliderOutsideHit)
                hitbox.enabled = false;
        }
        else
        {
            Debug.LogError("[MuguSkill] Hitbox�� �����ϴ�. Collider2D�� ���̰ų� hitbox�� �Ҵ��ϼ���.");
        }
    }

    void Start()
    {
        if (autoDestroyTime > 0f)
            Destroy(gameObject, autoDestroyTime);
    }

    // ========== ����� ������ ��� ==========

    void SetDamageActive(bool active)
    {
        _damageActive = active;

        if (hitbox && disableColliderOutsideHit)
            hitbox.enabled = active;

        if (active)
        {
            // "�� �����ӿ� �̹� ���� �ִ� �ֵ�" �� �� ���������� ���߱�
            DoDamageSnapshot();
        }
    }

    void DoDamageSnapshot()
    {
        if (!hitbox) return;
        if (damage <= 0) return;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyMask,
            useTriggers = true
        };

        _overlapBuf.Clear();
        _damagedCache.Clear();

        int count = hitbox.Overlap(filter, _overlapBuf);
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuf[i];
            if (!col) continue;

            // Owner�� ���� Ʈ������/�θ�� ���� (Ȥ�ó� ���� ����)
            if (owner && (col.transform == owner || col.transform.IsChildOf(owner)))
                continue;

            var dmg = col.GetComponent<IDamageable>()
                   ?? col.GetComponentInParent<IDamageable>()
                   ?? col.GetComponentInChildren<IDamageable>();

            if (dmg == null || _damagedCache.Contains(dmg)) continue;
            _damagedCache.Add(dmg);

            dmg.TakeDamage(damage);
        }
    }

    // ========== �ִϸ��̼� �̺�Ʈ�� API ==========

    /// <summary>
    /// �ִϸ��̼� �̺�Ʈ���� ȣ��: ����� ������ ����
    /// </summary>
    public void AnimEvent_MuguHitOn()
    {
        SetDamageActive(true);
    }

    /// <summary>
    /// �ִϸ��̼� �̺�Ʈ���� ȣ��: ����� ������ ����
    /// </summary>
    public void AnimEvent_MuguHitOff()
    {
        SetDamageActive(false);
    }

    /// <summary>
    /// �ִϸ��̼� �̺�Ʈ���� ȣ��: ���� ������Ʈ ����
    /// </summary>
    public void AnimEvent_MuguEnd()
    {
        Destroy(gameObject);
    }
}
