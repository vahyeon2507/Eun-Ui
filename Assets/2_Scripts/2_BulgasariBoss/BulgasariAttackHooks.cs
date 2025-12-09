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

    [Header("Attack Origins (Transform fallback)")]
    [Tooltip("가슴 기준 원점(미지정 시 self). 'Origin by Collider'가 지정되면 무시됨")]
    public Transform attackOriginCenter;
    [Tooltip("왼팔 원점(선택)")]
    public Transform attackOriginLeft;
    [Tooltip("오른팔 원점(선택)")]
    public Transform attackOriginRight;

    [Header("Origin by Collider (preferred)")]
    [Tooltip("가슴/왼팔/오른팔의 기준점을 Transform 대신 이 콜라이더의 bounds.center로 사용")]
    public Collider2D originCenterCollider;
    public Collider2D originLeftCollider;
    public Collider2D originRightCollider;

    // 내부 피벗(콜라이더 중심을 담는 숨김 트랜스폼)
    Transform _pivotCenter, _pivotLeft, _pivotRight;

    // ===== Legacy (optional, for bootstrap) =====
    [Header("Legacy (optional, for bootstrap)")]
    public AttackDefinition2D thorn;
    public AttackDefinition2D defSweep;
    public AttackDefinition2D defSlam;

    // ===== Anchors / Hitboxes =====
    [Serializable]
    public struct AnchorEntry
    {
        public string key;                 // "Left","Right","Center","SpineL"...
        public HitboxTrigger2D hit;        // 히트 트리거(선택)
        public Transform originOverride;   // 원점 오버라이드(선택, hit.transform/Center 대신)
    }

    [Header("Anchors / Hitboxes")]
    public List<AnchorEntry> anchors = new();

    // ===== Attack Table (ID ↔ Definition) =====
    [Serializable]
    public struct DefEntry
    {
        public string id;                  // "Thorn","Sweep","Slam" 등
        public AttackDefinition2D def;     // 공격 정의
        public Transform originOverride;   // 이 공격만 특정 원점 사용(선택)
        public int defaultBurst;           // 버스트 기본 횟수(0/1이면 단발)
        public float defaultInterval;      // 버스트 기본 간격(초)

        [Tooltip("이 공격이 '강공격'(일반 패링 불가, 쇠+불 부적으로만 강패링 가능)인지 여부")]
        public bool isStrongAttack;
    }

    [Header("Attack Table (ID ↔ Def)")]
    public List<DefEntry> attackDefs = new();

    [Header("OnAt Defaults")]
    public float defaultOnAtDuration = 0.20f;

    // ===== Gizmos / Preview =====
    [Header("Gizmos / Preview")]
    public bool gizmoEnabled = true;

    public enum GizmoMode { Off, SelectedID, All }
    public GizmoMode gizmoMode = GizmoMode.SelectedID;

    [Tooltip("SelectedID 모드일 때 볼 공격 ID")]
    public string gizmoAttackId = "Thorn";

    [Tooltip("지정하면 이 앵커로 미리봄 (예: Left / Right / Center). 비워두면 ID와 같은 키/기본 원점 사용")]
    public string gizmoAnchorKey = "";

    public Color gizmoFill = new(1f, 0.2f, 0.2f, 0.12f);
    public Color gizmoWire = new(1f, 0.2f, 0.2f, 0.36f);

    // ====== 라이프사이클 ======
    void Awake()
    {
        EnsurePivots();
        UpdateAllPivotsFromColliders();
        BootstrapLegacyDefaults();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 키/ID 공백 정리
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
                defaultInterval = attackDefs[i].defaultInterval,
                isStrongAttack = attackDefs[i].isStrongAttack   // ★ 새 필드 유지
            };
        }

        if (!Application.isPlaying)
        {
            EnsurePivots();
            UpdateAllPivotsFromColliders(); // 에디터에서도 기즈모 정확히 보이도록
        }
    }
