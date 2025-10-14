using UnityEngine;

public class DebugGivePotion : MonoBehaviour
{
    public Inventory inventory;
    public HealItemSO potion;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P) && inventory != null && potion != null)
        {
            bool ok = inventory.Add(potion, 1);
            Debug.Log(ok ? "[DEBUG] 포션 +1" : "[DEBUG] 인벤토리 가득");
        }
    }
}
