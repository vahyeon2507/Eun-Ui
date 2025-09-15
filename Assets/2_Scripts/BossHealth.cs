// BossHealth.cs (개선/안정화 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHp = 20;
    [SerializeField] private int currentHp = 0;   // 인스펙터 노출되지만 외부에서 직접 수정하지 않는 편이 안전
    public int CurrentHp => currentHp;

    [Header("UI (optional)")]
    public Slider hpBar;      // World-space or Screen-space slider
    public Text hpText;       // "15 / 20" 같은 텍스트 표시

    [Header("Hit Reaction")]
    public float invulnTime = 0.6f;      // 전체 무적 시간
    public float staggerTime = 0.18f;    // 행동 차단 시간(짧게)
    public float hitMoveDistance = 0.25f; // 피격 시 보스를 살짝 밀어내는 거리
    public bool useScriptPush = true;    // Kinematic 보스일 때는 true

    [Header("VFX / SFX")]
    public GameObject hitEffectPrefab;
    public AudioClip hitSfx;
    public AudioClip deathSfx;
    public float hitEffectYOffset = 0.5f;

    [Header("Optional refs")]
    public Animator animator;            // Hurt / Die 트리거 이름을 Animator에 맞게 설정
    public SpriteRenderer spriteRenderer;
    public Transform playerTransform;    // 추천: 외부에서 boss 스크립트가 할당하거나 씬에서 드래그

    [Header("Events (optional)")]
    public UnityEvent onDamaged;
    public UnityEvent onDied;

    // 내부
    private bool isInvulnerable = false;
    private bool isStaggered = false;
    private Rigidbody2D rb;
    private AudioSource audioSource;

    void Awake()
    {
        // currentHp 기본값 처리:
        if (currentHp <= 0) currentHp = maxHp;

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

        // 플레이어 참조가 없으면 씬에서 찾지만 한 번만
        if (playerTransform == null)
        {
            GameObject pl = GameObject.FindWithTag("Player");
            if (pl != null) playerTransform = pl.transform;
        }
    }

    // IDamageable 구현
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (isInvulnerable) return;
        if (currentHp <= 0) return; // 이미 죽음 처리된 경우 무시

        Debug.Log($"[BossHealth] Took {amount} dmg. HP before: {currentHp}");

        currentHp -= amount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

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

        // 애니메이터 트리거 (안전하게 호출)
        if (animator != null)
        {
            // Animator에 'Hurt' 트리거가 없다면 조용히 무시됨
            animator.SetTrigger("Hurt");
        }

        // 이벤트(인스펙터에 연결 가능)
        onDamaged?.Invoke();

        // 피격반응 코루틴: 죽음 판정보다 먼저 실행하되, 죽으면 코루틴이 중단되거나 Die가 코루틴을 정리하도록 한다
        StartCoroutine(HitReactionCoroutine());

        // 죽음 체크
        if (currentHp <= 0)
        {
            Die();
        }
    }

    IEnumerator HitReactionCoroutine()
    {
        // 시작: 무적 + 스태거 상태
        isInvulnerable = true;
        isStaggered = true;

        // 깜빡임(간단) — 스프라이트가 없으면 전체 invulnTime을 그냥 기다림
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
            // 보장
            spriteRenderer.color = orig;
        }
        else
        {
            // 스프라이트가 없으면 invulnTime 만큼 대기
            yield return new WaitForSeconds(invulnTime);
        }

        // 넉백(플레이어에서 멀어지는 방향)
        Vector2 push = Vector2.zero;
        if (playerTransform != null)
        {
            float dir = Mathf.Sign(transform.position.x - playerTransform.position.x);
            push = new Vector2(dir * hitMoveDistance, 0f);
        }

        if (useScriptPush && rb != null)
        {
            // kinematic이면 MovePosition 또는 transform 이동
            rb.MovePosition(rb.position + push);
        }
        else if (useScriptPush)
        {
            transform.position += (Vector3)push;
        }

        // 스태거 유지
        yield return new WaitForSeconds(staggerTime);

        isStaggered = false;

        // 남은 invuln 시간(이미 스프라이트 깜빡임으로 대기했을 수 있으므로 잔여 계산)
        float remaining = invulnTime - staggerTime;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        isInvulnerable = false;
    }

    void Die()
    {
        Debug.Log("[BossHealth] DIED");

        // 중복 호출 방지
        if (currentHp > 0) currentHp = 0;

        // 애니메이션, 사운드
        if (animator != null) animator.SetTrigger("Die");
        if (audioSource != null && deathSfx != null) audioSource.PlayOneShot(deathSfx);

        onDied?.Invoke();

        // 충돌 비활성
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 코루틴 정리 (피격 반응이 돌아가면 이상하니 멈춤)
        StopAllCoroutines();

        // 행동 스크립트들 안전하게 비활성화 - 필요하면 특정 스크립트만 끄도록 변경
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb != this) mb.enabled = false;
        }

        // 릿지바디 비활성
        if (rb != null) rb.simulated = false;

        // 제거 (애니메이션 길이에 따라 시간 조정)
        Destroy(gameObject, 1.2f);
    }

    // 편의: 치유
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHp += amount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
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

    // 외부에서 피격 불능 상태 확인용
    public bool IsInvulnerable() => isInvulnerable;
}
