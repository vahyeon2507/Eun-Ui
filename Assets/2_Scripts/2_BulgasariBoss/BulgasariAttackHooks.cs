// BulgasariAttackHooks.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class BulgasariAttackHooks : MonoBehaviour
{

    // ===== Refs =====

    public IEnumerable<string> GetAttackIds()
    {
        // 네가 쓰는 테이블 타입에 맞춰서 순회
        foreach (var e in attackDefs)            // 예: List<AttackIdDef> attackDefs;
            if (!string.IsNullOrEmpty(e.id))
                yield return e.id;
    }

    // 3) 직접 실행용(애니 없이 즉시 히트 발생)
    public void ExecById(string id)
    {
        if (!TryGetDefById(id, out var def)) return;
        Perform(def); // 기존 Perform(AttackDefinition2D def, Transform originOverride = null)
    }

   public bool TryGetDefById(string id, out AttackDefinition2D def)
{
    def = null;
    if (string.IsNullOrEmpty(id) || attackDefs == null) return false;

    foreach (var e in attackDefs) // e : DefEntry (struct)
    {
        // struct는 null 비교 불가 → default(struct)인지 검사
        if (EqualityComparer<DefEntry>.Default.Equals(e, default)) 
            continue;

        // ID 일치 + 실제 Def 존재
        if (e.id == id && e.def != null)
        {
            def = e.def;
            return true;                 // ← bool을 리턴해야 함
        }
    }
    return false;
}

    

    public bool HasAttackId(string id) => TryGetDefById(id, out _);
    public BulgasariBoss boss;
    public Transform attackOriginCenter;   // �⺻ ���� (������ transform)
    public Transform attackOriginLeft;     // (����) ����/���� ��Ŀ
    public Transform attackOriginRight;    // (����) ������/������ ��Ŀ

    // ===== Legacy (�ɼ�) : ����Ʈ�� ����� �� �ڵ� ��Ͽ� =====
    [Header("Legacy (optional, for bootstrap)")]
    public AttackDefinition2D thorn;
    public AttackDefinition2D defSweep;
    public AttackDefinition2D defSlam;

    [Header("Gizmos / Preview")]
    public bool gizmoEnabled = true;

    public enum GizmoMode { Off, SelectedID, All }
    public GizmoMode gizmoMode = GizmoMode.SelectedID;

    [Tooltip("SelectedID ����� �� �� ���� ID")]
    public string gizmoAttackId = "Thorn";

    [Tooltip("���Ѵٸ� ��Ŀ�� ���� (��: Left / Right / Center). �� ���̸� ID�� ��Ī�� ��Ŀ ���")]
    public string gizmoAnchorKey = "";

    public Color gizmoFill = new(1f, 0.2f, 0.2f, 0.12f);
    public Color gizmoWire = new(1f, 0.2f, 0.2f, 0.36f);

    // ===== Anchors / Hitboxes =====
    [Serializable]
    public struct AnchorEntry
    {
        public string key;                 // "Left","Right","Center","SpineL"...
        public HitboxTrigger2D hit;        // ���� Ʈ����(������)
        public Transform originOverride;   // ���� ����(������ hit.transform/Center ���)
    }
    [Header("Anchors / Hitboxes")]
    public List<AnchorEntry> anchors = new();

    // BulgasariAttackHooks.cs ���� (���� �ڵ� �״�� �ΰ� �Ʒ��� �߰�)

    // �ν����� �⺻ ���ӽð�(������ �� �� ���)
    [Header("OnAt Defaults")]
    public float defaultOnAtDuration = 0.20f;

    // ���� ����
    void OnAt_Internal(string key, float dur)
    {
        var a = FindAnchor(key);
        if (a == null || a.Value.hit == null)
        {
            Debug.LogWarning($"[Bulgasari] Anchor '{key}' not found or no HitboxTrigger2D.");
            return;
        }
        a.Value.hit.Activate(Mathf.Max(0f, dur));
    }

    // 1) Ű�� �޴� ����(���ӽð��� �⺻�� ���) �� �̺�Ʈ���� ���ڿ� 1����
    public void Anim_OnAt_Key(string key)
    {
        OnAt_Internal(key, defaultOnAtDuration);
    }

    // 2) "key,duration" �� �ٷ� �޴� ���� �� �̺�Ʈ���� ���ڿ� 1����
    // ��) Anim_OnAt_Spec("Left,0.35")
    public void Anim_OnAt_Spec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;
        var p = spec.Split(new char[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string key = p.Length >= 1 ? p[0] : "Center";
        float dur = (p.Length >= 2 && float.TryParse(p[1], out var d)) ? d : defaultOnAtDuration;
        OnAt_Internal(key, dur);
    }


    // ===== Attack Table (ID �� Definition) =====
    [Serializable]
    public struct DefEntry
    {
        public string id;                  // "Thorn","Sweep","Slam"... ���� ���ڿ�
        public AttackDefinition2D def;     // ������ ���� ������
        public Transform originOverride;   // �� ���� ���� ������(���� ���)
        public int defaultBurst;        // ��Ÿ �⺻ Ƚ��(0/1�̸� �ܹ�)
        public float defaultInterval;     // ��Ÿ �⺻ ����(��)
    }
    [Header("Attack Table (ID �� Def)")]
    public List<DefEntry> attackDefs = new();

    // ====== ���� ���� ======
    public void Perform(AttackDefinition2D def, Transform originOverride = null)
    {
        if (!def) return;

        var origin = originOverride
                   ? originOverride
                   : (attackOriginCenter ? attackOriginCenter : transform);

        bool facingRight = transform.localScale.x >= 0f;

        HitExec2D.ExecuteAttack(def, origin, facingRight, col =>
        {
            // �и� �켱 �Һ�
            var pc = col.GetComponent<PlayerController>() ??
                     col.GetComponentInParent<PlayerController>() ??
                     col.GetComponentInChildren<PlayerController>();
            if (pc != null && pc.IsParrying && pc.ConsumeHitboxIfParrying(col)) return;

            // ����� ����
            var dmg = col.GetComponent<IDamageable>() ??
                      col.GetComponentInParent<IDamageable>() ??
                      col.GetComponentInChildren<IDamageable>();
            if (dmg != null) dmg.TakeDamage(def.damage);
        });
    }





    // ====== �ִϸ��̼� �̺�Ʈ API (Ȯ����) ======

    // A) ���� ���� ����ġ: �ش� ��Ŀ�� ��Ʈ�ڽ��� dur�� �Ҵ�
    public void Anim_OnAt(string key, float dur)
    {
        var a = FindAnchor(key);
        if (a == null || a.Value.hit == null)
        {
            Debug.LogWarning($"[Bulgasari] Anchor '{key}' not found or no HitboxTrigger2D.");
            return;
        }
        a.Value.hit.Activate(dur);
    }

    // B) ���� 1ȸ: �ν����� ���̺��� "����ID"�� ����
    public void Anim_ATK_ID(string id)
    {
        var de = FindDef(id);
        if (de == null || !de.Value.def)
        {
            Debug.LogWarning($"[Bulgasari] AttackID '{id}' not found.");
            return;
        }

        // ���� �̸��� ��Ŀ�� ������ �� ������ �ڵ� ���
        var a = FindAnchor(id);
        var origin = ResolveOrigin(de.Value, a, null);
        Perform(de.Value.def, origin);
    }

    // C) ���� ��Ÿ: "id,Ƚ��,����[,��ĿŰ]" (Ƚ��/���� ���� �� ���̺� �⺻��)
    //   ��) "Thorn,4,0.07"  /  "Thorn"  /  "Thorn,3,0.05,Left"
    public void Anim_ATK_Burst(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;

        var parts = spec.Split(new char[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string id = parts[0];

        var de = FindDef(id);
        if (de == null || !de.Value.def)
        {
            Debug.LogWarning($"[Bulgasari] AttackID '{id}' not found.");
            return;
        }

        int count = (de.Value.defaultBurst > 0) ? de.Value.defaultBurst : 1;
        float interval = (de.Value.defaultInterval > 0f) ? de.Value.defaultInterval : 0f;
        string keyOverride = null;

        if (parts.Length >= 2 && int.TryParse(parts[1], out var c)) count = Mathf.Max(1, c);
        if (parts.Length >= 3 && float.TryParse(parts[2], out var itv)) interval = Mathf.Max(0f, itv);
        if (parts.Length >= 4) keyOverride = parts[3];

        var a = string.IsNullOrEmpty(keyOverride) ? FindAnchor(id) : FindAnchor(keyOverride);
        var origin = ResolveOrigin(de.Value, a, keyOverride);
        StartCoroutine(BurstRoutine(de.Value.def, origin, count, interval));
    }

    // D) ����: Ư�� ��ĿŰ�� �����ؼ� �����ϰ� ���� ��
    public void Anim_ATK_ID_At(string id, string key)
    {
        var de = FindDef(id);
        if (de == null || !de.Value.def) { Debug.LogWarning($"[Bulgasari] AttackID '{id}' not found."); return; }

        var a = FindAnchor(key);
        var origin = ResolveOrigin(de.Value, a, key);
        Perform(de.Value.def, origin);
    }

    IEnumerator BurstRoutine(AttackDefinition2D def, Transform origin, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            Perform(def, origin);
            if (i < count - 1 && interval > 0f) yield return new WaitForSeconds(interval);
        }
    }

    // ====== ȣȯ��(���� �̺�Ʈ �̸� ����) ======
    public void Anim_ATK_DoublePunch() => Anim_ATK_ID("Thorn");
    public void Anim_ATK_Sweep() => Anim_ATK_ID("Sweep");
    public void Anim_ATK_Slam() => Anim_ATK_ID("Slam");
    public void Anim_ATK_LeftPunch() => Anim_ATK_ID_At("Thorn", "Left");
    public void Anim_ATK_RightPunch() => Anim_ATK_ID_At("Thorn", "Right");

    // ====== Gizmos ======
    void OnDrawGizmosSelected()
    {
        if (!gizmoEnabled) return;

        bool facingRight = transform.localScale.x >= 0f;

        if (gizmoMode == GizmoMode.All)
        {
            foreach (var de in attackDefs)
            {
                if (!de.def) continue;
                var a = string.IsNullOrEmpty(gizmoAnchorKey) ? FindAnchor(de.id) : FindAnchor(gizmoAnchorKey);
                var origin = ResolveOrigin(de, a, gizmoAnchorKey);
                HitExec2D.DrawGizmos(de.def, origin, facingRight, gizmoFill, gizmoWire);
            }
            // ���Ž� 3���� ������
            var legacyOrigin = attackOriginCenter ? attackOriginCenter : transform;
            HitExec2D.DrawGizmos(thorn, legacyOrigin, facingRight, gizmoFill, gizmoWire);
            HitExec2D.DrawGizmos(defSweep, legacyOrigin, facingRight, gizmoFill, gizmoWire);
            HitExec2D.DrawGizmos(defSlam, legacyOrigin, facingRight, gizmoFill, gizmoWire);
            return;
        }

        if (gizmoMode == GizmoMode.SelectedID)
        {
            var deOpt = FindDef(gizmoAttackId);
            if (deOpt != null && deOpt.Value.def)
            {
                var a = string.IsNullOrEmpty(gizmoAnchorKey) ? FindAnchor(gizmoAttackId) : FindAnchor(gizmoAnchorKey);
                var origin = ResolveOrigin(deOpt.Value, a, gizmoAnchorKey);
                HitExec2D.DrawGizmos(deOpt.Value.def, origin, facingRight, gizmoFill, gizmoWire);
                return;
            }
            // ���Ž� ��Ī�� ���
            AttackDefinition2D legacy = (gizmoAttackId == "Thorn") ? thorn :
                                        (gizmoAttackId == "Sweep") ? defSweep :
                                        (gizmoAttackId == "Slam") ? defSlam : null;
            if (legacy)
            {
                var origin = attackOriginCenter ? attackOriginCenter : transform;
                HitExec2D.DrawGizmos(legacy, origin, facingRight, gizmoFill, gizmoWire);
            }
        }
    }

    // ====== Internals ======
    AnchorEntry? FindAnchor(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return anchors.FirstOrDefault(a => string.Equals(a.key, key, StringComparison.OrdinalIgnoreCase));
    }

    DefEntry? FindDef(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return attackDefs.FirstOrDefault(a => string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase));
    }

    Transform ResolveOrigin(DefEntry de, AnchorEntry? anchorMaybe, string keyOverride)
    {
        if (de.originOverride) return de.originOverride;

        // �켱����: ���� Ű �� ���� �̸� ��Ŀ �� ��Ŀ�� hit/override �� Center
        if (anchorMaybe != null)
        {
            var a = anchorMaybe.Value;
            if (a.originOverride) return a.originOverride;
            if (a.hit) return a.hit.transform;
        }

        // ���� Ű�� ���� ã�� (������ �� ã���� ��)
        if (!string.IsNullOrEmpty(keyOverride))
        {
            var a2 = FindAnchor(keyOverride);
            if (a2 != null)
            {
                if (a2.Value.originOverride) return a2.Value.originOverride;
                if (a2.Value.hit) return a2.Value.hit.transform;
            }
        }

        return attackOriginCenter ? attackOriginCenter : transform;
    }

    void Awake() => BootstrapLegacyDefaults();

    void BootstrapLegacyDefaults()
    {
        // �⺻ ��Ŀ �ڵ� �߰� (������� ����)
        AddDefaultAnchor("Center", attackOriginCenter);
        AddDefaultAnchor("Left", attackOriginLeft);
        AddDefaultAnchor("Right", attackOriginRight);

        // ���� ���̺� ������� ���Ž� �ʵ�� 1ȸ ��Ʈ��Ʈ��
        if (attackDefs == null) attackDefs = new List<DefEntry>();
        if (attackDefs.Count == 0)
        {
            if (thorn) attackDefs.Add(new DefEntry { id = "Thorn", def = thorn });
            if (defSweep) attackDefs.Add(new DefEntry { id = "Sweep", def = defSweep });
            if (defSlam) attackDefs.Add(new DefEntry { id = "Slam", def = defSlam });
        }
    }

    void AddDefaultAnchor(string key, Transform t)
    {
        if (!t) return;
        if (anchors.Any(a => string.Equals(a.key, key, StringComparison.OrdinalIgnoreCase))) return;

        anchors.Add(new AnchorEntry
        {
            key = key,
            hit = null,
            originOverride = t
        });
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Ű ���� ����
        for (int i = 0; i < anchors.Count; i++)
        {
            anchors[i] = new AnchorEntry
            {
                key = string.IsNullOrWhiteSpace(anchors[i].key) ? anchors[i].key : anchors[i].key.Trim(),
                hit = anchors[i].hit,
                originOverride = anchors[i].originOverride
            };
        }
        for (int i = 0; i < attackDefs.Count; i++)
        {
            attackDefs[i] = new DefEntry
            {
                id = string.IsNullOrWhiteSpace(attackDefs[i].id) ? attackDefs[i].id : attackDefs[i].id.Trim(),
                def = attackDefs[i].def,
                originOverride = attackDefs[i].originOverride,
                defaultBurst = attackDefs[i].defaultBurst,
                defaultInterval = attackDefs[i].defaultInterval
            };
        }
    }
#endif
}
