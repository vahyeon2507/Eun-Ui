using System.Collections.Generic;
using UnityEngine;

public enum HitShapeKind { Box, Circle, Capsule, ArcSector }

[CreateAssetMenu(menuName = "Combat/HitShape2D")]
public class HitShape2D : ScriptableObject
{
    public HitShapeKind kind = HitShapeKind.Box;

    [Header("Common")]
    public Vector2 offset = new Vector2(1.5f, 0f);
    [Tooltip("로컬 각도(도). facingRight에 따라 부호 반영 옵션")]
    public float angleDeg = 0f;
    public bool signedByFacing = true;

    [Header("Box / Capsule")]
    public Vector2 size = new Vector2(3f, 1.2f);
    public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal;

    [Header("Circle / Arc")]
    public float radius = 1.5f;
    [Tooltip("부채꼴 각도(도)")]
    public float arcAngle = 90f;

    [Header("Filter")]
    public LayerMask layerMask = ~0;

    // === 실행 ===
    public int Overlap(Transform origin, bool facingRight, List<Collider2D> resultsBuffer)
    {
        resultsBuffer.Clear();

        float sign = signedByFacing ? (facingRight ? 1f : -1f) : 1f;
        Vector2 worldCenter = (Vector2)origin.position + Rotate(offset * new Vector2(sign, 1f), angleDeg * sign);
        float worldAngle = angleDeg * sign;

        switch (kind)
        {
            case HitShapeKind.Box:
                {
                    var arr = Physics2D.OverlapBoxAll(worldCenter, size, worldAngle, layerMask);
                    resultsBuffer.AddRange(arr);
                    return resultsBuffer.Count;
                }
            case HitShapeKind.Circle:
                {
                    var arr = Physics2D.OverlapCircleAll(worldCenter, radius, layerMask);
                    resultsBuffer.AddRange(arr);
                    return resultsBuffer.Count;
                }
            case HitShapeKind.Capsule:
                {
#if UNITY_2018_2_OR_NEWER
                    var arr = Physics2D.OverlapCapsuleAll(worldCenter, size, capsuleDirection, worldAngle, layerMask);
                    resultsBuffer.AddRange(arr);
                    return resultsBuffer.Count;
#else
                // 폴백: 박스로 근사
                var arr = Physics2D.OverlapBoxAll(worldCenter, size, worldAngle, layerMask);
                resultsBuffer.AddRange(arr);
                return resultsBuffer.Count;
#endif
                }
            case HitShapeKind.ArcSector:
                {
                    var arr = Physics2D.OverlapCircleAll(worldCenter, radius, layerMask);
                    Vector2 forward = AngleToDir(worldAngle);
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var col = arr[i]; if (!col) continue;
                        Vector2 p = col.ClosestPoint(worldCenter);
                        Vector2 dir = (p - worldCenter).normalized;
                        float ang = Vector2.Angle(forward, dir);
                        if (ang <= arcAngle * 0.5f) resultsBuffer.Add(col);
                    }
                    return resultsBuffer.Count;
                }
        }
        return 0;
    }

    // === 기즈모 ===
    public void DrawGizmos(Transform origin, bool facingRight, Color fill, Color wire)
    {
        float sign = signedByFacing ? (facingRight ? 1f : -1f) : 1f;
        Vector2 worldCenter = (Vector2)origin.position + Rotate(offset * new Vector2(sign, 1f), angleDeg * sign);
        float worldAngle = angleDeg * sign;

        var prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(new Vector3(worldCenter.x, worldCenter.y, 0f), Quaternion.Euler(0, 0, worldAngle), Vector3.one);

        if (kind == HitShapeKind.Box || kind == HitShapeKind.Capsule)
        {
            Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
            Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        }
        else if (kind == HitShapeKind.Circle)
        {
            Gizmos.color = wire; DrawWireCircle(Vector3.zero, radius);
        }
        else if (kind == HitShapeKind.ArcSector)
        {
            Gizmos.color = wire; DrawWireArc(Vector3.zero, radius, arcAngle);
        }

        Gizmos.matrix = prev;
    }

    // --- helpers ---
    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad; float c = Mathf.Cos(r); float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
    static Vector2 AngleToDir(float deg)
    {
        float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }
    static void DrawWireCircle(Vector3 c, float r, int seg = 24)
    {
        Vector3 prev = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            Gizmos.DrawLine(prev, p); prev = p;
        }
    }
    static void DrawWireArc(Vector3 c, float r, float deg, int seg = 16)
    {
        float half = deg * 0.5f;
        float start = -half * Mathf.Deg2Rad; float end = half * Mathf.Deg2Rad;
        Vector3 prev = c + new Vector3(Mathf.Cos(start) * r, Mathf.Sin(start) * r, 0);
        for (int i = 1; i <= seg; i++)
        {
            float t = Mathf.Lerp(start, end, i / (float)seg);
            Vector3 p = c + new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0);
            Gizmos.DrawLine(prev, p); prev = p;
        }
        Gizmos.DrawLine(c, c + new Vector3(Mathf.Cos(start) * r, Mathf.Sin(start) * r, 0));
        Gizmos.DrawLine(c, c + new Vector3(Mathf.Cos(end) * r, Mathf.Sin(end) * r, 0));
    }
}
