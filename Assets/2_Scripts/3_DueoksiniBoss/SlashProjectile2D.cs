using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlashProjectile2D : MonoBehaviour
{
    [Header("Damage & Speed (Prefab-owned)")]
    public int damage = 1;
    public float startSpeed = 3f;
    public float fastSpeed = 14f;
    public float accelDelay = 0.12f;
    public float accelTime = 0.35f;
    public AnimationCurve speedCurve;

    [Header("Life/Hit")]
    public float lifeTime = 5f;
    public LayerMask hitMask = ~0;
    public bool destroyOnHit = true;

    [Header("Graphics (optional)")]
    public Animator gfxAnimator;
    public SpriteRenderer spriteRenderer;
    public Transform gfxFlipRoot;
    public DirectionalAnimPairSync pairSync;
    [Tooltip("DirectionalAnimPairSync 사용할 때의 베이스 이름(예: \"Fly\")")]
    public string flyBase = "Fly";
    public float animCrossFade = 0f;
    [Tooltip("PairSync가 없을 때 flip 처리할지")]
    public bool flipIfNoPairSync = true;

    // =========================
    // NEW: Scale morph over life (visual-only)
    // =========================
    [Header("Scale Morph Over Life (visual)")]
    [Tooltip("활성화 시, 수명 진행에 따라 X↑, Y↓로 비주얼 스케일 변화")]
    public bool scaleMorph = true;

    [Tooltip("스케일 기준 대상(비우면 gfxFlipRoot → 없으면 transform)")]
    public Transform scaleTarget;

    [Tooltip("수명 0일 때 X배수")]
    public float scaleX_From = 1f;
    [Tooltip("수명 1일 때 X배수")]
    public float scaleX_To = 1.35f;

    [Tooltip("수명 0일 때 Y배수")]
    public float scaleY_From = 1f;
    [Tooltip("수명 1일 때 Y배수")]
    public float scaleY_To = 0.7f;

    [Tooltip("0~1 진행 보간 커브(없으면 Linear)")]
    public AnimationCurve scaleCurve = null;

    // runtime
    Transform _owner;
    Vector2 _dir = Vector2.right;
    float _age = 0f;
    float _accelT = 0f;

    // scale cache (드리프트 방지용)
    Vector3 _baseAbs;   // |기본 스케일|
    Vector3 _sign;      // 부호(+1/-1)

    void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (!gfxAnimator) gfxAnimator = GetComponentInChildren<Animator>(true);
        if (!gfxFlipRoot) gfxFlipRoot = spriteRenderer ? spriteRenderer.transform : transform;
        if (!pairSync) pairSync = GetComponentInChildren<DirectionalAnimPairSync>(true);
        if (speedCurve == null || speedCurve.length == 0) speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (!scaleTarget) scaleTarget = gfxFlipRoot ? gfxFlipRoot : transform;
        if (scaleCurve == null || scaleCurve.length == 0) scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        CacheBaseScale(); // 초기 스케일 저장
    }

    void CacheBaseScale()
    {
        var s = (scaleTarget ? scaleTarget.localScale : transform.localScale);
        _sign = new Vector3(
            Mathf.Sign(s.x == 0 ? 1 : s.x),
            Mathf.Sign(s.y == 0 ? 1 : s.y),
            Mathf.Sign(s.z == 0 ? 1 : s.z)
        );
        _baseAbs = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    public void LaunchWithPrefabDefaults(Vector2 dir, Transform owner, bool facingRightForAnim)
    {
        _owner = owner;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        _age = 0f; _accelT = 0f;

        ApplyFacingForGraphics(facingRightForAnim);
        CacheBaseScale(); // 발사 시점 스케일을 기준으로 사용
        enabled = true;
    }

    public void Launch(Vector2 dir, Transform owner, int dmg, float start, float fast, float dly, float time,
                       AnimationCurve curve, float life, LayerMask mask, bool destroyOnContact, bool facingRightForAnim)
    {
        damage = dmg; startSpeed = start; fastSpeed = fast;
        accelDelay = dly; accelTime = time; if (curve != null && curve.length > 0) speedCurve = curve;
        lifeTime = life; hitMask = mask; destroyOnHit = destroyOnContact;
        LaunchWithPrefabDefaults(dir, owner, facingRightForAnim);
    }

    void ApplyFacingForGraphics(bool right)
    {
        if (pairSync)
        {
            pairSync.facingRight = right;
            pairSync.PlayByBase(string.IsNullOrEmpty(flyBase) ? "Fly" : flyBase, animCrossFade);
        }
        else if (flipIfNoPairSync)
        {
            if (spriteRenderer) spriteRenderer.flipX = !right;
            // 스케일 부호는 _sign로 관리하므로 여기선 건드리지 않음
        }
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age >= lifeTime) { Destroy(gameObject); return; }

        // 이동 속도
        float speed;
        if (_age < accelDelay) speed = startSpeed;
        else
        {
            _accelT += Time.deltaTime;
            float a = Mathf.Clamp01(_accelT / Mathf.Max(0.0001f, accelTime));
            float k = (speedCurve != null && speedCurve.length > 0) ? speedCurve.Evaluate(a) : a;
            speed = Mathf.LerpUnclamped(startSpeed, fastSpeed, k);
        }
        transform.position += (Vector3)(_dir * speed * Time.deltaTime);

        // NEW: 수명 진행 비율로 비주얼 스케일 보간 (드리프트 없이 절대 계산)
        if (scaleMorph && lifeTime > 0f && scaleTarget)
        {
            float u = Mathf.Clamp01(_age / Mathf.Max(0.0001f, lifeTime));
            float k = (scaleCurve != null && scaleCurve.length > 0) ? scaleCurve.Evaluate(u) : u;

            float mx = Mathf.Lerp(scaleX_From, scaleX_To, k);
            float my = Mathf.Lerp(scaleY_From, scaleY_To, k);

            Vector3 abs = new Vector3(_baseAbs.x * mx, _baseAbs.y * my, _baseAbs.z);
            scaleTarget.localScale = Vector3.Scale(abs, _sign);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_owner && other.transform == _owner) return;
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        var dmgTarget = other.GetComponentInParent<IDamageable>();
        if (dmgTarget != null) dmgTarget.TakeDamage(damage);

        if (destroyOnHit) Destroy(gameObject);
    }
}
