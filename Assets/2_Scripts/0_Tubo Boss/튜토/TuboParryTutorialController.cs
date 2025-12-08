using System.Collections;
using UnityEngine;

public class TuboParryTutorialController : MonoBehaviour
{
    [Header("Refs")]
    public BossTuboController boss;      // 튜보 보스
    public GuideMask guide;             // 화면 어두워지는 마스크
    public RectTransform focusRect;     // 첫 패링 튜토용 구멍 영역

    [Header("UI Roots")]
    [Tooltip("1차 패링 튜토리얼(가이드 마스크) 루트 오브젝트 - 예: Canvas/Image")]
    public GameObject firstTutorialRoot;

    [Tooltip("2차 슬라이드 튜토리얼 루트 오브젝트 - 예: Canvas/TutorialSlidePanel")]
    public GameObject slideTutorialRoot;

    [Header("Timing - First Tutorial")]
    [Tooltip("튜보가 첫 공격 트리거를 친 후, 첫 튜토리얼을 시작하기까지의 딜레이(초)")]
    public float firstAttackDelay = 0.2f;

    // === 2차 튜토(슬라이드) 관련 ===
    [Header("Slide Tutorial")]
    [Tooltip("슬라이드 튜토리얼 UI 패널(TutorialSlideUI)")]
    public TutorialSlideUI slideUI;

    [Tooltip("첫 튜토가 끝난 후, 슬라이드 튜토를 시작하기까지의 딜레이(초)")]
    public float slideTutorialDelay = 3f;

    bool tutorialStarted = false;   // 첫 공격 튜토 시작 여부 (한 번만)
    bool tutorialActive = false;    // 1차 튜토(패링 가이드) 활성 여부

    bool slideTutorialActive = false;  // 2차 튜토(슬라이드) 진행 중인지
    bool slideTutorialDone = false;    // 2차 튜토를 이미 끝냈는지

    void Awake()
    {
        if (!boss) boss = FindAnyObjectByType<BossTuboController>();
        if (!guide) guide = FindAnyObjectByType<GuideMask>();

        // 시작 시엔 둘 다 꺼두기
        if (firstTutorialRoot) firstTutorialRoot.SetActive(false);
        if (slideTutorialRoot) slideTutorialRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (!boss) boss = FindAnyObjectByType<BossTuboController>();

        if (boss != null)
        {
            boss.onFirstAttackIssued += OnFirstAttackIssued;
        }
        else
        {
            Debug.LogWarning("[Tuto] BossTuboController를 찾지 못했습니다.");
        }

        if (slideUI != null)
        {
            slideUI.OnSlidesFinished += OnSlideTutorialFinished;
        }
    }

    void OnDisable()
    {
        if (boss != null)
        {
            boss.onFirstAttackIssued -= OnFirstAttackIssued;
        }

        if (slideUI != null)
        {
            slideUI.OnSlidesFinished -= OnSlideTutorialFinished;
        }
    }

    // ==== 1차 튜토: 튜보 첫 공격 기준 ====
    void OnFirstAttackIssued()
    {
        // 첫 공격 이벤트는 한 번만 반응
        if (tutorialStarted) return;
        tutorialStarted = true;

        StartCoroutine(CoStartTutorialAfterDelay());
    }

    IEnumerator CoStartTutorialAfterDelay()
    {
        // 아직 timeScale = 1 이라 WaitForSeconds 정상 동작
        yield return new WaitForSeconds(firstAttackDelay);
        StartTutorial();
    }

    void StartTutorial()
    {
        if (tutorialActive) return;
        tutorialActive = true;

        // 1차 튜토 루트 ON, 2차 튜토 루트 OFF
        if (firstTutorialRoot) firstTutorialRoot.SetActive(true);
        if (slideTutorialRoot) slideTutorialRoot.SetActive(false);

        PlayerController.TutorialInputLocked = true;
        Time.timeScale = 0f;

        if (guide != null)
        {
            if (focusRect != null)
                guide.Play(focusRect);
            else
                guide.Play(null);
        }

        Debug.Log("[Tuto] 1차 패링 튜토 START");
    }

    void EndTutorial()
    {
        if (!tutorialActive) return;
        tutorialActive = false;

        Time.timeScale = 1f;
        PlayerController.TutorialInputLocked = false;

        if (guide != null)
            guide.Close();

        // 1차 튜토 UI 비활성화
        if (firstTutorialRoot) firstTutorialRoot.SetActive(false);

        Debug.Log("[Tuto] 1차 패링 튜토 END");

        // === 여기서 2차 튜토 예약 ===
        if (slideUI != null && !slideTutorialDone)
        {
            StartCoroutine(CoStartSlideTutorialAfterDelay());
        }
    }

    IEnumerator CoStartSlideTutorialAfterDelay()
    {
        // 이때는 timeScale = 1 상태이므로 일반 WaitForSeconds 사용 가능
        yield return new WaitForSeconds(slideTutorialDelay);
        StartSlideTutorial();
    }

    // ==== 2차 튜토: 슬라이드 ====
    void StartSlideTutorial()
    {
        if (slideTutorialActive || slideTutorialDone) return;

        slideTutorialActive = true;

        // 1차 루트 끄고, 2차 루트 켜기
        if (firstTutorialRoot) firstTutorialRoot.SetActive(false);
        if (slideTutorialRoot) slideTutorialRoot.SetActive(true);

        PlayerController.TutorialInputLocked = true;
        Time.timeScale = 0f;

        if (guide != null)
        {
            // 슬라이드 튜토에서도 같은 focusRect를 쓰거나,
            // 필요하면 별도 Rect를 만들어서 다른 걸 연결해도 됨.
            if (focusRect != null)
                guide.Play(focusRect);
            else
                guide.Play(null);
        }

        if (slideUI != null)
        {
            slideUI.gameObject.SetActive(true);
            slideUI.Begin();
        }
        else
        {
            Debug.LogWarning("[Tuto] slideUI가 설정되지 않아 슬라이드 튜토를 시작할 수 없습니다.");
            // 실패 시 안전하게 풀기
            Time.timeScale = 1f;
            PlayerController.TutorialInputLocked = false;
            slideTutorialActive = false;
            slideTutorialDone = true;
        }

        Debug.Log("[Tuto] 2차 슬라이드 튜토 START");
    }

    void OnSlideTutorialFinished()
    {
        if (!slideTutorialActive) return;

        slideTutorialActive = false;
        slideTutorialDone = true;

        if (guide != null)
            guide.Close();

        // 2차 튜토 UI 비활성화
        if (slideTutorialRoot) slideTutorialRoot.SetActive(false);

        Time.timeScale = 1f;
        PlayerController.TutorialInputLocked = false;

        Debug.Log("[Tuto] 2차 슬라이드 튜토 END");
    }

    void Update()
    {
        // 디버그용: T키로 1차 튜토 수동 토글 (슬라이드 진행 중엔 무시)
        if (Input.GetKeyDown(KeyCode.T) && !slideTutorialActive)
        {
            if (!tutorialActive)
                StartTutorial();
            else
                EndTutorial();
        }

        // 1차 튜토(패링 가이드) 중일 때만 C/LeftCtrl로 종료
        if (tutorialActive)
        {
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
            {
                EndTutorial();
            }
        }

        // 2차 튜토 중에는 여기서 따로 키 처리할 필요 없음
        // 슬라이드 입력은 TutorialSlideUI.Update()에서 처리 중
    }
}
