using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHitShake : MonoBehaviour
{
    [Header("Link to CameraSimple2D")]
    public CameraSimple2D target;

    [Header("Defaults (fallback)")]
    public float amplitude = 0.8f;
    public float duration = 0.15f;
    public float frequency = 22f;

    [Tooltip("있으면 자동 호출(없어도 됨). 타입은 CinemachineImpulseSource")]
    public Component cinemachineImpulseSource;

    void Reset()
    {
        if (target == null) target = FindObjectOfType<CameraSimple2D>();
        if (cinemachineImpulseSource == null)
        {
            var c = GetComponent("CinemachineImpulseSource");
            if (c) cinemachineImpulseSource = c as Component;
        }
    }

    void Awake()
    {
        if (target == null) target = FindObjectOfType<CameraSimple2D>();
    }

    public void Shake(float? amp = null, float? dur = null, float? freq = null)
    {
        float a = amp ?? amplitude;
        float d = dur ?? duration;
        float f = freq ?? frequency;

        // 1) 내장 카메라 흔들림(무조건 보장)
        if (target == null) target = FindObjectOfType<CameraSimple2D>();
        if (target != null) target.Shake(a, d, f);

        // 2) (선택) Cinemachine Impulse도 있으면 같이 발사
        if (cinemachineImpulseSource != null)
        {
            var m = cinemachineImpulseSource.GetType().GetMethod("GenerateImpulse");
            if (m != null) m.Invoke(cinemachineImpulseSource, null);
        }
    }

    // 에디터 빠른 테스트용
    [ContextMenu("Test Shake")]
    void TestShake() => Shake();
}
