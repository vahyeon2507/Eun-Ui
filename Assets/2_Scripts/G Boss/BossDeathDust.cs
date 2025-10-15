using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 사망 연출: 자신의 콜라이더/스프라이트 영역 안에 파티클을 촘촘히 스폰해
/// 가루가 되어 사라지는 느낌을 만든다. 기존 로직(포탈 생성, 점수, 드랍 등)과 충돌 X.
/// </summary>
[DisallowMultipleComponent]
public class BossDeathDust : MonoBehaviour
{
    [Header("Particle")]
    [Tooltip("한 점에서 터질 파티클 프리팹 (ParticleSystem 포함된 프리팹)")]
    public GameObject dustParticlePrefab;

    [Tooltip("스폰할 점(지점) 개수 — 클수록 촘촘")]
    [Min(1)] public int spawnPoints = 24;

    [Tooltip("한 점에서 동시에 여러 개 Emit하고 싶다면 증가 (프리팹에 따라 무시될 수 있음)")]
    [Min(1)] public int burstsPerPoint = 1;

    [Tooltip("각 점의 약간의 랜덤 산포(월드 단위). 0이면 정확히 점에서만)")]
    [Min(0f)] public float jitter = 0.05f;

    [Tooltip("파티클을 스폰한 뒤, 원본 보스 렌더러를 숨길지")]
    public bool hideBossRenderers = true;

    [Tooltip("스폰 직후 보스 콜라이더/스크립트를 비활성화할지(추가 히트, 넉백 등 방지)")]
    public bool disableBossComponents = true;

    [Header("Sample Area")]
    [Tooltip("영역 샘플에 사용할 콜라이더(들). 비우면 자식의 Collider2D들 자동 사용 → 없으면 SpriteRenderer.bounds 사용.")]
    public Collider2D[] sampleAreas;
    [Tooltip("스프라이트의 Bounds를 사용할 때 확장/축소 계수(1=그대로)")]
    public Vector2 boundsScale = Vector2.one;

    [Header("Cleanup")]
    [Tooltip("스폰 후 원본 보스를 제거할지")]
    public bool destroyBossAfter = true;
    [Tooltip("보스를 제거하기 전 대기시간(초). 애니/사운드와 어울리도록 살짝 여유)")]
    public float destroyDelay = 1.2f;

    // 내부 캐시
    SpriteRenderer[] _renderers;
    Collider2D[] _colliders;

    void Awake()
    {
        if (dustParticlePrefab == null)
            Debug.LogWarning("[BossDeathDust] dustParticlePrefab 이 비어있습니다.", this);

        // 샘플 영역 지정이 비어있으면 자식 Colliders 자동수집
        if (sampleAreas == null || sampleAreas.Length == 0)
            _colliders = GetComponentsInChildren<Collider2D>(includeInactive: false);
        else
            _colliders = sampleAreas;

        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
    }

    /// <summary>외부(보스 사망 시)에서 한 줄로 호출</summary>
    public void Play()
    {
        if (dustParticlePrefab == null) return;

        // 1) 샘플 영역 준비
        var areas = ValidAreas();

        // 2) 포인트 뽑기 → 해당 위치에 파티클 프리팹 생성
        for (int i = 0; i < spawnPoints; i++)
        {
            Vector3 p = SamplePoint(areas);
            if (jitter > 0f)
                p += (Vector3)(Random.insideUnitCircle * jitter);

            // 프리팹 생성
            var go = Instantiate(dustParticlePrefab, p, Quaternion.identity);
            // 파티클은 프리팹 설정대로 재생될 것. (ParticleSystem 있는 경우 자동 Play)
            // 한 점에 여러 번 방출하고 싶으면 여기에 옵션 추가 가능:
            // var ps = go.GetComponent<ParticleSystem>(); if (ps) for(...) ps.Emit(emitCount);
            // 하지만 대부분 프리팹 자체 버스트/서브에미터로 충분.
            Destroy(go, 5f); // 안전 차원: 뒤처리(프리팹 자체에서 StopAction=Destroy면 생략 가능)
        }

        // 3) 원본 비주얼/상호작용 정리
        if (hideBossRenderers) HideRenderers(_renderers, true);
        if (disableBossComponents) DisableCollidersAndAI();

        // 4) 마지막 청소
        if (destroyBossAfter) Destroy(gameObject, destroyDelay);
    }

    // ===== 샘플/보조 =====

    List<Collider2D> ValidAreas()
    {
        var list = new List<Collider2D>();
        if (_colliders != null)
        {
            foreach (var c in _colliders)
            {
                if (c && c.enabled) list.Add(c);
            }
        }
        return list;
    }

    Vector3 SamplePoint(List<Collider2D> areas)
    {
        // 1) 콜라이더가 있으면 그 내부에서
        if (areas != null && areas.Count > 0)
        {
            var hb = areas[Random.Range(0, areas.Count)];
            return RandomPointInsideCollider(hb);
        }

        // 2) 없다면 스프라이트 Bounds 폴백
        var sr = (_renderers != null && _renderers.Length > 0) ? _renderers[0] : null;
        if (sr != null)
        {
            Bounds b = sr.bounds;
            Vector2 c = b.center;
            Vector2 ext = Vector2.Scale((Vector2)b.extents, boundsScale);
            return new Vector3(
                Random.Range(c.x - ext.x, c.x + ext.x),
                Random.Range(c.y - ext.y, c.y + ext.y),
                sr.transform.position.z
            );
        }
        // 최후 폴백: 자신의 위치
        return transform.position;
    }

    static Vector3 RandomPointInsideCollider(Collider2D hb)
    {
        var b = hb.bounds;
        // 시도 횟수 제한(무한루프 방지)
        for (int i = 0; i < 12; i++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float y = Random.Range(b.min.y, b.max.y);
            var p = new Vector2(x, y);
            if (hb.OverlapPoint(p)) return p;
        }
        return hb.ClosestPoint(b.center);
    }

    void HideRenderers(SpriteRenderer[] srs, bool hide)
    {
        if (srs == null) return;
        foreach (var r in srs) if (r) r.enabled = !hide;
    }

    void DisableCollidersAndAI()
    {
        // 콜라이더/데미저블/AI 스크립트 비활성화(프로젝트에 맞춰 확장 가능)
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;

        var dmgables = GetComponentsInChildren<MonoBehaviour>();
        foreach (var mb in dmgables)
        {
            // 필요 시 보스 AI/공격 스크립트 이름으로 필터링
            // if (mb is BossAI || mb is EnemyAttack) mb.enabled = false;
            // 안전하게 전부 끄고 싶지 않다면 주석 유지하고 필요한 것만 끄자.
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.25f);
        if (sampleAreas != null && sampleAreas.Length > 0)
        {
            foreach (var c in sampleAreas)
            {
                if (!c) continue;
                Gizmos.DrawCube(c.bounds.center, c.bounds.size);
            }
        }
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Bounds b = sr.bounds;
                Gizmos.DrawCube(b.center, Vector3.Scale(b.size, new Vector3(boundsScale.x, boundsScale.y, 1f)));
            }
        }
    }
#endif
}
