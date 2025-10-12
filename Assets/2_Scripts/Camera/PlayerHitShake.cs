using Unity.Cinemachine;
using UnityEngine;
public class PlayerHitShake : MonoBehaviour
{
    [SerializeField] CinemachineImpulseSource softSource;

    public void Shake(float scale = 1f)
    {
        if (!softSource) return;

        // v3: Default Velocity의 크기로 세기 결정
        // 기본값을 (0, 1.2, 0)로 두고, 배율만 주고 싶으면 아래처럼:
        softSource.GenerateImpulse();             // 가장 안전 (Default Velocity 사용)

        // 더 세게 하고 싶으면 다른 소스(세기 큰 Default Velocity)를 따로 하나 더 두는 편이 깔끔.
    }
}
