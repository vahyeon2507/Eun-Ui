using UnityEngine;

[CreateAssetMenu(menuName = "Items/Heal Item")]
public class HealItemSO : ItemSO
{
    public int healAmount = 1;

    private void Reset() { type = ItemType.Heal; }   // 에셋 만들면 자동으로 Heal로 설정

    public override bool Use(GameObject user)
    {
        var hp = user.GetComponent<PlayerHealth>();
        if (hp == null) return false;

        int before = hp.currentHealth;
        hp.Heal(healAmount);                 // 네 프로젝트 PlayerHealth의 Heal(int) 사용
        return hp.currentHealth > before;    // 실제로 회복되었을 때만 true
    }
}
