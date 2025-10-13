using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraSimple2D : MonoBehaviour
{
    [Header("Follow")]
    public Transform followTarget;          // Player의 CameraTarget 같은 빈 오브젝트
    [Range(0f, 1f)] public float followDamping = 0.12f;
    public Vector2 followOffset;

    [Header("Clamp (optional)")]
    public BoxCollider2D clampBounds;       // 씬에 CamBoundary(BoxCollider2D, Static, IsTrigger 꺼짐)

    [Header("Shake Defaults")]
    public float defaultAmplitude = 0.8f;
    public float defaultDuration  = 0.15f;
    public float defaultFrequency = 22f;

    Vector3 _vel;               // SmoothDamp용
    Vector3 _shakeOffset;       // 흔들림 오프셋
    Coroutine _shakeCo;
    Camera _cam;

    void Awake() { _cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        if (!followTarget) return;

        // 1) 타겟 따라가기(부드럽게)
        Vector3 target = followTarget.position + (Vector3)followOffset;
        target.z = transform.position.z;

        // 2) 구역 클램프(있으면)
        if (clampBounds)
        {
            var b = clampBounds.bounds;
            float camHalfH = _cam.orthographicSize;
            float camHalfW = camHalfH * _cam.aspect;

            float minX = b.min.x + camHalfW;
            float maxX = b.max.x - camHalfW;
            float minY = b.min.y + camHalfH;
            float maxY = b.max.y - camHalfH;

            target.x = Mathf.Clamp(target.x, minX, maxX);
            target.y = Mathf.Clamp(target.y, minY, maxY);
        }

        // 3) follow + shake 합성
        Vector3 smooth = Vector3.SmoothDamp(transform.position, target, ref _vel, followDamping);
        transform.position = smooth + _shakeOffset;
    }

    /// <summary>카메라 흔들기(값 생략하면 기본값 사용)</summary>
    public void Shake(float amplitude = -1f, float duration = -1f, float frequency = -1f)
    {
        if (amplitude <= 0f)  amplitude = defaultAmplitude;
        if (duration  <= 0f)  duration  = defaultDuration;
        if (frequency <= 0f)  frequency = defaultFrequency;

        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(ShakeRoutine(amplitude, duration, frequency));
    }

    System.Collections.IEnumerator ShakeRoutine(float amp, float dur, float freq)
    {
        _shakeOffset = Vector3.zero;
        float t = 0f;
        // Perlin 기반 스무스 흔들림(시간이 지날수록 감쇠)
        float seedX = Random.value * 10f;
        float seedY = Random.value * 10f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;                // 타임스케일 0에서도 작동
            float atten = 1f - (t / dur);               // 끝으로 갈수록 줄어듦
            float nx = (Mathf.PerlinNoise(seedX, t * freq) * 2f - 1f);
            float ny = (Mathf.PerlinNoise(seedY, t * freq) * 2f - 1f);
            _shakeOffset = new Vector3(nx, ny, 0f) * amp * atten;
            yield return null;
        }

        _shakeOffset = Vector3.zero;
        _shakeCo = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Shake")]
    void _Test() => Shake();
#endif
}
