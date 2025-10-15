using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("참조")]
    public Inventory inventory;             // 플레이어 인벤토리
    public Transform gridParent;            // 슬롯들이 배치될 부모(그리드/수직 레이아웃)
    public InventorySlotUI slotPrefab;      // 슬롯 프리팹
    public GameObject rootPanel;            // 인벤토리 전체 패널(열고/닫기)
    public KeyCode toggleKey = KeyCode.I;   // 인벤토리 토글 키

    [Header("필터 옵션")]
    public bool showOnlyHeal = false;       // true면 Heal 타입만 보이게

    void OnEnable()
    {
        if (inventory != null) inventory.OnChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (inventory != null) inventory.OnChanged -= Rebuild;
    }

    void Update()
    {
        if (rootPanel != null && Input.GetKeyDown(toggleKey))
        {
            rootPanel.SetActive(!rootPanel.activeSelf);
            if (rootPanel.activeSelf) Rebuild();
        }
    }

    // === UI 빌드 ===
    public void Rebuild()
    {
        if (inventory == null || gridParent == null || slotPrefab == null) return;

        ClearAllSlotViews();

        var slots = inventory.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || s.IsEmpty || s.item == null) continue;        // 빈 슬롯 스킵
            if (showOnlyHeal && s.item.type != ItemType.Heal) continue;    // 선택 필터

            CreateSlotView(i, s);
        }
    }

    // 자식 슬롯들 정리
    void ClearAllSlotViews()
    {
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            Destroy(gridParent.GetChild(i).gameObject);
        }
    }

    // 슬롯 프리팹 인스턴스 + 바인딩
    void CreateSlotView(int index, Inventory.Slot data)
    {
        var ui = Instantiate(slotPrefab, gridParent);
        ui.Bind(inventory, index, data);
    }
}
