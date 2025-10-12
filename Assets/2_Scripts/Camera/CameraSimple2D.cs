using System.Collections;
using UnityEngine;

/// <summary>
/// vCam 없이 쓰는 초간단 2D 카메라 컨트롤러:
/// - Target 따라가기(감속)
/// - BoxCollider2D로 영역 클램프
/// - 간단 흔들림(Shake)
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraSimple2D : MonoBehaviour
{
    public static CameraSimple2D Inst { get; private set; }

    [Header("Follow")]
    public Transform followTarget;
    [Range(0f, 1f)] public float followDamping = 0.12f;   // 0=즉시, 0.1~0.2 추천
    public Vector2 followOffset;                           // 화면 내 오프셋(월드 단위)

    [Header("Clamp (optional)")]
    public BoxCollider2D clampBounds;                     // 없으면 미적용

    [Header("Shake Defaults")]
    public float defaultAmplitude = 0.8f;                  // 흔들 세기(월드 단위)
    public float defaultDuration = 0.15f;                 // 지속시간
    public float defaultFrequency = 22f;                   // 진동 속도

    Camera _cam;
    Vector3 _vel;                // SmoothDamp용
    Vector3 _shakeOffset;        // 현재 흔들림 오프셋
    Coroutine _shakeCo;

    void Awake()
    {
        Inst = this;
        _cam = GetComponent<Camera>();
        if (!_cam.orthographic) _cam.orthographic = true; // 2D 기본값
    }

    void LateUpdate()
    {
        if (!followTarget) return;

        // 1) 타겟 위치 + 오프셋
        Vector3 target = followTarget.position + (Vector3)followOffset;
        target.z = transform.position.z; // 카메라는 Z 고정

        // 2) 경계 클램프(있을 때만)
        if (clampBounds) target = ClampToBounds(target);

        // 3) 감속 이동 + 흔들림 오프셋 적용
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, target, ref _vel, followDamping);
        transform.position = smoothed + _shakeOffset;
    }

    Vector3 ClampToBounds(Vector3 desired)
    {
        Bounds b = clampBounds.bounds;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        desired.x = Mathf.Clamp(desired.x, minX, maxX);
        desired.y = Mathf.Clamp(desired.y, minY, maxY);
        return desired;
    }

    // ───────────── 흔들림 API ─────────────
    public void Shake() => Shake(defaultAmplitude, defaultDuration, defaultFrequency);

    public void Shake(float amplitude, float duration) => Shake(amplitude, duration, defaultFrequency);

    public void Shake(float amplitude, float duration, float frequency)
    {
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(ShakeRoutine(amplitude, duration, frequency));
    }

    IEnumerator ShakeRoutine(float amp, float dur, float freq)
    {
        _shakeOffset = Vector3.zero;
        float t = 0f;

        // 진동: 프레임마다 무작위 방향으로 작은 오프셋(감쇠)
        while (t < dur)
        {
            float decay = 1f - (t / dur); // 후반부로 갈수록 약해짐
            float step = amp * decay;

            // 화면이 너무 거칠게 튀지 않게 부드러운 노이즈
            float angle = Random.value * Mathf.PI * 2f;
            float dist = Mathf.Sin(t * freq) * step; // 주기성 + 감쇠
            _shakeOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * dist;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        _shakeOffset = Vector3.zero;
        _shakeCo = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!clampBounds || !_cam) return;
        // 현재 카메라 뷰 크기 표시(클램프 확인용)
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;
        Gizmos.color = new Color(0, 1, 0, 0.15f);
        Gizmos.DrawCube(new Vector3(transform.position.x, transform.position.y, 0),
                        new Vector3(halfW * 2f, halfH * 2f, 0.01f));
    }
#endif
}
