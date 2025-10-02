using System;
using System.Linq;
using UnityEngine;

[Serializable]
public struct NamedHitbox
{
    public string key;            // "Left","Right","Center","Head"...
    public HitboxTrigger2D hit;   // 지속 트리거 히트박스(자식 콜라이더에 붙은 스크립트)
    public Transform origin;      // 원샷 기준(없으면 hit.transform 사용)
}

public class BulgasariAnimEvents : MonoBehaviour
{
    [Header("Anchors / Hitboxes")]
    public NamedHitbox[] map;

    [Header("One-shot Attacks")]
    public BulgasariAttackHooks hooks;
    public AttackDefinition2D[] defs;   // 0,1,2...

    NamedHitbox? Find(string key) => map.FirstOrDefault(m => m.key == key);

    // === Animation Events에서 호출할 것들 ===

    // 지속 판정 스위치: 해당 앵커 히트박스를 dur초 동안 켠다
    public void OnAt(string key, float dur)
    {
        var m = Find(key); if (m == null || m.Value.hit == null) return;
        m.Value.hit.Activate(dur);
    }

    // 원샷: AttackDefinition 인덱스 실행 (originOverride는 앵커)
    public void Impact(int defIndex, string key = "Center")
    {
        if (!hooks || defs == null || defIndex < 0 || defIndex >= defs.Length) return;

        var m = Find(key);
        Transform origin = (m != null && (m.Value.origin || m.Value.hit))
                         ? (m.Value.origin ? m.Value.origin : m.Value.hit.transform)
                         : transform;

        // ★ hooks에 public Perform가 있으면 이 줄 사용
        hooks.Perform(defs[defIndex], origin);

        // ★ 만약 Perform를 안 만들었다면, 너희 훅의 공개 메서드로 교체:
        // hooks.Anim_ATK_Index(defIndex); // 이런 식으로
    }
}
