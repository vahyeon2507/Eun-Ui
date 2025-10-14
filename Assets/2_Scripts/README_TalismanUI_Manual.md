# 부적 선택 UI 수동 생성 가이드

## 🎯 수동 생성 방법

### 1단계: Canvas 생성
```
1. Hierarchy에서 우클릭 → UI → Canvas
2. 이름을 "TalismanSelectionCanvas"로 변경
3. Canvas 컴포넌트 설정:
   - Render Mode: World Space
   - Event Camera: Main Camera 할당
4. CanvasScaler 컴포넌트 추가:
   - UI Scale Mode: Constant Pixel Size
   - Scale Factor: 1
```

### 2단계: UI Panel 생성
```
1. TalismanSelectionCanvas 하위에 빈 GameObject 생성
2. 이름을 "TalismanPanel"로 변경
3. RectTransform 컴포넌트 추가
4. 크기 설정: Width 300, Height 100
```

### 3단계: 부적 이미지 3개 생성

#### LeftTalisman (이전 부적)
```
1. TalismanPanel 하위에 UI → Image 생성
2. 이름을 "LeftTalisman"으로 변경
3. RectTransform 설정:
   - Anchored Position: (-100, 0, 0)
   - Size: 80 x 80
   - Scale: (0.7, 0.7, 1)
4. Image 컴포넌트:
   - Color Alpha: 0.75
```

#### CenterTalisman (현재 부적)
```
1. TalismanPanel 하위에 UI → Image 생성
2. 이름을 "CenterTalisman"으로 변경
3. RectTransform 설정:
   - Anchored Position: (0, 0, 0)
   - Size: 80 x 80
   - Scale: (1, 1, 1)
4. Image 컴포넌트:
   - Color Alpha: 1
```

#### RightTalisman (다음 부적)
```
1. TalismanPanel 하위에 UI → Image 생성
2. 이름을 "RightTalisman"으로 변경
3. RectTransform 설정:
   - Anchored Position: (100, 0, 0)
   - Size: 80 x 80
   - Scale: (0.7, 0.7, 1)
4. Image 컴포넌트:
   - Color Alpha: 0.75
```

### 4단계: TalismanSelectionUI 연결
```
1. 플레이어 GameObject에 TalismanSelectionUI 컴포넌트 추가
2. Inspector에서 참조 연결:
   - UI Canvas: TalismanSelectionCanvas 드래그
   - UI Panel: TalismanPanel 드래그
   - Left Talisman: LeftTalisman 드래그
   - Center Talisman: CenterTalisman 드래그
   - Right Talisman: RightTalisman 드래그
   - Talisman Sprites: 5개 부적 스프라이트 할당
```

### 5단계: 테스트
```
1. Play 버튼 누르기
2. Console에서 "[TalismanUI] Start() 호출됨" 확인
3. Tab 키로 UI 토글 테스트
4. Q/E 키로 부적 변경 테스트
5. D 키로 부적 발사 테스트
```

## 🎨 최종 UI 구조
```
TalismanSelectionCanvas (WorldSpace)
└── TalismanPanel
    ├── LeftTalisman (이전, 70% 크기, 75% 투명도)
    ├── CenterTalisman (현재, 100% 크기, 100% 투명도)
    └── RightTalisman (다음, 70% 크기, 75% 투명도)
```

## 🔧 설정값
- **Offset From Player**: (0, 2, 0) - 플레이어 머리 위 2유닛
- **Side Scale**: 0.7 - 좌우 부적 크기
- **Side Alpha**: 0.75 - 좌우 부적 투명도
- **Animation Speed**: 5 - 부적 변경 애니메이션 속도
