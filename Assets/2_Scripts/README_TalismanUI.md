# 부적 선택 UI 시스템 사용법

## 🎯 개요
플레이어 머리 위에 표시되는 부적 선택 UI 시스템입니다.
롤의 텔레포트 UI와 유사한 스타일로 5개의 오행 부적을 선택할 수 있습니다.

## 🎮 조작법
- **Tab**: UI 토글 (켜기/끄기)
- **Q**: 이전 부적 선택 (UI가 켜져있을 때만)
- **E**: 다음 부적 선택 (UI가 켜져있을 때만)
- **D**: 부적 발사 (기존 우클릭에서 변경)
- **C**: 패링 (기존 유지)

## 🛠️ 설정 방법

### 1. TalismanSelectionUI 컴포넌트 추가
```
플레이어 GameObject에 TalismanSelectionUI 스크립트 추가
```

### 2. 부적 스프라이트 할당
```
Inspector에서 Talisman Sprites 배열에 5개 스프라이트 할당:
[0] Fire (화)
[1] Earth (토)
[2] Water (수)
[3] Metal (금)
[4] Wood (목)
```

### 3. UI 설정 조정
```
- Offset From Player: 플레이어로부터의 오프셋 (기본: 0, 2, 0)
- Side Scale: 좌우 부적 크기 비율 (기본: 0.7)
- Side Alpha: 좌우 부적 투명도 (기본: 0.75)
- Animation Speed: 부적 변경 애니메이션 속도 (기본: 5)
```

## 🔧 자동 기능
- **자동 UI 생성**: Canvas나 Panel이 없으면 자동으로 생성
- **자동 참조 찾기**: PlayerTalismanUnified와 자동 연동
- **동기화**: 기존 부적 시스템과 실시간 동기화
- **빌보드 효과**: UI가 항상 카메라를 향함

## 🎨 UI 구조
```
TalismanSelectionCanvas (WorldSpace)
└── TalismanPanel
    ├── LeftTalisman (이전 부적, 75% 투명도, 70% 크기)
    ├── CenterTalisman (현재 부적, 100% 투명도, 100% 크기)
    └── RightTalisman (다음 부적, 75% 투명도, 70% 크기)
```

## 🐛 디버그 기능
- **Context Menu**: Inspector에서 우클릭으로 테스트 가능
  - "UI 토글 테스트"
  - "다음 부적 테스트"
  - "이전 부적 테스트"

## 📝 주요 변경사항
1. **PlayerTalismanUnified**: D키 발사 추가, UI 시스템과 연동
2. **PlayerController**: C키 패링 기능 그대로 유지
3. **TalismanSelectionUI**: 새로운 부적 선택 UI 시스템

## 🔄 호환성
- 기존 부적 시스템과 완전 호환
- UI가 없어도 기존 Q/E 키로 부적 변경 가능
- 기존 우클릭 발사도 여전히 작동
