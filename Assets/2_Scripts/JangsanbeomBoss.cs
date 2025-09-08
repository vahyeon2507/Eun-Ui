using System.Collections;
using UnityEngine;

[System.Serializable]
public class AttackData
{
    public string name = "Attack";
    public Vector2 offset = new Vector2(1.5f, 0f);
    public Vector2 size = new Vector2(3f, 1.5f);
    public float angle = 0f;
    public int damage = 2;
    public float lifetime = 0.12f;
    public GameObject telegraphPrefab = null;
    public float telegraphTime = 0.8f;
    public bool defaultFake = false;
    public Sprite fakeSprite = null;
    public float fakeSpriteDuration = 0.18f;
    public Transform originOverride = null;
}

public class JangsanbeomBoss : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    [Header("Prefabs & visuals")]
    public GameObject hitboxPrefab;      // must have Collider2D + Hitbox

    [Header("AI / Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Attacks")]
    public AttackData[] attacks = new AttackData[1];

    [Header("General")]
    public LayerMask hitboxTargetLayer = 0;
    public string hitboxTargetTag = "Player";
    public bool useAnimationEvents = true;
    public float cooldownTime = 1f;
    public bool drawGizmos = true;

    // internals
    bool busy = false;
    public bool facingRight = true; // made public so editor can use it reliably
    Coroutine aiCoroutine;

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
    }

    void Start()
    {
        if (player == null) Debug.LogWarning("[Boss] player reference not set!");
        if (hitboxPrefab == null) Debug.LogWarning("[Boss] hitboxPrefab not assigned!");
        if (attacks == null || attacks.Length == 0) Debug.LogWarning("[Boss] No attacks defined!");
        if (animator == null) Debug.LogWarning("[Boss] Animator not assigned!");
        if (animator != null && animator.runtimeAnimatorController == null) Debug.LogWarning("[Boss] Animator has no Controller assigned (None).");

        if (aiCoroutine == null) aiCoroutine = StartCoroutine(AIBehavior());
    }

    void Update()
    {
        // debug keys: K = force play animation (attempt trigger then Play), L = spawn real hit (no anim), ; = log anim state
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[Boss DEBUG] K pressed: force-play animation for attack 0");
            ForcePlayAttackAnimation(0);
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("[Boss DEBUG] L pressed: spawn REAL hit for attack 0 (no anim)");
            SpawnRealHit(GetAttackWorldPos(attacks[0]), attacks[0]);
        }
        if (Input.GetKeyDown(KeyCode.Semicolon))
        {
            if (animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"[Boss DEBUG] Animator state info: fullPathHash={st.fullPathHash}, normalizedTime={st.normalizedTime}, isName:0-> {animator.GetCurrentAnimatorStateInfo(0).IsName(animator.GetCurrentAnimatorClipInfo(0).Length > 0 ? animator.GetCurrentAnimatorClipInfo(0)[0].clip.name : "")}");
            }
            else Debug.Log("[Boss DEBUG] animator == null");
        }

        if (player == null) return;

        float dx = player.position.x - transform.position.x;
        float absdx = Mathf.Abs(dx);
        if (!busy && absdx > followMinDistanceX && absdx < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            float targetX = player.position.x - Mathf.Sign(dx) * followMinDistanceX;
            Vector3 newPos = transform.position;
            newPos.x = Mathf.MoveTowards(transform.position.x, targetX, step);
            transform.position = newPos;
        }

        if (player.position.x > transform.position.x && !facingRight) FlipTo(true);
        else if (player.position.x < transform.position.x && facingRight) FlipTo(false);
    }

    void FlipTo(bool faceRight)
    {
        facingRight = faceRight;
        if (spriteRenderer != null) spriteRenderer.flipX = !faceRight;
        Vector3 s = transform.localScale; s.x = Mathf.Abs(s.x) * (faceRight ? 1f : -1f); transform.localScale = s;
    }

    // --- Attack API ---
    public void StartAttack(int attackIndex, bool forceReal = false)
    {
        if (busy)
        {
            Debug.Log($"[Boss] StartAttack({attackIndex}) aborted: busy");
            return;
        }
        if (attacks == null || attackIndex < 0 || attackIndex >= attacks.Length)
        {
            Debug.LogWarning("[Boss] invalid attack index");
            return;
        }

        AttackData ad = attacks[attackIndex];
        bool willBeFake = ad.defaultFake && !forceReal;
        StartCoroutine(AttackRoutine(attackIndex, willBeFake));
    }

    IEnumerator AttackRoutine(int idx, bool isFake)
    {
        busy = true;
        AttackData ad = attacks[idx];

        // telegraph visual
        if (ad.telegraphPrefab != null)
        {
            Vector2 tpos = (ad.originOverride != null) ? (Vector2)ad.originOverride.position : GetAttackWorldPos(ad);
            GameObject t = Instantiate(ad.telegraphPrefab, tpos, Quaternion.identity);
            Destroy(t, Mathf.Max(0.05f, ad.telegraphTime));
        }

        // Try to trigger animator: prefer Trigger param named: ad.name + "Execute"
        string triggerName = ad.name + "Execute";
        bool triggered = TrySetTrigger(triggerName);

        if (!triggered)
        {
            // If trigger not present, try to Play state with same name fallback.
            bool played = TryPlayState(ad.name + "Execute");
            if (!played)
            {
                Debug.LogWarning($"[Boss] Neither Trigger '{triggerName}' exists nor state '{ad.name + "Execute"}' playable. Animator won't change state.");
            }
        }
        else
        {
            Debug.Log($"[Boss] Animator.SetTrigger called: {triggerName} (attack {ad.name}, fake={isFake})");
        }

        // fallback if animation events not used
        if (!useAnimationEvents)
        {
            if (isFake)
            {
                if (ad.fakeSprite != null && spriteRenderer != null) StartCoroutine(TemporarilyShow(ad.fakeSprite, ad.fakeSpriteDuration));
                yield return new WaitForSeconds(0.06f);
                SpawnFakeHit(GetAttackWorldPos(ad), ad);
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
                SpawnRealHit(GetAttackWorldPos(ad), ad);
            }
        }

        yield return new WaitForSeconds(cooldownTime);
        busy = false;
    }

    // --- Animation event entry points ---
    // Int-param variants (preferred for multiple attacks)
    public void OnAttackHitFrame(int attackIndex)
    {
        if (attacks == null || attackIndex < 0 || attackIndex >= attacks.Length) return;
        AttackData ad = attacks[attackIndex];
        SpawnRealHit(GetAttackWorldPos(ad), ad);
    }
    public void OnAttackFakeHitFrame(int attackIndex)
    {
        if (attacks == null || attackIndex < 0 || attackIndex >= attacks.Length) return;
        AttackData ad = attacks[attackIndex];
        if (ad.fakeSprite != null && spriteRenderer != null) StartCoroutine(TemporarilyShow(ad.fakeSprite, ad.fakeSpriteDuration));
        SpawnFakeHit(GetAttackWorldPos(ad), ad);
    }

    // No-param overloads for convenience (if your animation event was set without int)
    public void OnAttackHitFrame()
    {
        if (attacks != null && attacks.Length > 0) OnAttackHitFrame(0);
    }
    public void OnAttackFakeHitFrame()
    {
        if (attacks != null && attacks.Length > 0) OnAttackFakeHitFrame(0);
    }

    // --- Helpers: spawn hitbox prefab ---
    GameObject SpawnHitboxPrefab(Vector2 center, Vector2 size, float angleDeg, int damage, string targetTag, LayerMask layerMask, float lifetime)
    {
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[Boss] hitboxPrefab null - cannot spawn hit");
            return null;
        }

        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.Euler(0f, 0f, angleDeg));
        BoxCollider2D box = hb.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.offset = Vector2.zero;
            box.size = size;
        }
        else
        {
            hb.transform.localScale = new Vector3(size.x, size.y, 1f);
            Collider2D col = hb.GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        Hitbox hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = damage;
            hbComp.targetTag = targetTag;
            hbComp.targetLayerMask = layerMask;
            hbComp.singleUse = true;
            hbComp.destroyOnHit = false;
        }
        else
        {
            Debug.LogWarning("[Boss] spawned hitbox prefab missing Hitbox component");
        }

        if (lifetime > 0f) Destroy(hb, lifetime + 0.05f);
        return hb;
    }

    void SpawnRealHit(Vector2 center, AttackData ad)
    {
        SpawnHitboxPrefab(center, ad.size, ad.angle, ad.damage, hitboxTargetTag, hitboxTargetLayer, ad.lifetime);
        Debug.Log($"[Boss] Spawned REAL {ad.name} at {center}, size {ad.size}");
    }
    void SpawnFakeHit(Vector2 center, AttackData ad)
    {
        GameObject hb = SpawnHitboxPrefab(center, ad.size, ad.angle, 0, "", 0, ad.lifetime);
        if (hb != null) Debug.Log($"[Boss] Spawned FAKE {ad.name} at {center}");
    }

    Vector2 GetAttackWorldPos(AttackData ad)
    {
        if (ad.originOverride != null) return ad.originOverride.position;
        float dir = facingRight ? 1f : -1f;
        Vector2 offs = new Vector2(ad.offset.x * dir, ad.offset.y);
        return (Vector2)transform.position + offs;
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
            if (!busy && player != null && attacks != null && attacks.Length > 0)
            {
                float d = Mathf.Abs(player.position.x - transform.position.x);
                if (d <= aggroRange)
                {
                    int choice = Random.Range(0, 100);
                    if (choice < 60)
                    {
                        int idx = Random.Range(0, attacks.Length);
                        StartAttack(idx, false);
                    }
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.5f));
        }
    }

    // --- Animator utility methods ---
    bool TrySetTrigger(string triggerName)
    {
        if (animator == null) return false;
        // check param exists
        foreach (var p in animator.parameters)
        {
            if (p.name == triggerName && p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(triggerName);
                return true;
            }
        }
        return false;
    }

    // Try to Play an animator state (stateName) in Base Layer 0
    bool TryPlayState(string stateName)
    {
        if (animator == null) return false;
        // try direct Play (stateName must match state's name in animator)
        try
        {
            animator.Play(stateName, 0, 0f);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // explicit force-play utility used by debug key
    public void ForcePlayAttackAnimation(int attackIndex)
    {
        if (attacks == null || attackIndex < 0 || attackIndex >= attacks.Length) return;
        string stateName = attacks[attackIndex].name + "Execute";
        if (!TrySetTrigger(stateName))
        {
            if (!TryPlayState(stateName))
            {
                Debug.LogWarning($"[Boss] ForcePlay: couldn't trigger or play state '{stateName}'. Check Animator params or state names.");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || attacks == null) return;
        for (int i = 0; i < attacks.Length; i++)
        {
            AttackData a = attacks[i];
            Vector2 c = Application.isPlaying ? GetAttackWorldPos(a) : (Vector2)transform.position + a.offset;
            Gizmos.color = Color.Lerp(Color.red, Color.yellow, i / (float)Mathf.Max(1, attacks.Length - 1));
            Gizmos.DrawWireCube(c, new Vector3(a.size.x, a.size.y, 0.01f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(c + Vector2.up * 0.25f, $"{a.name} ({i})");
#endif
        }
    }
}
