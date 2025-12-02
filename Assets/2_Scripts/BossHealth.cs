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
    [Tooltip("※ 무적시간은 사용하지 않습니다(호환용 필드).")]
    public float invulnTime = 0.6f;   // 논리적으로 사용하지 않음(호환만)
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
    public Material flashSwapMaterial; // “완전 백색” 대체 머티리얼(선택)

    private static readonly int FlashProp = Shader.PropertyToID("_FlashAmount");
    private bool _hasFlashProp = false;
    private MaterialPropertyBlock _mpb;
    private Material _cachedOriginalMat;
    private Color _cachedOrigColor;
    private Coroutine _flashCR;
    private bool _flashUsingSwap = false;

    // =======================
    // Squash & Stretch (on hit)
    // =======================
    [Header("Squash & Stretch (on hit)")]
    public bool useSquashOnHit = true;
    [Tooltip("스케일을 적용할 대상(보통 GfxRoot). 비우면 SpriteRenderer의 Transform")]
    public Transform squashTarget;
    public float squashDuration = 0.10f;
    public float reboundDuration = 0.08f;
    [Range(0.5f, 1.8f)] public float squashX = 1.08f;
    [Range(0.3f, 1.5f)] public float squashY = 0.85f;
    public AnimationCurve squashCurve;
    public AnimationCurve reboundCurve;
    [Tooltip("복귀 시 살짝 오버슈트(탄성)")]
    public bool reboundOvershoot = true;
    [Range(0f, 1f)] public float reboundFactor = 0.6f;

    [Tooltip("이미 실행 중일 때 또 맞으면 겹치게 둘지(권장: 끄기). 켜도 동시 중첩은 막고, 기존 효과를 유지만 함.")]
    public bool allowSquashOverlap = false;

    private Coroutine _squashCR;

    // 드리프트 방지용 ‘중립 스케일’ 절대값 보관
    private Vector3 _squashRestAbs;
    private bool _squashRestCaptured;

    // ── 무적 플래그는 호환만 유지(항상 false)
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

        if (squashCurve == null || squashCurve.length == 0)
            squashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (reboundCurve == null || reboundCurve.length == 0)
            reboundCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (!squashTarget && spriteRenderer)
            squashTarget = spriteRenderer.transform;

        CacheSquashRestFromCurrent();
    }

    void OnEnable()
    {
        CacheSquashRestFromCurrent();
        EnsureRestScale(); // 에디터에서 값 만지다가 플레이 시작해도 중립 보장
    }

    // === ‘중립 스케일’ 캡처/복구 ===
    void CacheSquashRestFromCurrent()
    {
        if (!squashTarget) return;
        var ls = squashTarget.localScale;
        _squashRestAbs = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
        _squashRestCaptured = true;
    }

    Vector3 SignVec(Vector3 v) => new Vector3(
        Mathf.Sign(v.x == 0 ? 1 : v.x),
        Mathf.Sign(v.y == 0 ? 1 : v.y),
        Mathf.Sign(v.z == 0 ? 1 : v.z)
    );

    void EnsureRestScale()
    {
        if (!squashTarget || !_squashRestCaptured) return;
        var sign = SignVec(squashTarget.localScale);               // 현재 방향 유지
        squashTarget.localScale = Vector3.Scale(_squashRestAbs, sign);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (currentHp <= 0) return;

        // ※ 무적 제거 → 항상 대미지 적용
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

        // 플래시(중복 방지)
        if (_flashCR != null) { StopCoroutine(_flashCR); RestoreSpriteVisual(); }
        _flashCR = StartCoroutine(FlashWhiteOnce());

        // 스쿼시 — 드리프트 방지: 항상 ‘중립 스케일’ 기준
        if (useSquashOnHit && squashTarget)
        {
            if (_squashCR != null)
            {
                if (!allowSquashOverlap)
                {
                    StopCoroutine(_squashCR);
                    _squashCR = null;
                    EnsureRestScale();    // 기준점 재고정
                    _squashCR = StartCoroutine(CoSquashPulse());
                }
                // 겹침 허용이면 현재 효과 유지(새로 시작 안 함) → 누적 깨짐 방지
            }
            else
            {
                _squashCR = StartCoroutine(CoSquashPulse());
            }
        }

        // 넉백/스태거( i-frame 없음 )
        StartCoroutine(HitReactionCoroutine());

        if (currentHp <= 0) Die();
    }

    IEnumerator HitReactionCoroutine()
    {
        // 넉백
        Vector2 push = Vector2.zero;
        if (playerTransform)
        {
            float dir = Mathf.Sign(transform.position.x - playerTransform.position.x);
            push = new Vector2(dir * hitMoveDistance, 0f);
        }
        if (useScriptPush && rb) rb.MovePosition(rb.position + push);
        else if (useScriptPush) transform.position += (Vector3)push;

        if (staggerTime > 0f)
            yield return new WaitForSeconds(staggerTime);
    }

    // --- White Flash ---
    IEnumerator FlashWhiteOnce()
    {
        if (!spriteRenderer || flashDuration <= 0f) yield break;

        float t = 0f;

        if (_hasFlashProp)
        {
            spriteRenderer.GetPropertyBlock(_mpb ??= new MaterialPropertyBlock());
            _mpb.SetFloat(FlashProp, 0f);
            spriteRenderer.SetPropertyBlock(_mpb);

            while (t < flashDuration)
            {
                float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, flashDuration));
                float v = Mathf.Clamp01(flashCurve.Evaluate(a));
                spriteRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(FlashProp, v);
                spriteRenderer.SetPropertyBlock(_mpb);
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashProp, 0f);
            spriteRenderer.SetPropertyBlock(_mpb);
        }
        else if (flashSwapMaterial)
        {
            _flashUsingSwap = true;
            var prev = spriteRenderer.sharedMaterial;
            spriteRenderer.sharedMaterial = flashSwapMaterial;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.sharedMaterial = prev;
            _flashUsingSwap = false;
        }
        else
        {
            var orig = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = orig;
        }
    }

    void RestoreSpriteVisual()
    {
        if (!spriteRenderer) return;

        if (_hasFlashProp)
        {
            spriteRenderer.GetPropertyBlock(_mpb ??= new MaterialPropertyBlock());
            _mpb.SetFloat(FlashProp, 0f);
            spriteRenderer.SetPropertyBlock(_mpb);
        }

        if (_flashUsingSwap && _cachedOriginalMat)
        {
            spriteRenderer.sharedMaterial = _cachedOriginalMat;
            _flashUsingSwap = false;
        }

        spriteRenderer.color = _cachedOrigColor;
    }

    void OnDisable() { RestoreSpriteVisual(); EnsureRestScale(); }
    void OnDestroy() { RestoreSpriteVisual(); }

    // --- Squash & Stretch (절대값 기반: 드리프트 없음) ---
    IEnumerator CoSquashPulse()
    {
        if (!_squashRestCaptured) CacheSquashRestFromCurrent();
        if (!_squashRestCaptured) yield break;

        // 목표 스케일들(절대값 기준)
        Vector3 restAbs = _squashRestAbs;
        Vector3 squashedAbs = new Vector3(restAbs.x * squashX, restAbs.y * squashY, restAbs.z);

        Vector3 stretchAbs = restAbs;
        if (reboundOvershoot)
        {
            float stretchMulX = Mathf.Max(0.5f, 1f - (squashX - 1f) * reboundFactor);
            float stretchMulY = Mathf.Max(0.5f, 1f + (1f - squashY) * reboundFactor);
            stretchAbs = new Vector3(restAbs.x * stretchMulX, restAbs.y * stretchMulY, restAbs.z);
        }

        // 1) Squash
        yield return ScaleOverTimeAbs(squashTarget, restAbs, squashedAbs, squashDuration, squashCurve);

        // 2) Rebound → 3) Rest
        if (reboundOvershoot)
        {
            yield return ScaleOverTimeAbs(squashTarget, squashedAbs, stretchAbs, reboundDuration, reboundCurve);
            yield return ScaleOverTimeAbs(squashTarget, stretchAbs, restAbs, reboundDuration * 0.6f, reboundCurve);
        }
        else
        {
            yield return ScaleOverTimeAbs(squashTarget, squashedAbs, restAbs, reboundDuration, reboundCurve);
        }

        // 완료 후 정확히 중립 세팅(부동소수 오차 제거)
        EnsureRestScale();
        _squashCR = null;
    }

    static IEnumerator ScaleOverTimeAbs(Transform target, Vector3 fromAbs, Vector3 toAbs, float time, AnimationCurve curve)
    {
        if (!target) yield break;

        if (time <= 0f)
        {
            var sign = new Vector3(
                Mathf.Sign(target.localScale.x == 0 ? 1 : target.localScale.x),
                Mathf.Sign(target.localScale.y == 0 ? 1 : target.localScale.y),
                Mathf.Sign(target.localScale.z == 0 ? 1 : target.localScale.z)
            );
            target.localScale = Vector3.Scale(toAbs, sign);
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            float a = Mathf.Clamp01(t / time);
            float k = (curve != null && curve.length > 0) ? curve.Evaluate(a) : a;

            var sign = new Vector3(
                Mathf.Sign(target.localScale.x == 0 ? 1 : target.localScale.x),
                Mathf.Sign(target.localScale.y == 0 ? 1 : target.localScale.y),
                Mathf.Sign(target.localScale.z == 0 ? 1 : target.localScale.z)
            );

            Vector3 v = Vector3.LerpUnclamped(fromAbs, toAbs, k);
            target.localScale = Vector3.Scale(v, sign);

            t += Time.deltaTime;
            yield return null;
        }

        {
            var sign = new Vector3(
                Mathf.Sign(target.localScale.x == 0 ? 1 : target.localScale.x),
                Mathf.Sign(target.localScale.y == 0 ? 1 : target.localScale.y),
                Mathf.Sign(target.localScale.z == 0 ? 1 : target.localScale.z)
            );
            target.localScale = Vector3.Scale(toAbs, sign);
        }
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

        RestoreSpriteVisual();
        if (_squashCR != null) { StopCoroutine(_squashCR); _squashCR = null; }
        EnsureRestScale();

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

    // 호환용: 항상 false (무적 없음)
    public bool IsInvulnerable() => false;
}
