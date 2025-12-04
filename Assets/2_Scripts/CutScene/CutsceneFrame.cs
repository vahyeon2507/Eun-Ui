using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

// 컷씬 프레임의 콘텐츠 타입 정의
public enum FrameContentType
{
    StaticImage,        // 정적인 그림 한 장
    SpriteAnimation,    // 유니티 Animator를 사용하는 애니메이션 프리팹
    TimelineSequence    // 유니티 타임라인을 사용한 복잡한 연출
}

// 하나의 컷씬 장면 데이터를 담는 클래스
[System.Serializable]
public class CutsceneFrame
{
    [Header("Content Type Selection")]
    [Tooltip("이 프레임에서 어떤 종류의 콘텐츠를 보여줄지 선택하세요.")]
    public FrameContentType contentType = FrameContentType.StaticImage;

    // --- 공통 필드 ---

    [Header("Subtitle & Timing")]
    [TextArea(3, 5)]
    public string subtitleText;         // 자막 내용
    [Tooltip("사용자 입력(Skip)을 받기 전까지 최소한 머무를 시간 (초)")]
    public float minDuration = 3f;     // 최소 대기 시간

    // --- 1. Static Image 필드 ---

    [Header("1. Static Image")]
    [Tooltip("Static Image 선택 시, 표시할 그림 스프라이트")]
    public Sprite illustrationSprite;

    // --- 2. Sprite Animation 필드 (수정됨) ---

    [Header("2. Sprite Animation (Prefab)")]
    [Tooltip("Sprite Animation 선택 시, 씬에 나타낼 애니메이션 오브젝트 프리팹을 할당합니다. 이 프리팹에는 Animator 컴포넌트가 있어야 합니다.")]
    public GameObject animatedPrefab;

    // --- 3. Timeline Sequence 필드 ---

    [Header("3. Timeline Sequence")]
    [Tooltip("Timeline Sequence 선택 시, 재생할 Timeline Asset")]
    public PlayableAsset timelineClip;
}

