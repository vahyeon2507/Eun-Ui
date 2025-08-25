// JangsanbeomBoss_Fallback_Modified.cs
using System.Collections;
using UnityEngine;

public class JangsanbeomBoss_Fallback_Modified : MonoBehaviour
{
    public enum JangsanState { Idle, ClawSwipe, BodySlam, DecoySplit, Cooldown }

    [Header("Basic")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public bool useAnimatorFallback = true;

    [Header("References")]
    public Transform player;                    // 반드시 연결 (자동검색도 시도함)
    private Rigidbody2D rb;

    [Header("Prefabs & Points")]
    public GameObject telegraphPrefab;
    public GameObject hitboxPrefab;
    public GameObject decoyPrefab;
    public Transform[] decoySpawnPoints;

    [Header("Timings")]
    public float idleDelay = 1f;
    public float telegraphTime = 1f;
    public float cooldownTime = 1f;

    [Header("Claw Settings")]
    public float clawOffsetX = 1.5f;            // 기본 offset (X축 기준)
    public Vector2 clawWideSize = new Vector2(3f, 1f);
    public Vector2 clawShortSize = new Vector2(1.5f, 1f);
    public float wideDashDistance = 0.6f;      // 넓은 공격의 돌진거리
    public float shortDashDistance = 0.8f;     // 좁은(연속) 공격의 각각 돌진거리
    public float dashMoveDuration = 0.06f;     // dash 할 때 한 프레임 대체 시간 (FixedUpdate 한틱으로 움직이기)

    [Header("BodySlam Settings")]
    public Vector2 bodySlamSize = new Vector2(4f, 2f);
    public float bodySlamOffsetX = 2f;

    private JangsanState currentState = JangsanState.Idle;
    private Coroutine bossLoopCoroutine;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        // 보스는 절대 플레이어에 의해 밀리지 않음
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // 자동으로 플레이어 찾기 (태그 "Player" 사용)
        if (player == null)
        {
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }

        bossLoopCoroutine = StartCoroutine(BossLoop());
    }

    IEnumerator BossLoop()
    {
        while (true)
        {
            switch (currentState)
            {
                case JangsanState.Idle:
                    yield return new WaitForSeconds(idleDelay);
                    ChooseNextAttack();
                    break;

                case JangsanState.ClawSwipe:
                    yield return ClawSwipeRoutine(Random.value < 0.5f);
                    currentState = JangsanState.Cooldown;
                    break;

                case JangsanState.BodySlam:
                    yield return BodySlamRoutine();
                    currentState = JangsanState.Cooldown;
                    break;

                case JangsanState.DecoySplit:
                    yield return DecoySplitRoutine();
                    currentState = JangsanState.Cooldown;
                    break;

                case JangsanState.Cooldown:
                    yield return new WaitForSeconds(cooldownTime);
                    currentState = JangsanState.Idle;
                    break;
            }
            yield return null;
        }
    }

    void ChooseNextAttack()
    {
        int r = Random.Range(0, 3);
        if (r == 0) currentState = JangsanState.ClawSwipe;
        else if (r == 1) currentState = JangsanState.BodySlam;
        else currentState = JangsanState.DecoySplit;
    }

    #region ClawSwipe
    IEnumerator ClawSwipeRoutine(bool isWide)
    {
        if (player == null) Debug.LogWarning("[Boss] player reference not set; default to right.");

        int dir = GetPlayerDir(); // +1 or -1
        PlayAnimSafe("Telegraph_Claw");
        ShowTelegraph(clawOffsetX * dir, isWide ? clawWideSize.x : clawShortSize.x, telegraphTime);

        yield return new WaitForSeconds(telegraphTime);

        if (isWide)
        {
            PlayAnimSafe("Claw_Wide");
            CreateHitboxSafe(clawOffsetX * dir, clawWideSize, 0.35f);
            // 한 번의 소폭 돌진
            yield return DashHorizontal(dir, wideDashDistance);
            yield return new WaitForSeconds(0.25f);
        }
        else
        {
            // 3회 연속: 매 공격마다 예고->타격->돌진
            for (int i = 0; i < 3; i++)
            {
                PlayAnimSafe("Claw_Narrow");
                ShowTelegraph(clawOffsetX * dir, clawShortSize.x, 0.25f);
                yield return new WaitForSeconds(0.25f);
                CreateHitboxSafe(clawOffsetX * dir, clawShortSize, 0.2f);

                // 각 공격마다 짧은 돌진 실행
                yield return DashHorizontal(dir, shortDashDistance);

                // 약간의 후딜
                yield return new WaitForSeconds(0.15f);
            }
        }
    }
    #endregion