#endif

    // ===== 유틸: 공격 ID 헬퍼 =====
    public IEnumerable<string> GetAttackIds()
    {
        if (attackDefs == null) yield break;
        foreach (var e in attackDefs)
            if (!string.IsNullOrEmpty(e.id))
                yield return e.id;
    }

    public bool HasAttackId(string id) => TryGetDefById(id, out _);

    public bool TryGetDefById(string id, out AttackDefinition2D def)
    {
        def = null;
        if (string.IsNullOrEmpty(id) || attackDefs == null) return false;

        foreach (var e in attackDefs) // struct는 null 아님
        {
            // default(struct) 보호
            if (EqualityComparer<DefEntry>.Default.Equals(e, default)) continue;
            if (e.id == id && e.def != null)
            {
                def = e.def;
                return true;
            }
        }
        return false;
    }

    // 애니 없이 즉시 실행 (레거시용)
    public void ExecById(string id)
    {
        var de = FindDef(id);
        if (!de.HasValue || !de.Value.def) return;
        Perform(de.Value.def, de.Value.isStrongAttack);
    }

    // ====== 지정 프레임 히트(OnAt) ======
    void OnAt_Internal(string key, float dur)
    {
        var a = FindAnchor(key);
        if (!a.HasValue || a.Value.hit == null)
        {
            Debug.LogWarning($"[Bulgasari] Anchor '{key}' not found or no HitboxTrigger2D.");
            return;
        }
        a.Value.hit.Activate(Mathf.Max(0f, dur));
    }

    // 1) key만 받음(기본 지속시간 사용)
    public void Anim_OnAt_Key(string key) => OnAt_Internal(key, defaultOnAtDuration);

    // 2) "key,duration" 형태로 받음
    public void Anim_OnAt_Spec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;
        var p = spec.Split(new char[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string key = p.Length >= 1 ? p[0] : "Center";
        float dur = (p.Length >= 2 && float.TryParse(p[1], out var d)) ? d : defaultOnAtDuration;
        OnAt_Internal(key, dur);
    }

    // ====== 공통 실행 ======
    // 기존 시그니처 유지 (외부 호환용) → 항상 '일반 공격'으로 처리
    public void Perform(AttackDefinition2D def, Transform originOverride = null)
    {
        Perform(def, false, originOverride);
    }

    // ★ 강공격 여부를 함께 받는 실제 실행 버전
    public void Perform(AttackDefinition2D def, bool isStrongAttack, Transform originOverride = null)
    {
        if (!def) return;

        var origin = originOverride ? originOverride : ResolveCenterOrigin();
        bool facingRight = transform.localScale.x >= 0f;

        HitExec2D.ExecuteAttack(def, origin, facingRight, col =>
        {
            if (!col) return;

            // 1) 플레이어 우선 처리
            var pc = col.GetComponent<PlayerController>() ??
                     col.GetComponentInParent<PlayerController>() ??
                     col.GetComponentInChildren<PlayerController>();

            if (pc != null)
            {
                int dmg = Mathf.Max(1, def.damage);

                if (isStrongAttack)
                {
                    // ▼ 강공격: 먼저 강패링 존에 의한 강패링 시도
                    if (TalismanStrongParryZone.TryHandleStrongAttackHit(pc))
                    {
                        // 강패링 성공 → 대미지 없음, 히트 소비
                        return;
                    }

                    // ▲ 강패링 실패 → 일반 패링으로는 막을 수 없음
                    DealStrongDamageToPlayer(pc, dmg);
                    return;
                }
                else
                {
                    // ▼ 일반 공격: 기존 패링 로직 유지
                    if (pc.IsParrying)
                    {
                        bool consumed = pc.ConsumeHitboxIfParrying(col);
                        if (consumed) return;
                    }

                    pc.TakeDamage(dmg);
                    return;
                }
            }

            // 2) 기타 IDamageable 대상 처리
            var dmgTarget = col.GetComponent<IDamageable>() ??
                            col.GetComponentInParent<IDamageable>() ??
                            col.GetComponentInChildren<IDamageable>();
            if (dmgTarget != null)
            {
                int dmg = Mathf.Max(1, def.damage);
                dmgTarget.TakeDamage(dmg);
            }
        });
    }

    void DealStrongDamageToPlayer(PlayerController pc, int amount)
    {
        if (pc == null || amount <= 0) return;

        // 1순위: PlayerController가 들고 있는 healthComponent 사용
        var hc = pc.healthComponent;
        if (hc != null)
        {
            var mi = hc.GetType().GetMethod("TakeDamage", new Type[] { typeof(int) });
            if (mi != null)
            {
                mi.Invoke(hc, new object[] { amount });
                return;
            }
        }

        // 폴백: IDamageable 직접 찾기
        var dmg = pc.GetComponent<IDamageable>() ??
                  pc.GetComponentInParent<IDamageable>() ??
                  pc.GetComponentInChildren<IDamageable>();
        if (dmg != null)
            dmg.TakeDamage(amount);
    }

    // ====== 애니메이션 이벤트 API ======
    public void Anim_ATK_ID(string id)
    {
        var de = FindDef(id);
        if (!de.HasValue || !de.Value.def)
        {
            Debug.LogWarning($"[Bulgasari] AttackID '{id}' not found.");
            return;
        }
        var a = FindAnchor(id);
        var origin = ResolveOrigin(de.Value, a, null);
        Perform(de.Value.def, de.Value.isStrongAttack, origin);
    }

    // "id,횟수,간격[,앵커키]"
    public void Anim_ATK_Burst(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return;

        var parts = spec.Split(new char[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string id = parts[0];

        var de = FindDef(id);
        if (!de.HasValue || !de.Value.def)
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
        StartCoroutine(BurstRoutine(de.Value, origin, count, interval));
    }

    public void Anim_ATK_ID_At(string id, string key)
    {
        var de = FindDef(id);
        if (!de.HasValue || !de.Value.def)
        {
            Debug.LogWarning($"[Bulgasari] AttackID '{id}' not found.");
            return;
        }
        var a = FindAnchor(key);
        var origin = ResolveOrigin(de.Value, a, key);
        Perform(de.Value.def, de.Value.isStrongAttack, origin);
    }

    IEnumerator BurstRoutine(DefEntry de, Transform origin, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            Perform(de.def, de.isStrongAttack, origin);
            if (i < count - 1 && interval > 0f) yield return new WaitForSeconds(interval);
        }
    }

    // 편의 래퍼(레거시)
    public void Anim_ATK_DoublePunch() => Anim_ATK_ID("Thorn");
    public void Anim_ATK_Sweep() => Anim_ATK_ID("Sweep");
    public void Anim_ATK_Slam() => Anim_ATK_ID("Slam");
    public void Anim_ATK_LeftPunch() => Anim_ATK_ID_At("Thorn", "Left");
    public void Anim_ATK_RightPunch() => Anim_ATK_ID_At("Thorn", "Right");

    // ====== Gizmos ======
    void OnDrawGizmosSelected()
    {
        if (!gizmoEnabled) return;
        EnsurePivots();
        UpdateAllPivotsFromColliders();

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
            // 레거시 3개도 보정
            var legacyOrigin = ResolveCenterOrigin();
            HitExec2D.DrawGizmos(thorn, legacyOrigin, facingRight, gizmoFill, gizmoWire);
            HitExec2D.DrawGizmos(defSweep, legacyOrigin, facingRight, gizmoFill, gizmoWire);
            HitExec2D.DrawGizmos(defSlam, legacyOrigin, facingRight, gizmoFill, gizmoWire);
            return;
        }

        if (gizmoMode == GizmoMode.SelectedID)
        {
            var deOpt = FindDef(gizmoAttackId);
            if (deOpt.HasValue && deOpt.Value.def)
            {
                var a = string.IsNullOrEmpty(gizmoAnchorKey) ? FindAnchor(gizmoAttackId) : FindAnchor(gizmoAnchorKey);
                var origin = ResolveOrigin(deOpt.Value, a, gizmoAnchorKey);
                HitExec2D.DrawGizmos(deOpt.Value.def, origin, facingRight, gizmoFill, gizmoWire);
                return;
            }
            // 레거시 fallback
            AttackDefinition2D legacy = (gizmoAttackId == "Thorn") ? thorn :
                                        (gizmoAttackId == "Sweep") ? defSweep :
                                        (gizmoAttackId == "Slam") ? defSlam : null;
            if (legacy)
            {
                var origin = ResolveCenterOrigin();
                HitExec2D.DrawGizmos(legacy, origin, facingRight, gizmoFill, gizmoWire);
            }
        }
    }

    // ====== Internals ======
    AnchorEntry? FindAnchor(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || anchors == null || anchors.Count == 0) return null;
        for (int i = 0; i < anchors.Count; i++)
        {
            if (string.Equals(anchors[i].key, key, StringComparison.OrdinalIgnoreCase))
                return anchors[i];
        }
        return null;
    }

    DefEntry? FindDef(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || attackDefs == null || attackDefs.Count == 0) return null;
        for (int i = 0; i < attackDefs.Count; i++)
        {
            if (string.Equals(attackDefs[i].id, id, StringComparison.OrdinalIgnoreCase))
                return attackDefs[i];
        }
        return null;
    }

    // --- 콜라이더 피벗/원점 해석 ---
    Transform ResolveCenterOrigin()
    {
        if (originCenterCollider != null)
        {
            if (_pivotCenter == null) _pivotCenter = CreatePivot("__OriginPivot(Center)");
            SnapPivotToCollider(_pivotCenter, originCenterCollider);
            return _pivotCenter;
        }
        return attackOriginCenter ? attackOriginCenter : transform;
    }

    Transform ResolveLeftOrigin()
    {
        if (originLeftCollider != null)
        {
            if (_pivotLeft == null) _pivotLeft = CreatePivot("__OriginPivot(Left)");
            SnapPivotToCollider(_pivotLeft, originLeftCollider);
            return _pivotLeft;
        }
        return attackOriginLeft ? attackOriginLeft : transform;
    }

    Transform ResolveRightOrigin()
    {
        if (originRightCollider != null)
        {
            if (_pivotRight == null) _pivotRight = CreatePivot("__OriginPivot(Right)");
            SnapPivotToCollider(_pivotRight, originRightCollider);
            return _pivotRight;
        }
        return attackOriginRight ? attackOriginRight : transform;
    }

    Transform ResolveOrigin(DefEntry de, AnchorEntry? anchorMaybe, string keyOverride)
    {
        // 1) 공격별 오버라이드
        if (de.originOverride) return de.originOverride;

        // 2) 앵커 기반
        if (anchorMaybe.HasValue)
        {
            var a = anchorMaybe.Value;
            if (a.originOverride) return a.originOverride;
            if (a.hit) return a.hit.transform;
        }

        // 3) 키 기반: "Center/Left/Right"면 콜라이더 피벗 우선
        if (!string.IsNullOrEmpty(keyOverride))
        {
            var t = ResolvePivotByKey(keyOverride);
            if (t != null) return t;

            // 앵커 테이블에만 있고 키가 맞을 수 있으니 한 번 더
            var a2 = FindAnchor(keyOverride);
            if (a2.HasValue)
            {
                if (a2.Value.originOverride) return a2.Value.originOverride;
                if (a2.Value.hit) return a2.Value.hit.transform;
            }
        }

        // 4) 최종: Center 콜라이더 피벗 → 트랜스폼 → 자기 자신
        return ResolveCenterOrigin();
    }

    Transform ResolvePivotByKey(string key)
    {
        if (string.Equals(key, "Center", StringComparison.OrdinalIgnoreCase)) return ResolveCenterOrigin();
        if (string.Equals(key, "Left", StringComparison.OrdinalIgnoreCase)) return ResolveLeftOrigin();
        if (string.Equals(key, "Right", StringComparison.OrdinalIgnoreCase)) return ResolveRightOrigin();
        return null;
    }

    // --- Pivot helpers ---
    void EnsurePivots()
    {
        if (_pivotCenter == null) _pivotCenter = FindOrCreate("__OriginPivot(Center)");
        if (_pivotLeft == null) _pivotLeft = FindOrCreate("__OriginPivot(Left)");
        if (_pivotRight == null) _pivotRight = FindOrCreate("__OriginPivot(Right)");
    }

    Transform FindOrCreate(string name)
    {
        var t = transform.Find(name);
        return t ? t : CreatePivot(name);
    }

    Transform CreatePivot(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, worldPositionStays: false);
        go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;
        return go.transform;
    }

    void SnapPivotToCollider(Transform pivot, Collider2D col)
    {
        if (!pivot || !col) return;
        var b = col.bounds;
        pivot.position = b.center;
        pivot.rotation = Quaternion.identity;
    }

    void UpdateAllPivotsFromColliders()
    {
        if (originCenterCollider) SnapPivotToCollider(_pivotCenter, originCenterCollider);
        if (originLeftCollider) SnapPivotToCollider(_pivotLeft, originLeftCollider);
        if (originRightCollider) SnapPivotToCollider(_pivotRight, originRightCollider);
    }

    void DrawPivotGizmo(Transform t, Color c)
    {
        if (!t) return;
        Gizmos.color = c; Gizmos.DrawWireSphere(t.position, 0.06f);
    }

    // --- 부트스트랩 ---
    void BootstrapLegacyDefaults()
    {
        // 기본 앵커 추가(중복 방지)
        AddDefaultAnchor("Center", attackOriginCenter);
        AddDefaultAnchor("Left", attackOriginLeft);
        AddDefaultAnchor("Right", attackOriginRight);

        // AttackDefs 비어 있으면 레거시 1회 셋업
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
}
