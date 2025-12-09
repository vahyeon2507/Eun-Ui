using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuideMask : MaskableGraphic, ICanvasRaycastFilter
{
    public static GuideMask Self;

    [Header("Target")]
    [Tooltip("구멍 위치를 표시할 RectTransform. 비워두면 자식에서 'TargetArea' 이름으로 찾음.")]
    public RectTransform targetAreaExplicit;

    private RectTransform _target;      // 현재 포커스할 타겟(RectTransform)
    private Vector2 _targetMin;
    private Vector2 _targetMax;
    private RectTransform _targetArea;  // 실제 구멍을 표현하는 내부 Rect

    [Header("Debug")]
    public bool debugLog = false;

    // MaskableGraphic / UIBehaviour 의 Awake를 제대로 이어받기
    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    // ==== Raycast 방지 ====
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // 꺼져 있거나 타겟이 없으면 그냥 통과
        if (!gameObject.activeInHierarchy || _targetArea == null)
            return true;

        return !RectTransformUtility.RectangleContainsScreenPoint(_targetArea, sp, eventCamera);
    }

    // ==== 외부에서 호출 ====
    public void Close()
    {
        if (debugLog)
            Debug.Log("[GuideMask] Close()", this);

        _target = null;
        gameObject.SetActive(false);
    }

    public void Play(RectTransform target)
    {
        if (debugLog)
            Debug.Log($"[GuideMask] Play() called, target={target}", this);

        if (target == null)
        {
            Debug.LogWarning("[GuideMask] Play() 호출 시 target 이 null 입니다.");
            Close();
            return;
        }

        EnsureTargetArea();
        if (_targetArea == null)
        {
            Debug.LogError("[GuideMask] 'TargetArea' 를 찾지 못해 마스크를 표시할 수 없습니다.");
            Close();
            return;
        }

        gameObject.SetActive(true);

        // Canvas / Camera 결정
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                cam = null;
            else
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }
        else
        {
            cam = Camera.main;
        }

        // 타겟의 화면 좌표 → 이 마스크(RectTransform) 기준 로컬 좌표로 변환
        var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, target.position);

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, cam, out localPoint))
        {
            Debug.LogWarning("[GuideMask] ScreenPointToLocalPointInRectangle 실패. 마스크를 닫습니다.");
            Close();
            return;
        }

        // TargetArea의 Rect 설정을 타겟과 동일하게 맞추고, 위치만 로컬 기준으로 세팅
        _targetArea.anchorMax = target.anchorMax;
        _targetArea.anchorMin = target.anchorMin;
        _targetArea.anchoredPosition = target.anchoredPosition;
        _targetArea.anchoredPosition3D = target.anchoredPosition3D;
        _targetArea.offsetMax = target.offsetMax;
        _targetArea.offsetMin = target.offsetMin;
        _targetArea.pivot = target.pivot;
        _targetArea.sizeDelta = target.sizeDelta;
        _targetArea.localPosition = localPoint;

        _targetArea.ForceUpdateRectTransforms();

        _target = _targetArea;
        _target.ForceUpdateRectTransforms();

        // 바로 한 번 뷰 갱신
        RefreshView();
    }

    // ==== 초기 설정 ====
    public void Init()
    {
        EnsureTargetArea();

        if (Self == null)
            Self = this;

        if (debugLog)
            Debug.Log($"[GuideMask] Init() done. active={gameObject.activeSelf}, targetArea={_targetArea}", this);
        // 여기서 Close() 안 한다. 초기 활성/비활성은 외부(TutorialController)에서 제어.
    }

    void EnsureTargetArea()
    {
        if (_targetArea != null) return;

        if (targetAreaExplicit != null)
        {
            _targetArea = targetAreaExplicit;
        }
        else
        {
            var found = transform.Find("TargetArea");
            _targetArea = found as RectTransform;
        }

        if (_targetArea == null)
        {
            Debug.LogError($"[GuideMask] 자식 'TargetArea' 를 찾지 못했습니다. ({name})");
        }
    }

    // ==== 메쉬 그리기 ====
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        toFill.Clear();

        var maskRect = rectTransform.rect;

        var maskRectLeftTop = new Vector2(-maskRect.width / 2, maskRect.height / 2);
        var maskRectLeftBottom = new Vector2(-maskRect.width / 2, -maskRect.height / 2);
        var maskRectRightTop = new Vector2(maskRect.width / 2, maskRect.height / 2);
        var maskRectRightBottom = new Vector2(maskRect.width / 2, -maskRect.height / 2);

        var targetRectLeftTop = new Vector2(_targetMin.x, _targetMax.y);
        var targetRectLeftBottom = _targetMin;
        var targetRectRightTop = _targetMax;
        var targetRectRightBottom = new Vector2(_targetMax.x, _targetMin.y);

        // 8개의 버텍스로 외곽 + 구멍 영역 구성
        toFill.AddVert(maskRectLeftBottom, color, Vector2.zero); // 0
        toFill.AddVert(targetRectLeftBottom, color, Vector2.zero); // 1
        toFill.AddVert(targetRectRightBottom, color, Vector2.zero); // 2
        toFill.AddVert(maskRectRightBottom, color, Vector2.zero); // 3
        toFill.AddVert(targetRectRightTop, color, Vector2.zero); // 4
        toFill.AddVert(maskRectRightTop, color, Vector2.zero); // 5
        toFill.AddVert(targetRectLeftTop, color, Vector2.zero); // 6
        toFill.AddVert(maskRectLeftTop, color, Vector2.zero); // 7

        // 네 모서리 영역을 4개의 큰 쿼드(=8개의 삼각형)로 채움
        toFill.AddTriangle(0, 1, 2);
        toFill.AddTriangle(2, 3, 0);
        toFill.AddTriangle(3, 2, 4);
        toFill.AddTriangle(4, 5, 3);
        toFill.AddTriangle(6, 7, 5);
        toFill.AddTriangle(5, 4, 6);
        toFill.AddTriangle(7, 6, 1);
        toFill.AddTriangle(1, 0, 7);
    }

    void LateUpdate()
    {
        RefreshView();
    }

    private void RefreshView()
    {
        Vector2 newMin;
        Vector2 newMax;

        if (_target != null && _target.gameObject.activeSelf)
        {
            // 이 마스크(RectTransform)를 기준으로 타겟의 bounds 계산
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(transform, _target);
            newMin = bounds.min;
            newMax = bounds.max;
        }
        else
        {
            newMin = Vector2.zero;
            newMax = Vector2.zero;
        }

        if (_targetMin != newMin || _targetMax != newMax)
        {
            if (debugLog)
                Debug.Log($"[GuideMask] bounds updated: min={newMin}, max={newMax}", this);

            _targetMin = newMin;
            _targetMax = newMax;
            SetAllDirty(); // 메쉬 다시 그리기
        }
    }
}
