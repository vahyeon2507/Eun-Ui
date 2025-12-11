using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무수 스킬:
/// - 일정 시간 동안 플레이어가 받는 피해를 감소(예: 50%)
/// - 감소시킨 총 피해량을, 방패가 사라질 때 주변 적에게 되돌려 대미지로 줌
/// - 대미지 감소 시작 시점은 "애니메이션 이벤트"로 제어
/// - 삭제 시점은 lifeTime(Inspector)로 고정, 그 시점에 대미지 발동
/// - 설치형: 플레이어 위치에 생성 후 그 자리에 고정, Y축으로만 둥둥 뜨는 연출
/// </summary>
[DisallowMultipleComponent]
public class MusuShield : MonoBehaviour
{
    [Header("Owner / Refs")]
    [Tooltip("이 방패와 연결된 플레이어의 체력 컴포넌트")]
    public PlayerHealth ownerHealth;

    [Tooltip("방패의 영역(Trigger 권장). 이 영역 안의 적에게 종료 시 대미지 적용")]
    public BoxCollider2D shieldArea;

    [Tooltip("적 레이어 마스크")]
    public LayerMask enemyMask;

    [Header("Shield Settings")]
    [Tooltip("받는 피해에 곱해질 배율 (0.5 => 50%만 실제로 받음 / 50%는 흡수)")]
    [Range(0f, 1f)]
    public float damageMultiplier = 0.5f;

    [Tooltip("흡수한 피해 총량을 그대로 되돌려줄지 여부")]
    public bool useTotalAbsorbedAsDamage = true;

    [Tooltip("되돌려줄 수 있는 최대 대미지 (0이면 제한 없음)")]
    public int maxReturnDamage = 0;

    [Header("Lifetime")]
    [Tooltip("방패가 유지되는 시간(초). 이 시간이 지나면 자동으로 폭발 + 제거")]
    public float lifeTime = 4f;

    [Tooltip("수명 끝나면 게임 오브젝트를 파괴할지 여부")]
    public bool destroyOnEnd = true;

    [Header("Floating Effect")]
    [Tooltip("활성화 이후 위아래로 뜨는 연출의 높이(진폭)")]
    public float floatAmplitude = 0.2f;

    [Tooltip("떠오르는 속도(주파수)")]
    public float floatFrequency = 2.5f;

    // ===== 내부 상태 =====
    public bool IsActive { get; private set; } = false; // "피해 감소" 활성 여부

    int _totalAbsorbedDamage = 0;
    bool _ended = false;

    // 떠다니기
    bool _floatingEnabled = false;
    Vector3 _floatBasePos;
    float _floatTime = 0f;

    static readonly List<Collider2D> _overlapBuf = new List<Collider2D>(16);
    readonly HashSet<IDamageable> _hitCache = new HashSet<IDamageable>();

    void Awake()
    {
        if (!shieldArea)
            shieldArea = GetComponent<BoxCollider2D>();

        if (shieldArea)
            shieldArea.isTrigger = true;
    }

    void Update()
    {
        if (_floatingEnabled)
        {
            _floatTime += Time.deltaTime;
            float offsetY = Mathf.Sin(_floatTime * floatFrequency) * floatAmplitude;

            // X, Z는 고정, Y만 부드럽게 위아래
            Vector3 pos = _floatBasePos;
            pos.y += offsetY;
            transform.position = pos;
        }
    }

    /// <summary>
    /// 스킬 생성 직후 PlayerSkillController에서 호출.
    /// - ownerHealth 연결
    /// - PlayerHealth에 "피해 감소 방패" 등록
    /// - lifeTime 타이머 시작
    /// </summary>
    public void Initialize(PlayerHealth health)
    {
        ownerHealth = health;

        if (ownerHealth == null)
        {
            Debug.LogWarning("[MusuShield] ownerHealth가 없습니다. 피해 감소가 적용되지 않습니다.");
            return;
        }

        // 플레이어에 붙이지 않고, "설치형"으로 월드에 남겨두는 구조.
        // → transform.SetParent(...) 하지 않는다.

        // PlayerHealth 쪽에 이 방패 등록 (실제 감소 여부는 IsActive로 제어)
        ownerHealth.AttachDamageShield(this);

        _totalAbsorbedDamage = 0;
        IsActive = false;          // 시작 시점에는 아직 비활성
        _floatingEnabled = false;  // 떠다니기 또한 비활성

        // 생성 시점의 위치를 떠다니기 기준점으로 사용
        _floatBasePos = transform.position;
        _floatTime = 0f;

        // 수명 타이머 시작
        if (lifeTime > 0f)
            StartCoroutine(CoLifetime());
    }

