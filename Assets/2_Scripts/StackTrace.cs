using UnityEngine;
using System;

public class WhoDestroyedMe : MonoBehaviour
{
    void OnDestroy()
    {
#if UNITY_EDITOR
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!\n{Environment.StackTrace}", this);
        // 에디터에서만 브레이크포인트로 확인 가능
        Debug.Break();
#else
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!");
#endif
    }

    void OnDisable()
    {
        // 비활성화되거나 삭제될 때 로그
        Debug.LogWarning($"[WhoDestroyedMe] {name} got disabled (not destroyed).", this);
    }
}