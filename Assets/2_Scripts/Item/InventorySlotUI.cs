using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    [Header("위젯")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text countText;
    public Button useButton;

    // 바인딩 정보(사용 버튼에서 참조)
    Inventory inventory;
    int index;
    Inventory.Slot slot;

    public void Bind(Inventory inv, int idx, Inventory.Slot s)
    {
        inventory = inv;
        index = idx;
        slot = s;

        // 아이콘/이름/갯수 표시
        if (icon != null) icon.sprite = s.item != null ? s.item.icon : null;
        if (nameText != null) nameText.text = s.item != null ? s.item.name : "";
        if (countText != null) countText.text = (s.count > 1) ? s.count.ToString() : "";

        // 버튼 연결
        if (useButton != null)
        {
            useButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(OnClickUse);
        }
    }

    void OnClickUse()
    {
        if (inventory == null) return;
        bool ok = inventory.TryUseAt(index);
        if (!ok) return;

        // 사용 후 수량/표시 갱신(0되면 UI 전체 리빌드를 권장)
        if (slot != null && countText != null)
            countText.text = (slot.count > 1) ? slot.count.ToString() : "";

        // 슬롯이 비었거나 아이템/수량 변경이 복잡하면 전체 Rebuild가 안전
        var ui = GetComponentInParent<InventoryUI>();
        if (ui != null) ui.Rebuild();
    }
}
