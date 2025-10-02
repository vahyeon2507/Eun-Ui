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
    public BulgasariBoss boss;
    public Transform attackOriginCenter;   // 기본 기준 (없으면 transform)
    public Transform attackOriginLeft;     // (선택) 왼팔/왼쪽 앵커
    public Transform attackOriginRight;    // (선택) 오른팔/오른쪽 앵커

    // ===== Legacy (옵션) : 리스트가 비었을 때 자동 등록용 =====
    [Header("Legacy (optional, for bootstrap)")]
    public AttackDefinition2D thorn;
    public AttackDefinition2D defSweep;
    public AttackDefinition2D defSlam;

    [Header("Gizmos / Preview")]
    public bool gizmoEnabled = true;

    public enum GizmoMode { Off, SelectedID, All }
    public GizmoMode gizmoMode = GizmoMode.SelectedID;

    [Tooltip("SelectedID 모드일 때 볼 공격 ID")]
    public string gizmoAttackId = "Thorn";

    [Tooltip("원한다면 앵커도 지정 (예: Left / Right / Center). 빈 값이면 ID에 매칭된 앵커 사용")]
    public string gizmoAnchorKey = "";

    public Color gizmoFill = new(1f, 0.2f, 0.2f, 0.12f);
    public Color gizmoWire = new(1f, 0.2f, 0.2f, 0.36f);

    // ===== Anchors / Hitboxes =====
    [Serializable]
    public struct AnchorEntry
    {
        public string key;                 // "Left","Right","Center","SpineL"...
        public HitboxTrigger2D hit;        // 지속 트리거(있으면)
        public Transform originOverride;   // 원샷 기준(없으면 hit.transform/Center 사용)
    }
    [Header("Anchors / Hitboxes")]
    public List<AnchorEntry> anchors = new();

    // BulgasariAttackHooks.cs 내부 (기존 코드 그대로 두고 아래만 추가)

    // 인스펙터 기본 지속시간(없으면 이 값 사용)
    [Header("OnAt Defaults")]
    public float defaultOnAtDuration = 0.20f;

    // 내부 공통
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

    // 1) 키만 받는 버전(지속시간은 기본값 사용) → 이벤트에서 문자열 1개만
    public void Anim_OnAt_Key(string key)
    {
        OnAt_Internal(key, defaultOnAtDuration);
    }

    // 2) "key,duration" 한 줄로 받는 버전 → 이벤트에서 문자열 1개만
    // 예) Anim_OnAt_Spec("Left,0.35")
    public void Anim_OnAt_Spec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;
        var p = spec.Split(new char[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string key = p.Length >= 1 ? p[0] : "Center";
        float dur = (p.Length >= 2 && float.TryParse(p[1], out var d)) ? d : defaultOnAtDuration;
        OnAt_Internal(key, dur);
    }


    // ===== Attack Table (ID → Definition) =====
    [Serializable]
    public struct DefEntry
    {
        public string id;                  // "Thorn","Sweep","Slam"... 임의 문자열
        public AttackDefinition2D def;     // 실행할 어택 데이터
        public Transform originOverride;   // 이 공격 전용 기준점(보통 비움)
        public int defaultBurst;        // 연타 기본 횟수(0/1이면 단발)
        public float defaultInterval;     // 연타 기본 간격(초)
    }
    [Header("Attack Table (ID → Def)")]
    public List<DefEntry> attackDefs = new();

    // ====== 공통 실행 ======
    public void Perform(AttackDefinition2D def, Transform originOverride = null)
    {
        if (!def) return;

        var origin = originOverride
                   ? originOverride
                   : (attackOriginCenter ? attackOriginCenter : transform);

        bool facingRight = transform.localScale.x >= 0f;

        HitExec2D.ExecuteAttack(def, origin, facingRight, col =>
        {
            // 패링 우선 소비
            var pc = col.GetComponent<PlayerController>() ??
                     col.GetComponentInParent<PlayerController>() ??
                     col.GetComponentInChildren<PlayerController>();
            if (pc != null && pc.IsParrying && pc.ConsumeHitboxIfParrying(col)) return;

            // 대미지 전달
            var dmg = col.GetComponent<IDamageable>() ??
                      col.GetComponentInParent<IDamageable>() ??
                      col.GetComponentInChildren<IDamageable>();
            if (dmg != null) dmg.TakeDamage(def.damage);
        });
    }





    // ====== 애니메이션 이벤트 API (확장형) ======

    // A) 지속 판정 스위치: 해당 앵커의 히트박스를 dur초 켠다
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

    // B) 원샷 1회: 인스펙터 테이블의 "공격ID"를 실행
    public void Anim_ATK_ID(string id)
    {
        var de = FindDef(id);
        if (de == null || !de.Value.def)
        {
            Debug.LogWarning($"[Bulgasari] AttackID '{id}' not found.");
            return;
        }

        // 같은 이름의 앵커가 있으면 그 기준을 자동 사용
        var a = FindAnchor(id);
        var origin = ResolveOrigin(de.Value, a, null);
        Perform(de.Value.def, origin);
    }

    // C) 원샷 연타: "id,횟수,간격[,앵커키]" (횟수/간격 생략 시 테이블 기본값)
    //   예) "Thorn,4,0.07"  /  "Thorn"  /  "Thorn,3,0.05,Left"
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

    // D) 원샷: 특정 앵커키를 명시해서 실행하고 싶을 때
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

    // ====== 호환용(기존 이벤트 이름 유지) ======
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
            // 레거시 3종도 보조로
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
            // 레거시 명칭도 허용
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

        // 우선순위: 명시 키 → 같은 이름 앵커 → 앵커의 hit/override → Center
        if (anchorMaybe != null)
        {
            var a = anchorMaybe.Value;
            if (a.originOverride) return a.originOverride;
            if (a.hit) return a.hit.transform;
        }

        // 별도 키로 직접 찾기 (위에서 못 찾았을 때)
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
        // 기본 앵커 자동 추가 (비어있을 때만)
        AddDefaultAnchor("Center", attackOriginCenter);
        AddDefaultAnchor("Left", attackOriginLeft);
        AddDefaultAnchor("Right", attackOriginRight);

        // 공격 테이블 비었으면 레거시 필드로 1회 부트스트랩
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
        // 키 공백 정리
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
