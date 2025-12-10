using UnityEngine;

public class BossAnimEventRelay : MonoBehaviour
{
    public BossDueoksiniController controller;

    public void AnimEvent_FireSimpleProjectile() { controller?.AnimEvent_FireSimpleProjectile(); }
    public void AnimEvent_PrepReady() { controller?.AnimEvent_PrepReady(); }
    public void AnimEvent_GenericHitOn() { controller?.AnimEvent_GenericHitOn(); }
    public void AnimEvent_GenericHitOff() { controller?.AnimEvent_GenericHitOff(); }
    public void AnimEvent_SlamHitOn() { controller?.AnimEvent_SlamHitOn(); }
    public void AnimEvent_SlamHitOff() { controller?.AnimEvent_SlamHitOff(); }

    // ▼ 여기 추가
    public void AnimEvent_MoveStart(float deltaX) { controller?.AnimEvent_MoveStart(deltaX); }
    public void AnimEvent_MoveStop() { controller?.AnimEvent_MoveStop(); }

    public void AnimEvent_SimpleDamage() { controller?.AnimEvent_SimpleDamage(); }
    public void AnimEvent_SimpleDamageInt(int damage) { controller?.AnimEvent_SimpleDamageInt(damage); }

}
