using UnityEngine;

public class TuboTutorialLite : MonoBehaviour
{
    public RectTransform anchorRect;
    public Canvas overlayCanvas;

    GuideMask guide;
    bool started;

    void Awake()
    {
        guide = FindAnyObjectByType<GuideMask>();
        if (guide != null)
            guide.Init();
    }

    void Update()
    {
        // 테스트용: T 키로 튜토 시작
        if (Input.GetKeyDown(KeyCode.T))
            StartTutorial();

        if (!started) return;

        // === 튜토 정지 상태에서만 실행 ===

        // 패링 키(C / LeftControl)가 눌리면 튜토 종료
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
            EndTutorial();

        // 디버그용: Y로 그냥 취소
        if (Input.GetKeyDown(KeyCode.Y))
            CancelTutorial();
    }

    void StartTutorial()
    {
        if (started || guide == null) return;
        started = true;

        // 먼저 플레이어 행동 잠그고, 그 다음 시간 멈추자 (1프레임 미끄러짐 방지)
        PlayerController.TutorialInputLocked = true;
        Time.timeScale = 0f;

        guide.Play(anchorRect);
        Debug.Log("[Tuto] START (lock ON)");
    }

    void EndTutorial()
    {
        if (!started) return;
        started = false;

        Time.timeScale = 1f;
        PlayerController.TutorialInputLocked = false;

        if (guide != null)
            guide.Close();

        Debug.Log("[Tuto] END by parry key");
        // 여기서 정말 바로 패링까지 시키고 싶으면:
        // var player = FindAnyObjectByType<PlayerController>();
        // if (player != null) player.ForceParryFromTutorial();
    }

    void CancelTutorial()
    {
        if (!started) return;
        started = false;

        Time.timeScale = 1f;
        PlayerController.TutorialInputLocked = false;

        if (guide != null)
            guide.Close();

        Debug.Log("[Tuto] CANCEL");
    }
}
