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
    public Transform player;                                // 플레이어 Transform(비워두면 Tag=Player 자동 탐색)

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
    [Tooltip("점프 총 시간(초)")]
    public float jumpDuration = 0.8f;
    [Tooltip("포물선 최고 높이 오프셋")]
    public float jumpArcHeight = 3.5f;
    [Tooltip("점프 시작 시 애니 트리거(선택)")]
    public string jumpTrigger = "JumpAtk";
    [Tooltip("착지 위치에서 GroundSlam을 같이 터뜨릴지")]
    public bool jumpEndsWithSlam = true;

    // =========================
    // Attack-Prep list (multi)
    // =========================
    [System.Serializable]
    public class PrepOption
    {
        [Tooltip("공격 준비 모션 트리거(예: PrepA, PrepB …)")]
        public string prepTrigger = "PrepA";
        [Tooltip("이 준비 이후 실행할 공격 타입")]
        public AttackKind attack = AttackKind.Swipe;
        [Tooltip("공격 시 쏠 애니 트리거(선택)")]
        public string attackTrigger = "Atk_Swipe";
        [Tooltip("이벤트를 쓰지 않는다면 준비 대기 시간(초)")]
        public float prepHoldTime = 0.5f;
        [Tooltip("랜덤 선택 가중치(클수록 잘 뽑힘)")]
        public float weight = 1f;
    }

    public enum AttackKind { Swipe, ProjectileBurst, GroundSlam }

    [Header("Prep & Attack")]
    [Tooltip("여러 준비 모션 후보 + 각 준비에 매핑된 공격")]
    public List<PrepOption> preps = new List<PrepOption>();
    [Tooltip("공격 준비 종료를 애니메이션 이벤트로(AnimEvent_PrepReady) 처리")]
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

    [Tooltip("카메라 흔들림 (CameraSimple2D.Shake)")]
    public float slamShakeAmp = 0.8f;
    public float slamShakeDur = 0.2f;
    public float slamShakeFreq = 22f;

    // =========================
    // Internals
    // =========================
    Animator _anim;
    Rigidbody2D _rb;
    Vector3 _spawnPos;
    int _lastPrepIndex = -1;

    bool _waitingPrepEvent = false;   // 애니 이벤트로 준비 종료 대기 중
    bool _inRoutine = false;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _spawnPos = transform.position;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
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

            // Charge-Prep 종료 시 분기 결정
            bool far = IsPlayerFar(farDistanceThreshold);

            if (far)
            {
                // 2-a) Jump Attack → (landing) → 루프 리셋
                yield return DoJumpAttack();
                // 요구사항: 점프 후에는 공격 준비로 가지 않고, 루프를 초기화
                continue; // while(true) 처음으로
            }
            else
            {
                // 2-b) Dash Charge (X만 이동)
                yield return DoCharge();
            }

            // 3) Attack-Prep (multi) → 4) Attack (매핑)
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

        // 목표 X 계산(플레이어 방향)
        int dir = DirToPlayer(); // -1 or +1
        float startX = transform.position.x;
        float targetX = startX + dir * Mathf.Abs(chargeDistance);

        // 벽 확인: 시작 위치에서 타깃 방향으로 레이
        float rayLen = Mathf.Abs(chargeDistance) + wallSkin;
        RaycastHit2D hit = Physics2D.Raycast(
            origin: new Vector2(transform.position.x, transform.position.y),
            direction: new Vector2(dir, 0f),
            distance: rayLen,
            layerMask: environmentMask
        );
        if (hit.collider)
        {
            // 벽 앞에서 멈춤
            targetX = hit.point.x - dir * wallSkin;
        }

        // 시간 보간 (Y 고정!)
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

        // 점프 목표: 현재 프레임의 플레이어 X로 수평 정렬 (착지 Y는 시작 Y로 복귀)
        Vector3 start = transform.position;
        Vector3 end = new Vector3(player ? player.position.x : start.x, start.y, start.z);

        // 얼굴 방향
        if (facePlayer) FaceTowardPlayer();

        float t = 0f;
        while (t < jumpDuration)
        {
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, jumpDuration));
            // 포물선: yOffset = 4h*a*(1-a)
            float yOffset = 4f * jumpArcHeight * a * (1f - a);
            float x = Mathf.Lerp(start.x, end.x, a);
            float y = Mathf.Lerp(start.y, start.y, a) + yOffset;

            transform.position = new Vector3(x, y, start.z);

            t += Time.deltaTime;
            yield return null;
        }
        transform.position = new Vector3(end.x, start.y, start.z);

        // 착지 효과: Ground Slam 옵션
        if (jumpEndsWithSlam)
        {
            yield return DoSlamOnce();
        }
    }

    PrepOption PickPrep()
    {
        if (preps == null || preps.Count == 0)
            return new PrepOption(); // 기본값

        // 직전과 같은 준비는 피하고, weight로 가중치 선택
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
            case AttackKind.Swipe:
                yield return DoSwipeOnce();
                break;
            case AttackKind.ProjectileBurst:
                yield return DoProjectileBurst();
                break;
            case AttackKind.GroundSlam:
                yield return DoSlamOnce();
                break;
        }
    }

    // =========================
    // Attacks
    // =========================
    IEnumerator DoSwipeOnce()
    {
        if (swipeUseAnimEvent)
        {
            // 애니 이벤트가 On/Off를 제어한다. 여기서는 단순히 짧은 대기만.
            // (필요하면 공격 애니 길이만큼 Wait)
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
        if (!projectilePrefab || projectileMuzzles.Count == 0)
        {
            yield break;
        }

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
            if (projectileInterval > 0f)
                yield return new WaitForSeconds(projectileInterval);
            else
                yield return null;
        }
    }

    IEnumerator DoSlamOnce()
    {
        // 카메라 흔들림(있으면)
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
        int dir = DirToPlayer();
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir >= 0 ? 1f : -1f);
        transform.localScale = s;
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
        // smootherstep-ish
        return x * x * (3f - 2f * x);
    }

    // =========================
    // Animation Events
    // =========================
    // 준비 모션 종료 이벤트(Charge-Prep/Attack-Prep 공용)
    public void AnimEvent_PrepReady()
    {
        _waitingPrepEvent = false;
    }

    // 스와이프/슬램 충돌 On/Off (애니 이벤트용)
    public void AnimEvent_SwipeHitOn() { ToggleColliders(swipeHitboxes, true); }
    public void AnimEvent_SwipeHitOff() { ToggleColliders(swipeHitboxes, false); }
    public void AnimEvent_SlamHitOn() { ToggleColliders(slamHitboxes, true); }
    public void AnimEvent_SlamHitOff() { ToggleColliders(slamHitboxes, false); }

    // =========================
    // Gizmos (에디터 미리보기)
    // =========================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 a = transform.position;
        Vector3 b = a + Vector3.right * Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x) * chargeDistance;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(b, 0.08f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, Mathf.Abs(farDistanceThreshold));
    }
}
