using UnityEngine;
using UnityEngine.UI;

public class UIBinder : MonoBehaviour
{
    [Header("Optional explicit wiring (없으면 자동 탐색)")]
    public Camera canvasCamera;           // 생략 가능
    public Image redBar, yellowBar, parryBar;

    void Awake()
    {
        // 1) Canvas 카메라 자동 세팅
        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            if (canvas.worldCamera == null)
                canvas.worldCamera = canvasCamera != null ? canvasCamera : Camera.main;
        }

        // 2) PlayerHealth 자동 연결
        var health = FindObjectOfType<PlayerHealth>();
        if (health != null)
        {
            if (redBar != null) health.redBar = redBar;
            if (yellowBar != null) health.yellowBar = yellowBar;
            if (parryBar != null) health.parryBar = parryBar;

            // 즉시 UI 반영 (health가 Start에서 갱신하긴 하지만 안전 차원)
            var m = health.GetType().GetMethod("CheckHealthBarStatus");
            if (m != null) m.Invoke(health, null);
        }

        // 3) PlayerController ↔ PlayerHealth 링크(선택)
        // 이미 PlayerController.healthComponent에 PlayerHealth가 물려 있지 않다면 자동으로 물려준다.
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.healthComponent == null && health != null)
            pc.healthComponent = health;
    }
}
