using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHp = 20;
    [SerializeField] private int currentHp = 0;
    public int CurrentHp => currentHp;

    [Header("UI (optional)")]
    public Slider hpBar;
    public Text hpText;

    [Header("Hit Reaction")]
    public float invulnTime = 0.6f;
    public float staggerTime = 0.18f;
    public float hitMoveDistance = 0.25f;
    public bool useScriptPush = true;

    [Header("VFX / SFX")]
    public GameObject hitEffectPrefab;
    public AudioClip hitSfx;
    public AudioClip deathSfx;
    public float hitEffectYOffset = 0.5f;

    [Header("Optional refs")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Transform playerTransform;

    [Header("Events (optional)")]
    public UnityEvent onDamaged;
    public UnityEvent onDied;

    // =======================
    // White Flash (real white)
    // =======================
    [Header("White Flash (real white)")]
    public float flashDuration = 0.12f;
    public AnimationCurve flashCurve;
    public Material flashSwapMaterial; // 폴백용 “완전 백색” 머티리얼(선택)

    private static readonly int FlashProp = Shader.PropertyToID("_FlashAmount");
    private bool _hasFlashProp = false;
    private MaterialPropertyBlock _mpb;
    private Material _cachedOriginalMat;
    private Color _cachedOrigColor;

    // =======================
    // Squash & Stretch (on hit)
    // =======================
    [Header("Squash & Stretch (on hit)")]
    [Tooltip("피격 순간 찌그러졌다가 펴지기")]
    public bool useSquashOnHit = true;

    [Tooltip("스케일을 적용할 대상(보통 GfxRoot). 비우면 SpriteRenderer의 Transform")]
    public Transform squashTarget;

    [Tooltip("찌그러지는 구간 시간")]
    public float squashDuration = 0.10f;

    [Tooltip("펴지는(복귀) 구간 시간")]
    public float reboundDuration = 0.08f;

    [Tooltip("가로 배율(>1 넓게)")]
    [Range(0.5f, 1.8f)] public float squashX = 1.08f;

    [Tooltip("세로 배율(<1 낮게)")]
    [Range(0.3f, 1.5f)] public float squashY = 0.85f;

    [Tooltip("0~1 커브. 비우면 부드러운 EaseInOut")]
    public AnimationCurve squashCurve;
    public AnimationCurve reboundCurve;

    [Tooltip("복귀 시 살짝 오버슈트(탄성) 줄지")]
    public bool reboundOvershoot = true;

    [Tooltip("오버슈트 정도(0~1). 0.6 추천")]
    [Range(0f, 1f)] public float reboundFactor = 0.6f;

    [Tooltip("이미 실행 중일 때 또 맞으면 새로 시작(중첩 X). 꺼두면 이전 효과를 끊고 다시 시작")]
    public bool allowSquashOverlap = false;

    private Coroutine _squashCR;

    // internals
    private bool isInvulnerable = false;
    private Rigidbody2D rb;
    private AudioSource audioSource;

    void Awake()
    {
        if (currentHp <= 0) currentHp = maxHp;

        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!animator) animator = GetComponent<Animator>();

        if (hpBar) { hpBar.maxValue = maxHp; hpBar.value = currentHp; }
        if (hpText) hpText.text = $"{currentHp} / {maxHp}";

        if (!playerTransform)
        {
            var pl = GameObject.FindWithTag("Player");
            if (pl) playerTransform = pl.transform;
        }

        // Flash 준비
        if (spriteRenderer)
        {
            _cachedOrigColor = spriteRenderer.color;
            _mpb = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(_mpb);

            var mat = spriteRenderer.sharedMaterial;
            _hasFlashProp = (mat && mat.HasProperty(FlashProp));
            _cachedOriginalMat = mat;
        }

        if (flashCurve == null || flashCurve.length == 0)
        {
            flashCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1);
            flashCurve.AddKey(1, 0);
        }

        // Squash 기본 커브
        if (squashCurve == null || squashCurve.length == 0)
            squashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (reboundCurve == null || reboundCurve.length == 0)
            reboundCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (!squashTarget && spriteRenderer)
            squashTarget = spriteRenderer.transform;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (isInvulnerable) return;
        if (currentHp <= 0) return;

        currentHp = Mathf.Clamp(currentHp - amount, 0, maxHp);
        if (hpBar) hpBar.value = currentHp;
        if (hpText) hpText.text = $"{currentHp} / {maxHp}";

        if (hitEffectPrefab)
        {
            Vector3 fxPos = transform.position + Vector3.up * hitEffectYOffset;
            Instantiate(hitEffectPrefab, fxPos, Quaternion.identity);
        }

        if (AudioManager.Instance) AudioManager.Instance.PlayBossHurt();
        else if (audioSource && hitSfx) audioSource.PlayOneShot(hitSfx);

        if (animator)
        {
            foreach (var p in animator.parameters)
                if (p.name == "Hurt" && p.type == AnimatorControllerParameterType.Trigger)
                { animator.SetTrigger("Hurt"); break; }
        }

        onDamaged?.Invoke();

        // 동시에 진행: 하얀 플래시 + 스쿼시
        StartCoroutine(HitReactionCoroutine());
        if (useSquashOnHit && squashTarget)
        {
            if (_squashCR != null && !allowSquashOverlap) StopCoroutine(_squashCR);
            _squashCR = StartCoroutine(CoSquashPulse());
        }

        if (currentHp <= 0) Die();
    }

    IEnumerator HitReactionCoroutine()
    {
        isInvulnerable = true;

        // White flash
        yield return FlashWhiteOnce();

        // 넉백
        Vector2 push = Vector2.zero;
        if (playerTransform)
        {
            float dir = Mathf.Sign(transform.position.x - playerTransform.position.x);
            push = new Vector2(dir * hitMoveDistance, 0f);
        }
        if (useScriptPush && rb) rb.MovePosition(rb.position + push);
        else if (useScriptPush) transform.position += (Vector3)push;

        // 짧은 스태거
        yield return new WaitForSeconds(staggerTime);

        // 남은 무적 시간
        float remaining = Mathf.Max(0f, invulnTime - flashDuration - staggerTime);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        isInvulnerable = false;
    }

    // --- White Flash ---
    IEnumerator FlashWhiteOnce()
    {
        if (!spriteRenderer || flashDuration <= 0f) yield break;
        float t = 0f;

        if (_hasFlashProp)
        {
            spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashProp, 0f);
            spriteRenderer.SetPropertyBlock(_mpb);

            while (t < flashDuration)
            {
                float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, flashDuration));
                float v = Mathf.Clamp01(flashCurve.Evaluate(a));
                spriteRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(FlashProp, v);
                spriteRenderer.SetPropertyBlock(_mpb);
                t += Time.deltaTime;
                yield return null;
            }
            spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashProp, 0f);
            spriteRenderer.SetPropertyBlock(_mpb);
        }
        else if (flashSwapMaterial)
        {
            var prev = spriteRenderer.sharedMaterial;
            spriteRenderer.sharedMaterial = flashSwapMaterial;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.sharedMaterial = prev;
        }
        else
        {
            var orig = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = orig;
        }
    }

    // --- Squash & Stretch ---
    IEnumerator CoSquashPulse()
    {
        // 현재 방향(부호) 보존
        Vector3 baseScale = squashTarget.localScale;
        Vector3 sign = new Vector3(
            Mathf.Sign(baseScale.x == 0 ? 1 : baseScale.x),
            Mathf.Sign(baseScale.y == 0 ? 1 : baseScale.y),
            Mathf.Sign(baseScale.z == 0 ? 1 : baseScale.z)
        );

        Vector3 baseAbs = new Vector3(Mathf.Abs(baseScale.x), Mathf.Abs(baseScale.y), Mathf.Abs(baseScale.z));

        // 목표 스케일(찌그러짐)
        Vector3 squashedAbs = new Vector3(baseAbs.x * squashX, baseAbs.y * squashY, baseAbs.z);

        // 살짝 튕겨오르는 오버슈트(선택)
        Vector3 stretchAbs = baseAbs;
        if (reboundOvershoot)
        {
            float stretchMulX = Mathf.Max(0.5f, 1f - (squashX - 1f) * reboundFactor);  // 가로는 살짝 더 좁게
            float stretchMulY = Mathf.Max(0.5f, 1f + (1f - squashY) * reboundFactor);  // 세로는 살짝 더 길게
            stretchAbs = new Vector3(baseAbs.x * stretchMulX, baseAbs.y * stretchMulY, baseAbs.z);
        }

        // 1) Squash
        yield return ScaleOverTime(squashTarget, baseAbs, squashedAbs, squashDuration, squashCurve, sign);

        // 2) Rebound(오버슈트) → 3) Base 복귀
        if (reboundOvershoot)
        {
            yield return ScaleOverTime(squashTarget, squashedAbs, stretchAbs, reboundDuration, reboundCurve, sign);
            yield return ScaleOverTime(squashTarget, stretchAbs, baseAbs, reboundDuration * 0.6f, reboundCurve, sign);
        }
        else
        {
            yield return ScaleOverTime(squashTarget, squashedAbs, baseAbs, reboundDuration, reboundCurve, sign);
        }
    }

    static IEnumerator ScaleOverTime(Transform target, Vector3 fromAbs, Vector3 toAbs, float time, AnimationCurve curve, Vector3 sign)
    {
        if (!target) yield break;
        if (time <= 0f)
        {
            target.localScale = Vector3.Scale(toAbs, sign);
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            float a = Mathf.Clamp01(t / time);
            float k = (curve != null && curve.length > 0) ? curve.Evaluate(a) : a;
            Vector3 v = Vector3.LerpUnclamped(fromAbs, toAbs, k);
            target.localScale = Vector3.Scale(v, sign);
            t += Time.deltaTime;
            yield return null;
        }
        target.localScale = Vector3.Scale(toAbs, sign);
    }

    void Die()
    {
        GetComponent<BossDeathDust>()?.Play();
        if (currentHp > 0) currentHp = 0;

        BossKillTeleportDirector.SignalBossDied();

        if (animator)
        {
            foreach (var p in animator.parameters)
                if (p.name == "Die" && p.type == AnimatorControllerParameterType.Trigger)
                { animator.SetTrigger("Die"); break; }
        }

        if (AudioManager.Instance) AudioManager.Instance.PlayBossDeath();
        else if (audioSource && deathSfx) audioSource.PlayOneShot(deathSfx);

        onDied?.Invoke();

        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        StopAllCoroutines(); // 플래시/스쿼시 정리

        foreach (var mb in GetComponents<MonoBehaviour>())
            if (mb != this) mb.enabled = false;

        if (rb) rb.simulated = false;

        Destroy(gameObject, 1.2f);
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        if (hpBar) hpBar.value = currentHp;
        if (hpText) hpText.text = $"{currentHp} / {maxHp}";
    }

    public void SetHp(int hp)
    {
        currentHp = Mathf.Clamp(hp, 0, maxHp);
        if (hpBar) hpBar.value = currentHp;
        if (hpText) hpText.text = $"{currentHp} / {maxHp}";
    }

    public bool IsInvulnerable() => isInvulnerable;
}
