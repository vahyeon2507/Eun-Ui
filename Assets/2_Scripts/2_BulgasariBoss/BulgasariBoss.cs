using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Bulgasari boss – 패링 스페셜 “적중 시” 그로기 확정 버전
/// - 4회 패링 달성 시: '보류' 상태로 무장
/// - 보류 시간 내 첫 피격이 들어오면: 그로기 ON
/// </summary>
[DisallowMultipleComponent]
public class BulgasariBoss : MonoBehaviour, IDamageable
{
    [Header("Refs")]
    public Animator animator;
    public MonoBehaviour healthReceiver; // 체력 컴포넌트(선택)
    public Transform player;

    [Header("Damage Mitigation")]
    [Range(0f, 0.95f)] public float baseDamageMitigation = 0.6f; // 평시 경감
    [Range(0f, 0.95f)] public float groggyMitigation = 0f;       // 그로기 경감(보통 0)

    [Header("Groggy")]
    public float groggyDuration = 4.0f;
    public string animTrigGroggyOn = "GroggyOn";
    public string animTrigGroggyOff = "GroggyOff";
    public bool debugLog;

    [Header("Parry / Special Link")]
    [Tooltip("플레이어(또는 공급자). IParryStreakProvider 구현체거나, 리플렉션으로 스트릭 값을 폴링함.")]
    public MonoBehaviour parryProvider;
    [Tooltip("리플렉션으로 읽을 속성/필드명 우선순위 맨 앞")]
    public string reflectionParryProp = "CurrentParryStreak";
    public int parryNeeded = 4;
    public bool useReflectionPollIfNoProvider = true;
    public float reflectPollInterval = 0.1f;

    [Header("Special Confirm Settings")]
    [Tooltip("스페셜 ‘적중’으로 확인할 수 있는 시간창(초). 4회 패링 달성 순간부터 카운트.")]
    public float specialConfirmWindow = 1.0f;
    [Tooltip("확인 타격(스페셜)에 한해, 그 타격부터 경감 0%(=그로기 경감)로 계산할지 여부.")]
    public bool specialHitGetsFullDamage = true;

    // ===== Internals =====


    [Header("Groggy trigger (by Parry Special hit)")]
    public bool groggyOnParrySpecialHit = true;   // 스페셜 적중으로 그로기?
    public Collider2D chestGroggyCollider;        // 가슴 콜라이더(드래그)

    // 플레이어 스페셜이 '이 보스의 콜라이더'를 때렸을 때 호출됨
    public void OnParrySpecialLanded(Collider2D hitCol)
    {
        if (!groggyOnParrySpecialHit || _groggy) return;

        // 가슴 콜라이더가 지정되어 있으면 '정확히 가슴'일 때만
        if (chestGroggyCollider != null)
        {
            if (hitCol == chestGroggyCollider)
                EnterGroggy();
            return;
        }

        // 폴백: 가슴이 지정 안 됐으면 '보스의 어떤 콜라이더든' 스페셜이면 허용
        // (원하면 여기서 이름 검사나 태그 검사 추가 가능)
        EnterGroggy();
    }

    // ====== 아래는 기존 그로기 루틴 재사용 ======
    void EnterGroggy()
    {
        if (_groggyCo != null) StopCoroutine(_groggyCo);
        _groggyCo = StartCoroutine(GroggyRoutine());
    }
    float _currentMitigation;
    bool _groggy;
    Coroutine _groggyCo;

    int _prevStreak = 0;
    int _lastHandledStreakGroup = -1;
    float _reflectTimer;

    bool _groggyPending = false;
    float _groggyPendingUntil = 0f;

    // health forwarding(선택)
    MethodInfo _miTakeDamage;
    IDamageable _idmg;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Awake()
    {
        _currentMitigation = baseDamageMitigation;

        // 체력 위임: IDamageable > 리플렉션(TakeDamage)
        _idmg = healthReceiver as IDamageable ?? GetComponent<IDamageable>();
        if (healthReceiver && _idmg == null)
        {
            _miTakeDamage = healthReceiver.GetType().GetMethod(
                "TakeDamage",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { typeof(int) }, null);
        }

        // 이벤트 구독 (프로젝트에 IParryStreakProvider가 이미 있음)
        var prov = parryProvider as IParryStreakProvider;
        if (prov != null)
        {
            prov.OnParryStreakChanged += OnParryChanged;
            OnParryChanged(prov.CurrentParryStreak);
        }
    }

