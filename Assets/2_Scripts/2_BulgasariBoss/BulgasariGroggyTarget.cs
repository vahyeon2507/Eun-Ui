// BulgasariGroggyTarget.cs
using UnityEngine;

[DisallowMultipleComponent]
public class BulgasariGroggyTarget : MonoBehaviour
{
    public BulgasariBoss boss;
    [Tooltip("이 값을 >0으로 설정하면 그로기 시간을 이 값으로 강제")]
    public float groggyDurationOverride = -1f;
    [Tooltip("한 프레임에 여러 번 맞아도 1회만 처리")]
    public bool oneShotPerFrame = true;

    int _lastFrame = -1;

    public void NotifyParrySpecialHit(PlayerController who)
    {
        if (!boss) boss = GetComponentInParent<BulgasariBoss>();
        if (!boss) return;

        if (oneShotPerFrame && _lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;

        boss.TriggerGroggyFromSpecial(groggyDurationOverride);
    }
}
