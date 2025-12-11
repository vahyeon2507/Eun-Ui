using System;
using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class CutsceneSpeechBubble : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("말풍선 안의 텍스트 (TextMeshProUGUI)")]
    public TextMeshProUGUI textUI;

    [Header("팝인(Scale) 효과")]
    [Tooltip("처음 등장할 때의 스케일 (음수면 뒤집힌 상태에서 튀어나오는 느낌)")]
    public Vector3 hiddenScale = new Vector3(-0.2f, 0.2f, 1f);

    [Tooltip("팝인에 걸리는 시간(초)")]
    public float popDuration = 0.08f;

    public AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("타자 효과")]
    [Tooltip("글자 하나가 찍히는 간격(초)")]
    public float charInterval = 0.03f;

    // ── 런타임 상태 ──
    RectTransform _rect;
    string _fullText;
    Action _onClosed;

    bool _typingFinished;
    bool _closing;

    bool _requireClick = true;
    float _autoCloseDelay = 0f;
    float _autoTimer = 0f;

    void Awake()
    {
        _rect = transform as RectTransform;
        if (!textUI)
            textUI = GetComponentInChildren<TextMeshProUGUI>();
    }

    /// <summary>
    /// 말풍선 표시 시작.
    /// overrideText가 null/빈 문자열이면, 프리팹 안에 이미 들어있는 텍스트를 사용.
    /// </summary>
    public void Show(string overrideText, bool requireClick, float autoCloseDelay, Action onClosed)
    {
        _requireClick = requireClick;
        _autoCloseDelay = Mathf.Max(0f, autoCloseDelay);
        _onClosed = onClosed;

        // 1) 실제 사용할 텍스트 결정
        string effectiveText = overrideText;
        if (string.IsNullOrEmpty(effectiveText) && textUI != null)
        {
            // 프리팹에 미리 넣어둔 텍스트 사용
            effectiveText = textUI.text;
        }

        _fullText = effectiveText ?? "";
        _typingFinished = false;
        _closing = false;
        _autoTimer = 0f;

        // 2) 텍스트 초기화 (타자기 효과를 위해 비우기)
        if (textUI)
            textUI.text = "";

        // 3) 스케일 초기화
        if (_rect)
            _rect.localScale = hiddenScale;

        StopAllCoroutines();
        StartCoroutine(CoRun());
    }

    IEnumerator CoRun()
    {
        // 1) 팝인 스케일
        if (_rect && popDuration > 0f)
        {
            float t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / popDuration);
                float k = (popCurve != null) ? popCurve.Evaluate(p) : p;
                _rect.localScale = Vector3.Lerp(hiddenScale, Vector3.one, k);
                yield return null;
            }
            _rect.localScale = Vector3.one;
        }
        else if (_rect)
        {
            _rect.localScale = Vector3.one;
        }

        // 2) 타자기 효과
        if (textUI && !string.IsNullOrEmpty(_fullText))
        {
            textUI.text = "";
            for (int i = 0; i < _fullText.Length; i++)
            {
                textUI.text += _fullText[i];
                if (charInterval > 0f)
                    yield return new WaitForSeconds(charInterval);
                else
                    yield return null;
            }
        }
        else if (textUI)
        {
            textUI.text = _fullText;
        }

        _typingFinished = true;
        _autoTimer = 0f;
    }

    void Update()
    {
        if (!_typingFinished || _closing) return;

        // 1) 클릭으로 넘기는 말풍선
        if (_requireClick)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                Close();
            return;
        }

        // 2) 자동으로 넘어가는 말풍선
        if (_autoCloseDelay > 0f)
        {
            _autoTimer += Time.deltaTime;
            if (_autoTimer >= _autoCloseDelay)
                Close();
        }
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;

        _onClosed?.Invoke();
        _onClosed = null;

        Destroy(gameObject);
    }
}
