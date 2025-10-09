using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Hit Shape 2D")]
public class HitShape2D : ScriptableObject
{
    // === 공통 ===
    public LayerMask layerMask = ~0; // Everything

    // 어떤 소스의 기하를 쓸지
    public enum GeometryMode { Kind, Prefab }
    [Header("Mode")]
    public GeometryMode mode = GeometryMode.Kind;

    // ----------------------------
    // KIND 모드 (기존 방식)
    // ----------------------------
    public enum Kind { Box, Capsule, Circle, Arc }
    [Header("Kind Mode")]
    public Kind kind = Kind.Box;

    public Vector2 kindOffset = Vector2.zero;
    public float kindAngleDeg = 0f;
    public bool kindSignedByFacing = true;

    // Box
    public Vector2 boxSize = new(1, 1);

    // Capsule
    public Vector2 capsuleSize = new(1, 1);
    public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal;

    // Circle
    public float circleRadius = 1.5f;

    // Arc(원호) — 런타임은 원으로 맞추고 각도로 필터링
    public float arcRadius = 1.5f;
    public float arcAngle = 90f;

    // ----------------------------
    // PREFAB 모드 (프리팹 크기/모양 기반)
    // ----------------------------
    public enum PrefabSource { SpriteBounds, ColliderBounds }
    [Header("Prefab Mode")]
    public PrefabSource prefabSource = PrefabSource.SpriteBounds;
    public GameObject prefab;
    public bool prefabIncludeChildren = true;

    public Vector2 prefabOffset = Vector2.zero; // 프리팹 모드 전용 오프셋
    public float prefabAngleDeg = 0f;
    public bool prefabSignedByFacing = true;

    [Tooltip("프리팹 바운즈 크기 보정")]
    public Vector2 prefabBoundsScale = Vector2.one;

    // =========================================================
    // =======  런타임 충돌(Overlap)  ==========================
    // =========================================================
    public void Overlap(Transform origin, bool facingRight, List<Collider2D> results)
    {
        if (!origin || results == null) return;

        if (mode == GeometryMode.Kind)
        {
            Vector2 center = ComposeCenter(origin, kindOffset, kindAngleDeg, kindSignedByFacing, facingRight, out float worldAngle);
            switch (kind)
            {
                case Kind.Box:
                    Add(Physics2D.OverlapBoxAll(center, boxSize, worldAngle, layerMask), results);
                    break;
                case Kind.Circle:
                    Add(Physics2D.OverlapCircleAll(center, circleRadius, layerMask), results);
                    break;
                case Kind.Capsule:
                    Add(Physics2D.OverlapCapsuleAll(center, capsuleSize, capsuleDirection, worldAngle, layerMask), results);
                    break;
                case Kind.Arc:
                    // 원으로 먼저 모으고 각도로 거르기
                    var all = Physics2D.OverlapCircleAll(center, arcRadius, layerMask);
                    Vector2 forward = Rot(Vector2.right, worldAngle);
                    foreach (var c in all)
                    {
                        if (!c) continue;
                        Vector2 v = (Vector2)c.bounds.center - center;
                        if (v.sqrMagnitude < 0.0001f) { results.Add(c); continue; }
                        float ang = Vector2.Angle(forward, v);
                        if (ang <= arcAngle * 0.5f) results.Add(c);
                    }
                    break;
            }
            return;
        }

        // === Prefab 모드: 프리팹 바운즈를 회전박스로 사용 ===
        if (!TryGetPrefabBounds(out Bounds b)) return;

        Vector2 size = Vector2.Scale(new Vector2(b.size.x, b.size.y), prefabBoundsScale);
        Vector2 centerP = ComposeCenter(origin, prefabOffset, prefabAngleDeg, prefabSignedByFacing, facingRight, out float worldAngP);

        Add(Physics2D.OverlapBoxAll(centerP, size, worldAngP, layerMask), results);
    }

