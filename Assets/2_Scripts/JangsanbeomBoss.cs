using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// JangsanbeomBoss - editor-friendly edition
[DisallowMultipleComponent]
public class JangsanbeomBoss : MonoBehaviour
{
    // --- Attack data type expected by the custom editor ---
    [System.Serializable]
    public class AttackData
    {
        public string Name = "Claw_Execute";
        public Vector2 Offset = Vector2.right;   // local offset from boss transform (x,y)
        public Vector2 Size = new Vector2(3f, 1.5f);
        public float Angle = 0f;
        public int Damage = 2;
        public float Lifetime = 0.12f;

        // optional telegraph / visuals
        public GameObject TelegraphPrefab;
        public float TelegraphTime = 0.8f;
        public bool DefaultFake = false;
        public Sprite FakeSprite;
        public float FakeSpriteDuration = 0.18f;
        public Transform OriginOverride;
    }

    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb; // will be set to Kinematic to avoid being pushed

    [Header("Prefabs & visuals")]
    public GameObject telegraphPrefab;   // optional global telegraph prefab
    public GameObject hitboxPrefab;      // prefab must have Collider2D + Hitbox component
    public GameObject decoyPrefab;       // optional

    [Header("AI / Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f; // keep this X distance from player
    public float aggroRange = 12f;

    [Header("Default claw settings (used if attack list empty)")]
    public float defaultClawOffsetX = 1.5f;
    public Vector2 defaultClawSize = new Vector2(3f, 1.5f);
    public int defaultClawDamage = 2;
    public float defaultHitboxLifetime = 0.12f;

    [Header("Fake claw")]
    public bool enableFakeClaw = true;
    [Range(0f, 1f)] public float fakeChance = 0.25f;
    public Sprite fakeSprite;            // optional visual swap for fake
    public float fakeSpriteDuration = 0.18f;
    public Transform fakeOrigin;         // optional

    [Header("Hit target settings")]
    public LayerMask hitboxTargetLayer;  // assign properly in inspector
    public string hitboxTargetTag = "Player"; // default

    [Header("Animator parameter names")]
    public string trig_ClawTelegraph = "ClawTelegraph";
    public string trig_ClawExecute = "ClawExecute";
    public string trig_ClawFakeTelegraph = "ClawFakeTelegraph";
    public string trig_ClawFakeExecute = "ClawFakeExecute";

    [Header("Behavior options")]
    public bool useAnimationEvents = true; // true = animation calls OnClawHitFrame etc

    [Header("Editor-tunable attacks (editor expects this list)")]
    public List<AttackData> attacks = new List<AttackData>();

    [Header("Gizmos (Scene only)")]
    public bool showGizmos = true;
    public Color gizmoRealColor = new Color(1f, 0.2f, 0.2f, 0.18f);
    public Color gizmoFakeColor = new Color(1f, 0.85f, 0.2f, 0.12f);

    // --- internals (note: facingRight is PUBLIC because editor reads it) ---
    [HideInInspector] public bool facingRight = true; // editor wants to read this
    [HideInInspector] public bool busy = false;

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
        if (telegraphPrefab == null) Debug.Log("[Boss] telegraphPrefab not assigned (telegraph visuals skipped).");

        if (aiCoroutine == null) aiCoroutine = StartCoroutine(AIBehavior());
    }

    void Update()
    {
        if (player == null) return;

        float dx = player.position.x - transform.position.x;
        if (!busy && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            Vector3 newPos = transform.position;
            // keep boss at a fixed X offset relative to player (not following Y)
            newPos.x = Mathf.MoveTowards(transform.position.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
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

    // public entrypoint: choose an attack by index, or default if index invalid
    public void StartAttackByIndex(int index = 0, bool forceReal = false)
    {
        if (busy) return;

        AttackData atk = null;
        if (index >= 0 && index < attacks.Count) atk = attacks[index];
        if (atk == null)
        {
            // make a default AttackData from the fallback values
            atk = new AttackData()
            {
                Name = "DefaultClaw",
                Offset = new Vector2(defaultClawOffsetX, 0f),
                Size = defaultClawSize,
                Damage = defaultClawDamage,
                Lifetime = defaultHitboxLifetime,
                TelegraphPrefab = telegraphPrefab,
                TelegraphTime = 0f,
                DefaultFake = false
            };
        }

        StartCoroutine(ClawRoutine_Generic(atk, forceReal));
    }

    IEnumerator ClawRoutine_Generic(AttackData atk, bool forceReal)
    {
        busy = true;

        bool isFake = false;
        if (!forceReal && enableFakeClaw)
            isFake = Random.value < fakeChance || atk.DefaultFake;

        // trigger animator
        if (animator != null)
        {
            string trig = isFake ? trig_ClawFakeExecute : trig_ClawExecute;
            if (!string.IsNullOrEmpty(trig)) animator.SetTrigger(trig);
        }

        if (!useAnimationEvents)
        {
            // fallback spawn after small delay if no animation event exists
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

        yield return new WaitForSeconds(atk.TelegraphTime > 0f ? atk.TelegraphTime : 0f);
        yield return new WaitForSeconds(Mathf.Max(0f, 0.01f)); // small buffer
        yield return new WaitForSeconds(0.0f);
        yield return new WaitForSeconds(Mathf.Max(0f, 0f)); // yep
        busy = false;
    }

    // Animation event hooks:
    public void OnClawHitFrame()   // If your animation event doesn't include which attack, default to 0
    {
        // spawn using attack 0 or fallback
        AttackData atk = attacks != null && attacks.Count > 0 ? attacks[0] : null;
        if (atk == null)
            SpawnRealHit(GetClawPos(), defaultClawSize);
        else
            SpawnRealHit(GetAttackWorldPos(atk), atk.Size, atk);
    }

    public void OnClawFakeHitFrame()
    {
        AttackData atk = attacks != null && attacks.Count > 0 ? attacks[0] : null;
        if (atk == null)
            SpawnFakeHit(GetClawPos(), defaultClawSize);
        else
            SpawnFakeHit(GetAttackWorldPos(atk), atk.Size, atk);
    }

    // spawn helpers (real)
    void SpawnRealHit(Vector2 center, Vector2 size)
    {
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[Boss] hitboxPrefab is null - can't spawn real hit.");
            return;
        }

        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);

        BoxCollider2D bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.offset = Vector2.zero;
            bc.size = size;
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
            hbComp.damage = defaultClawDamage;
            hbComp.targetTag = hitboxTargetTag;
            hbComp.targetLayerMask = hitboxTargetLayer;
            hbComp.singleUse = true;
        }

        if (defaultHitboxLifetime > 0f) Destroy(hb, defaultHitboxLifetime + 0.05f);
    }

    void SpawnRealHit(Vector2 center, Vector2 size, AttackData atk)
    {
        if (hitboxPrefab == null) return;

        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        BoxCollider2D bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.offset = Vector2.zero;
            bc.size = size;
        }

        Hitbox hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = atk.Damage;
            hbComp.targetTag = hitboxTargetTag;
            hbComp.targetLayerMask = hitboxTargetLayer;
            hbComp.singleUse = true;
        }

        if (atk.Lifetime > 0f) Destroy(hb, atk.Lifetime + 0.05f);
    }

