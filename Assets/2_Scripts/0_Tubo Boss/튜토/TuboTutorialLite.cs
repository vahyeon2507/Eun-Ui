using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TuboTutorialLite : MonoBehaviour
{
    [Header("Refs")]
    public BossTuboController tubo;                 // 이벤트: onAttackWindowOpen, onParrySuccessDirect
    public PlayerController player;                 // 입력락: PlayerController.TutorialAllow 사용
    public SpotlightOverlayController overlay;      // 화면 어둡게 + 스팟
    public TutorialSlideShow slideUI;               // 없으면 자동 생략
    public CanvasGroup promptUI; public TMP_Text promptText;

    [Header("Slides")]
    public List<TutorialSlideShow.Slide> slidesParry = new List<TutorialSlideShow.Slide>();
    public List<TutorialSlideShow.Slide> slidesCombo = new List<TutorialSlideShow.Slide>();

    [Header("Settings")]
    public bool runOnce = true;
    public string playerPrefsKey = "TuboTutorialDone";
    public float camZoomGroggy = 0.8f;
    public float camZoomTime = 0.45f;

    bool firstAttackSeen = false;
    bool waitingParry = false;
    bool finished = false;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (!player) player = Object.FindFirstObjectByType<PlayerController>();
        if (!tubo) tubo = Object.FindFirstObjectByType<BossTuboController>();
#else
        if (!player) player = FindObjectOfType<PlayerController>();
        if (!tubo)   tubo   = FindObjectOfType<BossTuboController>();
#endif
    }

    void OnEnable()
    {
        if (runOnce && PlayerPrefs.GetInt(playerPrefsKey, 0) == 1) { enabled = false; return; }
        if (tubo != null)
        {
            tubo.onAttackWindowOpen += OnTuboFirstAttackWindow;
            tubo.onParrySuccessDirect += OnParryDirect;
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
        SetPrompt(false);
        if (overlay) overlay.Enable(false);
    }

    // ===== 이벤트 =====
    void OnTuboFirstAttackWindow()
    {
        if (finished || firstAttackSeen) return;
        firstAttackSeen = true;
        StartCoroutine(CoStartParryFrozen());
    }

    void OnParryDirect()
    {
        if (!waitingParry || finished) return;
        waitingParry = false;
        StartCoroutine(CoAfterParrySuccess());
    }

    // ===== 유틸 =====
    void Pause(bool on) => Time.timeScale = on ? 0f : 1f;

    void GateParryOnly() => PlayerController.TutorialAllow = (a) => a == PlayerController.PlayerAction.Parry;
    void GateAttackOnly() => PlayerController.TutorialAllow = (a) => a == PlayerController.PlayerAction.Attack;
    void GateFree() => PlayerController.TutorialAllow = null;

    void SetPrompt(bool on, string text = null)
    {
        if (!promptUI) return;
        promptUI.alpha = on ? 1f : 0f;
        promptUI.blocksRaycasts = on;
        if (on && promptText && !string.IsNullOrEmpty(text)) promptText.text = text;
    }

    IEnumerator CamFocus(Transform target, float zoom, float time)
    {
        if (!target || !Camera.main) yield break;
        var camTr = Camera.main.transform;
        Vector3 startPos = camTr.position;
        Vector3 endPos = new Vector3(target.position.x, target.position.y, startPos.z);
        float t = 0f;
        float s0 = Camera.main.orthographicSize, s1 = s0 * zoom;
        while (t < time)
        {
            float a = t / time;
            camTr.position = Vector3.Lerp(startPos, endPos, a);
            Camera.main.orthographicSize = Mathf.Lerp(s0, s1, a);
            t += Time.unscaledDeltaTime; // 정지 중에도 진행
            yield return null;
        }
        camTr.position = endPos;
        Camera.main.orthographicSize = s1;
    }

    // ===== 1) 첫 공격: 정지 유지 + 슬라이드 먼저 =====
    IEnumerator CoStartParryFrozen()
    {
        Pause(true);

        if (overlay && player)
        {
            overlay.SetSingleWorldSpot(player.transform, 0.24f, 0.10f);
            overlay.Enable(true);
        }
        SetPrompt(true, "패링 타이밍 안내 (C 또는 Ctrl)");
        GateParryOnly(); // 실제 패링은 슬라이드 닫힌 뒤부터 허용

        // 슬라이드(패링편) — 정지 상태에서 띄움
        if (slideUI)
        {
            bool done = false;
            slideUI.Open(slidesParry, () => done = true);
            // 슬라이드 닫힐 때까지 정지 유지
            while (!done) yield return null;
        }

        // 슬라이드 닫힘 → 패링 받도록 해제
        SetPrompt(true, "이제 패링을 성공해 봐! (C 또는 Ctrl)");
        Pause(false);
        waitingParry = true;
    }

    // ===== 2) 패링 성공: 다시 정지 + 보스 강조 + 줌 + 콤보 슬라이드 =====
    IEnumerator CoAfterParrySuccess()
    {
        Pause(true);
        SetPrompt(false);
        if (overlay && tubo) overlay.SetSingleWorldSpot(tubo.transform, 0.28f, 0.12f);
        if (tubo) yield return CamFocus(tubo.transform, camZoomGroggy, camZoomTime);

        if (slideUI)
        {
            bool done = false;
            slideUI.Open(slidesCombo, () => done = true);
            while (!done) yield return null;
        }

        EndTutorial();
    }

    void EndTutorial()
    {
        finished = true;
        Pause(false);
        GateFree();
        if (overlay) overlay.Enable(false);
        SetPrompt(false);
        if (runOnce) { PlayerPrefs.SetInt(playerPrefsKey, 1); PlayerPrefs.Save(); }
        enabled = false;
    }
}
