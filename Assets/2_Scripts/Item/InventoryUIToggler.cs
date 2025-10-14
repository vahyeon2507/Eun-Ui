// 항상 켜져있는 오브젝트에 붙이세요.
using UnityEngine;

public class InventoryUIToggler : MonoBehaviour
{
    public GameObject panel;           // InventoryPanel
    public KeyCode key = KeyCode.I;    // I

    void Update()
    {
        if (Input.GetKeyDown(key) && panel != null)
        {
            panel.SetActive(!panel.activeSelf);
            // 패널이 켜질 때 새로 그려주기
            if (panel.activeSelf)
            {
                var ui = panel.GetComponentInChildren<InventoryUI>(true);
                if (ui != null) ui.Rebuild();
            }
        }
    }
}
