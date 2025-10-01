using System.Collections.Generic;
using UnityEngine;

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

    public static void DrawGizmos(AttackDefinition2D def, Transform origin, bool facingRight, Color fill, Color wire)
    {
        if (def == null) return;
        foreach (var s in def.shapes) if (s != null) s.DrawGizmos(origin, facingRight, fill, wire);
    }
}
