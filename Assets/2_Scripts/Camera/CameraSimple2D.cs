using UnityEngine;

[DisallowMultipleComponent]
public class CameraSimple2D : MonoBehaviour
{
    [Header("Follow")]
    public Transform followTarget;
    [Range(0f, 1f)] public float followDamping = 0.12f;
    public Vector2 followOffset;

    [Header("Clamp (optional)")]
    public BoxCollider2D clampBounds;

    // ---- shake runtime ----
    Vector2 _shakeOffset;
    float _shakeAmp, _shakeFreq, _shakeRemain;

    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;

        // 씬에 하나뿐이면 자동 연결
        if (followTarget == null)
        {
            var t = GameObject.Find("CameraTarget");
            if (t) followTarget = t.transform;
        }
    }

    void LateUpdate()
    {
        // 1) follow
        Vector3 pos = transform.position;
        if (followTarget)
        {
            Vector3 target = followTarget.position + (Vector3)followOffset;
            // 언스케일드 기반의 지연 보간(일시정지 영향 X)
            float k = 1f - Mathf.Pow(1f - followDamping, Time.unscaledDeltaTime * 60f);
            pos = Vector3.Lerp(pos, new Vector3(target.x, target.y, pos.z), k);
        }

        // 2) shake
        if (_shakeRemain > 0f)
        {
            _shakeRemain -= Time.unscaledDeltaTime;
            float t = Time.unscaledTime * _shakeFreq;
            _shakeOffset.x = (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * _shakeAmp;
            _shakeOffset.y = (Mathf.PerlinNoise(0.1f, t) - 0.5f) * 2f * _shakeAmp;
        }
        else _shakeOffset = Vector2.zero;

        pos.x += _shakeOffset.x;
        pos.y += _shakeOffset.y;

        // 3) clamp
        if (clampBounds != null && _cam != null)
        {
            Bounds b = clampBounds.bounds;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            pos.x = Mathf.Clamp(pos.x, b.min.x + halfW, b.max.x - halfW);
            pos.y = Mathf.Clamp(pos.y, b.min.y + halfH, b.max.y - halfH);
        }

        transform.position = pos;
    }

    /// <summary>외부에서 호출하는 흔들림 API</summary>
    public void Shake(float amplitude = 0.8f, float duration = 0.15f, float frequency = 22f)
    {
        _shakeAmp = amplitude;
        _shakeFreq = frequency;
        _shakeRemain = Mathf.Max(_shakeRemain, duration); // 중복 호출 시 더 긴 쪽 유지
    }
}
