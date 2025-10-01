//// BulgasariAnimEvents.cs  (Animator �޸� ������Ʈ�� ����)
//using UnityEngine;

//public class BulgasariAnimEvents : MonoBehaviour
//{
//    [Header("Hitboxes (�ڽ� �ݶ��̴��� ���� ��ũ��Ʈ)")]
//    public HitboxTrigger2D hitLeft;
//    public HitboxTrigger2D hitRight;
//    public HitboxTrigger2D hitChest;

//    [Header("�ɼ�: ���� ����(�� ���� ���󰡰�)")]
//    public BulgasariFollowHit followLeft;
//    public BulgasariFollowHit followRight;

//    [Header("�ɼ�: ���� ���� �����(�߾� �浹 ��)")]
//    public BulgasariAttackHooks hooks;           // �̹� ���� ����?
//    public AttackDefinition2D defClapImpact;     // �߾� ���� 1ȸ Ÿ�� ��

//    // === Animation Event�� �θ� ���� �Լ��� ===
//    public void LeftOn(float dur) { if (hitLeft) hitLeft.Activate(dur); }
//    public void RightOn(float dur) { if (hitRight) hitRight.Activate(dur); }
//    public void BothHandsOn(float dur) { LeftOn(dur); RightOn(dur); }
//    public void ChestOn(float dur) { if (hitChest) hitChest.Activate(dur); }

//    // �� ������ ���󰡴� ���� ����(�ʿ��� Ŭ�������� ���)
//    public void LeftFollow(float dur) { if (followLeft) followLeft.StartFollowHit_Segment(dur); }
//    public void RightFollow(float dur) { if (followRight) followRight.StartFollowHit_Segment(dur); }

//    // �߾� ������ �� �� ������(����/�ڽ� �� AttackDefinition ����)
//    public void ClapImpact() { if (hooks && defClapImpact) hooks.Perform(defClapImpact); }
//}
