
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("체력 설정")]
    public int maxHp = 3;
    public int currentHp;

    [Header("애니메이션")]
    public Animator animator;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("이펙트")]
    public GameObject hitEffect;
    public GameObject deathEffect;

    [Header("재화 드롭")]
    public CurrencyDropper currencyDropper;

    private bool isDead = false;

    void Start()
    {
        currentHp = maxHp;
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void TakeDamage(int amount)
    {
        if (isDead || amount <= 0) return;

        currentHp -= amount;
        currentHp = Mathf.Max(0, currentHp);

        Debug.Log($"적이 {amount} 데미지를 입었습니다! 현재 체력: {currentHp}");

        // 피격 이펙트
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }

        // 피격 사운드
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        // 피격 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }

        if (currentHp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("적이 죽었습니다!");

        // 죽음 이펙트
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        // 죽음 사운드
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // 죽음 애니메이션
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // 충돌체 비활성화
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Rigidbody 비활성화
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        // 재화 드롭
        if (currencyDropper != null)
        {
            currencyDropper.DropCurrency();
            Debug.Log("[EnemyHealth] 재화 드롭 시도 (currencyDropper 참조)");
        }
        else
        {
            CurrencyDropper dropper = GetComponent<CurrencyDropper>();
            if (dropper != null)
            {
                dropper.DropCurrency();
                Debug.Log("[EnemyHealth] 재화 드롭 시도 (GetComponent)");
            }
            else
            {
                Debug.LogWarning("[EnemyHealth] CurrencyDropper를 찾을 수 없습니다! 잡몹에 CurrencyDropper 컴포넌트를 추가하세요.");
            }
        }

        // 오브젝트 제거 (애니메이션 완료 후)
        Destroy(gameObject, 1f);
    }

    // 체력 회복 (필요시)
    public void Heal(int amount)
    {
        if (isDead) return;
        currentHp += amount;
        currentHp = Mathf.Min(currentHp, maxHp);
        Debug.Log($"적이 {amount} 체력을 회복했습니다! 현재 체력: {currentHp}");
    }
}
