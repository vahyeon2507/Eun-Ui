using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TalismanSelectionUI : MonoBehaviour
{
    [Header("UI 연결")]
    public Canvas uiCanvas;
    public GameObject uiPanel;
    public Image leftTalisman, centerTalisman, rightTalisman;
    public Sprite[] talismanSprites;

    [Header("플레이어")]
    public Transform player;
    public Vector3 offset = new Vector3(0, 2f, 0);

    [Header("입력키")]
    public KeyCode toggleKey = KeyCode.Tab;
    public KeyCode prevKey = KeyCode.Q;
    public KeyCode nextKey = KeyCode.E;

    [Header("애니메이션")]
    public float moveDistance = 150f;
    public float moveDuration = 0.3f;
    public float sideScale = 0.7f;
    public float sideAlpha = 0.5f;

    private int currentIndex = 0;
    private bool isVisible = false;
    private bool isAnimating = false;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        uiCanvas.renderMode = RenderMode.WorldSpace;
        uiCanvas.worldCamera = mainCam;
        uiPanel.SetActive(false);
        UpdateDisplay();
    }

    void Update()
    {
        if (player)
        {
            uiCanvas.transform.position = player.position + offset;
            uiCanvas.transform.rotation = Quaternion.LookRotation(uiCanvas.transform.position - mainCam.transform.position);
        }

        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            uiPanel.SetActive(isVisible);
        }

        if (!isVisible || isAnimating) return;

        if (Input.GetKeyDown(prevKey)) StartCoroutine(Slide(-1));
        else if (Input.GetKeyDown(nextKey)) StartCoroutine(Slide(1));
    }

    IEnumerator Slide(int dir)
    {
        isAnimating = true;

        // 현재 위치 저장
        Vector3 leftStart = leftTalisman.rectTransform.localPosition;
        Vector3 centerStart = centerTalisman.rectTransform.localPosition;
        Vector3 rightStart = rightTalisman.rectTransform.localPosition;

        // 목표 위치 계산 (dir 방향에 따라)
        Vector3 leftEnd = leftStart + Vector3.left * dir * moveDistance;
        Vector3 centerEnd = centerStart + Vector3.left * dir * moveDistance;
        Vector3 rightEnd = rightStart + Vector3.left * dir * moveDistance;

        // 다음 인덱스 계산
        int nextIndex = (currentIndex + dir + talismanSprites.Length) % talismanSprites.Length;

        // 슬라이드 애니메이션
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;

            // 중앙 → 사이드
            centerTalisman.rectTransform.localPosition = Vector3.Lerp(centerStart, centerEnd, t);
            centerTalisman.rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * sideScale, t);
            SetAlpha(centerTalisman, Mathf.Lerp(1f, sideAlpha, t));

            // 사이드 → 중앙
            Image incoming = (dir > 0) ? rightTalisman : leftTalisman;
            Vector3 incomingStart = (dir > 0) ? rightStart : leftStart;
            incoming.rectTransform.localPosition = Vector3.Lerp(incomingStart, centerStart, t);
            incoming.rectTransform.localScale = Vector3.Lerp(Vector3.one * sideScale, Vector3.one, t);
            SetAlpha(incoming, Mathf.Lerp(sideAlpha, 1f, t));

            // 반대편 사이드 위치 유지
            Image opposite = (dir > 0) ? leftTalisman : rightTalisman;
            opposite.rectTransform.localPosition = Vector3.Lerp(opposite.rectTransform.localPosition, opposite.rectTransform.localPosition, t);

            yield return null;
        }

        // 인덱스 업데이트 후 스프라이트 적용
        currentIndex = nextIndex;
        UpdateDisplay();
        isAnimating = false;
    }

    void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    void UpdateDisplay()
    {
        if (talismanSprites.Length == 0) return;

        int leftIdx = (currentIndex - 1 + talismanSprites.Length) % talismanSprites.Length;
        int rightIdx = (currentIndex + 1) % talismanSprites.Length;

        centerTalisman.sprite = talismanSprites[currentIndex];
        leftTalisman.sprite = talismanSprites[leftIdx];
        rightTalisman.sprite = talismanSprites[rightIdx];

        centerTalisman.rectTransform.localPosition = Vector3.zero;
        leftTalisman.rectTransform.localPosition = Vector3.left * moveDistance;
        rightTalisman.rectTransform.localPosition = Vector3.right * moveDistance;

        centerTalisman.rectTransform.localScale = Vector3.one;
        leftTalisman.rectTransform.localScale = rightTalisman.rectTransform.localScale = Vector3.one * sideScale;

        SetAlpha(centerTalisman, 1f);
        SetAlpha(leftTalisman, sideAlpha);
        SetAlpha(rightTalisman, sideAlpha);
    }
}

