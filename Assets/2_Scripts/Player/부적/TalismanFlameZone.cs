using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fire + Earth 콤보로 생성되는 불꽃 장판
/// - duration 동안 유지
/// - Enemy 태그 적들에게 점점 강해지는 DoT
/// - 아무 적도 없으면 바로 사라짐(옵션)
/// - 시간이 지날수록 scale 커짐
/// </summary>
[DisallowMultipleComponent]
public class TalismanFlameZone : MonoBehaviour
{
    [Header("Core")]
    [Tooltip("장판이 유지되는 시간(초)")]
    public float duration = 5f;

    [Tooltip("적 1명이 전체 시간 동안 맞았을 때 받는 총 피해량")]
    public int totalDamage = 20;

    [Tooltip("몇 번에 나누어 대미지를 줄지 (후반으로 갈수록 커짐)")]
    public int tickCount = 10;

    [Tooltip("영역 안에 Enemy가 하나도 없으면 바로 파괴할지 여부")]
    public bool destroyWhenNoEnemy = true;

    [Tooltip("대상 태그 (기본 Enemy)")]
    public string enemyTag = "Boss";

    [Header("Scale Over Time")]
    [Tooltip("장판이 끝나갈수록 목표가 되는 최종 XY 스케일")]
    public Vector2 targetScale = new Vector2(2f, 2f);

    [Header("Animation")]
    [Tooltip("불꽃 애니메이션용 Animator (비워두면 자식에서 자동 검색)")]
    public Animator animator;

    [Tooltip("시작 시 발사할 트리거 이름")]
    public string spawnTrigger = "Spawn";

    // ----- 내부 -----
    float _elapsed = 0f;
    float _tickInterval;
    float[] _tickDamages;   // 각 틱마다 줄 피해(후반으로 갈수록 큼)
    int _nextTickIndex = 0;

    Vector3 _initialScale;

    class Target
    {
        public Collider2D col;
        public IDamageable dmg;
        public float pending; // 아직 안 쏜 float 누적 데미지
    }

    readonly List<Target> _targets = new List<Target>();

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();

        _initialScale = transform.localScale;

        PrepareTicks();
    }

    void OnEnable()
    {
        _elapsed = 0f;
        _nextTickIndex = 0;

        if (animator && !string.IsNullOrEmpty(spawnTrigger))
            animator.SetTrigger(spawnTrigger);
    }

    void PrepareTicks()
    {
        tickCount = Mathf.Max(1, tickCount);
        duration = Mathf.Max(0.01f, duration);

        _tickInterval = duration / tickCount;
        _tickDamages = new float[tickCount];

        // 1,2,3,...,N 가중치로 후반에 더 큰 피해
        // 총합 = N(N+1)/2 이 되도록 분배
        float weightSum = tickCount * (tickCount + 1) * 0.5f;

        for (int i = 0; i < tickCount; i++)
        {
            float w = i + 1; // 뒤로 갈수록 더 큰 weight
            _tickDamages[i] = totalDamage * (w / weightSum);
        }
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        // 스케일 보간 (XY만 targetScale로, Z는 유지)
        float t = Mathf.Clamp01(_elapsed / duration);
        float sx = Mathf.Lerp(_initialScale.x, targetScale.x, t);
        float sy = Mathf.Lerp(_initialScale.y, targetScale.y, t);
        transform.localScale = new Vector3(sx, sy, _initialScale.z);

        // 틱 타이밍 체크 (여러 틱이 한 프레임에 몰려도 while로 모두 처리)
        while (_nextTickIndex < tickCount &&
               _elapsed >= (_nextTickIndex + 1) * _tickInterval)
        {
            ApplyTickDamage(_tickDamages[_nextTickIndex]);
            _nextTickIndex++;
        }

        // 누적 데미지 정수 단위로 쏘기
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var target = _targets[i];

            if (target.col == null)
            {
                _targets.RemoveAt(i);
                continue;
            }

            if (target.dmg != null && target.pending >= 1f)
            {
                int intDmg = Mathf.FloorToInt(target.pending);
                target.pending -= intDmg;
                target.dmg.TakeDamage(intDmg);
            }
        }

        // 수명 종료
        if (_elapsed >= duration)
        {
            Destroy(gameObject);
            return;
        }

        // 아무 적도 없으면 바로 삭제
        if (destroyWhenNoEnemy && _targets.Count == 0)
        {
            Destroy(gameObject);
        }
    }

    void ApplyTickDamage(float dmg)
    {
        if (dmg <= 0f) return;

        for (int i = 0; i < _targets.Count; i++)
        {
            var t = _targets[i];
            if (t.dmg != null)
                t.pending += dmg;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(enemyTag) && !other.CompareTag(enemyTag))
            return;

        // 이미 등록된 놈이면 무시
        for (int i = 0; i < _targets.Count; i++)
            if (_targets[i].col == other) return;

        var dmg = other.GetComponent<IDamageable>()
               ?? other.GetComponentInParent<IDamageable>()
               ?? other.GetComponentInChildren<IDamageable>();

        _targets.Add(new Target
        {
            col = other,
            dmg = dmg,
            pending = 0f
        });
    }

    void OnTriggerExit2D(Collider2D other)
    {
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            if (_targets[i].col == other)
            {
                _targets.RemoveAt(i);
                break;
            }
        }
    }
}
