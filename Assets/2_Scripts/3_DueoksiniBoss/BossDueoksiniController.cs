using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossDueoksiniController : MonoBehaviour
{
    // =========================
    // Refs
    // =========================
    [Header("Refs")]
    public Transform player; // (비워두면 Tag=Player 자동 탐색)

    // === NEW: Graphics/Animator 전용 참조(자식으로 옮겨도 동작) ===
    [Header("Graphics (optional)")]
    [Tooltip("보스의 애니메이터(보통 GfxRoot에 있음). 비워두면 자식에서 자동 탐색")]
    public Animator gfxAnimator;
    [Tooltip("보스의 스프라이트 렌더러(자식). 비워두면 자식에서 자동 탐색")]
    public SpriteRenderer gfxRenderer;
    [Tooltip("좌우 반전을 적용할 트랜스폼(보통 FacingPivot). 비워두면 gfxRenderer/Animator의 Transform → 없으면 루트")]
    public Transform gfxFlipRoot;

    // =========================
    // Charge (dash) flow
    // =========================
    [Header("Charge")]
    [Tooltip("돌진 총 가로 거리(+) = 우 / (–) = 좌. 실제로는 플레이어 방향을 보고 부호를 정함")]
    public float chargeDistance = 6f;
    [Tooltip("돌진에 걸리는 시간(초)")]
    public float chargeTime = 0.6f;
    [Tooltip("벽/바닥 레이어(여기에 맞으면 앞에서 정지)")]
    public LayerMask environmentMask;
    [Tooltip("벽 앞 세이프 간격")]
    public float wallSkin = 0.1f;
    [Tooltip("돌진 시작 애니 트리거(선택)")]
    public string chargeTrigger = "Charge";
    [Tooltip("돌진 중 true로 올릴 애니 Bool(선택)")]
    public string isChargingBool = "isCharging";

    [Header("Face/Move")]
    [Tooltip("행동 진입 시 플레이어를 바라보게 좌우 반전")]
    public bool facePlayer = true;

    // =========================
    // NEW: Charge-Prep (single)
    // =========================
    [Header("Charge ▸ Prep (single)")]
    [Tooltip("돌진 직전 준비 모션(하나만). 비우면 준비 없이 바로 돌진/점프 판정")]
    public string chargePrepTrigger = "Prep_Charge";
    [Tooltip("애니메이션 이벤트로 종료할지 여부 (AnimEvent_PrepReady 호출)")]
    public bool chargePrepEndByAnimEvent = false;
    [Tooltip("이벤트를 쓰지 않는다면 대기 시간(초)")]
    public float chargePrepHoldTime = 0.5f;

    [Header("Charge ▸ Far → Jump branch")]
    [Tooltip("돌진 준비 끝 시점에 플레이어와 X거리 절대값이 이 값보다 크면, 돌진 대신 점프 공격으로 분기")]
    public float farDistanceThreshold = 10f;

    // =========================
    // Jump attack (when far)
    // =========================
    [Header("Jump Attack (when far at Charge-Prep)")]
    public float jumpDuration = 0.8f;
    public float jumpArcHeight = 3.5f;
    public string jumpTrigger = "JumpAtk";
    public bool jumpEndsWithSlam = true;

    // =========================
    // Attack-Prep list (multi)
    // =========================
    [System.Serializable]
    public class PrepOption
    {
        public string prepTrigger = "PrepA";
        public AttackKind attack = AttackKind.Swipe;
        public string attackTrigger = "Atk_Swipe";
        public float prepHoldTime = 0.5f;
        public float weight = 1f;
    }

    public enum AttackKind { Swipe, ProjectileBurst, GroundSlam }

    [Header("Prep & Attack")]
    public List<PrepOption> preps = new List<PrepOption>();
    public bool attackPrepEndByAnimEvent = false;

    // =========================
    // Attack: Swipe
    // =========================
    [Header("Attack ▸ Swipe")]
    public List<Collider2D> swipeHitboxes = new List<Collider2D>();
    public float swipeActiveTime = 0.12f;
    [Tooltip("애니 이벤트 AnimEvent_SwipeHitOn/Off로 직접 토글할지")]
    public bool swipeUseAnimEvent = false;

    // =========================
    // Attack: Projectile Burst
    // =========================
    [Header("Attack ▸ Projectile Burst")]
    public GameObject projectilePrefab;
    public List<Transform> projectileMuzzles = new List<Transform>();
    public int projectileCount = 6;
    public float projectileInterval = 0.07f;
    public float projectileSpeed = 12f;

    // =========================
    // Attack: Ground Slam
    // =========================
    [Header("Attack ▸ Ground Slam")]
    public List<Collider2D> slamHitboxes = new List<Collider2D>();
    public float slamActiveTime = 0.15f;
    public float slamShakeAmp = 0.8f;
    public float slamShakeDur = 0.2f;
    public float slamShakeFreq = 22f;

    // =========================
    // Internals
    // =========================
    Animator _anim;               // 실제로 사용할 애니메이터(= gfxAnimator)
    Rigidbody2D _rb;
    Vector3 _spawnPos;
    int _lastPrepIndex = -1;

    bool _waitingPrepEvent = false;
    bool _inRoutine = false;

    void Awake()
    {
        // --- 플레이어 자동 찾기 ---
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // --- 그래픽/애니메이터 자동 바인딩 (자식 포함) ---
        if (!gfxAnimator) gfxAnimator = GetComponentInChildren<Animator>(true);
        if (!gfxRenderer) gfxRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (!gfxFlipRoot)
        {
            if (gfxRenderer) gfxFlipRoot = gfxRenderer.transform;
            else if (gfxAnimator) gfxFlipRoot = gfxAnimator.transform;
            else gfxFlipRoot = transform; // 최후 폴백
        }
        _anim = gfxAnimator; // 기존 코드와의 최소 변경을 위해 내부 필드에 연결

        _rb = GetComponent<Rigidbody2D>();
        _spawnPos = transform.position;
    }

    void OnEnable()
    {
        if (!_inRoutine) StartCoroutine(MainLoop());
    }

    // =========================
    // Main FSM Loop
    // =========================
    IEnumerator MainLoop()
    {
        _inRoutine = true;

        while (true)
        {
            // 1) Charge-Prep (single)
            if (facePlayer) FaceTowardPlayer();

            yield return DoChargePrep();

            // 분기
            bool far = IsPlayerFar(farDistanceThreshold);

            if (far)
            {
                // 2-a) Jump Attack → 루프 초기화
                yield return DoJumpAttack();
                continue;
            }
            else
            {
                // 2-b) Dash Charge (Y 고정)
                yield return DoCharge();
            }

            // 3) Attack-Prep → 4) Attack
            if (facePlayer) FaceTowardPlayer();

            var prep = PickPrep();
            yield return DoAttackPrep(prep);
            yield return DoAttack(prep);
        }
    }

    // =========================
    // Steps
    // =========================
    IEnumerator DoChargePrep()
    {
        if (!string.IsNullOrEmpty(chargePrepTrigger) && _anim)
            _anim.SetTrigger(chargePrepTrigger);

        if (chargePrepEndByAnimEvent)
        {
            _waitingPrepEvent = true;
            while (_waitingPrepEvent) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(chargePrepHoldTime);
        }
    }

    IEnumerator DoCharge()
    {
        if (_anim && !string.IsNullOrEmpty(chargeTrigger))
            _anim.SetTrigger(chargeTrigger);
        if (_anim && !string.IsNullOrEmpty(isChargingBool))
            _anim.SetBool(isChargingBool, true);

        // 목표 X(플레이어 방향)
        int dir = DirToPlayer();
        float startX = transform.position.x;
        float targetX = startX + dir * Mathf.Abs(chargeDistance);

        // 벽 체크
        float rayLen = Mathf.Abs(chargeDistance) + wallSkin;
        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(dir, 0f),
            rayLen,
            environmentMask
        );
        if (hit.collider)
            targetX = hit.point.x - dir * wallSkin;

        // 시간 보간 (Y 고정)
        float t = 0f;
        Vector3 p0 = transform.position;
        Vector3 p1 = new Vector3(targetX, p0.y, p0.z);

        while (t < chargeTime)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, chargeTime));
            float k = EaseInOut(a);
            Vector3 pos = Vector3.Lerp(p0, p1, k);
            pos.y = p0.y; // Y 고정
            transform.position = pos;

            t += Time.deltaTime;
            yield return null;
        }
        transform.position = p1;

        if (_anim && !string.IsNullOrEmpty(isChargingBool))
            _anim.SetBool(isChargingBool, false);
    }

    IEnumerator DoJumpAttack()
    {
        if (_anim && !string.IsNullOrEmpty(jumpTrigger))
            _anim.SetTrigger(jumpTrigger);

        Vector3 start = transform.position;
        Vector3 end = new Vector3(player ? player.position.x : start.x, start.y, start.z);

        if (facePlayer) FaceTowardPlayer();

        float t = 0f;
        while (t < jumpDuration)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, jumpDuration));
            float yOffset = 4f * jumpArcHeight * a * (1f - a);
            float x = Mathf.Lerp(start.x, end.x, a);
            float y = start.y + yOffset;

            transform.position = new Vector3(x, y, start.z);

            t += Time.deltaTime;
            yield return null;
        }
        transform.position = new Vector3(end.x, start.y, start.z);

        if (jumpEndsWithSlam)
            yield return DoSlamOnce();
    }

    PrepOption PickPrep()
    {
        if (preps == null || preps.Count == 0)
            return new PrepOption();

        int n = preps.Count;
        float total = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i == _lastPrepIndex) continue;
            total += Mathf.Max(0f, preps[i].weight);
        }
        float r = Random.value * total;
        for (int i = 0; i < n; i++)
        {
            if (i == _lastPrepIndex) continue;
            float w = Mathf.Max(0f, preps[i].weight);
            if (r < w) { _lastPrepIndex = i; return preps[i]; }
            r -= w;
        }
        _lastPrepIndex = 0;
        return preps[0];
    }

    IEnumerator DoAttackPrep(PrepOption opt)
    {
        if (_anim && !string.IsNullOrEmpty(opt.prepTrigger))
            _anim.SetTrigger(opt.prepTrigger);

        if (attackPrepEndByAnimEvent)
        {
            _waitingPrepEvent = true;
            while (_waitingPrepEvent) yield return null;
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0f, opt.prepHoldTime));
        }
    }

    IEnumerator DoAttack(PrepOption opt)
    {
        if (_anim && !string.IsNullOrEmpty(opt.attackTrigger))
            _anim.SetTrigger(opt.attackTrigger);

        switch (opt.attack)
        {
            case AttackKind.Swipe: yield return DoSwipeOnce(); break;
            case AttackKind.ProjectileBurst: yield return DoProjectileBurst(); break;
            case AttackKind.GroundSlam: yield return DoSlamOnce(); break;
        }
    }

    // =========================
    // Attacks
    // =========================
    IEnumerator DoSwipeOnce()
    {
        if (swipeUseAnimEvent)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, swipeActiveTime));
        }
        else
        {
            ToggleColliders(swipeHitboxes, true);
            yield return new WaitForSeconds(swipeActiveTime);
            ToggleColliders(swipeHitboxes, false);
        }
    }

    IEnumerator DoProjectileBurst()
    {
        if (!projectilePrefab || projectileMuzzles.Count == 0) yield break;

        for (int i = 0; i < projectileCount; i++)
        {
            foreach (var m in projectileMuzzles)
            {
                if (!m) continue;
                var go = GameObject.Instantiate(projectilePrefab, m.position, m.rotation);
                var rb = go.GetComponent<Rigidbody2D>();
                Vector2 dir = Vector2.right * DirToPlayer();
                if (player) dir = (player.position - m.position).normalized;
                if (rb) rb.linearVelocity = dir * projectileSpeed;
            }
            if (projectileInterval > 0f) yield return new WaitForSeconds(projectileInterval);
            else yield return null;
        }
    }

    IEnumerator DoSlamOnce()
    {
        var cam = Camera.main ? Camera.main.GetComponent<CameraSimple2D>() : null;
        if (cam) cam.Shake(slamShakeAmp, slamShakeDur, slamShakeFreq);

        ToggleColliders(slamHitboxes, true);
        yield return new WaitForSeconds(slamActiveTime);
        ToggleColliders(slamHitboxes, false);
    }

    // =========================
    // Helpers
    // =========================
    bool IsPlayerFar(float threshold)
    {
        if (!player) return false;
        return Mathf.Abs(player.position.x - transform.position.x) > Mathf.Abs(threshold);
    }

    int DirToPlayer()
    {
        if (!player) return transform.localScale.x >= 0f ? 1 : -1;
        return (player.position.x - transform.position.x) >= 0f ? 1 : -1;
    }

    void FaceTowardPlayer()
    {
        // === 그래픽만 뒤집는다(히트박스/루트는 그대로) ===
        int dir = DirToPlayer();
        var target = gfxFlipRoot ? gfxFlipRoot : transform;
        Vector3 s = target.localScale;
        s.x = Mathf.Abs(s.x) * (dir >= 0 ? 1f : -1f);
        target.localScale = s;
    }

    static void ToggleColliders(List<Collider2D> list, bool on)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i]) list[i].enabled = on;
        }
    }

    static float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // =========================
    // Animation Events
    // =========================
    public void AnimEvent_PrepReady() { _waitingPrepEvent = false; }
    public void AnimEvent_SwipeHitOn() { ToggleColliders(swipeHitboxes, true); }
    public void AnimEvent_SwipeHitOff() { ToggleColliders(swipeHitboxes, false); }
    public void AnimEvent_SlamHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SlamHitOff() { ToggleColliders(slamHitboxes, false); }

    // =========================
    // Gizmos
    // =========================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 a = transform.position;
        float sign = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
        Vector3 b = a + Vector3.right * sign * chargeDistance;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(b, 0.08f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, Mathf.Abs(farDistanceThreshold));
    }
}
