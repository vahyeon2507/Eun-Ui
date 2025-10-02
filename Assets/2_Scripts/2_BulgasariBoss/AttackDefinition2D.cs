using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

[CreateAssetMenu(menuName = "Combat/AttackDefinition2D")]
public class AttackDefinition2D : ScriptableObject
{
    public string attackName = "Punch";
    public int damage = 2;
    public List<HitShape2D> shapes = new(); // 한 공격에 여러 히트영역 가능(양팔 등)
}

public static class HitExec2D
{
    static readonly List<Collider2D> _buf = new(32);

    // ===== Runtime =====
    public static void ExecuteAttack(AttackDefinition2D def, Transform origin, bool facingRight, System.Action<Collider2D> onHit)
    {
        if (def == null) return;
        _buf.Clear();
        HashSet<Collider2D> seen = new(); // 중복 제거

        foreach (var shape in def.shapes)
        {
            if (shape == null) continue;
            var tmp = new List<Collider2D>(16);
            shape.Overlap(origin, facingRight, tmp);
            for (int i = 0; i < tmp.Count; i++)
            {
                var c = tmp[i];
                if (!c || seen.Contains(c)) continue;
                seen.Add(c); _buf.Add(c);
            }
        }

        for (int i = 0; i < _buf.Count; i++) onHit?.Invoke(_buf[i]);
    }

    // ===== Editor Gizmo =====
    public static void DrawGizmos(AttackDefinition2D def, Transform origin, bool facingRight, Color fill, Color wire)
        => DrawGizmos(def, origin, facingRight, fill, wire, -1);

