using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TalismanStrongParryZone : MonoBehaviour
{
    // 한 번에 하나만 활성된다고 가정 (여러 개면 마지막으로 들어간 존이 Active)
    public static TalismanStrongParryZone ActiveZone { get; private set; }

    [Header("Boss link")]
    [Tooltip("강패링 성공 시 그로기/받뎀 증가를 줄 보스 HP. 비워두면 자동 탐색")]
    public BossHealth bossHealth;
    public bool autoFindBoss = true;

    [Header("Groggy / Vulnerability")]
    [Tooltip("강패링 성공 시 보스에게 적용할 받뎀 배율")]
    public float vulnerabilityMultiplier = 2.0f;
    [Tooltip("강패링 성공 시 받뎀 증가 지속 시간(초)")]
    public float vulnerabilityDuration = 3.0f;

    // ───────────────────────── Lifetime ─────────────────────────
    [Header("Lifetime (Strong Parry Zone)")]
    [Tooltip("플레이어가 주변에 없어도 유지되는 기본 시간(초)")]
    public float baseLifeTime = 0.5f;

    [Tooltip("플레이어가 계속 있어도 이 시간을 넘으면 무조건 사라짐")]
    public float maxLifeTime = 10f;

    [Tooltip("시간이 지날수록 줄어들 최소 스케일 배율 (0이면 완전 0까지)")]
    [Range(0f, 1f)]
    public float minScaleMultiplier = 0.2f;

    [Tooltip("플레이어 감지 반경")]
    public float playerCheckRadius = 2f;

    [Tooltip("플레이어가 있는 레이어 (지정하면 성능↑, 비워두면 태그 검색)")]
    public LayerMask playerLayer;

    bool _playerInside;

    float _zoneAge = 0f;          // 생성 이후 경과 시간
    float _timeSincePlayer = 0f;  // 마지막으로 플레이어를 본 뒤 누적 시간
    Vector3 _initialScale;        // 시작 스케일

    void Awake()
    {
        if (!bossHealth && autoFindBoss)
            bossHealth = FindObjectOfType<BossHealth>();
    }

    void OnEnable()
    {
        // 새 존이 나오면 이 존이 우선권
        ActiveZone = this;

        _zoneAge = 0f;
        _timeSincePlayer = 0f;
        _initialScale = transform.localScale;
    }

    void OnDestroy()
    {
        if (ActiveZone == this)
            ActiveZone = null;
    }

    void Update()
    {
        // ───── Strong Parry Zone Lifetime ─────
        _zoneAge += Time.deltaTime;

        bool hasPlayer = CheckHasPlayerNearby();

        if (hasPlayer)
        {
            // 플레이어가 근처에 있으면 0.5초 타이머 리셋
            _timeSincePlayer = 0f;
        }
        else
        {
            // 없으면 경과 시간 누적
            _timeSincePlayer += Time.deltaTime;
        }

        // 스케일 감소: maxLifeTime 기준으로 1 → minScaleMultiplier까지 줄어듦
        if (maxLifeTime > 0f)
        {
            float t = Mathf.Clamp01(_zoneAge / maxLifeTime);
            float mul = Mathf.Lerp(1f, minScaleMultiplier, t);
            transform.localScale = _initialScale * mul;
        }

        // 제거 조건:
        // 1) 플레이어가 없이 baseLifeTime 이상 버텼거나
        // 2) 전체 수명 maxLifeTime을 넘겼으면 삭제
        if (_timeSincePlayer >= baseLifeTime || (maxLifeTime > 0f && _zoneAge >= maxLifeTime))
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInside = true;
        ActiveZone = this;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInside = false;
        if (ActiveZone == this)
            ActiveZone = null;
    }

    /// <summary>
    /// 강공격이 플레이어를 맞추려 할 때 호출.
    /// 강패링에 성공하면 true(대미지/히트 소비), 실패하면 false.
    /// </summary>
    public static bool TryHandleStrongAttackHit(PlayerController pc)
    {
        var zone = ActiveZone;
        if (zone == null || !zone._playerInside || pc == null) return false;

        // 플레이어가 실제로 패링 중이 아니면 강패링 실패
        if (!pc.IsParrying) return false;

        zone.OnStrongParrySuccess(pc);
        return true;
    }

    void OnStrongParrySuccess(PlayerController pc)
    {
        // 1) 보스에게 받뎀 증가 / 강그로기 신호
        if (bossHealth != null)
        {
            bossHealth.ApplyVulnerability(vulnerabilityMultiplier, vulnerabilityDuration);

            // 선택: BossHealth에 ApplyStrongParryGroggy() 있으면 호출
            var mi = bossHealth.GetType().GetMethod("ApplyStrongParryGroggy", Type.EmptyTypes);
            if (mi != null)
                mi.Invoke(bossHealth, null);
        }

        // 2) 패링 게이지를 즉시 최대치까지 채우기 (UI 포함)
        var hp = pc.GetComponent<PlayerHealth>() ??
                 pc.GetComponentInParent<PlayerHealth>() ??
                 pc.GetComponentInChildren<PlayerHealth>();

        if (hp != null)
        {
            for (int i = hp.currentParryCount; i < hp.maxParryCount; i++)
                hp.AddParryCount();
        }

        // 3) 플레이어 컨트롤러에 스페셜 즉시 발동 요청
        pc.ForceParrySpecialFromStrongParry();

        // 4) 연출용 사운드
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.playerParrySFX);
    }

    // ───────────────────────── Helpers ─────────────────────────
    bool CheckHasPlayerNearby()
    {
        // 레이어가 지정되어 있으면 OverlapCircle로 한 번에 체크
        if (playerLayer.value != 0)
        {
            var col = Physics2D.OverlapCircle(transform.position, playerCheckRadius, playerLayer);
            return col != null;
        }

        // 레이어 안썼으면 태그 기반 폴백
        var players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (!players[i]) continue;
            float dist = Vector2.Distance(transform.position, players[i].transform.position);
            if (dist <= playerCheckRadius)
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (playerCheckRadius <= 0f) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, playerCheckRadius);
    }
#endif
}
