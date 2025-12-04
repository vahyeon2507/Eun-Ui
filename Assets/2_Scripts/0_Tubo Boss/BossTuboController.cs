using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossTuboController : MonoBehaviour
{
    [Header("Refs")]
    public Transform playerPoint;              // 플레이어 중앙 "Point"
    public BossHealth bossHealth;              // 2배 피해용
    public Animator animator;                  // Attack/Groggy 트리거 재생
    public SpriteRenderer gfx;                 // 방향 전환용(선택)

    [Header("Telegraph & Attack")]
    public GameObject telegraphPrefab;         // 경고 프리팹 (선택)
    public GameObject strikeProjectilePrefab;  // 실제 타격 프리팹(여기에 히트/애니 존재)
    public float telegraphTime = 0.6f;         // 경고 유지 시간
    public bool telegraphFollowsPoint = true;  // 경고가 유지 시간 동안 Point를 따라갈지
    public float attackCooldown = 1.8f;        // 다음 패턴까지 대기

    [Header("Animation Triggers (optional)")]
    public string attackTrigger = "Attack";
    public string groggyTrigger = "Groggy";

    [Header("Groggy (on Parry)")]
    public float groggyDuration = 2.0f;        // 그로기 유지 시간
    public float groggyDamageMul = 2.0f;       // 그로기 동안 받는 피해 배수

    [Header("Facing")]
    public bool facePlayer = true;             // 단순 좌우만 맞춤

    bool _running;
    bool _groggy;

    void Reset()
    {
        bossHealth = GetComponent<BossHealth>();
        animator = GetComponentInChildren<Animator>();
        gfx = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (!_running) StartCoroutine(MainLoop());
    }

    IEnumerator MainLoop()
    {
        _running = true;
        while (true)
        {
            // 그로기면 공격 잠시 홀드
            while (_groggy) yield return null;

            if (facePlayer) FaceToPlayerX();

            // 1) 경고 생성
            Vector3 pos = playerPoint ? playerPoint.position : transform.position;
            GameObject tg = null;
            if (telegraphPrefab)
            {
                tg = Instantiate(telegraphPrefab, pos, Quaternion.identity);
                var tgs = tg.GetComponent<TuboTelegraphIndicator2D>();
                if (tgs) tgs.Setup(telegraphFollowsPoint ? playerPoint : null, telegraphTime);
            }

            // 2) 경고 유지
            float t = 0f;
            while (t < telegraphTime)
            {
                if (facePlayer) FaceToPlayerX();
                // 경고가 따라가게 설정되어 있지 않다면, 마지막 1프레임마다 pos 갱신해둘 필요 X
                // 따라가는 모드여도 마지막 위치를 쓰기 위해 매 프레임 갱신
                if (telegraphFollowsPoint && playerPoint) pos = playerPoint.position;
                t += Time.deltaTime;
                yield return null;
            }

            if (tg) Destroy(tg);

            // 3) 공격 애니메이션 (선택)
            if (animator && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            // 4) 타격 프리팹 소환 (그 자리)
            if (strikeProjectilePrefab)
            {
                var go = Instantiate(strikeProjectilePrefab, pos, Quaternion.identity);
                var sp = go.GetComponent<TuboStrikeProjectile2D>();
                if (sp) sp.Play(); // 바로 재생(히트/애니/자멸)
            }

            // 5) 쿨다운
            float cd = attackCooldown;
            while (cd > 0f)
            {
                cd -= Time.deltaTime;
                yield return null;
            }
        }
    }

    void FaceToPlayerX()
    {
        if (!playerPoint || !gfx) return;
        bool right = playerPoint.position.x >= transform.position.x;
        gfx.flipX = !right; // 스프라이트 좌우 뒤집기(편의)
    }

    // 패링 성공 시 외부에서 호출
    public void OnParried()
    {
        if (_groggy) return;
        StartCoroutine(CoGroggy());
    }

    IEnumerator CoGroggy()
    {
        _groggy = true;

        if (animator && !string.IsNullOrEmpty(groggyTrigger))
            animator.SetTrigger(groggyTrigger);

        if (bossHealth != null)
            bossHealth.ApplyVulnerability(groggyDamageMul, groggyDuration);

        float t = 0f;
        while (t < groggyDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        _groggy = false;
    }
}
