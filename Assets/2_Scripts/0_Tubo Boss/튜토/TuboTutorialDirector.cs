using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TuboTutorialDirector : MonoBehaviour
{
    [Header("Refs")]
    public BossTuboController tubo;
    public PlayerController player;
    public CameraSimple2D cam2d;                 // 없으면 Camera.main
    public SpotlightOverlayController spotlight;
    public TutorialSlideShow slideUI;
    public CanvasGroup promptUI;
    public TMP_Text promptText;

    [Header("Settings")]
    public bool runOnce = true;
    public string playerPrefsKey = "TuboTutorialDone";
    public float approachDistance = 3.0f;
    public float camZoomGroggy = 0.8f;
    public float camZoomTime = 0.45f;

    [Header("Slides")]
    public List<TutorialSlideShow.Slide> slidesParry = new List<TutorialSlideShow.Slide>();
    public List<TutorialSlideShow.Slide> slidesCombo = new List<TutorialSlideShow.Slide>();

    bool firstAttackSeen = false;
    bool waitingParry = false;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (!player) player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (!tubo) tubo = UnityEngine.Object.FindFirstObjectByType<BossTuboController>();
#else
        if (!player) player = FindObjectOfType<PlayerController>();
        if (!tubo)   tubo   = FindObjectOfType<BossTuboController>();
#endif
        if (!cam2d && Camera.main) cam2d = Camera.main.GetComponent<CameraSimple2D>();
    }

    void OnEnable()
    {
        if (runOnce && PlayerPrefs.GetInt(playerPrefsKey, 0) == 1) { enabled = false; return; }

        if (tubo != null)
        {
            tubo.onAttackWindowOpen += OnTuboFirstAttackWindow; // Action()
            tubo.onParrySuccessDirect += OnParryDirect;          // Action()
        }
    }

    void OnDisable()
    {
        if (tubo != null)
        {
            tubo.onAttackWindowOpen -= OnTuboFirstAttackWindow;
            tubo.onParrySuccessDirect -= OnParryDirect;
        }
        PlayerController.TutorialAllow = null;
        if (spotlight) spotlight.Enable(false);
        ShowPrompt(false);
    }

    // ========== 이벤트 ==========
    void OnTuboFirstAttackWindow()
    {
        if (firstAttackSeen) return;
        firstAttackSeen = true;
        StartCoroutine(CoStartParryTutorial_Frozen());
    }

    void OnParryDirect()
    {
        if (!waitingParry) return; // 아직 패링 유도 단계 아니면 무시
        waitingParry = false;
        StartCoroutine(CoAfterParrySuccess());
    }

    // ========== 유틸 ==========
    void PauseGame(bool on) => Time.timeScale = on ? 0f : 1f;

    void ShowPrompt(bool on, string text = null)
    {
        if (!promptUI) return;
        promptUI.alpha = on ? 1f : 0f;
        promptUI.blocksRaycasts = on;
        if (on && promptText && !string.IsNullOrEmpty(text)) promptText.text = text;
    }

    void SetInputMode_ParryOnly()
        => PlayerController.TutorialAllow = (a) => a == PlayerController.PlayerAction.Parry;
    void SetInputMode_AttackOnly()
        => PlayerController.TutorialAllow = (a) => a == PlayerController.PlayerAction.Attack;
    void SetInputMode_Free()
        => PlayerController.TutorialAllow = null;

    IEnumerator CamFocus(Transform target, float zoom, float time)
    {
        if (!target || !Camera.main) yield break;
        var camTr = Camera.main.transform;
        Vector3 startPos = camTr.position;
        Vector3 endPos = new Vector3(target.position.x, target.position.y, startPos.z);
        float t = 0f;
        float startSize = Camera.main.orthographicSize;
        float endSize = startSize * zoom;

        while (t < time)
        {
            float a = t / time;
            camTr.position = Vector3.Lerp(startPos, endPos, a);
            Camera.main.orthographicSize = Mathf.Lerp(startSize, endSize, a);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        camTr.position = endPos;
        Camera.main.orthographicSize = endSize;
    }

    // ========== 1) 첫 공격 → 정지 유지 + 슬라이드 먼저 ==========
    IEnumerator CoStartParryTutorial_Frozen()
    {
        // 완전 정지
        PauseGame(true);

        // 플레이어만 스팟
        if (spotlight && player)
        {
            spotlight.SetSingleWorldSpot(player.transform, 0.24f, 0.10f);
            spotlight.Enable(true);
        }

        // 입력: 패링만 허용
        SetInputMode_ParryOnly();

        // 텍스트 가이드
        ShowPrompt(true, "패링 타이밍 안내 (C 또는 Ctrl)");

        // 슬라이드(패링편) **정지 상태에서** 바로 띄움
        if (slideUI != null)
        {
            slideUI.Open(slidesParry, () =>
            {
                // 슬라이드 닫히면: 정지 해제하고 패링을 실제로 받는다
                StartCoroutine(CoArmedForParry());
            });
        }
        else
        {
            // 슬라이드가 없다면 바로 패링 대기
            StartCoroutine(CoArmedForParry());
        }

        yield break;
    }

    // 슬라이드 닫힘 → 패링을 실제로 받는 구간 (정지 해제)
    IEnumerator CoArmedForParry()
    {
        ShowPrompt(true, "이제 패링을 성공해 봐! (C 또는 Ctrl)");
        PauseGame(false);        // 실제 게임 진행 시작
        waitingParry = true;     // OnParryDirect에서 잡는다
        yield break;
    }

    // ========== 2) 패링 성공 후 ==========
    IEnumerator CoAfterParrySuccess()
    {
        // 다시 정지하고 보스 강조 + 카메라 줌
        PauseGame(true);
        ShowPrompt(false);
        if (spotlight && tubo) spotlight.SetSingleWorldSpot(tubo.transform, 0.28f, 0.12f);
        if (tubo) yield return CamFocus(tubo.transform, camZoomGroggy, camZoomTime);

        // 콤보 슬라이드
        if (slideUI != null)
        {
            slideUI.Open(slidesCombo, () => { StartCoroutine(CoFreeFightGate()); });
        }
        else
        {
            StartCoroutine(CoFreeFightGate());
        }
    }

    // ========== 3) 보스에 다가가면 공격 튜토 (원하면 유지, 필요없으면 생략 가능) ==========
    IEnumerator CoFreeFightGate()
    {
        // 카메라 대충 복귀
        if (Camera.main)
        {
            float targetSize = 5f;
            float s = Camera.main.orthographicSize;
            float t = 0f;
            while (t < 0.35f)
            {
                Camera.main.orthographicSize = Mathf.Lerp(s, targetSize, t / 0.35f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Camera.main.orthographicSize = targetSize;
        }

        // 자유 이동으로 보스에 접근시키기
        PauseGame(false);
        SetInputMode_Free();
        if (spotlight) spotlight.Enable(false);

        // (원래 설계 유지 시) 보스에 근접하면 공격 튜토 시작
        while (player && tubo && Vector2.Distance(player.transform.position, tubo.transform.position) > approachDistance)
            yield return null;

        // 공격 튜토: 잠깐 정지 + 공격만 허용
        PauseGame(true);
        if (spotlight && player) { spotlight.SetSingleWorldSpot(player.transform, 0.24f, 0.10f); spotlight.Enable(true); }
        ShowPrompt(true, "공격으로 적을 타격! (X 또는 좌클릭)");
        SetInputMode_AttackOnly();
        yield return new WaitForSecondsRealtime(0.6f);
        PauseGame(false);

        // 적중 감지 (간단히 HP 감소 폴링)
        int prevHp = (tubo && tubo.bossHealth != null) ? tubo.bossHealth.CurrentHp : int.MaxValue;
        while (tubo && tubo.bossHealth != null && tubo.bossHealth.CurrentHp >= prevHp) yield return null;

        // 끝
        EndTutorial();
    }

    void EndTutorial()
    {
        PauseGame(false);
        SetInputMode_Free();
        if (spotlight) spotlight.Enable(false);
        ShowPrompt(false);
        if (runOnce)
        {
            PlayerPrefs.SetInt(playerPrefsKey, 1);
            PlayerPrefs.Save();
        }
        enabled = false;
    }
}
