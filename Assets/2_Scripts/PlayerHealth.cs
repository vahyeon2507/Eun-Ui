using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("UI Bars")]
    public Image redBar;     // 즉시 줄어드는 바(빠르게)
    public Image yellowBar;  // 잔상처럼 따라오는 바(느리게)
    public float redBarAnimationDuration = 0.15f;
    public float yellowBarDelay = 0.3f;
    public float yellowBarSpeed = 0.3f;

    [Header("Parry Bar")]
    public Image parryBar;                 // 패링 강화 공격 바
    public float parryBarAnimationDuration = 0.15f;
    public int maxParryCount = 4;
    public int currentParryCount = 0;

    [Header("Invulnerability")]
    public float iFrameDuration = 0.8f;
    bool isInvulnerable = false;

    [Header("References (optional)")]
    public Animator animator;
    public MonoBehaviour disableOnDeath;

    [Header("Hitbox Filter")]
    [Tooltip("실제로 맞는 '몸통' 콜라이더. 이 콜라이더에 맞았을 때만 대미지를 받는다. 비워두면 예전처럼 모든 콜라이더에서 맞음.")]
    public Collider2D mainBodyCollider;

    Rigidbody2D rb;
    SpriteRenderer sr;

    // === 무수 방패 연계용 ===
    MusuShield _activeShield;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        // 체력바 초기화는 Start에서
    }

    void Start()
    {
        // 체력바/패링바 즉시 초기화
        UpdateBarsInstant();
        UpdateParryBarInstant();

        // 디버그
        Debug.Log($"[PlayerHealth] 초기화 완료 - 체력: {currentHealth}/{maxHealth}");
        Debug.Log($"[PlayerHealth] RedBar: {(redBar ? "설정됨" : "NULL")}, YellowBar: {(yellowBar ? "설정됨" : "NULL")}, ParryBar: {(parryBar ? "설정됨" : "NULL")}");
        Debug.Log($"[PlayerHealth] 패링 횟수: {currentParryCount}/{maxParryCount}");

        if (parryBar == null)
            Debug.LogError("[PlayerHealth] 패링바가 설정되지 않았습니다! PlayerController.healthComponent에 PlayerHealth를 할당하고 parryBar에 Image를 연결하세요.");
    }
    readonly System.Collections.Generic.List<MusuShield> _damageShields = new System.Collections.Generic.List<MusuShield>();

    // ===== IDamageable =====
    public void TakeDamage(int amount)
    {
        if (isInvulnerable) return;

        // ★ 무수 적용
        int finalDamage = ApplyDamageShields(amount);
        Debug.Log($"[PlayerHealth] 피격 요청: {amount} -> 방패 적용 후: {finalDamage}");

        if (finalDamage <= 0)
        {
            // 완전 방어라면 여기서 연출만 하고 종료해도 됨
            GetComponentInChildren<SpriteFlash>()?.FlashOnce();
            return;
        }

        GetComponentInChildren<SpriteFlash>()?.FlashOnce();

        var shaker = GetComponent<PlayerHitShake>();
        if (shaker == null) shaker = FindObjectOfType<PlayerHitShake>();
        shaker?.Shake();

        Debug.Log($"[PlayerHealth] 최종 체력: {currentHealth} -> {currentHealth - finalDamage}");

        currentHealth -= finalDamage;
        if (currentHealth < 0) currentHealth = 0;

        if (redBar != null) StartCoroutine(AnimateRedBar());
        if (yellowBar != null) StartCoroutine(UpdateYellowBar());

        if (animator != null) animator.SetTrigger("Hurt");

        StartCoroutine(HitFlash());
        StartCoroutine(IFrameCoroutine());

        if (currentHealth <= 0) Die();
    }



    /// <summary>
    /// '어떤 콜라이더에 맞았는지'를 함께 넘겨서,
    /// mainBodyCollider 에 맞았을 때만 실제 데미지를 적용한다.
    /// </summary>
    public void TakeDamageFromHitbox(int amount, Collider2D hitCollider)
    {
        // mainBodyCollider 가 지정돼 있으면, 그 콜라이더만 인정
        if (mainBodyCollider != null && hitCollider != mainBodyCollider)
        {
            // Debug.Log($"[PlayerHealth] 무시된 피격: {hitCollider.name}");
            return;
        }

        ApplyDamageInternal(amount, hitCollider);
    }

    // === 무수 방패 연계용 공개 함수 ===
    public void AttachDamageShield(MusuShield shield)
    {
        if (shield == null) return;
        if (!_damageShields.Contains(shield))
        {
            _damageShields.Add(shield);
            Debug.Log($"[PlayerHealth] Shield Attached: {shield.name} (총 {_damageShields.Count}개)");
        }
    }

    public void DetachDamageShield(MusuShield shield)
    {
        if (shield == null) return;
        if (_damageShields.Remove(shield))
            Debug.Log($"[PlayerHealth] Shield Detached: {shield.name} (총 {_damageShields.Count}개)");
    }


    int ApplyDamageShields(int amount)
    {
        if (_damageShields.Count == 0)
        {
            // 여기가 찍힌다는 건 “애초에 방패가 등록이 안 됐다”는 뜻
            Debug.Log("[PlayerHealth] 적용 가능한 쉴드가 없음");
            return amount;
        }

        int result = amount;
        for (int i = 0; i < _damageShields.Count; i++)
        {
            var s = _damageShields[i];
            if (!s) continue;
            if (!s.IsActive) continue; // 아직 활성화 이벤트 전이면 무시

            int before = result;
            result = s.ModifyIncomingDamage(result);
            Debug.Log($"[PlayerHealth] Shield {s.name}: {before} -> {result}");
        }
        return result;
    }
    // 실제 데미지 처리 공통 함수
    void ApplyDamageInternal(int amount, Collider2D hitCollider)
    {
        if (amount <= 0) return;
        if (isInvulnerable) return;

        int finalAmount = amount;

        // 무수 방패가 켜져 있으면 여기서 감소 + 흡수량 누적
        if (_activeShield != null && _activeShield.IsActive)
        {
            finalAmount = _activeShield.ModifyIncomingDamage(finalAmount);
            finalAmount = Mathf.Max(0, finalAmount);
        }

        // 피격 연출 (대미지가 0이더라도 "맞았다"는 연출은 유지)
        GetComponentInChildren<SpriteFlash>()?.FlashOnce();

        var shaker = GetComponent<PlayerHitShake>();
        if (shaker == null) shaker = FindObjectOfType<PlayerHitShake>();
        shaker?.Shake();

        Debug.Log($"[PlayerHealth] 피격! 체력: {currentHealth} -> {currentHealth - finalAmount}");

        currentHealth -= finalAmount;
        if (currentHealth < 0) currentHealth = 0;

        if (redBar != null) StartCoroutine(AnimateRedBar());
        if (yellowBar != null) StartCoroutine(UpdateYellowBar());

        if (animator != null) animator.SetTrigger("Hurt");

        StartCoroutine(HitFlash());
        StartCoroutine(IFrameCoroutine());

        if (currentHealth <= 0) Die();
    }

    IEnumerator AnimateRedBar()
    {
        float startFill = redBar.fillAmount;
        float target = (float)currentHealth / maxHealth;

        float elapsedTime = 0f;
        while (elapsedTime < redBarAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / redBarAnimationDuration;
            // EaseOut Cubic
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            redBar.fillAmount = Mathf.Lerp(startFill, target, eased);
            yield return null;
        }
        redBar.fillAmount = target;
    }

    IEnumerator UpdateYellowBar()
    {
        yield return new WaitForSeconds(yellowBarDelay);

        float target = (float)currentHealth / maxHealth;
        float startFill = yellowBar.fillAmount;

        if (startFill <= target) yield break; // 이미 이하라면 스킵

        float animDur = (startFill - target) / Mathf.Max(0.0001f, yellowBarSpeed);

        float t = 0f;
        while (yellowBar.fillAmount > target)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / animDur);
            yellowBar.fillAmount = Mathf.Lerp(startFill, target, p);
            yield return null;
        }
        yellowBar.fillAmount = target;
    }

    IEnumerator HitFlash()
    {
        if (sr != null)
        {
            Color original = sr.color;
            sr.color = Color.white;           // 하얗게 번쩍
            yield return new WaitForSeconds(0.1f);
            sr.color = original;
        }
    }

    IEnumerator IFrameCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(iFrameDuration);
        isInvulnerable = false;
    }

    void Die()
    {
        if (animator != null) animator.SetTrigger("Die");
        if (disableOnDeath != null) disableOnDeath.enabled = false;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (rb != null) rb.simulated = false;

        GameOverController.TryBeginFrom(gameObject);

        Destroy(gameObject, 1f);
    }

    void UpdateBarsInstant()
    {
        float fill = (float)currentHealth / maxHealth;

        if (redBar != null) redBar.fillAmount = fill;
        else Debug.LogWarning("[PlayerHealth] RedBar가 설정되지 않았습니다!");

        if (yellowBar != null) yellowBar.fillAmount = fill;
        else Debug.LogWarning("[PlayerHealth] YellowBar가 설정되지 않았습니다!");
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateBarsInstant();
    }

    // ===== 패링 관련 =====
    public void AddParryCount()
    {
        Debug.Log($"[PlayerHealth] AddParryCount - {currentParryCount}/{maxParryCount}");

        if (currentParryCount < maxParryCount)
        {
            currentParryCount++;

            if (parryBar != null) StartCoroutine(AnimateParryBar());
            else Debug.LogError("[PlayerHealth] ParryBar가 NULL입니다!");

            if (currentParryCount >= maxParryCount)
                OnParryBarFull();
        }
        else
        {
            Debug.Log("[PlayerHealth] 패링 카운트가 이미 최대입니다!");
        }
    }

    public void ResetParryCount()
    {
        currentParryCount = 0;
        if (parryBar != null) StartCoroutine(AnimateParryBar());
    }

    public bool CanUseEnhancedAttack() => currentParryCount >= maxParryCount;

    public void UseEnhancedAttack()
    {
        if (CanUseEnhancedAttack())
        {
            Debug.Log("[PlayerHealth] 강화 공격 사용!");
            ResetParryCount();
            // 강화 공격 후속 로직은 프로젝트 규칙에 맞게 추가
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] 강화 공격을 사용할 수 없습니다!");
        }
    }

    void OnParryBarFull()
    {
        Debug.Log("[PlayerHealth] 패링바 가득참! 강화 공격 준비됨!");
        // 효과/사운드 등 연출 위치
    }

    IEnumerator AnimateParryBar()
    {
        if (parryBar == null) yield break;

        float start = parryBar.fillAmount;
        float target = (float)currentParryCount / maxParryCount;

        float t = 0f;
        while (t < parryBarAnimationDuration)
        {
            t += Time.deltaTime;
            float p = t / parryBarAnimationDuration;
            float eased = 1f - Mathf.Pow(1f - p, 3f); // EaseOut Cubic
            parryBar.fillAmount = Mathf.Lerp(start, target, eased);
            yield return null;
        }
        parryBar.fillAmount = target;
    }

    void UpdateParryBarInstant()
    {
        if (parryBar != null)
            parryBar.fillAmount = (float)currentParryCount / maxParryCount;
        else
            Debug.LogWarning("[PlayerHealth] ParryBar가 설정되지 않았습니다!");
    }

    // ===== 테스트용 컨텍스트 메뉴 =====
    [ContextMenu("테스트 대미지")] public void TestDamage() => TakeDamage(1);
    [ContextMenu("테스트 힐")] public void TestHeal() => Heal(1);

    [ContextMenu("빨간바 애니메이션 테스트")]
    public void TestRedBarAnimation()
    {
        if (!redBar) { Debug.LogError("[PlayerHealth] RedBar가 설정되지 않았습니다!"); return; }
        redBar.fillAmount = 1f;
        StartCoroutine(TestRedBarCoroutine());
    }
    IEnumerator TestRedBarCoroutine()
    {
        yield return new WaitForSeconds(1f);
        currentHealth = maxHealth / 2;
        StartCoroutine(AnimateRedBar());
    }

    [ContextMenu("노란바 애니메이션 강제 테스트")]
    public void TestYellowBarAnimation()
    {
        if (!yellowBar) { Debug.LogError("[PlayerHealth] YellowBar가 설정되지 않았습니다!"); return; }
        yellowBar.fillAmount = 1f;
        StartCoroutine(TestYellowBarCoroutine());
    }
    IEnumerator TestYellowBarCoroutine()
    {
        yield return new WaitForSeconds(1f);
        currentHealth = maxHealth / 2;
        StartCoroutine(UpdateYellowBar());
    }

    [ContextMenu("전체 애니메이션 테스트")]
    public void TestFullAnimation()
    {
        if (!redBar || !yellowBar) { Debug.LogError("[PlayerHealth] Red/YellowBar가 설정되지 않았습니다!"); return; }
        redBar.fillAmount = 1f;
        yellowBar.fillAmount = 1f;
        StartCoroutine(TestFullAnimationCoroutine());
    }
    IEnumerator TestFullAnimationCoroutine()
    {
        yield return new WaitForSeconds(1f);
        currentHealth = Mathf.RoundToInt(maxHealth * 0.3f);
        StartCoroutine(AnimateRedBar());
        StartCoroutine(UpdateYellowBar());
    }

    [ContextMenu("패링 카운트 추가")] public void TestAddParry() => AddParryCount();
    [ContextMenu("패링 카운트 리셋")] public void TestResetParry() => ResetParryCount();
    [ContextMenu("강화 공격 사용")] public void TestUseEnhancedAttack() => UseEnhancedAttack();

    [ContextMenu("체력바 상태 확인")]
    public void CheckHealthBarStatus()
    {
        Debug.Log("=== 체력바 상태 ===");
        Debug.Log($"현재 체력: {currentHealth}/{maxHealth}");
        Debug.Log($"RedBar: {(redBar ? $"OK {redBar.fillAmount:P}" : "NULL")}");
        Debug.Log($"YellowBar: {(yellowBar ? $"OK {yellowBar.fillAmount:P}" : "NULL")}");
        Debug.Log($"ParryBar: {(parryBar ? $"OK {parryBar.fillAmount:P}" : "NULL")}");
        Debug.Log($"패링: {currentParryCount}/{maxParryCount}  |  강화공격 가능: {CanUseEnhancedAttack()}");
    }
}
