using UnityEngine;

[DisallowMultipleComponent]
public class SpriteFlash : MonoBehaviour
{
    [Header("Target")]
    public SpriteRenderer sr;                 // 비워두면 자동 할당

    [Header("Flash Material (optional)")]
    [Tooltip("흰 번쩍 전용 머티리얼(Shader Graph/Unlit도 OK). 비워두면 색만 바꿔서 흰 번쩍 처리")]
    public Material flashMaterial;

    [Tooltip("머티리얼에 강도값이 있으면 이름을 넣어주세요 (예: _GlowAmount, _Intensity 등). 없으면 비워두기")]
    public string intensityProperty = "_GlowAmount";
    public float flashIntensity = 1f;

    [Header("Timing")]
    public float duration = 0.08f;            // 번쩍 유지 시간
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    Material _originalShared;                 // 인스펙터에 있던 기본 머티리얼(공유)
    Material _runtimeFlash;                   // 런타임 인스턴스
    int _propId = -1;
    Color _originalColor;
    Coroutine _co;

    void Reset()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        _originalShared = sr != null ? sr.sharedMaterial : null;
        _originalColor = sr != null ? sr.color : Color.white;

        if (!string.IsNullOrEmpty(intensityProperty))
            _propId = Shader.PropertyToID(intensityProperty);

        if (flashMaterial != null)
        {
            // 에셋을 직접 건드리지 않도록 인스턴스를 만들어 사용
            _runtimeFlash = Instantiate(flashMaterial);
            if (_propId != -1 && _runtimeFlash.HasProperty(_propId))
                _runtimeFlash.SetFloat(_propId, 0f);
        }
    }

    void OnDisable() => Restore();
    void OnDestroy() => Restore();

    void Restore()
    {
        if (sr != null)
        {
            sr.sharedMaterial = _originalShared; // 완전히 원상복귀
            sr.color = _originalColor;
        }
        if (_runtimeFlash != null)
        {
            Destroy(_runtimeFlash);
            _runtimeFlash = null;
        }
    }

    /// <summary>외부에서 호출: 한 번 번쩍</summary>
    public void FlashOnce(float? customDuration = null, float? customIntensity = null)
    {
        if (!isActiveAndEnabled || sr == null) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFlash(customDuration ?? duration, customIntensity ?? flashIntensity));
    }

    System.Collections.IEnumerator CoFlash(float dur, float intensity)
    {
        // 방법 1) 번쩍 머티리얼이 있을 때: 머티리얼 교체 + 강도 애니메이션
        if (_runtimeFlash != null)
        {
            sr.material = _runtimeFlash; // 일시 적용 (shared 아님!)
            float t = 0f;
            while (t < dur)
            {
                if (_propId != -1 && _runtimeFlash.HasProperty(_propId))
                {
                    float k = curve.Evaluate(t / dur); // 0→1
                    _runtimeFlash.SetFloat(_propId, Mathf.Lerp(0f, intensity, k));
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_propId != -1 && _runtimeFlash.HasProperty(_propId))
                _runtimeFlash.SetFloat(_propId, 0f);

            sr.sharedMaterial = _originalShared; // 확실히 복귀
        }
        // 방법 2) 머티리얼이 없으면: 색만 잠깐 순백으로
        else
        {
            float t = 0f;
            while (t < dur)
            {
                float k = curve.Evaluate(t / dur);
                sr.color = Color.Lerp(_originalColor, Color.white, k);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            sr.color = _originalColor;
        }

        _co = null;
    }
}
