using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("UI Bars")]
    public Image redBar;     // 즉시 줄어드는 바
    public Image yellowBar;  // 잔상처럼 따라오는 바
    public float redBarAnimationDuration = 0.15f;  // 빨간바 애니메이션 시간 (빠르게)
    public float yellowBarDelay = 0.3f;   // 잔상 시작 대기 (더 길게)
    public float yellowBarSpeed = 0.3f;   // 잔상이 따라오는 속도 (더 느리게)
    
    [Header("Parry Bar")]
    public Image parryBar;   // 패링 강화 공격 바
    public float parryBarAnimationDuration = 0.15f;  // 패링바 애니메이션 시간
    public int maxParryCount = 4;  // 최대 패링 횟수
    public int currentParryCount = 0;  // 현재 패링 횟수

    [Header("Invulnerability")]
    public float iFrameDuration = 0.8f;
    bool isInvulnerable = false;

    [Header("References (optional)")]
    public Animator animator;
    public MonoBehaviour disableOnDeath;

    Rigidbody2D rb;
    SpriteRenderer sr;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (animator == null)
            animator = GetComponent<Animator>();

        // 체력바 초기화를 Start에서 하도록 변경
    }

    void Start()
    {
        // 체력바 초기화
        UpdateBarsInstant();
        
        // 패링바 초기화
        UpdateParryBarInstant();
        
        // 디버그 로그
        Debug.Log($"[PlayerHealth] 초기화 완료 - 체력: {currentHealth}/{maxHealth}");
        Debug.Log($"[PlayerHealth] RedBar: {(redBar != null ? "설정됨" : "NULL")}");
        Debug.Log($"[PlayerHealth] YellowBar: {(yellowBar != null ? "설정됨" : "NULL")}");
        Debug.Log($"[PlayerHealth] ParryBar: {(parryBar != null ? "설정됨" : "NULL")}");
        Debug.Log($"[PlayerHealth] 패링 횟수: {currentParryCount}/{maxParryCount}");
        
        // 패링바가 설정되지 않은 경우 경고
        if (parryBar == null)
        {
            Debug.LogError("[PlayerHealth] 패링바가 설정되지 않았습니다! PlayerController의 healthComponent에 PlayerHealth를 할당하고, PlayerHealth의 parryBar에 Image를 할당하세요.");
        }
    }

    public void TakeDamage(int amount)
    {
        if (isInvulnerable) return;
        
        Debug.Log($"[PlayerHealth] 피격! 체력: {currentHealth} -> {currentHealth - amount}");
        
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        // 빨간바 애니메이션 시작
        if (redBar != null)
        {
            StartCoroutine(AnimateRedBar());
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] RedBar가 설정되지 않았습니다!");
        }

        // 노란바는 코루틴으로 천천히 따라감
        if (yellowBar != null)
        {
            StartCoroutine(UpdateYellowBar());
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] YellowBar가 설정되지 않았습니다!");
        }

        // 애니메이션
        if (animator != null) animator.SetTrigger("Hurt");

        // 피격 연출
        StartCoroutine(HitFlash());

        // 무적
        StartCoroutine(IFrameCoroutine());

        if (currentHealth <= 0)
            Die();
    }

    IEnumerator AnimateRedBar()
    {
        float startFill = redBar.fillAmount;
        float target = (float)currentHealth / maxHealth;
        
        Debug.Log($"[PlayerHealth] 빨간바 애니메이션 시작 - 시작: {startFill:P}, 목표: {target:P}");
        
        float elapsedTime = 0f;
        while (elapsedTime < redBarAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / redBarAnimationDuration;
            
            // 빠르고 부드러운 애니메이션을 위해 EaseOut 커브 사용
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f); // EaseOut Cubic
            
            redBar.fillAmount = Mathf.Lerp(startFill, target, easedProgress);
            
            yield return null;
        }
        
        // 최종값 보장
        redBar.fillAmount = target;
        Debug.Log("[PlayerHealth] 빨간바 애니메이션 완료");
    }

    IEnumerator UpdateYellowBar()
    {
        Debug.Log($"[PlayerHealth] 노란바 애니메이션 시작 - 지연시간: {yellowBarDelay}초");
        Debug.Log($"[PlayerHealth] 현재 노란바 FillAmount: {yellowBar.fillAmount:P}");
        
        yield return new WaitForSeconds(yellowBarDelay);

        float target = (float)currentHealth / maxHealth;
        float startFill = yellowBar.fillAmount;
        
        Debug.Log($"[PlayerHealth] 노란바 애니메이션 - 시작: {startFill:P}, 목표: {target:P}");
        
        if (startFill <= target)
        {
            Debug.Log("[PlayerHealth] 노란바가 이미 목표값 이하입니다. 애니메이션 스킵.");
            yield break;
        }
        
        float animationDuration = (startFill - target) / yellowBarSpeed;
        Debug.Log($"[PlayerHealth] 예상 애니메이션 시간: {animationDuration:F2}초");
        
        float elapsedTime = 0f;
        while (yellowBar.fillAmount > target)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            
            // 더 부드러운 애니메이션을 위해 Lerp 사용
            yellowBar.fillAmount = Mathf.Lerp(startFill, target, progress);
            
            Debug.Log($"[PlayerHealth] 노란바 진행: {progress:P} - FillAmount: {yellowBar.fillAmount:P}");
            
            yield return null;
        }
        
        // 최종값 보장
        yellowBar.fillAmount = target;
        Debug.Log("[PlayerHealth] 노란바 애니메이션 완료");
    }

    IEnumerator HitFlash()
    {
        if (sr != null)
        {
            Color original = sr.color;
            sr.color = Color.white; // 하얗게 번쩍
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

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (rb != null) rb.simulated = false;

        Destroy(gameObject, 1f);
    }

    void UpdateBarsInstant()
    {
        float fillAmount = (float)currentHealth / maxHealth;
        
        if (redBar != null)
        {
            redBar.fillAmount = fillAmount;
            Debug.Log($"[PlayerHealth] 빨간바 즉시 업데이트: {fillAmount:P}");
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] RedBar가 설정되지 않았습니다!");
        }
        
        if (yellowBar != null)
        {
            yellowBar.fillAmount = fillAmount;
            Debug.Log($"[PlayerHealth] 노란바 즉시 업데이트: {fillAmount:P}");
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] YellowBar가 설정되지 않았습니다!");
        }
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateBarsInstant();
    }
    
    // 패링 관련 메서드들
    public void AddParryCount()
    {
        Debug.Log($"[PlayerHealth] AddParryCount 호출됨 - 현재: {currentParryCount}/{maxParryCount}");
        
        if (currentParryCount < maxParryCount)
        {
            currentParryCount++;
            Debug.Log($"[PlayerHealth] 패링 성공! 현재: {currentParryCount}/{maxParryCount}");
            
            // 패링바 애니메이션
            if (parryBar != null)
            {
                Debug.Log($"[PlayerHealth] 패링바 애니메이션 시작");
                StartCoroutine(AnimateParryBar());
            }
            else
            {
                Debug.LogError("[PlayerHealth] ParryBar가 NULL입니다! 패링바를 설정하세요.");
            }
            
            // 최대 패링 달성 시 강화 공격 가능
            if (currentParryCount >= maxParryCount)
            {
                Debug.Log("[PlayerHealth] 강화 공격 준비 완료!");
                OnParryBarFull();
            }
        }
        else
        {
            Debug.Log("[PlayerHealth] 패링 카운트가 이미 최대입니다!");
        }
    }
    
    public void ResetParryCount()
    {
        currentParryCount = 0;
        Debug.Log("[PlayerHealth] 패링 카운트 리셋");
        
        if (parryBar != null)
        {
            StartCoroutine(AnimateParryBar());
        }
    }
    
    public bool CanUseEnhancedAttack()
    {
        return currentParryCount >= maxParryCount;
    }
    
    public void UseEnhancedAttack()
    {
        if (CanUseEnhancedAttack())
        {
            Debug.Log("[PlayerHealth] 강화 공격 사용!");
            ResetParryCount();
            // 여기에 강화 공격 로직 추가
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] 강화 공격을 사용할 수 없습니다!");
        }
    }
    
    void OnParryBarFull()
    {
        // 패링바가 가득 찼을 때의 이벤트
        // 예: UI 효과, 사운드, 파티클 등
        Debug.Log("[PlayerHealth] 패링바 가득참! 강화 공격 준비됨!");
    }
    
    IEnumerator AnimateParryBar()
    {
        float startFill = parryBar.fillAmount;
        float target = (float)currentParryCount / maxParryCount;
        
        Debug.Log($"[PlayerHealth] 패링바 애니메이션 시작 - 시작: {startFill:P}, 목표: {target:P}");
        
        float elapsedTime = 0f;
        while (elapsedTime < parryBarAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / parryBarAnimationDuration;
            
            // 빠르고 부드러운 애니메이션을 위해 EaseOut Cubic 사용
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f); // EaseOut Cubic
            
            parryBar.fillAmount = Mathf.Lerp(startFill, target, easedProgress);
            
            yield return null;
        }
        
        // 최종값 보장
        parryBar.fillAmount = target;
        Debug.Log("[PlayerHealth] 패링바 애니메이션 완료");
    }
    
    void UpdateParryBarInstant()
    {
        if (parryBar != null)
        {
            parryBar.fillAmount = (float)currentParryCount / maxParryCount;
            Debug.Log($"[PlayerHealth] 패링바 즉시 업데이트: {parryBar.fillAmount:P}");
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] ParryBar가 설정되지 않았습니다!");
        }
    }
    
    // 테스트용 메서드들
    [ContextMenu("테스트 대미지")]
    public void TestDamage()
    {
        TakeDamage(1);
    }
    
    [ContextMenu("테스트 힐")]
    public void TestHeal()
    {
        Heal(1);
    }
    
    [ContextMenu("빨간바 애니메이션 테스트")]
    public void TestRedBarAnimation()
    {
        if (redBar == null)
        {
            Debug.LogError("[PlayerHealth] RedBar가 설정되지 않았습니다!");
            return;
        }
        
        // 빨간바를 100%로 설정
        redBar.fillAmount = 1f;
        Debug.Log("[PlayerHealth] 빨간바를 100%로 설정했습니다.");
        
        // 1초 후 애니메이션 시작
        StartCoroutine(TestRedBarCoroutine());
    }
    
    IEnumerator TestRedBarCoroutine()
    {
        yield return new WaitForSeconds(1f);
        
        // 체력을 50%로 설정
        currentHealth = maxHealth / 2;
        Debug.Log($"[PlayerHealth] 체력을 {currentHealth}로 설정했습니다.");
        
        // 빨간바 애니메이션 시작
        StartCoroutine(AnimateRedBar());
    }
    
    [ContextMenu("노란바 애니메이션 강제 테스트")]
    public void TestYellowBarAnimation()
    {
        if (yellowBar == null)
        {
            Debug.LogError("[PlayerHealth] YellowBar가 설정되지 않았습니다!");
            return;
        }
        
        // 노란바를 100%로 설정
        yellowBar.fillAmount = 1f;
        Debug.Log("[PlayerHealth] 노란바를 100%로 설정했습니다.");
        
        // 1초 후 애니메이션 시작
        StartCoroutine(TestYellowBarCoroutine());
    }
    
    IEnumerator TestYellowBarCoroutine()
    {
        yield return new WaitForSeconds(1f);
        
        // 체력을 50%로 설정
        currentHealth = maxHealth / 2;
        Debug.Log($"[PlayerHealth] 체력을 {currentHealth}로 설정했습니다.");
        
        // 노란바 애니메이션 시작
        StartCoroutine(UpdateYellowBar());
    }
    
    [ContextMenu("전체 애니메이션 테스트")]
    public void TestFullAnimation()
    {
        if (redBar == null || yellowBar == null)
        {
            Debug.LogError("[PlayerHealth] RedBar 또는 YellowBar가 설정되지 않았습니다!");
            return;
        }
        
        // 두 바 모두 100%로 설정
        redBar.fillAmount = 1f;
        yellowBar.fillAmount = 1f;
        Debug.Log("[PlayerHealth] 두 바 모두 100%로 설정했습니다.");
        
        // 1초 후 전체 애니메이션 시작
        StartCoroutine(TestFullAnimationCoroutine());
    }
    
    IEnumerator TestFullAnimationCoroutine()
    {
        yield return new WaitForSeconds(1f);
        
        // 체력을 30%로 설정
        currentHealth = Mathf.RoundToInt(maxHealth * 0.3f);
        Debug.Log($"[PlayerHealth] 체력을 {currentHealth}로 설정했습니다.");
        
        // 빨간바와 노란바 애니메이션 동시 시작
        StartCoroutine(AnimateRedBar());
        StartCoroutine(UpdateYellowBar());
    }
    
    [ContextMenu("패링 카운트 추가")]
    public void TestAddParry()
    {
        AddParryCount();
    }
    
    [ContextMenu("패링 카운트 리셋")]
    public void TestResetParry()
    {
        ResetParryCount();
    }
    
    [ContextMenu("강화 공격 사용")]
    public void TestUseEnhancedAttack()
    {
        UseEnhancedAttack();
    }
    
    [ContextMenu("패링바 애니메이션 테스트")]
    public void TestParryBarAnimation()
    {
        if (parryBar == null)
        {
            Debug.LogError("[PlayerHealth] ParryBar가 설정되지 않았습니다!");
            return;
        }
        
        // 패링바를 0%로 설정
        parryBar.fillAmount = 0f;
        currentParryCount = 0;
        Debug.Log("[PlayerHealth] 패링바를 0%로 설정했습니다.");
        
        // 1초 후 애니메이션 시작
        StartCoroutine(TestParryBarCoroutine());
    }
    
    IEnumerator TestParryBarCoroutine()
    {
        yield return new WaitForSeconds(1f);
        
        // 패링 카운트를 최대로 설정
        currentParryCount = maxParryCount;
        Debug.Log($"[PlayerHealth] 패링 카운트를 {currentParryCount}로 설정했습니다.");
        
        // 패링바 애니메이션 시작
        StartCoroutine(AnimateParryBar());
    }
    
    [ContextMenu("체력바 상태 확인")]
    public void CheckHealthBarStatus()
    {
        Debug.Log($"=== 체력바 상태 ===");
        Debug.Log($"현재 체력: {currentHealth}/{maxHealth}");
        Debug.Log($"RedBar: {(redBar != null ? $"설정됨, FillAmount: {redBar.fillAmount:P}" : "NULL")}");
        Debug.Log($"YellowBar: {(yellowBar != null ? $"설정됨, FillAmount: {yellowBar.fillAmount:P}" : "NULL")}");
        Debug.Log($"ParryBar: {(parryBar != null ? $"설정됨, FillAmount: {parryBar.fillAmount:P}" : "NULL")}");
        Debug.Log($"패링 횟수: {currentParryCount}/{maxParryCount}");
        Debug.Log($"강화 공격 가능: {CanUseEnhancedAttack()}");
        Debug.Log($"YellowBarDelay: {yellowBarDelay}");
        Debug.Log($"YellowBarSpeed: {yellowBarSpeed}");
        Debug.Log($"ParryBarAnimationDuration: {parryBarAnimationDuration}");
    }
}
