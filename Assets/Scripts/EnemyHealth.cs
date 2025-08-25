
using UnityEngine;
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int hp = 3;

    public void TakeDamage(int amount)
    {
        hp -= amount;
        Debug.Log("적이 데미지를 입었습니다! 현재 체력: " + hp);

        if (hp <= 0)
        {
            Destroy(gameObject); // 혹은 죽는 애니메이션 등
        }
    }
}
