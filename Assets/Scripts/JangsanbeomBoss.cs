// JangsanbeomBoss_Updated.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JangsanbeomBoss : MonoBehaviour
{
    [Header("Basic")]
    public Transform player;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Prefabs & Layers")]
    public GameObject telegraphPrefab;
    public GameObject hitboxPrefab;       // 반드시 네 Hitbox 스크립트가 붙어 있어야 함
    public GameObject decoyPrefab;
    public List<Transform> decoySpawnPoints = new List<Transform>();

    [Header("Hitbox target settings")]
    public LayerMask hitboxTargetLayer;   // 히트박스가 목표로 삼을 레이어 (Inspector에서 설정)
    public string hitboxTargetTag = "Player"; // 기본 타겟 태그 (Hitbox.targetTag)

    [Header("Timings")]
    public float idleDelay = 1f;
    public float telegraphTime = 0.8f;
    public float cooldownTime = 1f;

    [Header("Claw Settings")]
    public Vector2 clawWideSize = new Vector2(6f, 1.5f);
    public Vector2 clawShortSize = new Vector2(3f, 1.5f);
    public float clawOffsetX = 1.5f;
    public float wideDashDistance = 0.6f;
    public float shortDashDistance = 2.0f;
    public float dashMoveDuration = 0.06f;
    public int clawDamage = 2;

    [Header("BodySlam Settings")]
    public Vector2 bodySlamSize = new Vector2(3f, 2f);
    public float bodySlamOffsetX = 2f;
    public int bodySlamDamage = 3;

    [Header("Decoy Settings")]
    public int decoyCount = 2;
    public float decoyLifetime = 4f;
    public Color fakeColor = Color.gray;
    public Color realColor = Color.white;

    // internals
    bool busy = false;
    bool facingRight = true;
// --- Paste inside your JangsanbeomBoss class ---

[Header("AI Behavior")]
public bool autoStartAI = true;            // 씬 시작 시 자동으로 AI 루틴 시작할지
public float aiCheckInterval = 0.5f;       // 플레이어 거리를 체크하는 간격
public float aiMinDelayBetweenActions = 0.6f;
public float aiMaxDelayBetweenActions = 1.8f;

private Coroutine aiCoroutine = null;

void OnEnable()
{
    if (autoStartAI)
    {
        // delay 한 프레임 후에 시작 (Start() 이후 안정적으로 참조들이 들어온 상태에서)
        StartCoroutine(StartAIWithDelayOneFrame());
    }
}

IEnumerator StartAIWithDelayOneFrame()
{
    yield return null;
    TryStartAI();
}

void TryStartAI()
{
    if (aiCoroutine == null)
    {
        aiCoroutine = StartCoroutine(AIBehavior());
        Debug.Log("[Boss] AIBehavior started.");
    }
}

void StopAI()
{
    if (aiCoroutine != null)
    {
        StopCoroutine(aiCoroutine);
        aiCoroutine = null;
        Debug.Log("[Boss] AIBehavior stopped.");
    }
}

IEnumerator AIBehavior()
{
    while (true)
    {
        // 플레이어가 존재하고 엔트리 범위 안에 있으면 행동
        if (player != null)
        {
            float dist = Vector2.Distance(new Vector2(player.position.x, 0f), new Vector2(transform.position.x, 0f));
            if (dist <= aggroRange)
            {
                // 플레이어가 가까이 있으면 패턴 실행 (busy면 기다림)
                if (!busy)
                {
                    // 랜덤으로 혹은 규칙으로 선택
                    int choice = Random.Range(0, 100);
                    if (choice < 50)
                    {
                        Debug.Log("[Boss AI] Chosen: Claw (maybe triple)");
                        bool triple = (Random.value > 0.6f); // 40% 확률로 3연타
                        StartClaw(triple);
                    }
                    else if (choice < 80)
                    {
                        Debug.Log("[Boss AI] Chosen: BodySlam");
                        StartBodySlam();
                    }
                    else
                    {
                        Debug.Log("[Boss AI] Chosen: Decoy");
                        StartDecoy();
                    }
                }
            }
        }
        // 행동 간 랜덤 대기
        float wait = Random.Range(aiMinDelayBetweenActions, aiMaxDelayBetweenActions);
        yield return new WaitForSeconds(aiCheckInterval + wait);
    }
}

// --- Test helpers so you can manually kick actions from Inspector (Play Mode) ---
[ContextMenu("TEST: StartClaw_Single")]
public void DEBUG_StartClawSingle() { StartClaw(false); Debug.Log("[DEBUG] StartClaw(false) called"); }

[ContextMenu("TEST: StartClaw_Triple")]
public void DEBUG_StartClawTriple() { StartClaw(true); Debug.Log("[DEBUG] StartClaw(true) called"); }

[ContextMenu("TEST: StartBodySlam")]
public void DEBUG_StartBodySlam() { StartBodySlam(); Debug.Log("[DEBUG] StartBodySlam() called"); }

[ContextMenu("TEST: StartDecoy")]
public void DEBUG_StartDecoy() { StartDecoy(); Debug.Log("[DEBUG] StartDecoy() called"); }

    void Start()
    {
        if (player == null) Debug.LogWarning("[Boss] Player reference not set!");
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        // optional: 물리 푸시 방지 (통제된 transform 이동)
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            // rb.simulated = false; // 필요시 사용
        }

        if (hitboxPrefab == null) Debug.LogWarning("[Boss] Hitbox prefab not assigned.");
    }

    void Update()
    {
        if (player == null) return;

        float dx = player.position.x - transform.position.x;
        if (!busy && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            Vector3 newPos = transform.position;
            newPos.x = Mathf.MoveTowards(transform.position.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = newPos;
        }

        if (player.position.x > transform.position.x && !facingRight) Flip();
        if (player.position.x < transform.position.x && facingRight) Flip();
    }

    void Flip()
    {
        facingRight = !facingRight;
        if (spriteRenderer != null) spriteRenderer.flipX = !spriteRenderer.flipX;
        Vector3 s = transform.localScale; s.x *= -1f; transform.localScale = s;
    }

    // -------------------------
    // Patterns public triggers
    // -------------------------
    public void StartClaw(bool triple = false)
    {
        if (!busy) StartCoroutine(ClawRoutine(triple));
    }

    public void StartBodySlam()
    {
        if (!busy) StartCoroutine(BodySlamRoutine());
    }

    public void StartDecoy()
    {
        if (!busy) StartCoroutine(DecoyRoutine());
    }

    // -------------------------
    // Pattern implementations
    // -------------------------
    IEnumerator ClawRoutine(bool triple)
    {
        busy = true;
        animator?.SetTrigger("ClawTelegraph");

        SpawnTelegraph(GetClawPos(), triple ? clawWideSize : clawShortSize, telegraphTime);
        yield return new WaitForSeconds(telegraphTime);

        if (!triple)
        {
            SpawnAndMaybeDashClaw(false, 0);
            yield return new WaitForSeconds(cooldownTime);
        }
        else
        {
            animator?.SetTrigger("ClawExecute");
            for (int i = 0; i < 3; i++)
            {
                SpawnAndMaybeDashClaw(true, i);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(cooldownTime);
        }

        busy = false;
    }

    void SpawnAndMaybeDashClaw(bool triple, int index)
    {
        Vector2 size = triple ? clawShortSize : clawWideSize;
        float dashDist = triple ? shortDashDistance : wideDashDistance;
        Vector2 pos = GetClawPos();

        // spawn hitbox: set only the fields that exist on Hitbox.cs
        GameObject hb = SpawnHitbox(pos, size, 0.15f, clawDamage, hitboxTargetTag, singleUse: true, targetLayer: hitboxTargetLayer);

        Vector3 dashTarget = transform.position + Vector3.right * (facingRight ? dashDist : -dashDist);
        StartCoroutine(QuickMoveTo(dashTarget, dashMoveDuration));
    }

    IEnumerator BodySlamRoutine()
    {
        busy = true;
        animator?.SetTrigger("BodySlamTelegraph");

        Vector2 pos = GetBodySlamPos();
        SpawnTelegraph(pos, bodySlamSize, telegraphTime);

        yield return new WaitForSeconds(telegraphTime);

        GameObject hb = SpawnHitbox(pos, bodySlamSize, 0.4f, bodySlamDamage, hitboxTargetTag, singleUse: true, targetLayer: hitboxTargetLayer);

        yield return new WaitForSeconds(cooldownTime);
        busy = false;
    }

    IEnumerator DecoyRoutine()
    {
        busy = true;
        animator?.SetTrigger("DecoyTelegraph");
        yield return new WaitForSeconds(0.4f);

        List<GameObject> spawned = new List<GameObject>();
        if (decoySpawnPoints.Count > 0)
        {
            for (int i = 0; i < decoyCount && i < decoySpawnPoints.Count; i++)
            {
                var dp = decoySpawnPoints[i];
                if (dp == null) continue;
                GameObject d = Instantiate(decoyPrefab, dp.position, Quaternion.identity);
                SetupDecoyVisual(d, i == 0 ? realColor : fakeColor);
                spawned.Add(d);
            }
        }
        else
        {
            for (int i = 0; i < decoyCount; i++)
            {
                Vector3 p = transform.position + new Vector3((i + 1) * 1.2f * (i % 2 == 0 ? 1 : -1), 0f, 0f);
                GameObject d = Instantiate(decoyPrefab, p, Quaternion.identity);
                SetupDecoyVisual(d, fakeColor);
                spawned.Add(d);
            }
        }

        yield return new WaitForSeconds(decoyLifetime);
        foreach (var d in spawned) if (d != null) Destroy(d);
        yield return new WaitForSeconds(cooldownTime);
        busy = false;
    }

    void SetupDecoyVisual(GameObject d, Color c)
    {
        if (d == null) return;
        var sr = d.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = c;

        // Try to call DecoyController.Setup(Color) if component exists
        var decCtrl = d.GetComponent<MonoBehaviour>(); // safe check
        if (decCtrl != null)
        {
            var t = decCtrl.GetType();
            var m = t.GetMethod("Setup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null)
            {
                var parms = m.GetParameters();
                if (parms.Length == 1 && parms[0].ParameterType == typeof(Color))
                {
                    m.Invoke(decCtrl, new object[] { c });
                }
                else if (parms.Length == 0)
                {
                    m.Invoke(decCtrl, null);
                }
            }
        }
    }

    // -------------------------
    // Spawn telegraph & hitbox (Hitbox API compatible)
    // -------------------------
    GameObject SpawnTelegraph(Vector2 center, Vector2 size, float duration)
    {
        if (telegraphPrefab == null)
        {
            Debug.LogWarning("[Boss] telegraphPrefab not assigned.");
            return null;
        }

        GameObject t = Instantiate(telegraphPrefab, center, Quaternion.identity);
        t.transform.localScale = new Vector3(size.x, size.y, 1f);
        Destroy(t, duration + 0.05f);
        return t;
    }

    // 타겟 레이어를 LayerMask로 전달하여 Hitbox.targetLayerMask에 셋팅
    GameObject SpawnHitbox(Vector2 center, Vector2 size, float lifetime, int damage, string targetTag = "Player", bool singleUse = true, LayerMask? targetLayer = null)
    {
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[Boss] hitboxPrefab not assigned.");
            return null;
        }

        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);
        hb.transform.localScale = new Vector3(size.x, size.y, 1f);

        // layer는 프리팹 기본값을 쓰거나, 원하면 hb.layer를 바꿀 수 있음 (대부분 불필요)
        // hb.layer = LayerMaskToLayerIndex(yourChosenLayer) // 필요시 구현

        var col = hb.GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.Log("[Boss] set hitbox collider isTrigger=true for reliable detection.");
        }
        else if (col == null)
        {
            Debug.LogWarning("[Boss] spawned hitbox has no Collider2D - hit detection won't work.");
        }

        // Hitbox 컴포넌트에 존재하는 멤버들만 안전하게 세팅
        var hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            // 필수/기본 필드
            hbComp.owner = this.gameObject;
            hbComp.targetTag = targetTag;
            hbComp.damage = damage;
            hbComp.singleUse = singleUse;

            // target 레이어마스크가 설정되어 있으면 적용
            if (targetLayer.HasValue)
            {
                try
                {
                    hbComp.targetLayerMask = targetLayer.Value;
                }
                catch
                {
                    // Hitbox API가 달라질 경우 대비: 그냥 무시
                }
            }
        }
        else
        {
            Debug.LogWarning("[Boss] spawned hitbox prefab has no Hitbox component. Add Hitbox script or update code.");
        }

        if (lifetime > 0f) Destroy(hb, lifetime + 0.05f);
        return hb;
    }

    IEnumerator QuickMoveTo(Vector3 target, float duration)
    {
        float start = Time.time;
        Vector3 startPos = transform.position;
        while (Time.time < start + duration)
        {
            float t = (Time.time - start) / duration;
            transform.position = Vector3.Lerp(startPos, target, t);
            yield return null;
        }
        transform.position = target;
    }

    Vector2 GetClawPos()
    {
        float dir = facingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(clawOffsetX * dir, 0f);
    }

    Vector2 GetBodySlamPos()
    {
        float dir = facingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(bodySlamOffsetX * dir, 0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(GetClawPos(), clawWideSize);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(GetBodySlamPos(), bodySlamSize);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
    }
}
