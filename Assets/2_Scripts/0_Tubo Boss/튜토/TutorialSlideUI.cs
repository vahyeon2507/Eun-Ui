using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class TutorialSlideUI : MonoBehaviour
{
    [System.Serializable]
    public class SlideData
    {
        public Sprite image;              // 이미지 슬라이드
        public VideoClip videoClip;       // 동영상 슬라이드(옵션)
        [TextArea(2, 4)]
        public string description;        // 설명 텍스트
    }

    [Header("UI")]
    public Image slideImage;             // 이미지 표시용
    public VideoPlayer videoPlayer;      // 동영상이 필요하면 연결 (없으면 null로 둬도 됨)
    public Text descriptionText;         // 설명 텍스트

    [Header("Slides")]
    public SlideData[] slides;

    [Header("Input")]
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode prevKey = KeyCode.LeftArrow;

    // 스페이스바도 다음으로 쓰고 싶으면 사용
    public bool allowSpaceAsNext = true;

    public System.Action OnSlidesFinished;

    int _index = 0;
    bool _active = false;

    void OnEnable()
    {
        if (_active)
            Refresh();
    }

    public void Begin()
    {
        if (slides == null || slides.Length == 0)
        {
            Debug.LogWarning("[TutorialSlideUI] 슬라이드가 비어 있습니다.");
            Finish();
            return;
        }

        _index = 0;
        _active = true;
        gameObject.SetActive(true);
        Refresh();
    }

    void Refresh()
    {
        if (slides == null || slides.Length == 0) return;
        if (_index < 0 || _index >= slides.Length) return;

        var s = slides[_index];

        // 이미지 / 비디오 토글
        if (videoPlayer != null && s.videoClip != null)
        {
            // 비디오 모드
            videoPlayer.gameObject.SetActive(true);
            if (slideImage != null) slideImage.gameObject.SetActive(false);

            if (videoPlayer.clip != s.videoClip)
            {
                videoPlayer.clip = s.videoClip;
                videoPlayer.Play();
            }
        }
        else
        {
            // 이미지 모드
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.gameObject.SetActive(false);
            }

            if (slideImage != null)
            {
                slideImage.gameObject.SetActive(true);
                slideImage.sprite = s.image;
            }
        }

        if (descriptionText != null)
            descriptionText.text = s.description;
    }

    void Update()
    {
        if (!_active) return;

        bool nextPressed =
            Input.GetKeyDown(nextKey) ||
            (allowSpaceAsNext && Input.GetKeyDown(KeyCode.Space)) ||
            Input.GetKeyDown(KeyCode.D);       // 서비스로 D도 같이

        bool prevPressed =
            Input.GetKeyDown(prevKey) ||
            Input.GetKeyDown(KeyCode.A);       // A는 뒤로가기

        if (nextPressed)
        {
            Next();
        }
        else if (prevPressed)
        {
            Previous();
        }
    }

    public void Next()
    {
        if (slides == null || slides.Length == 0)
        {
            Finish();
            return;
        }

        if (_index < slides.Length - 1)
        {
            _index++;
            Refresh();
        }
        else
        {
            // 마지막 슬라이드 상태에서 한번 더 → 종료
            Finish();
        }
    }

    public void Previous()
    {
        if (slides == null || slides.Length == 0) return;
        if (_index <= 0) return; // 첫 슬라이드면 무시

        _index--;
        Refresh();
    }

    void Finish()
    {
        if (!_active) return;
        _active = false;

        // 비디오 꺼주기
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);

        OnSlidesFinished?.Invoke();
    }
}
