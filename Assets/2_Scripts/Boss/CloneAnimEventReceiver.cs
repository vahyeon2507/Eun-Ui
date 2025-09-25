using UnityEngine;

[DisallowMultipleComponent]
public class CloneAnimEventReceiver : MonoBehaviour
{
    [Tooltip("��Ʈ�� ���� JangsanbeomClone(�н� ��ũ��Ʈ)")]
    public JangsanbeomClone owner;

    // �ִϸ��̼� �̺�Ʈ���� ȣ���� �޼��� �̸��� ��Ȯ�� ��ġ�ؾ� ��
    public void OnClawHitFrame() { owner?.OnCloneHitFrame(); }
    public void OnClawFakeHitFrame() { owner?.OnCloneFakeHitFrame(); }
}
