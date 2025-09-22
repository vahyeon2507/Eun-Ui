// JangsanbeomBoss.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class JangsanbeomBoss : MonoBehaviour
{
    [Serializable]
    public class AttackData
    {
        public string Name = "ClawExecute";
        public Vector2 Offset = Vector2.right;   // local offset from boss root
        public Vector2 Size = new Vector2(3f, 1.5f);
        public float Angle = 0f; // degrees, local
        public int Damage = 2;
        public float Lifetime = 0.12f;

        // editor visuals (optional)
        public GameObject TelegraphPrefab;
        public float TelegraphTime = 0f;
        public bool DefaultFake = false;
        public Sprite FakeSprite;
        public float FakeSpriteDuration = 0.18f;
        public Transform OriginOverride;
    }

    // ---------------- Refs & inspector ----------------
    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    [Header("Health / Phase")]
    [Tooltip("Optional: any component that exposes current/max HP (we will try to read common names). If left empty, use manual MaxHealth/CurrentHealth below.")]
    public MonoBehaviour bossHealthComponent;
    [Tooltip("Fallback if no health component assigned")]
    public float MaxHealth = 100f;
    public float CurrentHealth = 100f;
    [Range(0f, 1f)]
    public float Phase2HealthThreshold = 0.66f; // when current/max <= this -> enter phase2
    [Header("Phase map roots (enable/disable when transitioning)")]
    public GameObject Phase1MapRoot;
    public GameObject Phase2MapRoot;

    [Header("Player detection (no hitbox prefab)")]
    public LayerMask playerLayer; // set to player's layer
    public string playerTag = "Player";

    [Header("Visual & Flip")]
    [Tooltip("Optional: visual root. if empty we'll flip SpriteRenderer.flipX on children.")]
    public Transform graphicsRoot;
    [Tooltip("Flip trigger box (place at tail / behind). If child of boss, we mirror its local X on flip.")]
    public BoxCollider2D flipTrigger;
    public Transform flipPivot;
    public float flipPivotDeadzone = 0.6f;

    [Header("Flip tuning")]
    public float flipTriggerHysteresis = 0.05f;
    public float flipCooldown = 0.12f;
    [HideInInspector] public bool facingRight = true;
    public bool enforceVisualFlip = true;
    public bool debugFlip = false;

    [Header("AI / Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Animator movement params")]
    public string animParam_MoveBool = "Move";
    public string animParam_MoveSpeed = "Speed";

    [Header("Behavior")]
    public bool useAnimationEvents = true;

    [Header("Fake attack options (per-phase)")]
    public bool enableFakePhase1 = false;
    public bool enableFakePhase2 = true;
    [Range(0f, 1f)] public float fakeChancePhase2 = 0.25f;

    [Header("Editor-tunable attacks (per-phase)")]
    public List<AttackData> attacksPhase1 = new List<AttackData>();
    public List<AttackData> attacksPhase2 = new List<AttackData>();

    [Header("Gizmos")]
    public bool showAttackGizmos = true;
    public Color gizmoRealColor = new Color(1f, 0.2f, 0.2f, 0.18f);
    public Color gizmoFakeColor = new Color(1f, 0.85f, 0.2f, 0.12f);

    [Header("Animator param names (attack)")]
    public string trig_ClawTelegraph = "ClawTelegraph";
    public string trig_ClawExecute = "ClawExecute";
    public string trig_ClawFakeExecute = "ClawFakeExecute";

    // internals
    Vector3 _rootOriginalScale;
    Vector3 _graphicsOriginalScale;
    float _lastFlipTime = -10f;
    int _prevTriggerSide = 0; // -1 left, 0 near, +1 right
    Coroutine aiCoroutine;
    bool busy = false;
    float _flipTriggerOriginalLocalX = 0f;
    bool _flipTriggerIsChild = false;

    // poly colliders (physics flip)
    List<PolygonCollider2D> _polyColliders = new List<PolygonCollider2D>();
    List<Vector2[][]> _originalPolyPaths = new List<Vector2[][]>();

    // movement tracking
    Vector3 _lastPosition;

    // phase state
    bool _inPhase2 = false;

    // health reflection helpers
    float _lastKnownHp = -1f;
    float _lastKnownMaxHp = -1f;
    float _healthPollInterval = 0.2f;
    float _healthPollTimer = 0f;

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
            var s = graphicsRoot.localScale; s.x = Mathf.Abs(s.x); graphicsRoot.localScale = s;
        }

        if (flipTrigger != null)
        {
            _flipTriggerIsChild = flipTrigger.transform.IsChildOf(transform);
            if (_flipTriggerIsChild)
                _flipTriggerOriginalLocalX = flipTrigger.transform.localPosition.x;
            else
                _flipTriggerOriginalLocalX = 0f;
        }

        CachePolygonColliderPaths();
    }

    void Start()
    {
        if (player == null) Debug.LogWarning("[Boss] player reference not set!");
        aiCoroutine = StartCoroutine(AIBehavior());
        _lastPosition = transform.position;

        // init prev trigger side
        UpdateHealthCacheImmediate();
        if (player != null)
        {
            float rel = 0f;
            if (flipTrigger != null) rel = player.position.x - GetFlipTriggerWorldX();
            else if (flipPivot != null) rel = player.position.x - flipPivot.position.x;
            else rel = player.position.x - transform.position.x;
            _prevTriggerSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
        }

        UpdatePhaseFlagsFromHealth(); // initial check
        ApplyPhaseMapRoots(); // ensure correct roots active
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
            SafeSetBool(animParam_MoveBool, isMoving);

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
                        if (debugFlip) Debug.Log($"Flip -> wantRight={wantRight} triggerX={triggerX} rel={rel}");
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
                }
            }
        }

        // health polling (safe, handles external BossHealth components)
        _healthPollTimer -= Time.deltaTime;
        if (_healthPollTimer <= 0f)
        {
            _healthPollTimer = _healthPollInterval;
            UpdateHealthCacheImmediate();
            UpdatePhaseFlagsFromHealth();
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
            animator.SetFloat(animParam_MoveSpeed, Mathf.Abs(vx));
        }

        _lastPosition = transform.position;
    }

    // ---------------- Phase handling ----------------
    void UpdateHealthCacheImmediate()
    {
        if (bossHealthComponent != null)
        {
            // try common field/property names
            Type t = bossHealthComponent.GetType();
            float c = TryGetFloatMember(bossHealthComponent, "CurrentHp", "currentHp", "CurrentHP", "currentHP", "hp", "HP", "cur", "current");
            float m = TryGetFloatMember(bossHealthComponent, "MaxHp", "maxHp", "MaxHP", "maxHP", "maxHealth", "MaxHealth", "HPMax");

            if (!float.IsNaN(c)) CurrentHealth = c;
            if (!float.IsNaN(m) && m > 0f) MaxHealth = m;
        }
        // clamp
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, Mathf.Max(1f, MaxHealth));
        _lastKnownHp = CurrentHealth;
        _lastKnownMaxHp = MaxHealth;
    }

    float TryGetFloatMember(object obj, params string[] names)
    {
        if (obj == null) return float.NaN;
        Type t = obj.GetType();
        foreach (var n in names)
        {
            // property
            var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pi != null && (pi.PropertyType == typeof(float) || pi.PropertyType == typeof(double) || pi.PropertyType == typeof(int)))
            {
                object v = pi.GetValue(obj);
                if (v is float f) return f;
                if (v is double d) return (float)d;
                if (v is int i) return (float)i;
            }
            // field
            var fi = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null && (fi.FieldType == typeof(float) || fi.FieldType == typeof(double) || fi.FieldType == typeof(int)))
            {
                object v = fi.GetValue(obj);
                if (v is float f2) return f2;
                if (v is double d2) return (float)d2;
                if (v is int i2) return (float)i2;
            }
        }
        return float.NaN;
    }

    void UpdatePhaseFlagsFromHealth()
    {
        if (MaxHealth <= 0f) return;
        float perc = CurrentHealth / MaxHealth;
        bool shouldBePhase2 = perc <= Phase2HealthThreshold;
        if (shouldBePhase2 && !_inPhase2)
        {
            EnterPhase2();
        }
        _inPhase2 = shouldBePhase2;
    }

    void EnterPhase2()
    {
        Debug.Log("[Boss] Entering Phase 2!");
        // do phase-enter stuff
        ApplyPhaseMapRoots();
        // you can add animation trigger if desired here:
        SafeSetTrigger("EnterPhase2");
    }

    void ApplyPhaseMapRoots()
    {
        if (Phase1MapRoot != null) Phase1MapRoot.SetActive(!_inPhase2);
        if (Phase2MapRoot != null) Phase2MapRoot.SetActive(_inPhase2);
    }

    // ---------------- Flip / Visuals ----------------
    public void FlipTo(bool faceRight)
    {
        facingRight = faceRight;
        transform.localScale = _rootOriginalScale;

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
            if (spriteRenderer != null)
                spriteRenderer.flipX = !faceRight;
            else
                FlipAllSpriteRenderers(faceRight);
        }

        ApplyFlipToPolygonColliders(faceRight);

        if (flipTrigger != null && _flipTriggerIsChild)
        {
            Vector3 lp = flipTrigger.transform.localPosition;
            lp.x = _flipTriggerOriginalLocalX * (faceRight ? 1f : -1f);
            flipTrigger.transform.localPosition = lp;
        }

        float refX = (flipTrigger != null) ? GetFlipTriggerWorldX() : (flipPivot != null ? flipPivot.position.x : transform.position.x);
        float relAfter = player != null ? (player.position.x - refX) : 0f;
        _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 : (relAfter < -flipTriggerHysteresis ? -1 : 0);

        if (debugFlip) Debug.Log($"FlipTo({faceRight}) applied. refX={refX} relAfter={relAfter} prevSide={_prevTriggerSide}");
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

    float GetFlipTriggerWorldX()
    {
        if (flipTrigger == null) return transform.position.x;
        if (_flipTriggerIsChild)
        {
            float localX = _flipTriggerOriginalLocalX * (facingRight ? 1f : -1f);
            Vector3 world = transform.TransformPoint(new Vector3(localX, flipTrigger.transform.localPosition.y, 0f));
            return world.x;
        }
        return flipTrigger.bounds.center.x;
    }

    // ---------------- AI / Attack logic ----------------
    IEnumerator AIBehavior()
    {
        while (true)
        {
            if (!busy && player != null)
            {
                var attackList = _inPhase2 ? attacksPhase2 : attacksPhase1;
                float d = Mathf.Abs(player.position.x - transform.position.x);
                if (d <= aggroRange && attackList != null && attackList.Count > 0)
                {
                    int val = UnityEngine.Random.Range(0, 100);
                    if (val < 60)
                    {
                        StartAttackByIndex(0, false);
                    }
                }
            }
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.6f, 1.5f));
        }
    }

    public void StartAttackByIndex(int index = 0, bool forceReal = false)
    {
        if (busy) return;

        var attackList = _inPhase2 ? attacksPhase2 : attacksPhase1;
        AttackData atk = null;
        if (attackList != null && index >= 0 && index < attackList.Count) atk = attackList[index];

        if (atk == null)
        {
            atk = new AttackData() { Name = "DefaultClaw", Offset = Vector2.right * 1.5f, Size = new Vector2(3f, 1.5f), Damage = 2, Lifetime = 0.12f };
        }

        StartCoroutine(ClawRoutine_Generic(atk, forceReal));
    }

    IEnumerator ClawRoutine_Generic(AttackData atk, bool forceReal)
    {
        busy = true;

        bool isFake = false;
        if (!forceReal)
        {
            if (_inPhase2 && enableFakePhase2) isFake = UnityEngine.Random.value < fakeChancePhase2 || atk.DefaultFake;
            else if (!_inPhase2 && enableFakePhase1) isFake = UnityEngine.Random.value < fakeChancePhase2 || atk.DefaultFake; // but phase1 fake normally off
        }

        if (animator != null)
        {
            string trig = isFake ? trig_ClawFakeExecute : trig_ClawExecute;
            if (!string.IsNullOrEmpty(trig)) SafeSetTrigger(trig);
        }

        if (!useAnimationEvents)
        {
            if (isFake)
            {
                if (atk.FakeSprite != null && spriteRenderer != null)
                    StartCoroutine(TemporarilyShow(atk.FakeSprite, atk.FakeSpriteDuration));
                yield return new WaitForSeconds(0.06f);
                PerformAttackOnce(atk, true);
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
                PerformAttackOnce(atk, false);
            }
        }

        if (atk.TelegraphTime > 0f) yield return new WaitForSeconds(atk.TelegraphTime);

        yield return new WaitForSeconds(0.01f);
        busy = false;
    }

    // called by animation events
    public void OnClawHitFrame()
    {
        var list = _inPhase2 ? attacksPhase2 : attacksPhase1;
        if (list == null || list.Count == 0) return;
        PerformAttackOnce(list[0], false);
    }

    public void OnClawFakeHitFrame()
    {
        var list = _inPhase2 ? attacksPhase2 : attacksPhase1;
        if (list == null || list.Count == 0) return;
        PerformAttackOnce(list[0], true);
    }

    // Main: overlap check + damage application (respects player's parry)
    void PerformAttackOnce(AttackData atk, bool isFake)
    {
        if (playerLayer == 0)
        {
            Debug.LogWarning("[Boss] playerLayer not set - can't detect player for attacks.");
            return;
        }

        Vector2 center = GetAttackWorldPos(atk);
        float dirSign = facingRight ? 1f : -1f;
        float worldAngle = atk.Angle * dirSign;

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, atk.Size, worldAngle, playerLayer);

        HashSet<Collider2D> hitSet = new HashSet<Collider2D>();
        foreach (var col in hits)
        {
            if (col == null) continue;
            if (hitSet.Contains(col)) continue;
            hitSet.Add(col);

            if (!string.IsNullOrEmpty(playerTag) && !col.CompareTag(playerTag)) continue;

            var pc = col.GetComponent<PlayerController>() ?? col.GetComponentInParent<PlayerController>() ?? col.GetComponentInChildren<PlayerController>();
            if (pc != null)
            {
                if (pc.IsParrying)
                {
                    bool consumed = TryAskPlayerToConsumeParry(pc, atk.Damage);
                    if (consumed) continue;
                    else continue; // player's parry not handled -> treat as consumed for safety
                }

                var dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>() ?? col.GetComponentInChildren<IDamageable>();
                if (dmg != null)
                {
                    dmg.TakeDamage(atk.Damage);
                    continue;
                }

                var pHealth = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
                if (pHealth != null)
                {
                    pHealth.TakeDamage(atk.Damage);
                    continue;
                }

                // reflection fallback
                var comp = col.GetComponent<MonoBehaviour>() ?? col.GetComponentInParent<MonoBehaviour>();
                if (comp != null)
                {
                    MethodInfo mi = comp.GetType().GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null)
                    {
                        mi.Invoke(comp, new object[] { atk.Damage });
                        continue;
                    }
                }
            }
            else
            {
                var dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>() ?? col.GetComponentInChildren<IDamageable>();
                if (dmg != null) { dmg.TakeDamage(atk.Damage); }
            }
        }
    }

    // attempts to call player's parry-consume API (many name possibilities)
    bool TryAskPlayerToConsumeParry(PlayerController pc, int incomingDamage)
    {
        if (pc == null) return false;
        var type = pc.GetType();
        string[] methodNames = new string[] {
            "ConsumeHitboxIfParrying",
            "ConsumeParryHit",
            "ConsumeParry",
            "OnParriedByHitbox",
            "ConsumeHitIfParry"
        };

        foreach (var name in methodNames)
        {
            var m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) continue;
            var parms = m.GetParameters();
            try
            {
                object res = null;
                if (parms.Length == 0) res = m.Invoke(pc, null);
                else if (parms.Length == 1) res = m.Invoke(pc, new object[] { incomingDamage });
                else if (parms.Length == 2) res = m.Invoke(pc, new object[] { incomingDamage, this });
                else res = m.Invoke(pc, null);

                if (res is bool) return (bool)res;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Boss] Exception invoking player parry method '{name}': {ex.Message}");
            }
        }
        return false;
    }

    // ---------------- Utilities ----------------
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

    void SafeSetTrigger(string name)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return;
        foreach (var p in animator.parameters) if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) { animator.SetTrigger(name); return; }
    }

    void SafeSetBool(string name, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return;
        foreach (var p in animator.parameters) if (p.name == name && p.type == AnimatorControllerParameterType.Bool) { animator.SetBool(name, value); return; }
    }

    void OnDrawGizmos()
    {
        if (!showAttackGizmos) return;

        var list = Application.isPlaying ? (_inPhase2 ? attacksPhase2 : attacksPhase1) : (attacksPhase1);
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            Vector2 pos;
            float angleDeg;
            if (Application.isPlaying) { pos = GetAttackWorldPos(a); angleDeg = a.Angle * (facingRight ? 1f : -1f); }
            else
            {
                float dir = (facingRight ? 1f : -1f);
                Vector2 origin = (a.OriginOverride != null) ? (Vector2)a.OriginOverride.position : (Vector2)transform.position;
                Vector2 off = a.Offset; off.x *= dir;
                float ang = (a.Angle * dir) * Mathf.Deg2Rad;
                float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                Vector2 rotated = new Vector2(off.x * c - off.y * s, off.x * s + off.y * c);
                pos = origin + rotated;
                angleDeg = a.Angle * dir;
            }

            Color fill = a.DefaultFake ? gizmoFakeColor : gizmoRealColor;
            Color wire = new Color(fill.r, fill.g, fill.b, Mathf.Min(1f, fill.a * 2f));
            Vector3 center = new Vector3(pos.x, pos.y, 0f);
            Matrix4x4 prev = Gizmos.matrix;
            Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);
            Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
            Vector3 size = new Vector3(a.Size.x, a.Size.y, 0.01f);
            Gizmos.color = fill; Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = wire; Gizmos.DrawWireCube(Vector3.zero, size * 1.001f);
            Gizmos.matrix = prev;
        }
    }
}
