// BulgasariAttackHooks.cs
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BulgasariAttackHooks : MonoBehaviour
{
    public BulgasariBoss boss;
    public Transform attackOriginCenter;   // �⺻�� ����(������ transform)
    public Transform attackOriginLeft;     // (����) ���� ��
    public Transform attackOriginRight;    // (����) ������ ��

    [Header("Attack Definitions")]
    public AttackDefinition2D defDoublePunch;
    public AttackDefinition2D defSweep;
    public AttackDefinition2D defSlam;

    // === ���� ���� ===
    void Exec(AttackDefinition2D def, Transform originOverride = null)
    {
        if (!def) return;
        var origin = originOverride ? originOverride : (attackOriginCenter ? attackOriginCenter : transform);
        bool facingRight = transform.localScale.x >= 0f;

        HitExec2D.ExecuteAttack(def, origin, facingRight, col =>
        {
            // �и� �Һ� �켱
            var pc = col.GetComponent<PlayerController>() ??
                     col.GetComponentInParent<PlayerController>() ??
                     col.GetComponentInChildren<PlayerController>();
            if (pc != null && pc.IsParrying && pc.ConsumeHitboxIfParrying(col)) return;

            // ����� ����
            var dmg = col.GetComponent<IDamageable>() ??
                      col.GetComponentInParent<IDamageable>() ??
                      col.GetComponentInChildren<IDamageable>();
            if (dmg != null) { dmg.TakeDamage(def.damage); }
        });
    }

    // === �ִ� �̺�Ʈ�� ȣ���� �Լ��� ===
    public void Anim_ATK_DoublePunch() { Exec(defDoublePunch); }
    public void Anim_ATK_Sweep() { Exec(defSweep); }
    public void Anim_ATK_Slam() { Exec(defSlam); }

    // �� ���� ���� ������ �̷� ������
    public void Anim_ATK_LeftPunch()
    {
        if (!defDoublePunch) return;
        // ���ȸ� ġ�� �ʹٸ�: ��Ʈ����� '���� �ϳ�'�� ���� AttackDefinition�� ���� ����� �ΰų�,
        // �Ʒ�ó�� originOverride�� ���ȷ� �ѱ�� ���� AttackDefinition�� �����.
        Exec(defDoublePunch, attackOriginLeft);
    }
    public void Anim_ATK_RightPunch() { Exec(defDoublePunch, attackOriginRight); }

    // �����Ϳ��� ���� �̸�����
    void OnDrawGizmosSelected()
    {
        var origin = attackOriginCenter ? attackOriginCenter : transform;
        bool facingRight = transform.localScale.x >= 0f;
        Color fill = new Color(1f, 0.2f, 0.2f, 0.12f);
        Color wire = new Color(1f, 0.2f, 0.2f, 0.35f);

        HitExec2D.DrawGizmos(defDoublePunch, origin, facingRight, fill, wire);
        HitExec2D.DrawGizmos(defSweep, origin, facingRight, fill, wire);
        HitExec2D.DrawGizmos(defSlam, origin, facingRight, fill, wire);
    }
}
