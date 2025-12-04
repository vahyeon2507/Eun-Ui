using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TalismanSelectionUI : MonoBehaviour
{
    // ───────────────────────────────── UI 연결
    [Header("UI 연결")]
    public Canvas uiCanvas;
    public GameObject uiPanel;
    public Image leftTalisman, centerTalisman, rightTalisman;
    [Tooltip("표시용 아이콘 배열(순서는 자유). typeOrder로 실제 타입 매핑")]
    public Sprite[] talismanSprites;

    // ───────────────────────────────── 월드 배치
    [Header("플레이어 / 배치")]
    public Transform player;
    public Vector3 offset = new Vector3(0, 2f, 0);
    [Tooltip("3D라면 카메라를 바라보게, 2D라면 false로 두고 회전 고정")]
    public bool faceCamera = true;

    // ───────────────────────────────── 입력
    [Header("입력키")]
    [Tooltip("UI 열고/닫기")]
    public KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("왼쪽/오른쪽 선택 이동")]
    public KeyCode prevKey = KeyCode.Q;
    public KeyCode nextKey = KeyCode.E;
    [Tooltip("부적 발사 전용 키(토글 없이, 누를 때마다 발사)")]
    public KeyCode fireKey = KeyCode.D;

    // ───────────────────────────────── 슬라이드 연출
    [Header("애니메이션")]
    public float moveDistance = 150f;
    public float moveDuration = 0.30f;
    public float sideScale = 0.70f;
    public float sideAlpha = 0.50f;

    // ───────────────────────────────── 매핑(인덱스→타입)
    [Header("타입 매핑")]
    [Tooltip("UI 인덱스(아이콘) → 실제 부적 타입 매핑. talismanSprites 길이와 동일 권장")]
    public TalismanType[] typeOrder = new TalismanType[]
    {
        TalismanType.Fire, TalismanType.Earth, TalismanType.Water,
        TalismanType.Metal, TalismanType.Wood
    };

    // ───────────────────────────────── 이벤트
    [Serializable] public class TalismanTypeEvent : UnityEngine.Events.UnityEvent<TalismanType> { }
    [Header("이벤트")]
    [Tooltip("선택이 바뀔 때 알려줌(UI 시작 시 1회 발생)")]
    public TalismanTypeEvent onChanged;
    [Tooltip("발사키를 누르면 현재 타입으로 발사 요청")]
    public TalismanTypeEvent onRequestFire;

    // ───────────────────────────────── 내부 상태
    int currentIndex = 0;
    bool isVisible = false;
    bool isAnimating = false;
    Camera mainCam;

    // 외부 조회용
    public bool Visible => isVisible;
    public TalismanType CurrentType
    {
        get
        {
            if (typeOrder == null || typeOrder.Length == 0)
                return TalismanType.Fire;
            int i = Mathf.Clamp(currentIndex, 0, typeOrder.Length - 1);
            return typeOrder[i];
        }
    }

    void Start()
    {
        mainCam = Camera.main;

        if (uiCanvas)
        {
            uiCanvas.renderMode = RenderMode.WorldSpace;
            uiCanvas.worldCamera = mainCam;
        }

        // 배열 길이 보호(매핑/스프라이트 상호 보정)
        if (typeOrder == null || typeOrder.Length == 0)
        {
            typeOrder = new TalismanType[] { TalismanType.Fire };
        }
        if (talismanSprites == null || talismanSprites.Length == 0)
        {
            talismanSprites = new Sprite[typeOrder.Length];
        }

        // 패널 초기 비활성
        if (uiPanel) uiPanel.SetActive(false);

        UpdateDisplay();
        NotifyChanged(); // 시작 시 1회 통지
    }

    void Update()
    {
        // 월드 위치/방향
        if (player && uiCanvas)
        {
            uiCanvas.transform.position = player.position + offset;
            if (faceCamera && mainCam)
            {
                Vector3 toCam = uiCanvas.transform.position - mainCam.transform.position;
                uiCanvas.transform.rotation = Quaternion.LookRotation(toCam);
            }
            else
            {
                uiCanvas.transform.rotation = Quaternion.identity; // 2D 고정
            }
        }

        // 열고/닫기
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            if (uiPanel) uiPanel.SetActive(isVisible);
        }

        if (!isVisible || isAnimating)
            return;

        // 좌/우 이동
        if (Input.GetKeyDown(prevKey)) StartCoroutine(Slide(-1));
        else if (Input.GetKeyDown(nextKey)) StartCoroutine(Slide(+1));

        // 발사 요청(토글 없이, 누를 때마다)
        if (Input.GetKeyDown(fireKey))
        {
            onRequestFire?.Invoke(CurrentType);
        }
    }

    IEnumerator Slide(int dir)
    {
        isAnimating = true;

        // 현재 로컬 위치/스케일/알파 기준
        Vector3 leftStart = leftTalisman.rectTransform.localPosition;
        Vector3 centerStart = centerTalisman.rectTransform.localPosition;
        Vector3 rightStart = rightTalisman.rectTransform.localPosition;

        Vector3 leftEnd = leftStart + Vector3.left * dir * moveDistance;
        Vector3 centerEnd = centerStart + Vector3.left * dir * moveDistance;
        Vector3 rightEnd = rightStart + Vector3.left * dir * moveDistance;

        // dir>0 : 오른쪽이 중앙으로, dir<0 : 왼쪽이 중앙으로
        Image incoming = (dir > 0) ? rightTalisman : leftTalisman;
        Vector3 incomingStart = (dir > 0) ? rightStart : leftStart;

        Image outgoing = (dir > 0) ? leftTalisman : rightTalisman;
        Vector3 outgoingStart = (dir > 0) ? leftStart : rightStart;
        Vector3 outgoingEnd = outgoingStart + Vector3.left * dir * moveDistance;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / moveDuration);

            // 중앙 → 사이드
            centerTalisman.rectTransform.localPosition = Vector3.Lerp(centerStart, centerEnd, u);
            centerTalisman.rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * sideScale, u);
            SetAlpha(centerTalisman, Mathf.Lerp(1f, sideAlpha, u));

            // 사이드(들어오는 쪽) → 중앙
            incoming.rectTransform.localPosition = Vector3.Lerp(incomingStart, centerStart, u);
            incoming.rectTransform.localScale = Vector3.Lerp(Vector3.one * sideScale, Vector3.one, u);
            SetAlpha(incoming, Mathf.Lerp(sideAlpha, 1f, u));

            // 반대편 사이드(밀려나는 쪽) 사이드 유지 이동
            outgoing.rectTransform.localPosition = Vector3.Lerp(outgoingStart, outgoingEnd, u);
            outgoing.rectTransform.localScale = Vector3.one * sideScale; // 사이드 크기 유지
            SetAlpha(outgoing, sideAlpha);                                   // 사이드 알파 유지

            yield return null;
        }

        // 인덱스 갱신 후 아이콘 리셋
        currentIndex = (currentIndex + dir + talismanSprites.Length) % talismanSprites.Length;
        UpdateDisplay();
        NotifyChanged();

        isAnimating = false;
    }

    void UpdateDisplay()
    {
        if (talismanSprites == null || talismanSprites.Length == 0) return;

        int n = talismanSprites.Length;
        int leftIdx = (currentIndex - 1 + n) % n;
        int rightIdx = (currentIndex + 1) % n;

        centerTalisman.sprite = talismanSprites[currentIndex];
        leftTalisman.sprite = talismanSprites[leftIdx];
        rightTalisman.sprite = talismanSprites[rightIdx];

        // 위치 리셋
        centerTalisman.rectTransform.localPosition = Vector3.zero;
        leftTalisman.rectTransform.localPosition = Vector3.left * moveDistance;
        rightTalisman.rectTransform.localPosition = Vector3.right * moveDistance;

        // 스케일/알파 리셋
        centerTalisman.rectTransform.localScale = Vector3.one;
        leftTalisman.rectTransform.localScale = Vector3.one * sideScale;
        rightTalisman.rectTransform.localScale = Vector3.one * sideScale;

        SetAlpha(centerTalisman, 1f);
        SetAlpha(leftTalisman, sideAlpha);
        SetAlpha(rightTalisman, sideAlpha);
    }

    void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    void NotifyChanged()
    {
        onChanged?.Invoke(CurrentType);
    }

    // 외부에서 강제 지정하고 싶을 때(선택)
    public void SetIndex(int idx)
    {
        if (talismanSprites == null || talismanSprites.Length == 0) return;
        currentIndex = ((idx % talismanSprites.Length) + talismanSprites.Length) % talismanSprites.Length;
        UpdateDisplay();
        NotifyChanged();
    }

    public void SetType(TalismanType type)
    {
        // typeOrder에서 해당 타입의 인덱스를 찾아 currentIndex로 설정
        int idx = 0;
        for (int i = 0; i < typeOrder.Length; i++)
        {
            if (typeOrder[i] == type) { idx = i; break; }
        }
        SetIndex(idx);
    }
}
