using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections.Generic;

public class TutorialSlideShow : MonoBehaviour
{
    [System.Serializable]
    public class Slide
    {
        public VideoClip video;
        [TextArea] public string caption;
    }

    public VideoPlayer player;
    public TMP_Text caption;
    public Button nextBtn, prevBtn, closeBtn;
    public List<Slide> slides = new();
    int idx = 0;
    System.Action _onClosed;

    void Awake()
    {
        nextBtn.onClick.AddListener(() => Go(+1));
        prevBtn.onClick.AddListener(() => Go(-1));
        closeBtn.onClick.AddListener(() => { gameObject.SetActive(false); _onClosed?.Invoke(); });
        var g = GetComponent<CanvasGroup>();
        if (g) { g.alpha = 0f; g.interactable = false; g.blocksRaycasts = false; }

    }

    public void Open(List<Slide> data, System.Action onClosed = null)
    {
        slides = data;
        _onClosed = onClosed;
        idx = 0;
        gameObject.SetActive(true);
        Show();
    }

    void Go(int d) { idx = Mathf.Clamp(idx + d, 0, slides.Count - 1); Show(); }

    void Show()
    {
        if (slides == null || slides.Count == 0) { gameObject.SetActive(false); _onClosed?.Invoke(); return; }
        var s = slides[idx];
        if (player) { player.clip = s.video; player.Play(); }
        if (caption) caption.text = s.caption;
        prevBtn.interactable = idx > 0;
        nextBtn.interactable = idx < slides.Count - 1;
    }
}
