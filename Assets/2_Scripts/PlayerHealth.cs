using System.Collections;
using UnityEngine;
using UnityEngine.UI; // UI 사용 시

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("Invulnerability")]
    public float iFrameDuration = 0.8f;   // 맞고 잠깐 무적
    public float flashInterval = 0.1f;    // 무적 깜빡임 간격
    bool isInvulnerable = false;

    [Header("References (optional)")]
    public Slider healthBar;              // UI Slider 연결하면 자동 갱신
    public Animator animator;             // 피격/사망 애니메이션
    public MonoBehaviour disableOnDeath;  // 예: PlayerController (옵션으로 비활성화)

    Rigidbody2D rb;
    SpriteRenderer sr;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    // IDamageable 인터페이스 구현
    public void TakeDamage(int amount)
    {
        if (isInvulnerable) return;
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // UI 갱신
        if (healthBar != null) healthBar.value = currentHealth;

        // 피격 애니메이션 트리거 (있으면)
        if (animator != null) animator.SetTrigger("Hurt");

        // 무적 프레임 시작
        StartCoroutine(InvulnerabilityCoroutine());

        // 사망 체크
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;

        if (sr != null)
        {
            float t = 0f;
            while (t < iFrameDuration)
            {
                sr.enabled = !sr.enabled;
                yield return new WaitForSeconds(flashInterval);
                t += flashInterval;
            }
            sr.enabled = true;
        }
        else
        {
            yield return new WaitForSeconds(iFrameDuration);
        }

        isInvulnerable = false;
    }

    void Die()
    {
        // 사망 애니메이션
        if (animator != null) animator.SetTrigger("Die");

        // 플레이어 컨트롤 비활성화 (옵션)
        if (disableOnDeath != null) disableOnDeath.enabled = false;

        // 콜라이더 등 비활성화 (원하면)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Rigidbody 끄기(옵션)
        if (rb != null) rb.simulated = false;

        // 오브젝트 삭제(애니메이션 후)
        Destroy(gameObject, 1.0f); // 애니메이션 길이에 맞춰 조절
    }

    // (테스트용) 즉시 체력 회복 함수
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        if (healthBar != null) healthBar.value = currentHealth;
    }
}
