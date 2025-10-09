using UnityEngine;

/// <summary>
/// 스프라이트 캔버스/피벗이 달라도, 원하는 기준 피벗(예: 하단 중앙)을 고정시켜
/// 그래픽 위치가 흔들리지 않게 보정.
/// Player(루트) 밑의 Graphics(GameObject)에 붙이세요.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SpritePivotFixer : MonoBehaviour
{
    public SpriteRenderer sr;
    [Tooltip("고정하고 싶은 기준 피벗 (0~1). 예: 하단 중앙 (0.5, 0)")]
    public Vector2 targetPivotNormalized = new Vector2(0.5f, 0f);
    [Tooltip("Pixels Per Unit. sr.sprite에서 자동 추출하지만 수동 지정 가능.")]
    public float overridePPU = 0f;
    [Tooltip("변화 감지 주기(런타임). 0이면 매 프레임 체크")]
    public float checkInterval = 0f;

    Sprite _prevSprite;
    float _timer;

    void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable() => Refresh();

    void Update()
    {
#if UNITY_EDITOR
        // 에디터에서도 바로 보이도록
        if (!Application.isPlaying) { Refresh(); return; }
#endif
        if (checkInterval <= 0f) { if (sr && sr.sprite != _prevSprite) Refresh(); }
        else
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) { _timer = checkInterval; Refresh(); }
        }
    }

    public void Refresh()
    {
        if (!sr) return;
        var sp = sr.sprite;
        if (!sp) return;

        _prevSprite = sp;

        // 현재 스프라이트의 피벗(0~1) 계산
        var sizePx = sp.rect.size;                     // 픽셀
        Vector2 curPivot01 = sp.pivot / sizePx;        // 0~1
        Vector2 delta01 = curPivot01 - targetPivotNormalized; // 현재-목표 차이(정규화)

        // 픽셀 → 유닛 변환
        float ppu = (overridePPU > 0f) ? overridePPU : sp.pixelsPerUnit;
        Vector2 deltaUnits = new Vector2(delta01.x * sizePx.x / ppu,
                                         delta01.y * sizePx.y / ppu);

        // 그래픽(자식)을 역이동해서 루트 기준점은 고정
        // (Pivot이 위로 가 있으면 그래픽을 아래로 내림)
        transform.localPosition = -deltaUnits;
    }
}
