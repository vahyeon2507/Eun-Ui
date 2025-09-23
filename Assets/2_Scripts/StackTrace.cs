using UnityEngine;
using System;

public class WhoDestroyedMe : MonoBehaviour
{
    void OnDestroy()
    {
#if UNITY_EDITOR
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!\n{Environment.StackTrace}", this);
        // 에디터 일시정지로 콜스택 확인 쉽게
        Debug.Break();
#else
        Debug.LogError($"[WhoDestroyedMe] {name} is being Destroyed!");
#endif
    }

    void OnDisable()
    {
        // 비활성화만 되는 경우도 로깅
        Debug.LogWarning($"[WhoDestroyedMe] {name} got disabled (not destroyed).", this);
    }
}
