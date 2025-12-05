using System.Collections;
using UnityEngine;
using UnityEngine.Serialization; // ★ 직렬화 매핑

[DisallowMultipleComponent]
public class BossTuboController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PlayerController playerCtrl;
    public Transform playerPointOverride;
    public BossHealth bossHealth;
    public Animator animator;

    public bool IsParryWindowActive => _parryWatchActive;


    [Header("Prefabs")]
    public GameObject telegraphPrefab;
    public GameObject pointAttackPrefab;

    // === Tutorial/Event hooks ===
    public event System.Action onAttackWindowOpen;
    public event System.Action onAttackWindowClose;
    public event System.Action onParrySuccessDirect;

    [Header("Attack Timing")]
    public float telegraphTime = 0.6f;
    public float attackCooldown = 1.2f;

    [Header("Animations")]
    public string attackTrigger = "Attack";

    // ===== Groggy =====
    [Header("Groggy")]
    public float groggyDuration = 2.0f;
    public float parryAttributionGrace = 0.25f;
    [Range(0.1f, 10f)] public float groggyDamageMultiplier = 2f;

    [Tooltip("Groggy 입장: Animator Bool 이름. 비워두면 아래 'enterGroggyTrigger(레거시)'를 사용.")]
    public string groggyBool = "isGroggy";

    [Tooltip("Groggy 회복: Animator Trigger 이름(선택).")]
    public string groggyRecoverTrigger = "Recover";

    [Tooltip("★레거시 매핑: 예전 'groggyTrigger'(트리거로 입장) 값이 자동 이행됩니다.")]
    [FormerlySerializedAs("groggyTrigger")]
    public string enterGroggyTrigger = ""; // 비어있으면 미사용

    [Header("Disable During Groggy (optional)")]
    [Tooltip("그로기 동안 꺼둘 스크립트들(공격 스폰 등). 끝나면 자동 재활성화.")]
    public MonoBehaviour[] disableDuringGroggy;

    [Header("Player layer")]
    public LayerMask playerLayer;

    [Header("Debug")]
    public bool debugLog = false;

    // ---- internals ----
    int _lastParryCount = 0;
    bool _parryWatchActive = false;
    float _parryWatchTimer = 0f;
    bool _inLoop = false;
    Coroutine _groggyCR;

    bool _attacksEnabled = true;
    public bool AttacksEnabled => _attacksEnabled;

    void Reset()
    {
        bossHealth = GetComponent<BossHealth>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    public void NotifyParrySuccessDirect()
    {
        if (debugLog) Debug.Log($"[Tubo] Direct parry success. window={_parryWatchActive}");
        if (_parryWatchActive) TriggerGroggy();
        onParrySuccessDirect?.Invoke();
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
        if (_parryWatchActive)
        {
            _parryWatchTimer -= Time.deltaTime;
            if (_parryWatchTimer <= 0f)
            {
                _parryWatchActive = false;
                if (debugLog) Debug.Log("[Tubo] Parry window CLOSED");
            }
        }
    }

    IEnumerator MainLoop()
    {
        _inLoop = true;
        while (true)
        {
            // 그로기 중엔 대기
            while (!_attacksEnabled) yield return null;

            // 1) 목표 포인트
            Vector3 targetPos = GetPlayerPoint();

            // 2) 텔레그래프
            if (telegraphPrefab)
            {
                var tele = Instantiate(telegraphPrefab, targetPos, Quaternion.identity);
                Destroy(tele, telegraphTime + 0.2f);
            }

            // 3) 공격 애니
            if (animator && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            // 4) 텔레그래프 대기
            yield return new WaitForSeconds(telegraphTime);

            // 그로기 들어갔으면 스폰 스킵
            if (!_attacksEnabled) continue;

            // 5) 실제 공격 스폰
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

            // 6) 쿨다운
            float cd = attackCooldown;
            while (cd > 0f)
            {
                if (!_attacksEnabled) break;
                cd -= Time.deltaTime;
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

    // ---- 공격 창 알림(포인트 어택에서 호출) ----
    public void NotifyAttackWindow(bool active, float extraGrace = 0f)
    {
        Debug.Log($"[Tubo] AttackWindow {(active ? "OPEN" : "CLOSE")} dur+={extraGrace}");
        if (active) OpenParryAttributionWindow(extraGrace);
        else CloseParryAttributionWindow();


        if (active) onAttackWindowOpen?.Invoke();
        else onAttackWindowClose?.Invoke();
    }

    void OpenParryAttributionWindow(float extra = 0f)
    {
        _parryWatchActive = true;
        _parryWatchTimer = Mathf.Max(_parryWatchTimer, extra);
        if (debugLog) Debug.Log($"[Tubo] Parry window OPEN ({_parryWatchTimer:F2}s)");
    }

    void CloseParryAttributionWindow()
    {
        _parryWatchActive = false;
        _parryWatchTimer = 0f;
    }

    // ---- 패링 콜백 ----
    void OnPlayerParryStreakChanged(int newCount)
    {
        if (debugLog) Debug.Log($"[Tubo] ParryStreak {newCount} (last {_lastParryCount}) / window={_parryWatchActive}");
        if (_parryWatchActive && newCount > _lastParryCount)
        {
            // ★ 여기서도 튜토리얼 이벤트 쏴줘야 함
            onParrySuccessDirect?.Invoke();

            TriggerGroggy();
            CloseParryAttributionWindow();
        }
        _lastParryCount = newCount;
    }

    // ---- 그로기 ----
    void TriggerGroggy()
    {
        if (_groggyCR != null) return;
        _groggyCR = StartCoroutine(CoGroggy());
    }

    IEnumerator CoGroggy()
    {
        if (debugLog) Debug.Log("[Tubo] Enter GROGGY");

        SetAttacksEnabled(false);

        // 입장: Bool 우선, 비어있으면 레거시 트리거 사용
        if (animator)
        {
            if (!string.IsNullOrEmpty(groggyBool)) animator.SetBool(groggyBool, true);
            else if (!string.IsNullOrEmpty(enterGroggyTrigger)) animator.SetTrigger(enterGroggyTrigger);
        }

        if (bossHealth) bossHealth.ApplyVulnerability(groggyDamageMultiplier, groggyDuration);

        float t = 0f;
        while (t < groggyDuration) { t += Time.deltaTime; yield return null; }

        // 퇴장
        if (animator)
        {
            if (!string.IsNullOrEmpty(groggyBool)) animator.SetBool(groggyBool, false);
            if (!string.IsNullOrEmpty(groggyRecoverTrigger)) animator.SetTrigger(groggyRecoverTrigger);
        }

        SetAttacksEnabled(true);
        if (debugLog) Debug.Log("[Tubo] Exit GROGGY");
        _groggyCR = null;
    }

    void SetAttacksEnabled(bool on)
    {
        _attacksEnabled = on;

        // 지정한 스크립트 토글
        if (disableDuringGroggy != null)
        {
            for (int i = 0; i < disableDuringGroggy.Length; i++)
                if (disableDuringGroggy[i]) disableDuringGroggy[i].enabled = on;
        }
    }

    // ===== Legacy Parry API (호환)
    [Header("Parry Compatibility")]
    public bool groggyOnlyWhenAttacking = true;
    public void OnParried() { HandleLegacyParried(); }
    public void OnParried(Collider2D _) { HandleLegacyParried(); }
    public void OnParried(GameObject _) { HandleLegacyParried(); }
    public void OnParried(Transform _) { HandleLegacyParried(); }

    void HandleLegacyParried()
    {
        if (groggyOnlyWhenAttacking && !_parryWatchActive) return;
        TriggerGroggy();
    }
}
