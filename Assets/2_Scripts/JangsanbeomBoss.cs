// JangsanbeomBoss.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomBoss : MonoBehaviour
{
    [System.Serializable]
    public class AttackData
    {
        public string Name = "ClawExecute";
        public Vector2 Offset = Vector2.right;   // local offset (boss root 기준)
        public Vector2 Size = new Vector2(3f, 1.5f);
        public float Angle = 0f; // degrees
        public int Damage = 2;
        public float Lifetime = 0.12f;

        // visuals
        public GameObject TelegraphPrefab;
        public float TelegraphTime = 0f;
        public bool DefaultFake = false;
        public Sprite FakeSprite;
        public float FakeSpriteDuration = 0.18f;
        public Transform OriginOverride;
    }

    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    [Header("Visual & Flip")]
    [Tooltip("비주얼 루트. 비워두면 모든 SpriteRenderer.flipX를 뒤집습니다.")]
    public Transform graphicsRoot;
    [Tooltip("플립 기준(꼬리 권장). Boss의 자식으로 두면 로컬 Offset을 편하게 설정 가능.")]
    public BoxCollider2D flipTrigger;
    [Tooltip("피벗이 없을 때 사용할 폴백 피벗")]
    public Transform flipPivot;
    public float flipPivotDeadzone = 0.6f;

    [Header("Flip tuning")]
    public float flipTriggerHysteresis = 0.06f;
    public float flipCooldown = 0.12f;
    [HideInInspector] public bool facingRight = true;
    public bool enforceVisualFlip = true;

    [Header("AI / Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Attack")]
    public GameObject hitboxPrefab;
    public float defaultHitboxLifetime = 0.12f;
    public Vector2 defaultClawSize = new Vector2(3f, 1.5f);
    public int defaultClawDamage = 2;

    [Header("Animator param names")]
    public string trig_ClawTelegraph = "ClawTelegraph";
    public string trig_ClawExecute = "ClawExecute";
    public string trig_ClawFakeTelegraph = "ClawFakeTelegraph";
    public string trig_ClawFakeExecute = "ClawFakeExecute";

    [Header("Animator - Movement")]
    public string animParam_MoveBool = "Move";
    public string animParam_MoveSpeed = "Speed";

    [Header("Behavior")]
    public bool useAnimationEvents = true;
    public bool enableFakeClaw = true;
    [Range(0f, 1f)] public float fakeChance = 0.25f;

    [Header("Editor-tunable attacks")]
    public List<AttackData> attacks = new List<AttackData>();

    [Header("Gizmos")]
    public bool showAttackGizmos = true;
    public Color gizmoRealColor = new Color(1f, 0.2f, 0.2f, 0.18f);
    public Color gizmoFakeColor = new Color(1f, 0.85f, 0.2f, 0.12f);

    [Header("Debug")]
    public bool debugFlip = false;
    public bool debugPoly = false;

    // internals
    Vector3 _rootOriginalScale;
    Vector3 _graphicsOriginalScale;
    float _lastFlipTime = -10f;
    int _prevTriggerSide = 0; // -1 left, 0 near, +1 right
    List<PolygonCollider2D> _polyColliders = new List<PolygonCollider2D>();
    List<Vector2[][]> _originalPolyPaths = new List<Vector2[][]>();
    Coroutine aiCoroutine;
    bool busy = false;
    [SerializeField, HideInInspector] float _flipTriggerOriginalLocalX = 0f;
    [SerializeField, HideInInspector] bool _flipTriggerCached = false;

    // animator tracking
    Vector3 _lastPosition;

    void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }

        _rootOriginalScale = transform.localScale;
        _rootOriginalScale.x = Mathf.Abs(_rootOriginalScale.x);
        transform.localScale = _rootOriginalScale;

        if (graphicsRoot != null)
        {
            _graphicsOriginalScale = graphicsRoot.localScale;
            _graphicsOriginalScale.x = Mathf.Abs(_graphicsOriginalScale.x);
            var s = graphicsRoot.localScale; s.x = _graphicsOriginalScale.x; graphicsRoot.localScale = s;
        }

        if (flipTrigger != null)
        {
            if (flipTrigger.transform.IsChildOf(transform))
            {
                _flipTriggerOriginalLocalX = flipTrigger.transform.localPosition.x;
                _flipTriggerCached = true;
                if (debugFlip) Debug.Log($"[Boss] Cached flipTrigger localX={_flipTriggerOriginalLocalX}", this);
            }
            else
            {
                _flipTriggerCached = false;
                if (debugFlip) Debug.Log("[Boss] flipTrigger is NOT a child of boss - using world bounds center for trigger checks.", this);
            }
        }

        CachePolygonColliderPaths();
    }

    void Start()
    {
        if (player == null) Debug.LogWarning("[JangsanbeomBoss] player not set.");
        if (hitboxPrefab == null) Debug.LogWarning("[JangsanbeomBoss] hitboxPrefab not set.");

        aiCoroutine = StartCoroutine(AIBehavior());

        if (player != null)
        {
            float rel = 0f;
            if (flipTrigger != null) rel = player.position.x - GetFlipTriggerWorldX();
            else if (flipPivot != null) rel = player.position.x - flipPivot.position.x;
            else rel = player.position.x - transform.position.x;

            _prevTriggerSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
            if (debugFlip) Debug.Log($"[Boss] init prevSide={_prevTriggerSide} rel={rel}", this);
        }

        _lastPosition = transform.position;
    }

    void Update()
    {
        if (player == null) return;

        // follow X only
        float dx = player.position.x - transform.position.x;
        bool isMoving = false;

        if (!busy && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            float prevX = transform.position.x;
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(transform.position.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = p;

            isMoving = Mathf.Abs(p.x - prevX) > 0.0001f;
        }

        // animator Move bool
        if (animator != null && !string.IsNullOrEmpty(animParam_MoveBool))
            animator.SetBool(animParam_MoveBool, isMoving);

        // flip decision
        if (Time.time - _lastFlipTime > flipCooldown)
        {
            if (flipTrigger != null)
            {
                float triggerX = GetFlipTriggerWorldX();
                float rel = player.position.x - triggerX;
                int curSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);

                if (curSide != _prevTriggerSide)
                {
                    if (curSide != 0)
                    {
                        bool wantRight = rel > 0f;
                        FlipTo(wantRight);
                        _lastFlipTime = Time.time;
                        if (debugFlip) Debug.Log($"Flip -> wantRight={wantRight} triggerX={triggerX} rel={rel}", this);
                    }
                }
            }
            else if (flipPivot != null)
            {
                float rel = player.position.x - flipPivot.position.x;
                int curSide = (rel > flipPivotDeadzone) ? 1 : (rel < -flipPivotDeadzone ? -1 : 0);
                if (curSide != _prevTriggerSide)
                {
                    if (curSide != 0)
                    {
                        bool wantRight = rel > 0f;
                        FlipTo(wantRight);
                        _lastFlipTime = Time.time;
                        if (debugFlip) Debug.Log($"Flip (pivot) -> wantRight={wantRight}", this);
                    }
                }
            }
            else
            {
                float rel = player.position.x - transform.position.x;
                int curSide = (rel > 0f) ? 1 : (rel < 0f ? -1 : 0);
                if (curSide != _prevTriggerSide)
                {
                    bool wantRight = rel > 0f;
                    FlipTo(wantRight);
                    _lastFlipTime = Time.time;
                    if (debugFlip) Debug.Log($"Flip (center) -> wantRight={wantRight}", this);
                }
            }
        }
    }

    void LateUpdate()
    {
        if (transform.localScale.x != _rootOriginalScale.x)
        {
            var s = transform.localScale; s.x = _rootOriginalScale.x; transform.localScale = s;
        }

        if (enforceVisualFlip) ApplyVisualFlipNow();

        if (animator != null && !string.IsNullOrEmpty(animParam_MoveSpeed))
        {
            float dt = Time.deltaTime;
            float vx = 0f;
            if (dt > 0f) vx = (transform.position.x - _lastPosition.x) / dt;
            float speedAbs = Mathf.Abs(vx);
            animator.SetFloat(animParam_MoveSpeed, speedAbs);
        }

        _lastPosition = transform.position;
    }

    // compute trigger world X without moving trigger object
    float GetFlipTriggerWorldX()
    {
        if (flipTrigger == null) return transform.position.x;

        if (flipTrigger.transform.IsChildOf(transform) && _flipTriggerCached)
        {
            float localX = _flipTriggerOriginalLocalX * (facingRight ? 1f : -1f);
            Vector3 world = transform.TransformPoint(new Vector3(localX, flipTrigger.transform.localPosition.y, 0f));
            return world.x;
        }
        return flipTrigger.bounds.center.x;
    }

    public void FlipTo(bool faceRight)
    {
        facingRight = faceRight;

        if (graphicsRoot != null)
        {
            var s = _graphicsOriginalScale;
            s.x = _graphicsOriginalScale.x * (faceRight ? 1f : -1f);
            graphicsRoot.localScale = s;
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }
        else
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.flipX = !faceRight;
            else FlipAllSpriteRenderers(faceRight);
        }

        ApplyFlipToPolygonColliders(faceRight);

        float refX = (flipTrigger != null) ? GetFlipTriggerWorldX() : (flipPivot != null ? flipPivot.position.x : transform.position.x);
        float relAfter = player != null ? (player.position.x - refX) : 0f;
        _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 : (relAfter < -flipTriggerHysteresis ? -1 : 0);

        if (debugFlip) Debug.Log($"[Boss] FlipTo({faceRight}) applied. refX={refX} relAfter={relAfter} prevSide={_prevTriggerSide}", this);
    }

    void ApplyVisualFlipNow()
    {
        if (graphicsRoot != null)
        {
            float wantX = (facingRight ? Mathf.Abs(_graphicsOriginalScale.x) : -Mathf.Abs(_graphicsOriginalScale.x));
            if (graphicsRoot.localScale.x != wantX)
            {
                var s = _graphicsOriginalScale; s.x = wantX; graphicsRoot.localScale = s;
            }
        }
        else FlipAllSpriteRenderers(facingRight);
    }

    void FlipAllSpriteRenderers(bool faceRight)
    {
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs) sr.flipX = !faceRight;
    }

    // Attacks (kept)
    public void StartAttackByIndex(int index = 0, bool forceReal = false)
    {
        if (busy) return;
        AttackData atk = null;
        if (index >= 0 && index < attacks.Count) atk = attacks[index];
        if (atk == null)
        {
            atk = new AttackData()
            {
                Name = "DefaultClaw",
                Offset = new Vector2(defaultClawSize.x * 0.5f, 0f),
                Size = defaultClawSize,
                Damage = defaultClawDamage,
                Lifetime = defaultHitboxLifetime
            };
        }
        StartCoroutine(ClawRoutine_Generic(atk, forceReal));
    }

    IEnumerator ClawRoutine_Generic(AttackData atk, bool forceReal)
    {
        busy = true;
        bool isFake = false;
        if (!forceReal && enableFakeClaw) isFake = Random.value < fakeChance || atk.DefaultFake;

        if (animator != null)
        {
            string trig = isFake ? trig_ClawFakeExecute : trig_ClawExecute;
            if (!string.IsNullOrEmpty(trig)) animator.SetTrigger(trig);
        }

        if (!useAnimationEvents)
        {
            if (isFake)
            {
                if (atk.FakeSprite != null && spriteRenderer != null)
                    StartCoroutine(TemporarilyShow(atk.FakeSprite, atk.FakeSpriteDuration));
                yield return new WaitForSeconds(0.06f);
                SpawnFakeHit(GetAttackWorldPos(atk), atk.Size, atk);
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
                SpawnRealHit(GetAttackWorldPos(atk), atk.Size, atk);
            }
        }

        if (atk.TelegraphTime > 0f) yield return new WaitForSeconds(atk.TelegraphTime);
        yield return new WaitForSeconds(0.01f);

        busy = false;
    }

    public void OnClawHitFrame()
    {
        AttackData atk = (attacks != null && attacks.Count > 0) ? attacks[0] : null;
        if (atk == null) SpawnRealHit(GetClawPos(), defaultClawSize);
        else SpawnRealHit(GetAttackWorldPos(atk), atk.Size, atk);
    }

    public void OnClawFakeHitFrame()
    {
        AttackData atk = (attacks != null && attacks.Count > 0) ? attacks[0] : null;
        if (atk == null) SpawnFakeHit(GetClawPos(), defaultClawSize);
        else SpawnFakeHit(GetAttackWorldPos(atk), atk.Size, atk);
    }

    void SpawnRealHit(Vector2 center, Vector2 size)
    {
        if (hitboxPrefab == null) { Debug.LogWarning("[JangsanbeomBoss] hitboxPrefab missing"); return; }
        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        ConfigureColliderShape(hb, size, (facingRight ? 1f : -1f));
        SetupHitboxComponent(hb, defaultClawDamage, "Player", 0, true);
        if (defaultHitboxLifetime > 0f) Destroy(hb, defaultHitboxLifetime + 0.02f);
    }

    void SpawnRealHit(Vector2 center, Vector2 size, AttackData atk)
    {
        if (hitboxPrefab == null) return;
        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        float dir = facingRight ? 1f : -1f;
        ConfigureColliderShape(hb, size, dir);
        SetupHitboxComponent(hb, atk.Damage, "Player", 0, true);
        if (atk.Lifetime > 0f) Destroy(hb, atk.Lifetime + 0.02f);
    }

    void SpawnFakeHit(Vector2 center, Vector2 size)
    {
        SpawnFakeHit(center, size, null);
    }

    void SpawnFakeHit(Vector2 center, Vector2 size, AttackData atk)
    {
        if (hitboxPrefab == null) return;
        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        float dir = facingRight ? 1f : -1f;
        ConfigureColliderShape(hb, size, dir);
        SetupHitboxComponent(hb, 0, "", 0, true);
        float life = (atk != null ? atk.Lifetime : defaultHitboxLifetime);
        if (life > 0f) Destroy(hb, life + 0.02f);
    }

    void ConfigureColliderShape(GameObject hb, Vector2 size, float dir)
    {
        hb.transform.localScale = Vector3.one;
        BoxCollider2D box = hb.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.offset = Vector2.zero;
            box.size = size;
            return;
        }

        PolygonCollider2D poly = hb.GetComponent<PolygonCollider2D>();
        if (poly != null)
        {
            poly.isTrigger = true;
            Vector2[] pts = MakeRectPoints(size);
            if (dir < 0f)
            {
                for (int i = 0; i < pts.Length; i++) pts[i].x = -pts[i].x;
                System.Array.Reverse(pts);
            }
            poly.pathCount = 1;
            poly.SetPath(0, pts);
            return;
        }

        hb.transform.localScale = new Vector3(size.x, size.y, 1f);
        Collider2D col = hb.GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void SetupHitboxComponent(GameObject hb, int damage, string targetTag, int targetLayerMask, bool singleUse)
    {
        var hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = damage;
            hbComp.targetTag = targetTag;
            hbComp.targetLayerMask = (LayerMask)targetLayerMask;
            hbComp.singleUse = singleUse;
        }
    }

    Vector2 GetClawPos()
    {
        float dir = facingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(defaultClawSize.x * 0.5f * dir, 0f);
    }

    Vector2 GetAttackWorldPos(AttackData atk)
    {
        float dir = facingRight ? 1f : -1f;
        Vector2 origin = (atk.OriginOverride != null) ? (Vector2)atk.OriginOverride.position : (Vector2)transform.position;
        Vector2 localOff = atk.Offset; localOff.x *= dir;
        float ang = (atk.Angle * dir) * Mathf.Deg2Rad;
        float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
        Vector2 rotated = new Vector2(localOff.x * c - localOff.y * s, localOff.x * s + localOff.y * c);
        return origin + rotated;
    }

    IEnumerator TemporarilyShow(Sprite s, float dur)
    {
        if (spriteRenderer == null) yield break;
        Sprite prev = spriteRenderer.sprite;
        spriteRenderer.sprite = s;
        yield return new WaitForSeconds(dur);
        if (spriteRenderer != null) spriteRenderer.sprite = prev;
    }

    IEnumerator AIBehavior()
    {
        while (true)
        {
            if (!busy && player != null)
            {
                float d = Mathf.Abs(player.position.x - transform.position.x);
                if (d <= aggroRange)
                {
                    int val = Random.Range(0, 100);
                    if (val < 60) StartAttackByIndex(0, false);
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.5f));
        }
    }

    void OnDrawGizmos()
    {
        if (!showAttackGizmos) return;
        if (attacks == null) return;

        for (int i = 0; i < attacks.Count; i++)
        {
            var a = attacks[i];
            bool isFake = a.DefaultFake;
            Color fill = isFake ? gizmoFakeColor : gizmoRealColor;
            Color wire = new Color(fill.r, fill.g, fill.b, Mathf.Min(1f, fill.a * 2f));

            Vector2 pos2;
            float angleDeg;
            if (Application.isPlaying)
            {
                pos2 = GetAttackWorldPos(a);
                angleDeg = a.Angle * (facingRight ? 1f : -1f);
            }
            else
            {
                float dir = (facingRight ? 1f : -1f);
                Vector2 origin = (a.OriginOverride != null) ? (Vector2)a.OriginOverride.position : (Vector2)transform.position;
                Vector2 off = a.Offset; off.x *= dir;
                float ang = (a.Angle * dir) * Mathf.Deg2Rad;
                float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                Vector2 rotated = new Vector2(off.x * c - off.y * s, off.x * s + off.y * c);
                pos2 = origin + rotated;
                angleDeg = a.Angle * dir;
            }

            Vector3 pos = new Vector3(pos2.x, pos2.y, 0f);
            Vector3 size = new Vector3(a.Size.x, a.Size.y, 0.01f);

            Matrix4x4 prev = Gizmos.matrix;
            Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);

            Gizmos.color = fill;
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(Vector3.zero, size * 1.001f);

            Gizmos.matrix = prev;
        }
    }

    void CachePolygonColliderPaths()
    {
        _polyColliders.Clear();
        _originalPolyPaths.Clear();
        var polys = GetComponentsInChildren<PolygonCollider2D>(true);
        foreach (var p in polys)
        {
            if (graphicsRoot != null && p.transform.IsChildOf(graphicsRoot)) continue;
            _polyColliders.Add(p);
            int pc = p.pathCount;
            Vector2[][] paths = new Vector2[pc][];
            for (int i = 0; i < pc; i++)
            {
                Vector2[] path = p.GetPath(i);
                Vector2[] copy = new Vector2[path.Length];
                System.Array.Copy(path, copy, path.Length);
                paths[i] = copy;
            }
            _originalPolyPaths.Add(paths);
        }
        if (debugPoly) Debug.Log($"Cached {_polyColliders.Count} polygon colliders", this);
    }

    void ApplyFlipToPolygonColliders(bool faceRight)
    {
        if (_polyColliders == null || _originalPolyPaths == null) return;
        for (int k = 0; k < _polyColliders.Count; k++)
        {
            var poly = _polyColliders[k];
            var origPaths = _originalPolyPaths[k];
            if (poly == null) continue;
            poly.pathCount = origPaths.Length;
            bool mirror = !faceRight;
            for (int i = 0; i < origPaths.Length; i++)
            {
                Vector2[] src = origPaths[i];
                Vector2[] dst = new Vector2[src.Length];
                if (!mirror) System.Array.Copy(src, dst, src.Length);
                else
                {
                    for (int j = 0; j < src.Length; j++) dst[j] = new Vector2(-src[j].x, src[j].y);
                    System.Array.Reverse(dst);
                }
                poly.SetPath(i, dst);
            }
        }
    }

    Vector2[] MakeRectPoints(Vector2 size)
    {
        Vector2 half = size * 0.5f;
        return new Vector2[] {
            new Vector2(-half.x, -half.y),
            new Vector2( half.x, -half.y),
            new Vector2( half.x,  half.y),
            new Vector2(-half.x,  half.y)
        };
    }
}
