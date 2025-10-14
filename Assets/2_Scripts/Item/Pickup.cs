// Assets/_Scripts/Inventory/Pickup.cs
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public ItemSO item;
    public int amount = 1;

    void OnTriggerEnter2D(Collider2D other)
    {
        var inv = other.GetComponentInParent<Inventory>();
        if (inv != null && inv.Add(item, amount))
        {
            Destroy(gameObject); // 줍기 성공 시 제거
        }
    }
}