    /// <param name="onlyShapeIndex">>=0면 그 인덱스 shape만 그리기</param>
    public static void DrawGizmos(AttackDefinition2D def, Transform origin, bool facingRight, Color fill, Color wire, int onlyShapeIndex)
    {
#if UNITY_EDITOR
        if (def == null || origin == null) return;

        for (int i = 0; i < def.shapes.Count; i++)
        {
            var s = def.shapes[i];
            if (s == null) continue;
            if (onlyShapeIndex >= 0 && i != onlyShapeIndex) continue;

            // 프리팹 기반 미리보기 → 그리고 나서 기본모양 숨김 여부에 따라 반환
            if (TryDrawPrefabLikeGizmo(s, origin, facingRight, fill, wire, out bool hideBase))
            {
                if (hideBase) continue; // 기본모양 숨김
            }

            s.DrawGizmos(origin, facingRight, fill, wire); // 기본모양
        }
#endif
    }

#if UNITY_EDITOR
    // === Prefab 기반 미리보기 지원 ===
    // HitShape2D에 아래 필드가 "있으면" 자동으로 사용(없어도 컴파일 OK)
    //  - enum kind: "PrefabSpriteBounds" / "PrefabColliderOutline" (선택)
    //  - enum gizmoOverride: "None" / "PrefabSpriteBounds" / "PrefabColliderOutline"  (신규 권장)
    //  - bool gizmoHideRuntimeShape  (신규 권장)
    //  - GameObject prefabForGizmo; bool prefabIncludeChildren; Vector2 prefabBoundsScale
    //  - Vector2 offset; float angleDeg; bool signedByFacing;
    static bool TryDrawPrefabLikeGizmo(
        HitShape2D shape, Transform origin, bool facingRight, Color fill, Color wire, out bool hideBase)
    {
        hideBase = false;

        var t = shape.GetType();

        // 1) 무엇을 그릴지 결정: kind 또는 gizmoOverride 둘 중 하나라도 prefab 모드면 OK
        string kindName = GetEnumName(t, shape, "kind");                    // ex) "Capsule", "PrefabSpriteBounds" ...
        string overrideName = GetEnumName(t, shape, "gizmoOverride");       // ex) "None", "PrefabSpriteBounds" ...
        bool fromKind = (kindName == "PrefabSpriteBounds" || kindName == "PrefabColliderOutline");
        bool fromOverride = (overrideName == "PrefabSpriteBounds" || overrideName == "PrefabColliderOutline");
        if (!fromKind && !fromOverride) return false;

        bool useSpriteBounds = (kindName == "PrefabSpriteBounds") || (overrideName == "PrefabSpriteBounds");
        bool useColliderOutline = (kindName == "PrefabColliderOutline") || (overrideName == "PrefabColliderOutline");

        // 2) 숨김 옵션
        hideBase = GetField(t, shape, "gizmoHideRuntimeShape", true); // 기본값: 숨김

        // 3) 좌표 파라미터
        Vector2 offset = GetField(t, shape, "offset", Vector2.zero);
        float angleDeg = GetField(t, shape, "angleDeg", 0f);
        bool signed = GetField(t, shape, "signedByFacing", true);
        float sign = (facingRight ? 1f : -1f);
        Vector3 basePos = origin.position + (Vector3)Rotate(offset * (signed ? sign : 1f), angleDeg * (signed ? sign : 1f));
        Quaternion rot = Quaternion.Euler(0, 0, angleDeg * (signed ? sign : 1f));

        // 4) 프리팹 파라미터
        GameObject prefab = GetField<GameObject>(t, shape, "prefabForGizmo", null);
        if (prefab == null) return false; // 프리팹이 비어있으면 그냥 패스
        bool includeChildren = GetField(t, shape, "prefabIncludeChildren", true);
        Vector2 boundsScale = GetField(t, shape, "prefabBoundsScale", Vector2.one);

        if (useSpriteBounds)
        {
            var srs = includeChildren ? prefab.GetComponentsInChildren<SpriteRenderer>(true)
                                      : prefab.GetComponents<SpriteRenderer>();
            if (srs.Length == 0) return false;

            var b = srs[0].bounds;
            for (int i = 1; i < srs.Length; i++) b.Encapsulate(srs[i].bounds);

            Vector2 sz = new Vector2(b.size.x, b.size.y);
            sz = Vector2.Scale(sz, boundsScale);

            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(basePos, rot, Vector3.one);
            Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, new Vector3(sz.x, sz.y, 0.01f));
            Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, new Vector3(sz.x, sz.y, 0.01f) * 1.001f);
            Gizmos.matrix = prev;
            return true;
        }
        else if (useColliderOutline)
        {
            var cols = includeChildren ? prefab.GetComponentsInChildren<Collider2D>(true)
                                       : prefab.GetComponents<Collider2D>();
            if (cols.Length == 0) return false;

            UnityEditor.Handles.color = wire;
            foreach (var c in cols)
            {
                if (!c) continue;

                if (c is BoxCollider2D bc)
                {
                    Vector3 pos = basePos + (Vector3)(rot * bc.offset);
                    Matrix4x4 prev = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                    Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, new Vector3(bc.size.x, bc.size.y, 0.01f));
                    Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, new Vector3(bc.size.x, bc.size.y, 0.01f) * 1.001f);
                    Gizmos.matrix = prev;
                }
                else if (c is CircleCollider2D cc)
                {
                    Vector3 pos = basePos + (Vector3)(rot * cc.offset);
                    UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, cc.radius);
                }
                else if (c is CapsuleCollider2D cap)
                {
                    Vector3 pos = basePos + (Vector3)(rot * cap.offset);
                    Matrix4x4 prev = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                    Gizmos.color = wire;
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(cap.size.x, cap.size.y, 0.01f)); // 간단 근사
                    Gizmos.matrix = prev;
                }
                else if (c is PolygonCollider2D pc)
                {
                    for (int i = 0; i < pc.pathCount; i++)
                    {
                        var path = pc.GetPath(i);
                        for (int j = 0; j < path.Length; j++)
                        {
                            Vector3 a = basePos + (Vector3)(rot * (pc.offset + path[j]));
                            Vector3 b2 = basePos + (Vector3)(rot * (pc.offset + path[(j + 1) % path.Length]));
                            UnityEditor.Handles.DrawLine(a, b2);
                        }
                    }
                }
            }
            return true;
        }

        return false;
    }

    // 리플렉션 유틸
    static T GetField<T>(System.Type t, object obj, string name, T fallback)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return fallback;
        var v = f.GetValue(obj);
        if (v is T tv) return tv;
        return fallback;
    }

    static string GetEnumName(System.Type t, object obj, string name)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return null;
        var v = f.GetValue(obj);
        return v != null ? v.ToString() : null;
    }
#endif

    static Vector2 Rotate(Vector2 v, float angDeg)
    {
        float rad = angDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
