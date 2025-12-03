// BossAnimEventRelay.cs  (GfxRoot에 붙이기)
using UnityEngine;

public class BossAnimEventRelay : MonoBehaviour
{
    public BossDueoksiniController controller;

    // 애니메이션 이벤트에서 이 이름들을 호출
    public void AnimEvent_FireSimpleProjectile() { controller?.AnimEvent_FireSimpleProjectile(); }
    // 필요하면 추가
    public void AnimEvent_PrepReady() { controller?.AnimEvent_PrepReady(); }
    public void AnimEvent_GenericHitOn() { controller?.AnimEvent_GenericHitOn(); }
    public void AnimEvent_GenericHitOff() { controller?.AnimEvent_GenericHitOff(); }
    public void AnimEvent_SlamHitOn() { controller?.AnimEvent_SlamHitOn(); }
    public void AnimEvent_SlamHitOff() { controller?.AnimEvent_SlamHitOff(); }
}
