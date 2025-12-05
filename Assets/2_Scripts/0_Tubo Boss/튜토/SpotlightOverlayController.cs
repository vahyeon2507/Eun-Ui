using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SpotlightOverlayController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Image overlayImage;     // 전체를 덮는 UI Image
    [SerializeField] private Camera targetCam;       // 비우면 Camera.main

    [Header("Darkness")]
    [Range(0f, 1f)] public float darkAlpha = 0.75f;

    [Header("Spot Defaults")]
    public float defaultRadius = 0.25f;
    public float defaultFeather = 0.08f;

    [System.Serializable]
    public struct Spot
    {
        public bool useWorld;
        public Transform worldTarget;
        public Vector2 viewport; // 0..1
        public float radius;     // <=0 이면 defaultRadius
        public float feather;    // <=0 이면 defaultFeather
    }

    [Header("Spots (max 4)")]
    public List<Spot> spots = new List<Spot>();

    // shader props (셰이더 이름과 1:1)
    static readonly int _DarkAlpha = Shader.PropertyToID("_DarkAlpha");
    static readonly int _SpotCount = Shader.PropertyToID("_SpotCount");
    static readonly int _Spot0 = Shader.PropertyToID("_Spot0");
    static readonly int _Spot1 = Shader.PropertyToID("_Spot1");
    static readonly int _Spot2 = Shader.PropertyToID("_Spot2");
    static readonly int _Spot3 = Shader.PropertyToID("_Spot3");

    Material _mat; // 인스턴스 머티리얼

    void Awake()
    {
        if (!overlayImage) overlayImage = GetComponent<Image>();
        if (!targetCam) targetCam = Camera.main;

        // 인스턴스 머티리얼 생성 + 적용
        if (overlayImage && overlayImage.material)
        {
            _mat = new Material(overlayImage.material);
            overlayImage.material = _mat;
        }

        overlayImage.color = Color.white; // 알파 보장
        overlayImage.enabled = false;     // 시작 시 꺼둠
        ApplyNow();
    }

    void OnValidate()
    {
        if (defaultRadius <= 0f) defaultRadius = 0.25f;
        if (defaultFeather <= 0f) defaultFeather = 0.08f;
        ApplyNow();
    }

    void LateUpdate() => ApplyNow();

    public void Enable(bool on)
    {
        if (overlayImage) overlayImage.enabled = on;
    }

    public void SetSingleWorldSpot(Transform target, float radius, float feather)
    {
        spots.Clear();
        spots.Add(new Spot { useWorld = true, worldTarget = target, viewport = new Vector2(0.5f, 0.5f), radius = radius, feather = feather });
        ApplyNow();
    }

    public void SetSingleViewportSpot(Vector2 viewport, float radius, float feather)
    {
        spots.Clear();
        spots.Add(new Spot { useWorld = false, worldTarget = null, viewport = viewport, radius = radius, feather = feather });
        ApplyNow();
    }

    void ApplyNow()
    {
        if (_mat == null || overlayImage == null) return;

        _mat.SetFloat(_DarkAlpha, Mathf.Clamp01(darkAlpha));

        Vector4 s0 = Vector4.zero, s1 = Vector4.zero, s2 = Vector4.zero, s3 = Vector4.zero;
        int count = 0;

        for (int i = 0; i < spots.Count && i < 4; i++)
        {
            var s = spots[i];
            float r = (s.radius > 0f) ? s.radius : defaultRadius;
            float f = (s.feather > 0f) ? s.feather : defaultFeather;

            Vector2 vp = s.viewport;
            if (s.useWorld && s.worldTarget && targetCam)
                vp = targetCam.WorldToViewportPoint(s.worldTarget.position);

            var v4 = new Vector4(vp.x, vp.y, r, Mathf.Max(0.0001f, f));

            if (count == 0) s0 = v4;
            else if (count == 1) s1 = v4;
            else if (count == 2) s2 = v4;
            else if (count == 3) s3 = v4;

            count++;
        }

        _mat.SetInt(_SpotCount, count);
        _mat.SetVector(_Spot0, s0);
        _mat.SetVector(_Spot1, s1);
        _mat.SetVector(_Spot2, s2);
        _mat.SetVector(_Spot3, s3);
    }
}
