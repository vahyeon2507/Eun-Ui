using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverController : MonoBehaviour
{
    public static GameOverController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Image blackFade;           // 화면 전체 검은 Image (알파 0 시작)
    [SerializeField] private CanvasGroup panelGroup;    // 게임오버 패널(버튼 포함). alpha=0, interactable=false 시작

    [Header("Timings (realtime)")]
    [SerializeField] private float deathLead = 0.35f;   // 죽음 트리거 후, 슬로모션/페이드 시작 전 잠깐 기다림(애니 먼저 보이게)
    [SerializeField] private float slowInDuration = 0.35f; // 1 -> slowTarget까지 천천히 감속
    [SerializeField] private float slowTarget = 0.15f;     // 느려질 목표 Time.timeScale
    [SerializeField] private float fadeDuration = 0.8f;    // 검은화면 페이드 시간
    [SerializeField] private float panelDelay = 0.2f;      // 완전 암전 후 패널 띄우기 지연
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    bool running;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (blackFade)
        {
            var c = blackFade.color; c.a = 0f; blackFade.color = c;
        }
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }

    public static void TryBeginFrom(GameObject _)
    {
        if (Instance != null) Instance.BeginGameOver();
    }

    public void BeginGameOver()
    {
        if (running) return;
        if (!blackFade || !panelGroup)
        {
            Debug.LogError("[GameOverController] References not assigned!");
            return;
        }
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        running = true;

        // 1) 죽음 애니메이션 먼저 보이도록 잠깐 대기(실시간)
        yield return new WaitForSecondsRealtime(deathLead);

        // 2) 슬로모션으로 감속 (실시간 기준으로 Lerp)
        float t = 0f;
        float startTs = Mathf.Max(0.0001f, Time.timeScale);
        while (t < slowInDuration)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(startTs, slowTarget, t / slowInDuration);
            yield return null;
        }
        Time.timeScale = slowTarget;

        // 3) 화면 암전
        t = 0f;
        var c0 = blackFade.color;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeCurve.Evaluate(Mathf.Clamp01(t / fadeDuration));
            var c = c0; c.a = Mathf.Lerp(0f, 1f, k);
            blackFade.color = c;
            yield return null;
        }
        // 암전 후 잠깐 홀드
        yield return new WaitForSecondsRealtime(panelDelay);

        // 4) 게임오버 패널 등장(알파 페이드 + 인터랙션 on), 최종 정지
        yield return StartCoroutine(FadeInPanel(panelGroup, 0.25f));
        Time.timeScale = 0f; // 완전 정지
    }

    IEnumerator FadeInPanel(CanvasGroup g, float dur)
    {
        g.gameObject.SetActive(true);
        g.interactable = false;
        g.blocksRaycasts = false;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(0f, 1f, t / dur);
            yield return null;
        }
        g.alpha = 1f;
        g.interactable = true;
        g.blocksRaycasts = true;
    }

    // ===== 버튼 연결용 =====
    public void OnRestartButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnQuitButton()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    // (선택) 메인메뉴로 가기 등이 필요하면 여기에 메서드 추가
}
