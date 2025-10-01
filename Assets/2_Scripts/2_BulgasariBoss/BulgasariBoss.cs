using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;



[DisallowMultipleComponent]
public class BulgasariBoss : MonoBehaviour, IDamageable
{
    int _prevStreak = 0; // <== 추가

    [Header("Refs")]
    public Animator animator;
    public MonoBehaviour healthReceiver; // 내부 보스 체력 컴포넌트(선택)
    public Transform player;

    [Header("Damage Mitigation")]
    [Range(0f, 0.95f)] public float baseDamageMitigation = 0.6f; // 60% 경감(=40%만 받음)
    [Tooltip("그로기 중에는 경감이 이 값으로 강제됨(보통 0)")]
    [Range(0f, 0.95f)] public float groggyMitigation = 0f;

    [Header("Groggy")]
    public float groggyDuration = 4.0f;
    public string animTrigGroggyOn = "GroggyOn";
    public string animTrigGroggyOff = "GroggyOff";
    public bool debugLog;

    [Header("Parry Streak")]
    public MonoBehaviour parryProvider; // IParryStreakProvider 구현체를 기대
    public string reflectionParryProp = "CurrentParryStreak"; // 폴백용 이름
    public int parryNeeded = 4;
    public bool useReflectionPollIfNoProvider = true;
    public float reflectPollInterval = 0.1f;

    float _currentMitigation;      // 0~0.95
    bool _groggy;
    int _lastHandledStreakGroup = -1; // (streak/parryNeeded)의 그룹 인덱스
    Coroutine _groggyCo;
    float _reflectTimer;

    // health forwarding(선택)
    MethodInfo _miTakeDamage; IDamageable _idmg;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Awake()
    {
        _currentMitigation = baseDamageMitigation;

        // 피해 전달 경로 확보: IDamageable > 리플렉션(TakeDamage)
        _idmg = healthReceiver as IDamageable ?? GetComponent<IDamageable>();
        if (healthReceiver && _idmg == null)
        {
            _miTakeDamage = healthReceiver.GetType().GetMethod(
                "TakeDamage",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(int) },
                null
            );
        }

        // 패링 streak 이벤트 구독 시도
        var prov = parryProvider as IParryStreakProvider;
        if (prov != null)
        {
            prov.OnParryStreakChanged += OnParryChanged;
            OnParryChanged(prov.CurrentParryStreak);
        }
    }

    void OnDestroy()
    {
        var prov = parryProvider as IParryStreakProvider;
        if (prov != null) prov.OnParryStreakChanged -= OnParryChanged;
    }

    void Update()
    {
        // 폴백: 리플렉션 폴링
        if (useReflectionPollIfNoProvider && !(parryProvider is IParryStreakProvider) && parryProvider != null)
        {
            _reflectTimer -= Time.deltaTime;
            if (_reflectTimer <= 0f)
            {
                _reflectTimer = reflectPollInterval;
                int val = TryGetInt(parryProvider, reflectionParryProp, "ParryStreak", "ParryCount", "CurrentParry", "ComboParry", "parrySuccessCount");
                if (val >= 0) OnParryChanged(val);
            }
        }
    }

    // ====== IDamageable ======
    public void TakeDamage(int amount)
    {
        int final = Mathf.Max(0, Mathf.CeilToInt(amount * (1f - _currentMitigation)));
        if (_groggy && debugLog) Debug.Log($"[Bulgasari] GROGGY! incoming:{amount} → final:{final}");
        if (_idmg != null && !(ReferenceEquals(_idmg, this)))
        {
            _idmg.TakeDamage(final);
            return;
        }
        if (healthReceiver != null && _miTakeDamage != null)
        {
            _miTakeDamage.Invoke(healthReceiver, new object[] { final });
            return;
        }
        // 최후 폴백: 그냥 로그로 대체
        if (debugLog) Debug.Log($"[Bulgasari] TakeDamage {final}");
    }

    // ====== Parry 연동 ======
    void OnParryChanged(int streak)
    {
        if (parryNeeded <= 0) parryNeeded = 4;

        // ↓↓↓ 새로 추가: 감소(=리셋) 감지 → 그룹 초기화
        if (streak < _prevStreak)
            _lastHandledStreakGroup = -1;
        _prevStreak = streak;

        bool thresholdHit = streak > 0 && (streak % parryNeeded == 0);
        if (!thresholdHit) return;

        int group = streak / parryNeeded; // 4→1, 8→2 ...
        if (group != _lastHandledStreakGroup)
        {
            _lastHandledStreakGroup = group;
            EnterGroggy();
        }
    }

    void EnterGroggy()
    {
        if (_groggyCo != null) StopCoroutine(_groggyCo);
        _groggyCo = StartCoroutine(GroggyRoutine());
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
            if (p != null && (p.PropertyType == typeof(int))) return (int)p.GetValue(obj);
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && (f.FieldType == typeof(int))) return (int)f.GetValue(obj);
        }
        return -1;
    }
}
