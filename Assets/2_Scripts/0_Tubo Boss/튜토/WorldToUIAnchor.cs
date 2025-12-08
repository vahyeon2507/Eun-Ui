using UnityEngine;
using UnityEngine.UI;

public class WorldToUIAnchor : MonoBehaviour
{
    [Header("필수 참조")]
    public Camera cam;                 // 메인 카메라
    public RectTransform canvasRect;   // Overlay Canvas의 RectTransform
    public Transform worldTarget;      // 따라갈 월드 오브젝트 (플레이어 등)
    public RectTransform anchorRect;   // GuideMask의 TargetArea 같은 UI Rect

    [Header("스팟 크기 설정")]
    public Vector2 spotSize = new Vector2(220f, 260f);

    void LateUpdate()
    {
        if (!cam || !canvasRect || !worldTarget || !anchorRect)
            return;

        // 월드 → 스크린 포인트
        Vector3 sp = RectTransformUtility.WorldToScreenPoint(cam, worldTarget.position);

        // 스크린 → 캔버스 로컬 좌표
        Vector2 lp;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, cam, out lp))
        {
            anchorRect.anchoredPosition = lp;
            anchorRect.sizeDelta = spotSize;
        }
    }
}
