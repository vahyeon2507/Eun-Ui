using System.Collections;
using UnityEngine;

public class TuboParryTutorialController : MonoBehaviour
{
    [Header("Refs")]
    public BossTuboController boss;      // 튜보 보스
    public GuideMask guide;             // 화면 어두워지는 마스크 (Image/GuideMask)
    public RectTransform focusRect;     // 패링 구멍

    [Header("UI Roots")]
    [Tooltip("1차 튜토 전용 UI 루트 (텍스트/화살표 등). GuideMask 오브젝트 말고, 그 밑의 컨테이너를 넣어야 함")]
    public GameObject firstTutorialRoot;

    [Tooltip("2차 슬라이드 튜토리얼 루트 (TutorialSlidePanel 같은 애)")]
    public GameObject slideTutorialRoot;

    [Header("Timing - First Tutorial")]
    [Tooltip("첫 공격 이후 튜토가 실제로 뜨기까지의 딜레이(초)")]
    public float firstAttackDelay = 0.2f;

    [Header("Slide Tutorial")]
    [Tooltip("슬라이드 튜토리얼 UI 패널")]
    public TutorialSlideUI slideUI;

    [Tooltip("1차 튜토가 끝난 뒤 2차 튜토 시작까지 대기 시간(초)")]
    public float slideTutorialDelay = 3f;

    bool tutorialStarted = false;   // “이번 보스판에서 튜토를 한번이라도 시작했는지”
    bool tutorialActive = false;    // 1차 튜토(패링 가이드) 진행 중인지

    bool slideTutorialActive = false;  // 2차 튜토(슬라이드) 진행 중인지
    bool slideTutorialDone = false;    // 2차 튜토를 이미 끝냈는지

    void Awake()
    {
        if (!boss) boss = FindAnyObjectByType<BossTuboController>();
        if (!guide) guide = FindAnyObjectByType<GuideMask>();

        // 시작 시에는 UI 루트만 꺼두기 (GuideMask는 Close에서 자기 정리)
        if (firstTutorialRoot) firstTutorialRoot.SetActive(false);
        if (slideTutorialRoot) slideTutorialRoot.SetActive(false);

        if (guide && guide.gameObject.activeSelf)
            guide.Close();
    }

    void OnEnable()
    {
        if (!boss) boss = FindAnyObjectByType<BossTuboController>();

        if (boss != null)
        {
            // 보스의 첫 공격 이벤트에 붙어 있음
            boss.onFirstAttackIssued += OnFirstAttackIssued;
            Debug.Log("[Tuto] Subscribed to onFirstAttackIssued on Tubo");
        }
        else
        {
            Debug.LogWarning("[Tuto] BossTuboController를 찾지 못했습니다.");
        }

        if (slideUI != null)
            slideUI.OnSlidesFinished += OnSlideTutorialFinished;
    }

    void OnDisable()
    {
        if (boss != null)
            boss.onFirstAttackIssued -= OnFirstAttackIssued;

        if (slideUI != null)
            slideUI.OnSlidesFinished -= OnSlideTutorialFinished;
    }

    // ===== 외부(보스 등)에서 강제로 튜토리얼 시작시키고 싶을 때 쓰는 함수 =====
    // BossTuboController에서 호출하던 ForceStartTutorialFromBoss 복원판.
    public void ForceStartTutorialFromBoss()
    {
        // 이미 한 번 시작됐으면 다시 안 건드림
        if (tutorialStarted) return;

        tutorialStarted = true;

        // 기본 설계는 "첫 공격 후 firstAttackDelay 만큼 기다렸다가 시작"
        if (isActiveAndEnabled)
        {
            StartCoroutine(CoStartTutorialAfterDelay());
        }
        else
        {
            // 혹시 비활성 상태면 그냥 즉시 시작해서 튕김 방지
            StartTutorial();
        }

        Debug.Log("[Tuto] ForceStartTutorialFromBoss() called");
    }

    // ===== 1차 튜토: 보스 첫 공격 기준 =====
    void OnFirstAttackIssued()
    {
        // 첫 공격 이벤트는 한 번만 반응
        if (tutorialStarted) return;
        tutorialStarted = true;

        Debug.Log("[Tuto] OnFirstAttackIssued() → schedule first tutorial");
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

        // 1차 전용 UI ON / 2차 UI OFF
        if (firstTutorialRoot) firstTutorialRoot.SetActive(true);
        if (slideTutorialRoot) slideTutorialRoot.SetActive(false);

        PlayerController.TutorialInputLocked = true;
        Time.timeScale = 0f;

        if (guide != null)
        {
            guide.gameObject.SetActive(true);
            guide.Play(focusRect);
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

        if (firstTutorialRoot) firstTutorialRoot.SetActive(false);

        Debug.Log("[Tuto] 1차 패링 튜토 END");

        // === 여기서 2차 튜토 예약 ===
        if (slideUI != null && !slideTutorialDone)
            StartCoroutine(CoStartSlideTutorialAfterDelay());
    }

    IEnumerator CoStartSlideTutorialAfterDelay()
    {
        // 이때는 timeScale = 1 상태이므로 일반 WaitForSeconds 사용 가능
        yield return new WaitForSeconds(slideTutorialDelay);
        StartSlideTutorial();
    }

    // ===== 2차 튜토: 슬라이드 =====
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
            guide.gameObject.SetActive(true);
            guide.Play(focusRect);
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
