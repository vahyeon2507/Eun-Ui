// _SmokePauseAndOverlay.cs
using UnityEngine;

public class _SmokePauseAndOverlay : MonoBehaviour
{
    public SpotlightOverlayController overlay;
    public Transform focus; // player Transform

    void Start()
    {
        Debug.Log("[SMOKE] Try Pause + Overlay");
        Time.timeScale = 0f;                                   // 일단 강제 정지
        if (overlay && focus)
        {
            overlay.SetSingleWorldSpot(focus, 0.24f, 0.10f);   // 플레이어만 밝게
            overlay.Enable(true);
            Debug.Log("[SMOKE] Overlay enabled");
        }
    }
}
