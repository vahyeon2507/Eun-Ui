using UnityEngine;

[DisallowMultipleComponent]
public class TalismanComboDirector : MonoBehaviour
{

    [Header("Refs")]
    public PlayerTalismanUnified talisman;   // 비워두면 자동 탐색
    public GameObject treeWallPrefab;        // 위에서 만든 TreeWall 프리팹

    [Header("Spawn")]
    public Vector2 treeWallOffset = Vector2.zero; // 스폰 위치 미세 보정
    public LayerMask groundMask;                  // 바닥에 살짝 붙이고 싶으면 사용
    public float groundSnapRay = 2f;

    void Awake()
    {
        if (!talisman) talisman = FindObjectOfType<PlayerTalismanUnified>();
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

        // 물 + 흙 (순서 무관)
        bool isWaterEarth = (a == TalismanType.Water && b == TalismanType.Earth)
                         || (a == TalismanType.Earth && b == TalismanType.Water);
        if (!isWaterEarth) return;
        if (!treeWallPrefab) return;

        Vector2 spawn = pos + treeWallOffset;

        // 바닥에 스냅
        if (groundMask.value != 0)
        {
            var hit = Physics2D.Raycast(spawn + Vector2.up * groundSnapRay, Vector2.down, groundSnapRay * 2f, groundMask);
            if (hit.collider) spawn = hit.point;
        }

        Instantiate(treeWallPrefab, spawn, Quaternion.identity);
    }
}