    IEnumerator CoLifetime()
    {
        yield return new WaitForSeconds(lifeTime);
        EndShield();
    }

    /// <summary>
    /// PlayerHealth에서 들어오는 "원래 대미지"를 받아
    /// 실제로 들어갈 대미지를 조정하고, 감소한 양을 누적.
    /// (IsActive == true 일 때만 동작)
    /// </summary>
    public int ModifyIncomingDamage(int originalDamage)
    {
        if (!IsActive || originalDamage <= 0)
            return originalDamage;

        // 남는 피해 = original * multiplier
        float f = originalDamage * damageMultiplier;
        int final = Mathf.Max(0, Mathf.CeilToInt(f));

        int prevented = Mathf.Max(0, originalDamage - final);
        if (prevented > 0)
            _totalAbsorbedDamage += prevented;

        return final;
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출.
    /// "이 시점부터 실제로 피해 감소 + 떠다니는 연출 시작"
    /// </summary>
    public void AnimEvent_ShieldActivate()
    {
        if (_ended) return;

        IsActive = true;
        _floatingEnabled = true;
        _floatBasePos = transform.position; // 그 순간 위치를 기준점으로
        _floatTime = 0f;
    }

    /// <summary>
    /// (선택) 혹시 예전 AnimEvent_ShieldEnd를 아직 타임라인에서 쓰고 있다면
    /// 이걸 활성화용으로 재사용해도 된다.
    /// </summary>
    public void AnimEvent_ShieldEnd()
    {
        // 원래는 끝낼 때 쓰던 이벤트였지만,
        // 지금 구조에선 잘못 남아 있으면 버그라서 그냥 경고만 찍고 무시하는 편이 안전하다.
        Debug.LogWarning("[MusuShield] AnimEvent_ShieldEnd는 더 이상 종료용으로 사용되지 않습니다. AnimEvent_ShieldActivate로 교체하세요.");
    }

    /// <summary>
    /// lifeTime이 다 됐을 때 호출되는 종료 처리.
    /// - 피해 감소 비활성
    /// - PlayerHealth에서 방패 분리
    /// - 설치 영역 내의 적에게 되돌려 대미지
    /// - 옵션에 따라 오브젝트 파괴
    /// </summary>
    void EndShield()
    {
        if (_ended) return;
        _ended = true;

        IsActive = false;
        _floatingEnabled = false;

        if (ownerHealth != null)
            ownerHealth.DetachDamageShield(this);

        int damageToDeal = useTotalAbsorbedAsDamage ? _totalAbsorbedDamage : 0;
        if (maxReturnDamage > 0 && damageToDeal > maxReturnDamage)
            damageToDeal = maxReturnDamage;

        if (damageToDeal > 0 && shieldArea != null)
        {
            DealReturnDamage(damageToDeal);
        }

        if (destroyOnEnd)
            Destroy(gameObject);
    }

    void DealReturnDamage(int damage)
    {
        _overlapBuf.Clear();
        _hitCache.Clear();

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(enemyMask);

        int count = shieldArea.Overlap(filter, _overlapBuf);
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuf[i];
            if (!col) continue;

            var dmg = col.GetComponent<IDamageable>()
                   ?? col.GetComponentInParent<IDamageable>()
                   ?? col.GetComponentInChildren<IDamageable>();

            if (dmg == null || _hitCache.Contains(dmg)) continue;

            _hitCache.Add(dmg);
            dmg.TakeDamage(damage);
        }
    }

    void OnDisable()
    {
        // 파괴/비활성화될 때 혹시라도 남아 있으면 연결 해제만 해줌
        if (ownerHealth != null)
            ownerHealth.DetachDamageShield(this);
    }
}
