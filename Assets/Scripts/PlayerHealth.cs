using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 5;
    private int currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        Debug.Log("플레이어가 데미지를 입었습니다! 현재 체력: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("플레이어 사망!");
        // 사망 처리 로직 (애니메이션, 리스폰, 게임오버 등)
    }
}