    public void TriggerGroggyFromSpecial(float durationOverride = -1f)
{
    if (_groggyCo != null) StopCoroutine(_groggyCo);
    _groggyCo = StartCoroutine(GroggyRoutine_Ext(durationOverride));
}

IEnumerator GroggyRoutine_Ext(float durationOverride)
{
    _groggy = true;
    _currentMitigation = groggyMitigation;
    if (animator && !string.IsNullOrEmpty(animTrigGroggyOn)) animator.SetTrigger(animTrigGroggyOn);
    if (debugLog) Debug.Log("[Bulgasari] GROGGY ON (special)");

    float dur = (durationOverride > 0f) ? durationOverride : groggyDuration;
    yield return new WaitForSeconds(dur);

    _groggy = false;
    _currentMitigation = baseDamageMitigation;
    if (animator && !string.IsNullOrEmpty(animTrigGroggyOff)) animator.SetTrigger(animTrigGroggyOff);
    if (debugLog) Debug.Log("[Bulgasari] GROGGY OFF");
}

    void OnDestroy()
    {
        var prov = parryProvider as IParryStreakProvider;
        if (prov != null) prov.OnParryStreakChanged -= OnParryChanged;
    }

    void Update()
    {
        // 보류 타임아웃
        if (_groggyPending && Time.time > _groggyPendingUntil)
        {
            if (debugLog) Debug.Log("[Bulgasari] Groggy pending expired");
            _groggyPending = false;
        }

        // (옵션) 리플렉션 폴링
        if (useReflectionPollIfNoProvider && !(parryProvider is IParryStreakProvider) && parryProvider != null)
        {
            _reflectTimer -= Time.deltaTime;
            if (_reflectTimer <= 0f)
            {
                _reflectTimer = reflectPollInterval;
                int val = TryGetInt(parryProvider, reflectionParryProp,
                    "ParryStreak", "ParryCount", "CurrentParry", "ComboParry", "parrySuccessCount");
                if (val >= 0) OnParryChanged(val);
            }
        }
    }

    // ====== IDamageable ======
    public void TakeDamage(int amount)
    {
        // 스페셜 적중 보류 중이면, 이 타격을 스페셜로 간주(윈도우 내 첫 히트)
        bool confirmThisHit = _groggyPending && Time.time <= _groggyPendingUntil;

        float mitigation = _currentMitigation;
        if (_groggy) mitigation = groggyMitigation;
        else if (confirmThisHit && specialHitGetsFullDamage) mitigation = groggyMitigation;

        int final = Mathf.Max(0, Mathf.CeilToInt(amount * (1f - mitigation)));

        if (_idmg != null && !(ReferenceEquals(_idmg, this)))
        {
            _idmg.TakeDamage(final);
        }
        else if (healthReceiver != null && _miTakeDamage != null)
        {
            _miTakeDamage.Invoke(healthReceiver, new object[] { final });
        }
        else
        {
            if (debugLog) Debug.Log($"[Bulgasari] TakeDamage {final}");
        }

        // 타격으로 그로기 확정
        if (confirmThisHit)
        {
            _groggyPending = false;
            EnterGroggy();
        }
    }

    // ====== Parry 스트릭 수신 ======
    void OnParryChanged(int streak)
    {
        if (parryNeeded <= 0) parryNeeded = 4;

        // 하향(끊김) 감지 시 그룹 리셋
        if (streak < _prevStreak) _lastHandledStreakGroup = -1;
        _prevStreak = streak;

        bool thresholdHit = streak > 0 && (streak % parryNeeded == 0);
        if (!thresholdHit) return;

        int group = streak / parryNeeded; // 4→1, 8→2 ...
        if (group == _lastHandledStreakGroup) return;

        _lastHandledStreakGroup = group;

        // 이전: 바로 그로기 → 변경: ‘보류’만 켜고, 적중으로 확정
        ArmGroggyPending();
    }

    void ArmGroggyPending()
    {
        _groggyPending = true;
        _groggyPendingUntil = Time.time + Mathf.Max(0.05f, specialConfirmWindow);
        if (debugLog) Debug.Log($"[Bulgasari] Groggy pending armed for {specialConfirmWindow:0.00}s");
    }


    IEnumerator GroggyRoutine()
    {
        _groggy = true;
        _currentMitigation = groggyMitigation;
        if (animator && !string.IsNullOrEmpty(animTrigGroggyOn)) animator.SetTrigger(animTrigGroggyOn);
        if (debugLog) Debug.Log("[Bulgasari] GROGGY ON");

        yield return new WaitForSeconds(groggyDuration);

        _groggy = false;
        _currentMitigation = baseDamageMitigation;
        if (animator && !string.IsNullOrEmpty(animTrigGroggyOff)) animator.SetTrigger(animTrigGroggyOff);
        if (debugLog) Debug.Log("[Bulgasari] GROGGY OFF");
    }

    // ====== Utils ======
    int TryGetInt(object obj, params string[] names)
    {
        if (obj == null) return -1;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
        }
        return -1;
    }
}
