using UnityEngine;

[DisallowMultipleComponent]
public class CloneAnimEventReceiver : MonoBehaviour
{
    [Tooltip("루트에 붙은 JangsanbeomClone(분신 스크립트)")]
    public JangsanbeomClone owner;

    // 애니메이션 이벤트에서 호출할 메서드 이름과 정확히 일치해야 함
    public void OnClawHitFrame() { owner?.OnCloneHitFrame(); }
    public void OnClawFakeHitFrame() { owner?.OnCloneFakeHitFrame(); }
}
