using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JangsanbeomBoss : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb; // will be set to Kinematic to avoid being pushed

    [Header("Prefabs & visuals")]
    public GameObject telegraphPrefab;   // telegraph visual (spawn & destroy after telegraphTime)
    public GameObject hitboxPrefab;      // prefab must have Collider2D + Hitbox component
    public GameObject decoyPrefab;       // optional

    [Header("AI / Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f; // keep this X distance from player
    public float aggroRange = 12f;

    [Header("Claw settings")]
    public float telegraphTime = 0.8f;   // not used in NoTelegraph mode, kept for inspector
    public float cooldownTime = 1.0f;
    public float clawOffsetX = 1.5f;
    public Vector2 clawSize = new Vector2(3f, 1.5f);
    public int clawDamage = 2;
    public float hitboxLifetime = 0.12f;

    [Header("Fake claw")]
    public bool enableFakeClaw = true;
    [Range(0f, 1f)] public float fakeChance = 0.25f;
    public Sprite fakeSprite;            // optional visual swap for fake
    public float fakeSpriteDuration = 0.18f;
    public Transform fakeOrigin;         // optional: where fake telegraph appears

    [Header("Hit target settings")]
    public LayerMask hitboxTargetLayer;  // assign Player layer in inspector
    public string hitboxTargetTag = "Player"; // default

    [Header("Animator parameter names")]
    public string trig_ClawExecute = "ClawExecute";
    public string trig_ClawFakeExecute = "ClawFakeExecute";

    [Header("Behavior options")]
    public bool useAnimationEvents = true; // true = OnClawHitFrame is called from animation event

    // internals
    bool busy = false;
    bool facingRight = true;
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
        Debug.Log("[Boss] Start() called on " + name);
        if (player == null) Debug.LogWarning("[Boss] player reference not set!");
        if (hitboxPrefab == null) Debug.LogWarning("[Boss] hitboxPrefab not assigned!");
        if (telegraphPrefab == null) Debug.Log("[Boss] telegraphPrefab not assigned (telegraph visuals skipped).");
        if (animator == null) Debug.LogWarning("[Boss] animator not assigned! Animator needs controller with ClawExecute param.");

        if (aiCoroutine == null) aiCoroutine = StartCoroutine(AIBehavior());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("[Boss DEBUG] Forcing FAKE claw (L pressed)");
            StartClaw(false);            // AI처럼 호출하되 forceReal = false -> fakeChance 적용
        }
        if (Input.GetKeyDown(KeyCode.Semicolon)) // ; 키
        {
            if (animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"[Boss DEBUG] Animator state = {st.fullPathHash} / normalizedTime={st.normalizedTime}");
            }
            else Debug.Log("[Boss DEBUG] animator == null");
        }



        // DEBUG: Force a claw for quick testing
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[Boss] Debug: forcing claw (K pressed)");
            StartClaw(true);
        }

        if (player == null) return;

        float dx = player.position.x - transform.position.x;
        if (!busy && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            Vector3 newPos = transform.position;
            newPos.x = Mathf.MoveTowards(transform.position.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = newPos;
        }

        if (player.position.x > transform.position.x && !facingRight) FlipTo(true);
        else if (player.position.x < transform.position.x && facingRight) FlipTo(false);
    }

    void FlipTo(bool faceRight)
    {
        facingRight = faceRight;
        if (spriteRenderer != null)
            spriteRenderer.flipX = !faceRight;
        Vector3 s = transform.localScale; s.x = Mathf.Abs(s.x) * (faceRight ? 1f : -1f); transform.localScale = s;
    }

    // ---------- 핵심 수정: StartClaw가 실제로 루틴을 시작하도록 함 ----------
    public void StartClaw(bool forceReal = false)
    {
        if (busy)
        {
            Debug.Log("[Boss] StartClaw aborted: busy");
            return;
        }
        Debug.Log("[Boss] StartClaw called. forceReal=" + forceReal);
        StartCoroutine(ClawRoutine_NoTelegraph(forceReal));
    }

    IEnumerator ClawRoutine_NoTelegraph(bool forceReal)
    {
        busy = true;

        bool isFake = false;
        if (!forceReal && enableFakeClaw)
            isFake = Random.value < fakeChance;

        // Execute 트리거 바로 날림 (텔레그래프 없음)
        if (animator != null)
        {
            if (isFake) animator.SetTrigger(trig_ClawFakeExecute);
            else animator.SetTrigger(trig_ClawExecute);
            Debug.Log("[Boss] Animator trigger set: " + (isFake ? trig_ClawFakeExecute : trig_ClawExecute));
        }

        if (!useAnimationEvents)
        {
            if (isFake)
            {
                if (fakeSprite != null && spriteRenderer != null)
                    StartCoroutine(TemporarilyShow(fakeSprite, fakeSpriteDuration));
                yield return new WaitForSeconds(0.06f);
                SpawnFakeHit(GetFakePos(), clawSize);
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
                SpawnRealHit(GetClawPos(), clawSize);
            }
        }

        yield return new WaitForSeconds(cooldownTime);
        busy = false;
        Debug.Log("[Boss] ClawRoutine finished, busy=false");
    }

    // Animation event entrypoints
    public void OnClawHitFrame()
    {
        Debug.Log("[Boss] OnClawHitFrame called (animation event). Spawning real hit.");
        SpawnRealHit(GetClawPos(), clawSize);
    }

    public void OnClawFakeHitFrame()
    {
        Debug.Log("[Boss] OnClawFakeHitFrame called (animation event). Spawning fake hit.");
        if (fakeSprite != null && spriteRenderer != null)
            StartCoroutine(TemporarilyShow(fakeSprite, fakeSpriteDuration));
        SpawnFakeHit(GetFakePos(), clawSize);
    }

    // Hit spawning utilities
    void SpawnRealHit(Vector2 center, Vector2 size)
    {
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[Boss] hitboxPrefab is null - can't spawn real hit.");
            return;
        }

        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        hb.transform.localScale = new Vector3(size.x, size.y, 1f);

        Collider2D col = hb.GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        Hitbox hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = clawDamage;
            hbComp.targetTag = hitboxTargetTag;
            hbComp.targetLayerMask = hitboxTargetLayer;
            hbComp.singleUse = true;
        }
        else
        {
            Debug.LogWarning("[Boss] spawned hitbox prefab has no Hitbox component.");
        }

        if (hitboxLifetime > 0f) Destroy(hb, hitboxLifetime + 0.05f);
        Debug.Log("[Boss] Spawned real hit at " + center);
    }

    void SpawnFakeHit(Vector2 center, Vector2 size)
    {
        if (hitboxPrefab == null) return;
        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        hb.transform.localScale = new Vector3(size.x, size.y, 1f);

        Collider2D col = hb.GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        Hitbox hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = 0;
            hbComp.targetTag = "";
            hbComp.targetLayerMask = 0;
            hbComp.singleUse = true;
        }

        if (hitboxLifetime > 0f) Destroy(hb, hitboxLifetime + 0.05f);
        Debug.Log("[Boss] Spawned fake hit (harmless) at " + center);
    }

    Vector2 GetClawPos()
    {
        float dir = facingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(clawOffsetX * dir, 0f);
    }

    Vector2 GetFakePos()
    {
        if (fakeOrigin != null) return fakeOrigin.position;
        float dir = facingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(clawOffsetX * dir, 0f);
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
        Debug.Log("[Boss] AIBehavior started");
        while (true)
        {
            if (!busy && player != null)
            {
                float d = Mathf.Abs(player.position.x - transform.position.x);
                if (d <= aggroRange)
                {
                    int val = Random.Range(0, 100);
                    if (val < 60)
                    {
                        Debug.Log("[Boss] AI choose to claw (val=" + val + ")");
                        StartClaw(false);
                    }
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.5f));
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 p = GetClawPos();
        Gizmos.DrawWireCube(p, new Vector3(clawSize.x, clawSize.y, 0.1f));
    }
}
