using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class TutorialSlideShow : MonoBehaviour
{
    [Serializable]
    public struct Slide
    {
        public VideoClip video;
        [TextArea] public string caption;
    }

    // 진짜 구현 전까지는 즉시 닫히게 해 둔다.
    public void Open(List<Slide> slides, Action onClose)
    {
        onClose?.Invoke();
    }
}
