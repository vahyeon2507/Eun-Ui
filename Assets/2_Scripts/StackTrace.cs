using UnityEngine;
using System;

public class WhoDestroyedMe : MonoBehaviour
{
    void OnDestroy()
    {
#if UNITY_EDITOR
        // 에디터에서만 상세 스택트레이스를 포함해 로그 남김 (플레이는 중단하지 않음)
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!\n{Environment.StackTrace}", this);
#else
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!");
#endif
    }

    void OnDisable()
    {
        // 비활성화(또는 파괴 직전) 시점 로그
        Debug.LogWarning($"[WhoDestroyedMe] {name} got disabled (not destroyed).", this);
    }
}
