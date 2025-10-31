using UnityEngine;
using System.Collections; // IEnumerator
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    // ---- punch zoom runtime ----
    Camera _cam;
    Coroutine _zoomCR;

    // ===== Shake defaults (for convenient external calls) =====
    [Header("Shake Settings (Inspector defaults)")]
    [Tooltip("기본 흔들림 세기")]
    public float defaultShakeAmplitude = 0.8f;
    [Tooltip("기본 흔들림 지속시간")]
    public float defaultShakeDuration = 0.15f;
    [Tooltip("기본 흔들림 주파수")]
    public float defaultShakeFrequency = 22f;

    // ===== Parry Special Punch Zoom =====
    [Header("Punch Zoom (Parry Special)")]
    public bool punchZoomEnabled = true;
    [Tooltip("작을수록 더 확대됨 (목표 orthographicSize)")]
    public float punchZoomTargetSize = 3.6f;
    [Tooltip("줌-인 시간")] public float punchZoomIn = 0.08f;
    [Tooltip("유지 시간")] public float punchZoomHold = 0.12f;
    [Tooltip("복귀 시간")] public float punchZoomOut = 0.20f;
    [Tooltip("Time.unscaledDeltaTime 사용")] public bool punchZoomUseUnscaled = true;

    // ====== TEST PARAMS (Inspector Buttons use these) ======
    [Header("Test ▸ Shake")]
    [SerializeField] float testShakeAmplitude = 0.8f;
    [SerializeField] float testShakeDuration = 0.15f;
    [SerializeField] float testShakeFrequency = 22f;

    [Header("Test ▸ Punch Zoom (absolute size)")]
    [SerializeField] float testPunchTargetSize = 3.6f; // 목표 orthographicSize
    [SerializeField] float testPunchInTime = 0.08f;
    [SerializeField] float testPunchHoldTime = 0.10f;
    [SerializeField] float testPunchOutTime = 0.18f;
    [SerializeField] bool testPunchUnscaled = true;

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
            // 언스케일드 기반 지연 보간
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

    /// <summary>
    /// 외부 흔들림 API. 파라미터를 음수로 넣으면 인스펙터 기본값을 사용.
    /// </summary>
    public void Shake(float amplitude = -1f, float duration = -1f, float frequency = -1f)
    {
        _shakeAmp = (amplitude > 0f) ? amplitude : defaultShakeAmplitude;
        _shakeFreq = (frequency > 0f) ? frequency : defaultShakeFrequency;
        float dur = (duration > 0f) ? duration : defaultShakeDuration;

        _shakeRemain = Mathf.Max(_shakeRemain, dur); // 중복 호출 시 더 긴 쪽 유지
    }

    /// <summary>펀치 줌(목표 orthographicSize로 in→hold→out)</summary>
    public void PunchZoom(float targetSize, float inT, float holdT, float outT, bool unscaled = true)
    {
        if (_cam == null) return;
        if (_zoomCR != null) StopCoroutine(_zoomCR);
        _zoomCR = StartCoroutine(CoPunchZoom(targetSize, inT, holdT, outT, unscaled));
    }

    /// <summary>
    /// 패링 스페셜 전용 래퍼. PlayerController에서 cam.PunchZoomParrySpecial()로 호출.
    /// </summary>
    public void PunchZoomParrySpecial()
    {
        if (!punchZoomEnabled || _cam == null) return;

        if (_zoomCR != null) { StopCoroutine(_zoomCR); _zoomCR = null; }
        _zoomCR = StartCoroutine(CoPunchZoom(
            punchZoomTargetSize,
            punchZoomIn,
            punchZoomHold,
            punchZoomOut,
            punchZoomUseUnscaled));
    }

    // === 내부 구현 ===
    IEnumerator CoPunchZoom(float target, float inT, float holdT, float outT, bool unscaled)
    {
        float start = _cam.orthographicSize;
        float t = 0f;

        // in
        while (t < inT)
        {
            float a = inT > 0f ? t / inT : 1f;
            _cam.orthographicSize = Mathf.Lerp(start, target, EaseOutCubic(a));
            t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
            yield return null;
        }
        _cam.orthographicSize = target;

        // hold
        t = 0f;
        while (t < holdT)
        {
            t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
            yield return null;
        }

        // out
        t = 0f;
        while (t < outT)
        {
            float a = outT > 0f ? t / outT : 1f;
            _cam.orthographicSize = Mathf.Lerp(target, start, EaseOutCubic(a));
            t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
            yield return null;
        }
        _cam.orthographicSize = start;

        _zoomCR = null;
    }

    static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    // ===== Inspector Buttons (for quick tests) =====
    public void Editor_TestShake() =>
        Shake(testShakeAmplitude, testShakeDuration, testShakeFrequency);

    public void Editor_TestPunchZoom()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        PunchZoom(testPunchTargetSize, testPunchInTime, testPunchHoldTime, testPunchOutTime, testPunchUnscaled);
    }

    [ContextMenu("Test/Shake (use test params)")]
    void Ctx_TestShake() => Editor_TestShake();

    [ContextMenu("Test/Punch Zoom (use test params)")]
    void Ctx_TestPunchZoom() => Editor_TestPunchZoom();
}
