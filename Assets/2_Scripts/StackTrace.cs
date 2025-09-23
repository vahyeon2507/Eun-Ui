using UnityEngine;
using System;

public class WhoDestroyedMe : MonoBehaviour
{
    void OnDestroy()
    {
#if UNITY_EDITOR
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!\n{Environment.StackTrace}", this);
        // ������ �Ͻ������� �ݽ��� Ȯ�� ����
        Debug.Break();
#else
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!");
#endif
    }

    void OnDisable()
    {
        // ��Ȱ��ȭ�� �Ǵ� ��쵵 �α�
        Debug.LogWarning($"[WhoDestroyedMe] {name} got disabled (not destroyed).", this);
    }
}
