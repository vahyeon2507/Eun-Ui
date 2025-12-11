using UnityEngine;

/// <summary>
/// 2D 전용 페럴렉스 레이어.
/// - 카메라의 XY 이동에 비례해서 배경을 천천히 따라오게 한다.
/// - Z는 건드리지 않아서 정렬은 Sorting Layer / Order in Layer로 처리.
/// </summary>
[DisallowMultipleComponent]
public class ParallaxLayer2D : MonoBehaviour
{
    [Tooltip("기준이 될 카메라 Transform (비워두면 MainCamera 자동 검색)")]
    public Transform cameraTransform;

    [Tooltip("카메라 이동 대비 이 레이어가 따라갈 비율 (0이면 고정, 1이면 카메라와 동일 속도)")]
    public Vector2 parallaxFactor = new Vector2(0.3f, 0f);

    [Tooltip("Y축(수직) 페럴렉스를 쓸지 여부")]
    public bool useVerticalParallax = false;

    [Tooltip("플레이 시작 시 현재 위치를 기준점으로 초기화할지 여부")]
    public bool initializeOnStart = true;

    // 내부 기준
    Vector2 _startCamPos2D;
    Vector2 _startLayerPos2D;
    float _startZ;
    bool _initialized;

    void Start()
    {
        if (initializeOnStart)
            Init();
    }

    void Init()
    {
        if (!Application.isPlaying) return;   // ★ 에디터에서 호출돼도 기준 안 잡게

        if (!cameraTransform)
        {
            var cam = Camera.main;
            if (cam != null)
                cameraTransform = cam.transform;
        }

        if (!cameraTransform)
            return;

        Vector3 camPos = cameraTransform.position;
        Vector3 layerPos = transform.position;

        _startCamPos2D = new Vector2(camPos.x, camPos.y);
        _startLayerPos2D = new Vector2(layerPos.x, layerPos.y);
        _startZ = layerPos.z;
        _initialized = true;
    }

    void LateUpdate()
    {
        // ★ 플레이 모드가 아닐 땐 전혀 건드리지 않음 → 씬에서 마음대로 드래그 가능
        if (!Application.isPlaying) return;

        if (!_initialized || !cameraTransform)
            Init();

        if (!_initialized || !cameraTransform)
            return;

        Vector3 camPos3 = cameraTransform.position;
        Vector2 camPos2 = new Vector2(camPos3.x, camPos3.y);

        // 카메라가 기준점에서 얼마나 움직였는지
        Vector2 camDelta = camPos2 - _startCamPos2D;

        float moveX = camDelta.x * parallaxFactor.x;
        float moveY = useVerticalParallax ? camDelta.y * parallaxFactor.y : 0f;

        Vector3 target = new Vector3(
            _startLayerPos2D.x + moveX,
            _startLayerPos2D.y + moveY,
            _startZ
        );

        transform.position = target;
    }

    /// <summary>
    /// 컷씬/텔레포트 등으로 카메라/배경을 순간이동시킨 후,
    /// 그 상태를 새 기준점으로 삼고 싶을 때 호출.
    /// </summary>
    public void ResetParallaxOrigin()
    {
        if (!Application.isPlaying) return;
        if (!cameraTransform) return;

        Vector3 camPos = cameraTransform.position;
        Vector3 layerPos = transform.position;

        _startCamPos2D = new Vector2(camPos.x, camPos.y);
        _startLayerPos2D = new Vector2(layerPos.x, layerPos.y);
        _startZ = layerPos.z;
        _initialized = true;
    }
}
