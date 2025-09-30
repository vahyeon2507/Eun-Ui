// JangsanbeomOneShotClone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomOneShotClone : MonoBehaviour
{
    // ============ 인스펙터 노출 설정 ============
    [Header("Inspector Controls")]
    [Tooltip("켜면 보스가 넘겨준 값으로 덮어씁니다. 끄면 아래 인스펙터 값을 그대로 사용합니다.")]
    [SerializeField] private bool overrideByOwner = true;

    [Min(0f), Tooltip("등장 페이드 인 시간")]
    [SerializeField] private float inspectorFadeIn = 0.12f;

    [Min(0f), Tooltip("공격 전 아주 짧은 텔레그래프(딜레이)")]
    [SerializeField] private float inspectorPreDelay = 0.06f;

    [Min(0f), Tooltip("공격 후 제거까지 대기")]
    [SerializeField] private float inspectorDespawnDelay = 0.05f;

    [Min(0f), Tooltip("사라질 때 페이드 아웃 시간")]
    [SerializeField] private float inspectorFadeOut = 0.18f;

    [Tooltip("스폰 시 원보스의 바라보는 방향을 상속할지")]
    [SerializeField] private bool inspectorInheritFlip = true;

    // ============ 내부 ============
    JangsanbeomBoss _owner;      // 원 보스(리스트/콜백 관리)
    JangsanbeomBoss _self;       // 이 분신 자신의 보스 스크립트
    Transform _player;

    // 실제 사용될 런타임 값(Init 시 확정)
    float _preDelay, _fadeIn, _fadeOut, _despawnDelay;
    bool _inheritFlip;

    SpriteRenderer _sr;
    Color _baseColor;
    bool _running;

    /// <summary>
    /// 보스가 생성 직후 호출. overrideByOwner=true면 전달 인자를, false면 인스펙터 값을 사용.
    /// </summary>
    public void Init(
        JangsanbeomBoss owner,
        JangsanbeomBoss selfBoss,
        Transform player,
        float preDelay,
        float fadeIn,
        float fadeOut,
        float despawnDelay,
        bool inheritFlip
    )
    {
        _owner = owner;
        _self = selfBoss;
        _player = player;

        // 어떤 값을 쓸지 결정
        if (overrideByOwner)
        {
            _preDelay = Mathf.Max(0f, preDelay);
            _fadeIn = Mathf.Max(0f, fadeIn);
            _fadeOut = Mathf.Max(0f, fadeOut);
            _despawnDelay = Mathf.Max(0f, despawnDelay);
            _inheritFlip = inheritFlip;
        }
        else
        {
            _preDelay = inspectorPreDelay;
            _fadeIn = inspectorFadeIn;
            _fadeOut = inspectorFadeOut;
            _despawnDelay = inspectorDespawnDelay;
            _inheritFlip = inspectorInheritFlip;
        }

        // 시각 세팅
        _sr = (_self != null && _self.spriteRenderer != null)
            ? _self.spriteRenderer
            : GetComponentInChildren<SpriteRenderer>();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) { _baseColor = _sr.color; SetAlpha(0f); }

        // AI/이동 차단(걷지 않게)
        if (_self != null)
        {
            _self.StopAllCoroutines(); // AIBehavior 등
            _self.Anim_SetMoveLock(1);
            _self.Anim_SetFlipLock(1);
        }

        if (_inheritFlip && _owner != null && _self != null)
            _self.FlipTo(_owner.facingRight);

        if (!_running) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        _running = true;

        // 1) 페이드 인
        if (_sr && _fadeIn > 0f) yield return FadeTo(1f, _fadeIn);
        else SetAlpha(1f);

        // 2) 아주 짧은 텔레그래프(선택)
        if (_preDelay > 0f) yield return new WaitForSeconds(_preDelay);

        // 3) 공격 1회(원본 설정을 그대로 따름)
        var atk = PickAttackForClone();
        bool applyDamage = DecideShouldDealDamage(atk);

        if (_self != null) _self.PerformAttackOnceFrom(atk, transform, applyDamage);
        else _owner?.PerformAttackOnceFrom(atk, transform, applyDamage);

        // 4) 잔여 대기 → 페이드 아웃
        if (_despawnDelay > 0f) yield return new WaitForSeconds(_despawnDelay);
        if (_sr && _fadeOut > 0f) yield return FadeTo(0f, _fadeOut);
        else SetAlpha(0f);

        // 5) 정리
        if (_owner != null) _owner.OnCloneDied(_self?.GetComponent<JangsanbeomClone>());
        Destroy(gameObject);
    }

    // === 공격 풀 선택(2페 기준 + 2페 허용된 1페 기술) ===
    JangsanbeomBoss.AttackData PickAttackForClone()
    {
        var pool = new List<JangsanbeomBoss.AttackData>(8);
        var src = _self != null ? _self : _owner;
        if (src != null)
        {
            foreach (var a in src.attacksPhase2) if (a != null && a.AllowInPhase2) pool.Add(a);
            foreach (var a in src.attacksPhase1) if (a != null && a.AllowInPhase2) pool.Add(a);
        }
        if (pool.Count == 0) pool.Add(new JangsanbeomBoss.AttackData());
        return pool[0]; // 필요 시 Random.Range로 변경 가능
    }

    bool DecideShouldDealDamage(JangsanbeomBoss.AttackData atk)
    {
        var src = _self != null ? _self : _owner;
        if (src == null) return true;

        bool isFake = src.enableFakePhase2 &&
                      (Random.value < src.fakeChancePhase2 || (atk != null && atk.DefaultFake));
        if (!isFake) return true;
        return (atk != null) ? atk.Phase2_FakeDealsDamage : false;
    }

    // === 페이드 유틸 ===
    void SetAlpha(float a)
    {
        if (_sr) _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, Mathf.Clamp01(a));
    }
    IEnumerator FadeTo(float target, float t)
    {
        if (_sr == null || t <= 0f) { SetAlpha(target); yield break; }
        float start = _sr.color.a;
        float time = 0f;
        while (time < t)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / t);
            SetAlpha(Mathf.Lerp(start, target, k));
            yield return null;
        }
        SetAlpha(target);
    }
}
