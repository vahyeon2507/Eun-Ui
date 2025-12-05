using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossTuboController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;                         // 플레이어 트랜스폼
    public PlayerController playerCtrl;              // 자동 찾기 가능
    public Transform playerPointOverride;            // 비우면 player.transform 사용
    public BossHealth bossHealth;                    // ApplyVulnerability 사용
    public Animator animator;                        // "Attack", "Groggy" 트리거 사용

    [Header("Prefabs")]
    public GameObject telegraphPrefab;               // 경고 프리팹(비주얼만)
    public GameObject pointAttackPrefab;             // 실제 공격 프리팹(아래 스크립트 필요)

    [Header("Attack Timing")]
    public float telegraphTime = 0.6f;               // 경고 표시 시간
    public float attackCooldown = 1.2f;              // 다음 공격까지 대기

    [Header("Animations")]
    public string attackTrigger = "Attack";
    public string groggyTrigger = "Groggy";

    [Header("Groggy")]
    public float groggyDuration = 2.0f;              // 그로기 지속
    public float parryAttributionGrace = 0.25f;      // 카운트 증가 직후까지 인정하는 여유
    [Range(0.1f, 10f)] public float groggyDamageMultiplier = 2f; // 그로기 중 받뎀 배수
    [Tooltip("Groggy 루프 유지용 Bool (선택). 예: isGroggy")]
    public string groggyBool = "";
    [Tooltip("Groggy 종료 트리거(Groggy→Idle). 비우면 안 씀.")]
    public string groggyRecoverTrigger = "Recover";

    [Header("Disable During Groggy (optional)")]
    [Tooltip("그로기 동안 꺼둘 스크립트들. 끝나면 다시 켬.")]
    public List<MonoBehaviour> disableDuringGroggy = new();

    [Header("Player layer")]
    public LayerMask playerLayer;

    // ---- internals ----
    int _lastParryCount = 0;
    bool _parryWatchActive = false;
    float _parryWatchTimer = 0f;
    bool _inLoop = false;
    Coroutine _groggyCR;

    // 공격/텔레그래프 관리
    readonly HashSet<ITuboAttack> _liveAttacks = new();
    GameObject _lastTelegraph;
    public bool IsGroggy { get; private set; } = false;
    public bool AttacksEnabled => !IsGroggy;      // 공격 스크립트들이 참고

    void Reset()
    {
        bossHealth = GetComponent<BossHealth>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!playerCtrl && player) playerCtrl = player.GetComponent<PlayerController>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!bossHealth) bossHealth = GetComponent<BossHealth>();
    }

    void OnEnable()
    {
        // 플레이어 이벤트 구독
        if (!playerCtrl && player) playerCtrl = player.GetComponent<PlayerController>();
        if (playerCtrl != null)
        {
            playerCtrl.OnParryStreakChanged += OnPlayerParryStreakChanged;
            _lastParryCount = playerCtrl.CurrentParryStreak;
        }

        if (!_inLoop) StartCoroutine(MainLoop());
    }

    void OnDisable()
    {
        if (playerCtrl != null)
            playerCtrl.OnParryStreakChanged -= OnPlayerParryStreakChanged;
    }

    void Update()
    {
        // 패링 귀속창 타이머
        if (_parryWatchActive)
        {
            _parryWatchTimer -= Time.deltaTime;
            if (_parryWatchTimer <= 0f) _parryWatchActive = false;
        }
    }

    IEnumerator MainLoop()
    {
        _inLoop = true;
        while (true)
        {
            // 그로기 중이면 아무 것도 안 한다.
            if (IsGroggy) { yield return null; continue; }

            // 1) 목표 포인트
            Vector3 targetPos = GetPlayerPoint();

            // 2) 텔레그래프
            if (telegraphPrefab)
            {
                _lastTelegraph = Instantiate(telegraphPrefab, targetPos, Quaternion.identity);
                Destroy(_lastTelegraph, telegraphTime + 0.2f);
            }

            // 3) 공격 애니(선택)
            if (animator && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            // 4) 텔레그래프 대기
            float tWait = 0f;
            while (tWait < telegraphTime)
            {
                // 대기 중 그로기 들어가면 즉시 중단
                if (IsGroggy) break;
                tWait += Time.deltaTime;
                yield return null;
            }
            if (IsGroggy) continue; // 그로기면 공격 스킵

            // 5) 실제 공격 + 패링 인정창 on
            if (pointAttackPrefab)
            {
                var atk = Instantiate(pointAttackPrefab, targetPos, Quaternion.identity);
                var tuboAtk = atk.GetComponent<TuboPointAttack>();
                if (tuboAtk)
                {
                    tuboAtk.owner = this;
                    tuboAtk.playerLayer = playerLayer;
                }
            }
            OpenParryAttributionWindow(0.35f + parryAttributionGrace); // 활성 + 여유

            // 6) 쿨다운 (그로기 돌입하면 즉시 끊김)
            float cool = 0f;
            while (cool < attackCooldown)
            {
                if (IsGroggy) break;
                cool += Time.deltaTime;
                yield return null;
            }
        }
    }

    Vector3 GetPlayerPoint()
    {
        if (playerPointOverride) return playerPointOverride.position;
        if (player) return player.position;
        return transform.position;
    }

    // 공격 프리팹에서 호출(활성/비활성 구간 정밀 제어 원하면 사용)
    public void NotifyAttackWindow(bool active, float extraGrace = 0f)
    {
        if (active) OpenParryAttributionWindow(extraGrace);
        else CloseParryAttributionWindow();
    }

    void OpenParryAttributionWindow(float extra = 0f)
    {
        _parryWatchActive = true;
        _parryWatchTimer = Mathf.Max(_parryWatchTimer, extra);
    }

    void CloseParryAttributionWindow()
    {
        _parryWatchActive = false;
        _parryWatchTimer = 0f;
    }

    // ===== 공격 등록/해제 (공격 스크립트에서 OnEnable/OnDisable에 호출) =====
    public void RegisterAttack(ITuboAttack atk) { if (atk != null) _liveAttacks.Add(atk); }
    public void UnregisterAttack(ITuboAttack atk) { if (atk != null) _liveAttacks.Remove(atk); }

    void CancelActiveAttacks()
    {
        if (_lastTelegraph) { Destroy(_lastTelegraph); _lastTelegraph = null; }
        if (_liveAttacks.Count == 0) return;
        foreach (var a in _liveAttacks) { try { a?.CancelAttack(); } catch { } }
        _liveAttacks.Clear();
    }

    // ===== 플레이어 패링 카운트 이벤트 콜백 =====
    void OnPlayerParryStreakChanged(int newCount)
    {
        // 증가한 순간 + 우리가 막 때리던 타이밍이라면 = 우리 공격이 패링당함
        if (_parryWatchActive && newCount > _lastParryCount)
        {
            TriggerGroggy();
            CloseParryAttributionWindow();
        }
        _lastParryCount = newCount;
    }

    public void TriggerGroggy()
    {
        if (_groggyCR != null) return;
        _groggyCR = StartCoroutine(CoGroggy());
    }

    IEnumerator CoGroggy()
    {
        IsGroggy = true;

        // 0) 공격/경고 즉시 캔슬
        CancelActiveAttacks();
        CloseParryAttributionWindow();

        // 1) 그로기 애니 시작
        if (animator)
        {
            if (!string.IsNullOrEmpty(groggyBool)) animator.SetBool(groggyBool, true);
            if (!string.IsNullOrEmpty(groggyTrigger)) animator.SetTrigger(groggyTrigger);
        }

        // 2) 받뎀 배수 적용
        if (bossHealth) bossHealth.ApplyVulnerability(groggyDamageMultiplier, groggyDuration);

        // 3) 지정 스크립트 비활성
        ToggleScripts(false);

        // 4) 정확히 groggyDuration 유지
        float t = 0f;
        while (t < groggyDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // 5) 복구
        if (animator)
        {
            if (!string.IsNullOrEmpty(groggyBool)) animator.SetBool(groggyBool, false);
            if (!string.IsNullOrEmpty(groggyRecoverTrigger)) animator.SetTrigger(groggyRecoverTrigger);
        }

        ToggleScripts(true);

        IsGroggy = false;
        _groggyCR = null;
    }

    void ToggleScripts(bool enable)
    {
        if (disableDuringGroggy == null) return;
        foreach (var mb in disableDuringGroggy)
        {
            if (!mb) continue;
            // 자기 자신(BossTuboController)은 절대 끄지 않음
            if (ReferenceEquals(mb, this)) continue;
            mb.enabled = enable;
        }
    }

    // ===== Legacy Parry API (호환용) =====
    [Header("Parry Compatibility")]
    public bool groggyOnlyWhenAttacking = true; // 공격 중일 때만 그로기 처리할지

    public void OnParried() { HandleLegacyParried(null); }
    public void OnParried(Collider2D _) { HandleLegacyParried(null); }
    public void OnParried(GameObject _) { HandleLegacyParried(null); }
    public void OnParried(Transform _) { HandleLegacyParried(null); }

    void HandleLegacyParried(object _)
    {
        // 예전 스크립트들이 던지는 '패링 성공' 신호를 여기서 받아서 처리
        if (groggyOnlyWhenAttacking && !_parryWatchActive) return; // 원하면 공격 타이밍에서만
        TriggerGroggy();
    }
}
