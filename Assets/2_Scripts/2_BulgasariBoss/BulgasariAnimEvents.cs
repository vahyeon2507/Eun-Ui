//// BulgasariAnimEvents.cs  (Animator 달린 오브젝트에 부착)
//using UnityEngine;

//public class BulgasariAnimEvents : MonoBehaviour
//{
//    [Header("Hitboxes (자식 콜라이더에 붙인 스크립트)")]
//    public HitboxTrigger2D hitLeft;
//    public HitboxTrigger2D hitRight;
//    public HitboxTrigger2D hitChest;

//    [Header("옵션: 스윕 보강(손 궤적 따라가게)")]
//    public BulgasariFollowHit followLeft;
//    public BulgasariFollowHit followRight;

//    [Header("옵션: 원샷 어택 실행용(중앙 충돌 등)")]
//    public BulgasariAttackHooks hooks;           // 이미 씬에 있지?
//    public AttackDefinition2D defClapImpact;     // 중앙 원형 1회 타격 등

//    // === Animation Event로 부를 간단 함수들 ===
//    public void LeftOn(float dur) { if (hitLeft) hitLeft.Activate(dur); }
//    public void RightOn(float dur) { if (hitRight) hitRight.Activate(dur); }
//    public void BothHandsOn(float dur) { LeftOn(dur); RightOn(dur); }
//    public void ChestOn(float dur) { if (hitChest) hitChest.Activate(dur); }

//    // 손 궤적을 따라가는 지속 판정(필요한 클립에서만 사용)
//    public void LeftFollow(float dur) { if (followLeft) followLeft.StartFollowHit_Segment(dur); }
//    public void RightFollow(float dur) { if (followRight) followRight.StartFollowHit_Segment(dur); }

//    // 중앙 ‘딱’ 한 번 때리기(원형/박스 등 AttackDefinition 실행)
//    public void ClapImpact() { if (hooks && defClapImpact) hooks.Perform(defClapImpact); }
//}
