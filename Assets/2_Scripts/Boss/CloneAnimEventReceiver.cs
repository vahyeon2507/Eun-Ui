using UnityEngine;

[DisallowMultipleComponent]
public class CloneAnimEventReceiver : MonoBehaviour
{
    [Tooltip("루트에 있는 JangsanbeomClone(또는 분신 스크립트)")]
    public JangsanbeomClone owner;

    // 애니메이션 이벤트에서 그대로 호출되는 메서드들
    // 애니메이션 클립의 이벤트 이름은 이 두 개를 그대로 사용하세요.
    public void OnClawHitFrame() { owner?.OnClawHitFrame(); }
    public void OnClawFakeHitFrame() { owner?.OnClawFakeHitFrame(); }
}
