// BulgasariAttackHooks.cs
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BulgasariAttackHooks : MonoBehaviour
{
    public BulgasariBoss boss;
    public Transform attackOriginCenter;   // 기본은 가슴(없으면 transform)
    public Transform attackOriginLeft;     // (선택) 왼팔 본
    public Transform attackOriginRight;    // (선택) 오른팔 본

    [Header("Attack Definitions")]
    public AttackDefinition2D defDoublePunch;
    public AttackDefinition2D defSweep;
    public AttackDefinition2D defSlam;

    // === 공통 실행 ===
    void Exec(AttackDefinition2D def, Transform originOverride = null)
    {
        if (!def) return;
        var origin = originOverride ? originOverride : (attackOriginCenter ? attackOriginCenter : transform);
        bool facingRight = transform.localScale.x >= 0f;

        HitExec2D.ExecuteAttack(def, origin, facingRight, col =>
        {
            // 패링 소비 우선
            var pc = col.GetComponent<PlayerController>() ??
                     col.GetComponentInParent<PlayerController>() ??
                     col.GetComponentInChildren<PlayerController>();
            if (pc != null && pc.IsParrying && pc.ConsumeHitboxIfParrying(col)) return;

            // 대미지 전달
            var dmg = col.GetComponent<IDamageable>() ??
                      col.GetComponentInParent<IDamageable>() ??
                      col.GetComponentInChildren<IDamageable>();
            if (dmg != null) { dmg.TakeDamage(def.damage); }
        });
    }

    // === 애니 이벤트로 호출할 함수들 ===
    public void Anim_ATK_DoublePunch() { Exec(defDoublePunch); }
    public void Anim_ATK_Sweep() { Exec(defSweep); }
    public void Anim_ATK_Slam() { Exec(defSlam); }

    // 팔 따로 쓰고 싶으면 이런 식으로
    public void Anim_ATK_LeftPunch()
    {
        if (!defDoublePunch) return;
        // 왼팔만 치고 싶다면: 히트모양을 '왼쪽 하나'만 가진 AttackDefinition을 따로 만들어 두거나,
        // 아래처럼 originOverride를 왼팔로 넘기는 전용 AttackDefinition을 사용해.
        Exec(defDoublePunch, attackOriginLeft);
    }
    public void Anim_ATK_RightPunch() { Exec(defDoublePunch, attackOriginRight); }

    // 에디터에서 범위 미리보기
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