    // =========================================================
    // =======  에디터 기즈모  =================================
    // =========================================================
    public void DrawGizmos(Transform origin, bool facingRight, Color fill, Color wire)
    {
        if (!origin) return;

        if (mode == GeometryMode.Kind)
        {
            Vector2 center = ComposeCenter(origin, kindOffset, kindAngleDeg, kindSignedByFacing, facingRight, out float worldAngle);
            switch (kind)
            {
                case Kind.Box: DrawBox(center, boxSize, worldAngle, fill, wire); break;
                case Kind.Circle: DrawCircle(center, circleRadius, fill, wire); break;
                case Kind.Capsule: DrawCapsuleApprox(center, capsuleSize, capsuleDirection, worldAngle, fill, wire); break;
                case Kind.Arc: DrawArc(center, arcRadius, arcAngle, worldAngle, wire); break;
            }
            return;
        }

        // Prefab 모드
        if (!TryGetPrefabBounds(out Bounds pb)) return;

        Vector2 sizeP = Vector2.Scale(new Vector2(pb.size.x, pb.size.y), prefabBoundsScale);
        Vector2 centerP = ComposeCenter(origin, prefabOffset, prefabAngleDeg, prefabSignedByFacing, facingRight, out float worldAngP);
        DrawBox(centerP, sizeP, worldAngP, fill, wire);

#if UNITY_EDITOR
        // prefabSource = ColliderBounds면 보조 윤곽선도 표시
        if (prefabSource == PrefabSource.ColliderBounds && prefab)
        {
            UnityEditor.Handles.color = wire;
            var cols = prefabIncludeChildren ? prefab.GetComponentsInChildren<Collider2D>(true)
                                             : prefab.GetComponents<Collider2D>();
            foreach (var c in cols)
            {
                if (!c) continue;

                if (c is BoxCollider2D bc)
                {
                    Vector2 pos2 = centerP + Rot(bc.offset, worldAngP);
                    Matrix4x4 prev = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(new Vector3(pos2.x, pos2.y, 0f), Quaternion.Euler(0, 0, worldAngP), Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(bc.size.x, bc.size.y, 0.01f));
                    Gizmos.matrix = prev;
                }
                else if (c is CircleCollider2D cc)
                {
                    Vector2 cpos2 = centerP + Rot(cc.offset, worldAngP);
                    UnityEditor.Handles.DrawWireDisc(new Vector3(cpos2.x, cpos2.y, 0f), Vector3.forward, cc.radius);
                }
                else if (c is CapsuleCollider2D cap)
                {
                    Vector2 capPos2 = centerP + Rot(cap.offset, worldAngP);
                    Matrix4x4 prev = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(new Vector3(capPos2.x, capPos2.y, 0f), Quaternion.Euler(0, 0, worldAngP), Vector3.one);
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
                            Vector2 a2 = centerP + Rot(pc.offset + path[j], worldAngP);
                            Vector2 b2 = centerP + Rot(pc.offset + path[(j + 1) % path.Length], worldAngP);
                            UnityEditor.Handles.DrawLine(new Vector3(a2.x, a2.y, 0f), new Vector3(b2.x, b2.y, 0f));
                        }
                    }
                }
            }
        }
#endif
    }

    // =========================================================
    // =============== 내부 유틸 =================================
    // =========================================================
    static void Add(Collider2D[] arr, List<Collider2D> outList)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++) if (arr[i]) outList.Add(arr[i]);
    }

    static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    Vector2 ComposeCenter(Transform origin, Vector2 offset, float angleDeg, bool signedByFacing, bool facingRight, out float worldAngleDeg)
    {
        float sign = (facingRight ? 1f : -1f);
        float ang = angleDeg * (signedByFacing ? sign : 1f);
        worldAngleDeg = ang;
        Vector2 off = offset * (signedByFacing ? sign : 1f);
        return (Vector2)origin.position + Rot(off, ang);
    }

    bool TryGetPrefabBounds(out Bounds b)
    {
        b = default;
        if (!prefab) return false;

        if (prefabSource == PrefabSource.SpriteBounds)
        {
            var srs = prefabIncludeChildren ? prefab.GetComponentsInChildren<SpriteRenderer>(true)
                                            : prefab.GetComponents<SpriteRenderer>();
            if (srs.Length == 0) return false;
            b = srs[0].bounds;
            for (int i = 1; i < srs.Length; i++) b.Encapsulate(srs[i].bounds);
            return true;
        }
        else
        {
            var cols = prefabIncludeChildren ? prefab.GetComponentsInChildren<Collider2D>(true)
                                             : prefab.GetComponents<Collider2D>();
            if (cols.Length == 0) return false;
            b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return true;
        }
    }

    // --- Gizmo helpers ---
    void DrawBox(Vector2 center, Vector2 size, float angleDeg, Color fill, Color wire)
    {
        var prev = Gizmos.matrix;
        var c3 = new Vector3(center.x, center.y, 0f);
        Gizmos.matrix = Matrix4x4.TRS(c3, Quaternion.Euler(0, 0, angleDeg), Vector3.one);
        Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f) * 1.001f);
        Gizmos.matrix = prev;
    }

    void DrawCircle(Vector2 center, float radius, Color fill, Color wire)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = wire;
        UnityEditor.Handles.DrawWireDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, radius);
#else
        Gizmos.color = wire;
        Gizmos.DrawWireSphere(new Vector3(center.x, center.y, 0f), radius);
#endif
    }

    void DrawCapsuleApprox(Vector2 center, Vector2 size, CapsuleDirection2D dir, float angleDeg, Color fill, Color wire)
    {
        // 간단하게 박스로 근사(미리보기 목적)
        DrawBox(center, size, angleDeg, fill, wire);
    }

    void DrawArc(Vector2 center, float radius, float angle, float worldAngleDeg, Color wire)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = wire;
        float start = worldAngleDeg - angle * 0.5f;
        Vector2 dir2 = Rot(Vector2.right, start);
        UnityEditor.Handles.DrawWireArc(
            new Vector3(center.x, center.y, 0f),
            Vector3.forward,
            new Vector3(dir2.x, dir2.y, 0f),
            angle,
            radius
        );
#endif
    }
}