    // fake
    void SpawnFakeHit(Vector2 center, Vector2 size)
    {
        if (hitboxPrefab == null) return;
        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);

        BoxCollider2D bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.offset = Vector2.zero;
            bc.size = size;
        }

        Hitbox hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = 0;
            hbComp.targetTag = "";
            hbComp.targetLayerMask = 0;
            hbComp.singleUse = true;
        }

        if (defaultHitboxLifetime > 0f) Destroy(hb, defaultHitboxLifetime + 0.05f);
    }

    void SpawnFakeHit(Vector2 center, Vector2 size, AttackData atk)
    {
        if (hitboxPrefab == null) return;
        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);

        BoxCollider2D bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.offset = Vector2.zero;
            bc.size = size;
        }

        Hitbox hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = 0;
            hbComp.targetTag = ""; // safe
            hbComp.targetLayerMask = 0;
            hbComp.singleUse = true;
        }

        if (atk.Lifetime > 0f) Destroy(hb, atk.Lifetime + 0.05f);
    }

    // position helpers
    Vector2 GetClawPos()
    {
        float dir = facingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(defaultClawOffsetX * dir, 0f);
    }

    Vector2 GetAttackWorldPos(AttackData atk)
    {
        float dir = facingRight ? 1f : -1f;
        Vector2 localOff = atk.Offset;
        localOff.x *= dir; // flip horizontally if facing left
        Vector2 world = (Vector2)transform.position + localOff;
        return world;
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
                    if (val < 60)
                    {
                        StartAttackByIndex(0, false);
                    }
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.5f));
        }
    }

    // gizmos for scene tuning
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // draw each attack region
        if (attacks != null)
        {
            for (int i = 0; i < attacks.Count; i++)
            {
                AttackData a = attacks[i];
                Vector2 pos = GetAttackWorldPos(a);
                Vector3 size = new Vector3(a.Size.x, a.Size.y, 0.05f);

                Gizmos.color = gizmoRealColor;
                Gizmos.DrawCube(pos, size);
                Gizmos.color = gizmoRealColor * 1.4f;
                Gizmos.DrawWireCube(pos, size);
            }
        }
    }
}
