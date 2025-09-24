using UnityEngine;

[DisallowMultipleComponent]
public class CloneAnimEventReceiver : MonoBehaviour
{
    [Tooltip("��Ʈ�� �ִ� JangsanbeomClone(�Ǵ� �н� ��ũ��Ʈ)")]
    public JangsanbeomClone owner;

    // �ִϸ��̼� �̺�Ʈ���� �״�� ȣ��Ǵ� �޼����
    // �ִϸ��̼� Ŭ���� �̺�Ʈ �̸��� �� �� ���� �״�� ����ϼ���.
    public void OnClawHitFrame() { owner?.OnClawHitFrame(); }
    public void OnClawFakeHitFrame() { owner?.OnClawFakeHitFrame(); }
}
