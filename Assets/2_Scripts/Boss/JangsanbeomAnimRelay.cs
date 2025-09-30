// JangsanbeomAnimRelay.cs
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomAnimRelay : MonoBehaviour
{
    [SerializeField] MonoBehaviour target; // Boss 또는 Clone을 드래그해 둠

    void Awake() { if (!target) target = GetComponent<MonoBehaviour>(); }

    public void OnClawHitFrame() => target.SendMessage("EVT_ClawHit", SendMessageOptions.DontRequireReceiver);
    public void OnClawFakeHitFrame() => target.SendMessage("EVT_ClawFakeHit", SendMessageOptions.DontRequireReceiver);
    public void OnDashHitFrame() => target.SendMessage("EVT_DashHit", SendMessageOptions.DontRequireReceiver);

    public void Anim_DashStart() => target.SendMessage("EVT_DashStart", SendMessageOptions.DontRequireReceiver);
    public void Anim_DashEnd() => target.SendMessage("EVT_DashEnd", SendMessageOptions.DontRequireReceiver);
    public void Anim_SetMoveLock(int v) => target.SendMessage("EVT_SetMoveLock", v != 0, SendMessageOptions.DontRequireReceiver);
    public void Anim_SetFlipLock(int v) => target.SendMessage("EVT_SetFlipLock", v != 0, SendMessageOptions.DontRequireReceiver);
    public void Anim_InvulnOn() => target.SendMessage("EVT_Invuln", true, SendMessageOptions.DontRequireReceiver);
    public void Anim_InvulnOff() => target.SendMessage("EVT_Invuln", false, SendMessageOptions.DontRequireReceiver);
    public void Anim_MoveBy(string s) => target.SendMessage("EVT_MoveBy", s, SendMessageOptions.DontRequireReceiver);

    public void OnPhase2IntroStart() => target.SendMessage("EVT_Phase2Intro", true, SendMessageOptions.DontRequireReceiver);
    public void OnPhase2IntroEnd() => target.SendMessage("EVT_Phase2Intro", false, SendMessageOptions.DontRequireReceiver);
}
