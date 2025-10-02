using System;
using System.Linq;
using UnityEngine;

[Serializable]
public struct NamedHitbox
{
    public string key;            // "Left","Right","Center","Head"...
    public HitboxTrigger2D hit;   // ���� Ʈ���� ��Ʈ�ڽ�(�ڽ� �ݶ��̴��� ���� ��ũ��Ʈ)
    public Transform origin;      // ���� ����(������ hit.transform ���)
}

public class BulgasariAnimEvents : MonoBehaviour
{
    [Header("Anchors / Hitboxes")]
    public NamedHitbox[] map;

    [Header("One-shot Attacks")]
    public BulgasariAttackHooks hooks;
    public AttackDefinition2D[] defs;   // 0,1,2...

    NamedHitbox? Find(string key) => map.FirstOrDefault(m => m.key == key);

    // === Animation Events���� ȣ���� �͵� ===

    // ���� ���� ����ġ: �ش� ��Ŀ ��Ʈ�ڽ��� dur�� ���� �Ҵ�
    public void OnAt(string key, float dur)
    {
        var m = Find(key); if (m == null || m.Value.hit == null) return;
        m.Value.hit.Activate(dur);
    }

    // ����: AttackDefinition �ε��� ���� (originOverride�� ��Ŀ)
    public void Impact(int defIndex, string key = "Center")
    {
        if (!hooks || defs == null || defIndex < 0 || defIndex >= defs.Length) return;

        var m = Find(key);
        Transform origin = (m != null && (m.Value.origin || m.Value.hit))
                         ? (m.Value.origin ? m.Value.origin : m.Value.hit.transform)
                         : transform;

        // �� hooks�� public Perform�� ������ �� �� ���
        hooks.Perform(defs[defIndex], origin);

        // �� ���� Perform�� �� ������ٸ�, ���� ���� ���� �޼���� ��ü:
        // hooks.Anim_ATK_Index(defIndex); // �̷� ������
    }
}
