using UnityEngine;

public class PlayerHitShake : MonoBehaviour
{
    [Header("Link to CameraSimple2D")]
    public CameraSimple2D target;      // 메인 카메라에 붙은 CameraSimple2D
    [Header("Defaults (fallback)")]
    public float amplitude = 0.8f;
    public float duration = 0.15f;
    public float frequency = 22f;

    void Reset()
    {
        if (!target) target = FindObjectOfType<CameraSimple2D>();
    }

    public void Shake()
    {
        if (!target) target = FindObjectOfType<CameraSimple2D>();
        if (target) target.Shake(amplitude, duration, frequency);
        else Debug.LogWarning("[PlayerHitShake] CameraSimple2D not found.");
    }
}
