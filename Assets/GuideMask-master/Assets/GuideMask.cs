using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuideMask : MaskableGraphic, ICanvasRaycastFilter
{
    public static GuideMask Self;

    private RectTransform _target;      // 현재 포커스할 타겟(RectTransform)
    private Vector2 _targetMin;
    private Vector2 _targetMax;
    private RectTransform _targetArea;  // 실제 구멍을 표현하는 내부 Rect

    [Header("Debug")]
    public bool debugLog = false;

    void Awake()
    {
        Init();
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // _targetArea가 아직 초기화 안 됐으면 그냥 통과시키고, NRE 방지
        if (_targetArea == null) return true;

        return !RectTransformUtility.RectangleContainsScreenPoint(_targetArea, sp, eventCamera);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Play(RectTransform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[GuideMask] Play() 호출 시 target 이 null 입니다.");
            Close();
            return;
        }

        // 혹시 Init이 안 돌았다면 방어적으로 한 번 더
        if (_targetArea == null)
        {
            Init();
            if (_targetArea == null)
            {
                Debug.LogError("[GuideMask] 'TargetArea' 를 찾지 못해 마스크를 표시할 수 없습니다.");
                Close();
                return;
            }
        }

        gameObject.SetActive(true);

        // 타겟의 화면 좌표 → 이 마스크(RectTransform) 기준 로컬 좌표로 변환
        var screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, target.position);

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, Camera.main, out localPoint))
        {
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

    public void Init()
    {
        // 자식에서 "TargetArea" 찾기
        _targetArea = transform.Find("TargetArea") as RectTransform;
        if (_targetArea == null)
        {
            Debug.LogError($"[GuideMask] 자식 'TargetArea' 를 찾지 못했습니다. ({name})");
        }

        Self = this;
        Close();
    }

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
            {
                Debug.Log($"[GuideMask] bounds updated: min={newMin}, max={newMax}");
            }

            _targetMin = newMin;
            _targetMax = newMax;
            SetAllDirty(); // 메쉬 다시 그리기
        }
    }
}
