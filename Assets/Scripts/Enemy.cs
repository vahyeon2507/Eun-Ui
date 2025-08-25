using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHealth = 3;
    private int currentHealth;

    private Animator animator;
    private Rigidbody2D rb;
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;

        // 피격 애니메이션 (있다면)
        if (animator != null)
            animator.SetTrigger("Hurt");

        // 죽음 처리
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;

        // 죽는 애니메이션
        if (animator != null)
            animator.SetTrigger("Die");

        // 충돌 제거 & 중력 제거
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        // 오브젝트 제거 (애니메이션 끝난 뒤 제거를 원한다면 Invoke 사용)
        Destroy(gameObject, 1f); // 1초 뒤 삭제
    }
}
