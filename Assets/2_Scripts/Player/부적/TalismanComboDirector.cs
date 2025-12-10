using UnityEngine;

[DisallowMultipleComponent]
public class TalismanComboDirector : MonoBehaviour
{
    [Header("Refs")]
    public PlayerTalismanUnified talisman;   // 비워두면 자동 탐색

    [Header("Fire + Metal → Strong Parry Zone")]
    [Tooltip("강패링 존 프리팹 (TalismanStrongParryZone 붙어있어야 함)")]
    public GameObject strongParryZonePrefab;
    public Vector2 strongParryOffset = Vector2.zero;

    [Header("Water + Wood → TreeWall")]
    [Tooltip("나무 벽 프리팹")]
    public GameObject treeWallPrefab;
    public Vector2 treeWallOffset = Vector2.zero;

    [Header("Fire + Earth → Flame Zone")]
    [Tooltip("불꽃 장판 프리팹")]
    public GameObject flameZonePrefab;
    public Vector2 flameZoneOffset = Vector2.zero;

    [Header("Water + Earth → Heat Reset")]
    [Tooltip("불가사리 바닥 열기 시스템")]
    public BulgasariFloorHeatSystem floorHeatSystem;

    [Tooltip("열기 초기화 시 재생할 VFX (선택)")]
    public GameObject heatClearVFXPrefab;
    public Vector2 heatClearOffset = Vector2.zero;

    [Tooltip("열기 초기화 VFX를 자동으로 파괴할 시간(초). " +
             "애니메이션 길이와 맞추면 '애니 끝날 때 사라짐' 효과가 됨.")]
    public float heatClearVFXLifeTime = 2f;

    [Header("Ground Snap (공통)")]
    [Tooltip("바닥에 살짝 붙이고 싶으면 사용")]
    public LayerMask groundMask;
    public float groundSnapRay = 2f;

    void Awake()
    {
        if (!talisman)
            talisman = FindObjectOfType<PlayerTalismanUnified>();

        if (!floorHeatSystem)
            floorHeatSystem = FindObjectOfType<BulgasariFloorHeatSystem>();
    }

    void OnEnable()
    {
        if (talisman != null)
            talisman.onFieldCombo.AddListener(OnFieldCombo);
    }

    void OnDisable()
    {
        if (talisman != null)
            talisman.onFieldCombo.RemoveListener(OnFieldCombo);
    }

    void OnFieldCombo(TalismanType a, TalismanType b, Vector2 pos)
    {
        // 0) Fire + Metal → 강패링 존
        if (IsPair(a, b, TalismanType.Fire, TalismanType.Metal))
        {
            SpawnStrongParryZone(pos);
            return;
        }

        // 1) Fire + Earth → 불꽃 장판
        if (IsPair(a, b, TalismanType.Fire, TalismanType.Earth))
        {
            SpawnFlameZone(pos);
            return;
        }

        // 2) Water + Wood → 나무 벽
        if (IsPair(a, b, TalismanType.Water, TalismanType.Wood))
        {
            SpawnTreeWall(pos);
            return;
        }

        // 3) Water + Earth → 열기 0 + VFX
        if (IsPair(a, b, TalismanType.Water, TalismanType.Earth))
        {
            TriggerHeatReset(pos);
            return;
        }

        // 그 외 조합은 아무 것도 안 함
    }

    bool IsPair(TalismanType a, TalismanType b, TalismanType x, TalismanType y)
    {
        return (a == x && b == y) || (a == y && b == x);
    }

    // ───────── Strong Parry Zone ─────────
    void SpawnStrongParryZone(Vector2 pos)
    {
        if (!strongParryZonePrefab) return;

        Vector2 spawn = pos + strongParryOffset;
        spawn = SnapToGround(spawn);

        Instantiate(strongParryZonePrefab, spawn, Quaternion.identity);
    }

    // ───────── Tree Wall ─────────
    void SpawnTreeWall(Vector2 pos)
    {
        if (!treeWallPrefab) return;

        Vector2 spawn = pos + treeWallOffset;
        spawn = SnapToGround(spawn);

        Instantiate(treeWallPrefab, spawn, Quaternion.identity);
    }

    // ───────── Flame Zone ─────────
    void SpawnFlameZone(Vector2 pos)
    {
        if (!flameZonePrefab) return;

        Vector2 spawn = pos + flameZoneOffset;
        spawn = SnapToGround(spawn);

        Instantiate(flameZonePrefab, spawn, Quaternion.identity);
    }

    // ───────── Heat Reset ─────────
    void TriggerHeatReset(Vector2 pos)
    {
        // VFX 먼저
        if (heatClearVFXPrefab != null)
        {
            Vector2 spawn = pos + heatClearOffset;
            spawn = SnapToGround(spawn);

            var vfx = Instantiate(heatClearVFXPrefab, spawn, Quaternion.identity);

            // ⬇ 애니 끝날 때 사라지는 효과: 수명 후 자동 삭제
            if (heatClearVFXLifeTime > 0f)
                Destroy(vfx, heatClearVFXLifeTime);
        }

        // 열기 시스템 리셋
        if (floorHeatSystem != null)
        {
            floorHeatSystem.ResetHeat();
        }
        else
        {
            Debug.LogWarning("[TalismanComboDirector] floorHeatSystem 이 설정되지 않았습니다. Water+Earth 조합이 열기를 지우지 못합니다.");
        }
    }

    // ───────── 공통: 바닥 스냅 ─────────
    Vector2 SnapToGround(Vector2 pos)
    {
        if (groundMask.value == 0 || groundSnapRay <= 0f)
            return pos;

        var hit = Physics2D.Raycast(
            pos + Vector2.up * groundSnapRay,
            Vector2.down,
            groundSnapRay * 2f,
            groundMask);

        if (hit.collider) return hit.point;
        return pos;
    }
}
