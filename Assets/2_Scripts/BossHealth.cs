// BossHealth.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHp = 20;
    [SerializeField] public int currentHp;
    public int CurrentHp => currentHp; // ReadOnly attribute not built-in; shown for clarity

    [Header("UI (optional)")]
    public Slider hpBar;      // World-space or Screen-space slider
    public Text hpText;       // "15 / 20" 같은 텍스트 표시

    [Header("Hit Reaction")]
    public float invulnTime = 0.6f;      // 전체 무적 시간
    public float staggerTime = 0.18f;    // 행동 차단 시간(짧게)
    public float hitMoveDistance = 0.25f; // 피격 시 보스를 살짝 밀어내는 거리 (스크립트 이동)
    public bool useScriptPush = true;    // Kinematic 보스일 때는 true (물리 넉백 안 됨)

    [Header("VFX / SFX")]
    public GameObject hitEffectPrefab;
    public AudioClip hitSfx;
    public AudioClip deathSfx;
    public float hitEffectYOffset = 0.5f;

    [Header("Optional refs")]
    public Animator animator;            // Hurt / Die 트리거 이름을 Animator에 맞게 설정
    public SpriteRenderer spriteRenderer;

    [Header("Events (optional)")]
    public UnityEvent onDamaged;         // 데미지 받을 때 inspector에서 이벤트 연결 가능
    public UnityEvent onDied;            // 죽을 때 inspector에서 이벤트 연결 가능

    // 내부
    private bool isInvulnerable = false;
    private bool isStaggered = false;
    private Rigidbody2D rb;
    private AudioSource audioSource;

    void Awake()
    {
        currentHp = maxHp;

        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // UI 초기화
        if (hpBar != null)
        {
            hpBar.maxValue = maxHp;
            hpBar.value = currentHp;
        }
        if (hpText != null)
            hpText.text = $"{currentHp} / {maxHp}";
    }

    // IDamageable 구현
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (isInvulnerable) return;

        Debug.Log($"[BossHealth] Took {amount} dmg. HP before: {currentHp}");

        currentHp -= amount;
        if (currentHp < 0) currentHp = 0;

        // UI 갱신
        if (hpBar != null) hpBar.value = currentHp;
        if (hpText != null) hpText.text = $"{currentHp} / {maxHp}";

        // VFX / SFX
        if (hitEffectPrefab != null)
        {
            Vector3 fxPos = transform.position + Vector3.up * hitEffectYOffset;
            Instantiate(hitEffectPrefab, fxPos, Quaternion.identity);
        }
        if (audioSource != null && hitSfx != null)
            audioSource.PlayOneShot(hitSfx);

        // 애니메이터 트리거
        if (animator != null)
            animator.SetTrigger("Hurt");

        // 이벤트(인스펙터에 연결 가능)
        onDamaged?.Invoke();

        // 피격반응 코루틴
        StartCoroutine(HitReactionCoroutine());

        // 죽음 체크
        if (currentHp <= 0)
        {
            Die();
        }
    }

    IEnumerator HitReactionCoroutine()
    {
        isInvulnerable = true;
        isStaggered = true;

        // 깜빡임(간단)
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            float flashStep = 0.08f;
            int flashes = Mathf.Max(1, Mathf.CeilToInt(invulnTime / (flashStep * 2f)));
            for (int i = 0; i < flashes; i++)
            {
                spriteRenderer.color = Color.white; // 밝게
                yield return new WaitForSeconds(flashStep);
                spriteRenderer.color = orig;        // 원래색
                yield return new WaitForSeconds(flashStep);
            }
            spriteRenderer.color = orig;
        }
        else
        {
            // fallback wait
            yield return null;
        }

        // 짧은 스태거(행동 차단) + 스크립트 이동(넉백 대체)
        Vector2 push = Vector2.zero;
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            // 플레이어 쪽으로 밀리는 게 아니라 플레이어에서 멀어지는 방향으로 조금 이동
            float dir = Mathf.Sign(transform.position.x - player.transform.position.x); // 보스의 x - 플레이어 x
            push = new Vector2(dir * hitMoveDistance, 0f);
        }

        // 실제 이동 (Kinematic이어도 MovePosition 허용)
        if (useScriptPush && rb != null)
        {
            rb.MovePosition(rb.position + push);
        }
        else if (useScriptPush)
        {
            transform.position += (Vector3)push;
        }

        // 스태거 유지
        yield return new WaitForSeconds(staggerTime);

        isStaggered = false;

        // 남은 invuln 시간(전체 invulnTime에서 staggerTime 뺀 만큼 추가 대기)
        float remaining = invulnTime - staggerTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        isInvulnerable = false;
    }

    void Die()
    {
        Debug.Log("[BossHealth] DIED");
        if (animator != null) animator.SetTrigger("Die");
        if (audioSource != null && deathSfx != null) audioSource.PlayOneShot(deathSfx);

        onDied?.Invoke();

        // 비활성화 및 파괴
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 보스 행동 스크립트들 비활성화 - 안전하게 모든 MonoBehaviour 끄기
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb != this) mb.enabled = false;
        }

        // Rigidbody 비활성 (게임성에 따라)
        if (rb != null) rb.simulated = false;

        // 게임에서 완전 제거(애니메이션 길이에 따라 조정)
        Destroy(gameObject, 1.2f);
    }

    // 편의: 치유
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHp += amount;
        if (currentHp > maxHp) currentHp = maxHp;
        if (hpBar != null) hpBar.value = currentHp;
        if (hpText != null) hpText.text = $"{currentHp} / {maxHp}";
    }

    // 편의: 강제로 체력 설정 (디버그)
    public void SetHp(int hp)
    {
        currentHp = Mathf.Clamp(hp, 0, maxHp);
        if (hpBar != null) hpBar.value = currentHp;
        if (hpText != null) hpText.text = $"{currentHp} / {maxHp}";
    }

    // (Optional) 외부에서 피격 불능 상태 확인용
    public bool IsInvulnerable() => isInvulnerable;
}