    #region BodySlam
    IEnumerator BodySlamRoutine()
    {
        int dir = GetPlayerDir();
        PlayAnimSafe("Telegraph_BodySlam");
        // body slam은 앞쪽 방향 기준으로 고정된 직선 예고 (Y 무시)
        ShowTelegraph(bodySlamOffsetX * dir + (bodySlamSize.x / 2f) * dir, bodySlamSize.x, telegraphTime + 0.2f);

        yield return new WaitForSeconds(telegraphTime + 0.2f);

        PlayAnimSafe("BodySlam");
        CreateSlamHitboxSafe(bodySlamOffsetX * dir, bodySlamSize, 0.5f);
        yield return new WaitForSeconds(0.5f);
    }
    #endregion

    #region DecoySplit
    IEnumerator DecoySplitRoutine()
    {
        PlayAnimSafe("Decoy_Cast");
        ShowTelegraph(0f, 1f, 0.5f);
        yield return new WaitForSeconds(0.6f);

        if (decoyPrefab == null || decoySpawnPoints == null || decoySpawnPoints.Length == 0)
        {
            Debug.LogWarning("[Boss] decoyPrefab or spawn points not configured.");
            yield break;
        }

        int realIndex = Random.Range(0, decoySpawnPoints.Length);
        for (int i = 0; i < decoySpawnPoints.Length; i++)
        {
            var go = Instantiate(decoyPrefab, decoySpawnPoints[i].position, Quaternion.identity);
            var dc = go.GetComponent<DecoyController>();
            if (dc != null)
                dc.Setup(i == realIndex);
            else
                Debug.LogWarning("Decoy prefab missing DecoyController component.");
        }

        yield return new WaitForSeconds(2f);
    }
    #endregion

    #region Utilities
    int GetPlayerDir()
    {
        if (player == null) return 1;
        float dx = player.position.x - transform.position.x;
        return dx >= 0f ? 1 : -1;
    }

    IEnumerator DashHorizontal(int dir, float distance)
    {
        if (rb == null) yield break;
        Vector2 start = rb.position;
        Vector2 target = start + new Vector2(dir * distance, 0f);

        // MovePosition once then wait a physics tick for stability
        rb.MovePosition(target);
        yield return new WaitForFixedUpdate(); // 물리엔진이 적용된 후에 다음 동작
    }

    void PlayAnimSafe(string stateName)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Play(stateName);
        }
        else if (useAnimatorFallback)
        {
            if (spriteRenderer != null) StartCoroutine(FallbackFlashRoutine());
            else Debug.Log("[Boss] (fallback) would play: " + stateName);
        }
    }

    IEnumerator FallbackFlashRoutine()
    {
        if (spriteRenderer == null) yield break;
        Color original = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        Vector3 origScale = transform.localScale;
        transform.localScale = origScale * 1.05f;
        yield return new WaitForSeconds(0.12f);
        spriteRenderer.color = original;
        transform.localScale = origScale;
    }

    void ShowTelegraph(float centerOffsetX, float width, float duration)
    {
        if (telegraphPrefab == null)
        {
            Debug.LogWarning("[Boss] telegraphPrefab not assigned.");
            return;
        }

        Vector3 pos = transform.position + Vector3.right * centerOffsetX;
        var tg = Instantiate(telegraphPrefab, pos, Quaternion.identity);
        tg.transform.localScale = new Vector3(width, 1f, 1f);
        var dd = tg.GetComponent<DestroyAfter>();
        if (dd != null) dd.lifetime = duration;
        else Destroy(tg, duration);
    }

    void CreateHitboxSafe(float offsetX, Vector2 size, float life)
    {
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[Boss] hitboxPrefab not assigned.");
            return;
        }
        Vector3 pos = transform.position + Vector3.right * offsetX;
        var hb = Instantiate(hitboxPrefab, pos, Quaternion.identity);

        // collider direct set if available
        BoxCollider2D bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.size = size;
            bc.offset = Vector2.zero;
        }
        else
        {
            hb.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        var hscript = hb.GetComponent<Hitbox>();
        if (hscript != null) hscript.damage = 1;

        Destroy(hb, life);
    }

    void CreateSlamHitboxSafe(float offsetX, Vector2 size, float life)
    {
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[Boss] hitboxPrefab not assigned.");
            return;
        }
        Vector3 pos = transform.position + Vector3.right * offsetX;
        var hb = Instantiate(hitboxPrefab, pos, Quaternion.identity);

        BoxCollider2D bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.size = size;
            bc.offset = Vector2.zero;
        }
        else
        {
            hb.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        var hscript = hb.GetComponent<Hitbox>();
        if (hscript != null)
        {
            hscript.damage = 2;
            hscript.allowCenterSafe = true;
            hscript.centerSafeRadius = 1.0f;
        }
        Destroy(hb, life);
    }
    #endregion

    // debug public API
    public void ForceAttack_Claw(bool wide) { currentState = JangsanState.ClawSwipe; StartCoroutine(ClawSwipeRoutine(wide)); }
    public void ForceAttack_BodySlam() { currentState = JangsanState.BodySlam; StartCoroutine(BodySlamRoutine()); }
    public void ForceAttack_Decoy() { currentState = JangsanState.DecoySplit; StartCoroutine(DecoySplitRoutine()); }
}
