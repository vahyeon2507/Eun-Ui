using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossDueoksiniController : MonoBehaviour
{
    // ===== References =====
    [Header("Refs")]
    [Tooltip("플레이어 Transform. 비워두면 태그 Player로 자동 탐색")]
    public Transform player;
    Rigidbody2D rb;
    Animator anim;

    // ===== Charge (돌진) =====
    [Header("Charge")]
    [Tooltip("돌진 목표 거리(시작 시 플레이어 ‘가로’ 방향으로 이만큼 진행)")]
    public float chargeDistance = 6f;
    [Tooltip("돌진에 걸리는 시간")]
    public float chargeTime = 0.6f;
    [Tooltip("장애물 충돌 마스크. 맞으면 그 앞에서 멈춤")]
    public LayerMask environmentMask;
    [Tooltip("멈출 때 벽과의 최소 간격")]
    public float wallSkin = 0.1f;

    [Tooltip("돌진 애니메이션 트리거")]
    public string chargeTrigger = "Charge";
    [Tooltip("돌진 중 bool 파라미터(선택)")]
    public string isChargingBool = "isCharging";

    [Header("Facing/Move")]
    [Tooltip("플레이어를 향해 좌우 반전할지")]
    public bool facePlayer = true;

    // ===== Prep (준비 모션) & Attack =====
    public enum AttackKind { Swipe, ProjectileBurst, GroundSlam }

    [System.Serializable]
    public class PrepOption
    {
        [Tooltip("준비모션 애니 트리거명")]
        public string prepTrigger = "PrepA";
        [Tooltip("준비모션 대기 시간(애니메이션 이벤트로 끝내려면 0으로 두고 아래 플래그 사용)")]
        public float prepHoldTime = 0.6f;
        [Tooltip("랜덤 선택 가중치")]
        public float weight = 1f;
        [Tooltip("준비모션 이후 실행될 공격 종류")]
        public AttackKind attack = AttackKind.Swipe;
        [Tooltip("이 공격의 애니메이션 트리거(선택)")]
        public string attackTrigger = "Atk_Swipe";
    }

    [Header("Prep & Attack")]
    [Tooltip("준비모션 후보들(각 트리거마다 이어지는 공격이 다름)")]
    public PrepOption[] preps;
    [Tooltip("준비모션 종료를 애니메이션 이벤트로 받겠다면 체크")]
    public bool endPrepByAnimEvent = false;

    // 공격별 옵션
    [Header("Attack ▸ Swipe")]
    public Collider2D[] swipeHitboxes;
    public float swipeActiveTime = 0.12f;

    [Header("Attack ▸ Projectile Burst")]
    public GameObject projectilePrefab;
    public Transform[] projectileMuzzles;
    public int projectileCount = 6;
    public float projectileInterval = 0.07f;
    public float projectileSpeed = 12f;

    [Header("Attack ▸ Ground Slam")]
    public Collider2D[] slamHitboxes;
    public float slamActiveTime = 0.15f;
    public float slamShakeAmp = 0.8f, slamShakeDur = 0.2f, slamShakeFreq = 20f;

    enum State { Idle, Charging, Prep, Attacking, Recover }
    State state = State.Idle;

    bool _prepReadyFlag = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void OnEnable()
    {
        StartCoroutine(AIRoutine());
    }

    IEnumerator AIRoutine()
    {
        yield return null;

        while (true)
        {
            // 1) CHARGE
            state = State.Charging;
            if (facePlayer) FaceToPlayer();
            if (!string.IsNullOrEmpty(chargeTrigger)) anim.SetTrigger(chargeTrigger);
            if (!string.IsNullOrEmpty(isChargingBool)) anim.SetBool(isChargingBool, true);
            yield return ChargeTowardPlayer();
            if (!string.IsNullOrEmpty(isChargingBool)) anim.SetBool(isChargingBool, false);

            // 2) PREP
            state = State.Prep;
            var prep = PickPrep();
            if (facePlayer) FaceToPlayer();
            _prepReadyFlag = false;
            if (!string.IsNullOrEmpty(prep.prepTrigger)) anim.SetTrigger(prep.prepTrigger);

            if (endPrepByAnimEvent)
            {
                while (!_prepReadyFlag) yield return null;
            }
            else
            {
                yield return new WaitForSeconds(prep.prepHoldTime);
            }

            // 3) ATTACK
            state = State.Attacking;
            if (!string.IsNullOrEmpty(prep.attackTrigger))
                anim.SetTrigger(prep.attackTrigger);

            switch (prep.attack)
            {
                case AttackKind.Swipe: yield return DoAttack_Swipe(); break;
                case AttackKind.ProjectileBurst: yield return DoAttack_ProjectileBurst(); break;
                case AttackKind.GroundSlam: yield return DoAttack_GroundSlam(); break;
            }

            // 4) RECOVER
            state = State.Recover;
            yield return new WaitForSeconds(0.25f);
            state = State.Idle;
            yield return null;
        }
    }

    // ===== CHARGE (Y 고정) =====
    IEnumerator ChargeTowardPlayer()
    {
        Vector2 start = rb.position;
        float fixedY = start.y;

        // X 방향만 결정 (+1 or -1)
        float sign;
        if (player) sign = (player.position.x - transform.position.x) >= 0f ? 1f : -1f;
        else sign = transform.localScale.x >= 0f ? 1f : -1f;

        Vector2 dir = new Vector2(sign, 0f); // ★ Y 불변

        // 레이캐스트로 벽까지 거리 제한
        float dist = chargeDistance;
        var hit = Physics2D.Raycast(start, dir, chargeDistance, environmentMask);
        if (hit.collider) dist = Mathf.Max(0f, hit.distance - wallSkin);

        Vector2 end = start + dir * dist;

        float t = 0f;
        while (t < chargeTime)
        {
            t += Time.deltaTime;
            float a = chargeTime > 0 ? t / chargeTime : 1f;
            float k = EaseInOutCubic(a);
            float lerpX = Mathf.Lerp(start.x, end.x, k);
            rb.MovePosition(new Vector2(lerpX, fixedY)); // ★ Y 고정 이동
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(new Vector2(end.x, fixedY));
        rb.linearVelocity = Vector2.zero;
    }

    // ===== PREP PICK =====
    int _lastPrepIndex = -1;
    PrepOption PickPrep()
    {
        if (preps == null || preps.Length == 0) return new PrepOption();

        float total = 0f;
        for (int i = 0; i < preps.Length; i++)
        {
            if (i == _lastPrepIndex) continue;
            total += Mathf.Max(0f, preps[i].weight);
        }
        if (total <= 0f)
        {
            _lastPrepIndex = (_lastPrepIndex + 1) % preps.Length;
            return preps[_lastPrepIndex];
        }

        float r = Random.value * total;
        for (int i = 0; i < preps.Length; i++)
        {
            if (i == _lastPrepIndex) continue;
            float w = Mathf.Max(0f, preps[i].weight);
            if (r <= w) { _lastPrepIndex = i; return preps[i]; }
            r -= w;
        }
        _lastPrepIndex = preps.Length - 1;
        return preps[_lastPrepIndex];
    }

    // ===== ATTACKS =====
    IEnumerator DoAttack_Swipe()
    {
        if (swipeHitboxes != null && swipeHitboxes.Length > 0)
        {
            SetCollidersEnabled(swipeHitboxes, true);
            yield return new WaitForSeconds(swipeActiveTime);
            SetCollidersEnabled(swipeHitboxes, false);
        }
        else yield return new WaitForSeconds(0.15f);
    }

    IEnumerator DoAttack_ProjectileBurst()
    {
        if (!projectilePrefab || projectileMuzzles == null || projectileMuzzles.Length == 0)
        {
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        for (int i = 0; i < projectileCount; i++)
        {
            foreach (var mz in projectileMuzzles)
            {
                if (!mz) continue;
                var go = Instantiate(projectilePrefab, mz.position, mz.rotation);
                var rb2 = go.GetComponent<Rigidbody2D>();
                if (rb2)
                {
                    Vector2 dir = player ? (player.position - mz.position).normalized
                                         : (transform.localScale.x >= 0 ? Vector2.right : Vector2.left);
                    rb2.linearVelocity = dir * projectileSpeed;
                }
            }
            yield return new WaitForSeconds(projectileInterval);
        }
    }

    IEnumerator DoAttack_GroundSlam()
    {
        var cam2D = Camera.main ? Camera.main.GetComponent<CameraSimple2D>() : null;
        if (cam2D) cam2D.Shake(slamShakeAmp, slamShakeDur, slamShakeFreq);

        if (slamHitboxes != null && slamHitboxes.Length > 0)
        {
            SetCollidersEnabled(slamHitboxes, true);
            yield return new WaitForSeconds(slamActiveTime);
            SetCollidersEnabled(slamHitboxes, false);
        }
        else yield return new WaitForSeconds(0.2f);
    }

    // ===== Utilities =====
    void SetCollidersEnabled(Collider2D[] arr, bool on)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) arr[i].enabled = on;
    }

    void FaceToPlayer()
    {
        if (!player) return;
        bool right = (player.position.x - transform.position.x) >= 0f;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
        transform.localScale = s;
    }

    static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // ===== Animation Events =====
    public void AnimEvent_PrepReady() { _prepReadyFlag = true; }
    public void AnimEvent_SwipeHitOn() => SetCollidersEnabled(swipeHitboxes, true);
    public void AnimEvent_SwipeHitOff() => SetCollidersEnabled(swipeHitboxes, false);
    public void AnimEvent_SlamHitOn() => SetCollidersEnabled(slamHitboxes, true);
    public void AnimEvent_SlamHitOff() => SetCollidersEnabled(slamHitboxes, false);

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 가로 돌진 라인 미리보기
        float sign;
        if (player) sign = (player.position.x - transform.position.x) >= 0f ? 1f : -1f;
        else sign = transform.localScale.x >= 0f ? 1f : -1f;

        Vector3 dir = new Vector3(sign, 0f, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position,
                        transform.position + dir * chargeDistance);
    }
#endif
}
