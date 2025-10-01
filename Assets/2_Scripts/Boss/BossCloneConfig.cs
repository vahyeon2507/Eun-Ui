// BossCloneConfig.cs
using UnityEngine;

/// <summary>
/// 보스 프리팹을 "클론"처럼 쓰기 위한 런타임 설정 래퍼.
/// 보스 스크립트 내용을 바꾸지 않고, 페이즈/분신/보상 등을 비활성화한다.
/// </summary>
[DisallowMultipleComponent]
public class BossCloneConfig : MonoBehaviour
{
    [Header("Clone HP")]
    public int cloneMaxHp = 5;

    [Header("Disable Features")]
    public bool disablePhase2 = true;          // 페이즈 전환 막기
    public bool disableCloneSpawns = true;     // 분신 소환 막기
    public bool disablePhase2Intro = true;     // 2페 인트로/텔레포트 무시
    public bool disableRewardsOnDeath = true;  // 드랍/보상 컴포넌트 끄기



    [Header("Optional clean-up")]
    public bool clearMapRoots = true;          // 맵 루트 참조 제거(보스 맵 제어 방지)
    public bool hideGizmosInPlay = true;       // 플레이 중 기즈모 숨김

    [Header("Reward/Drop components to disable (optional)")]
    public MonoBehaviour[] rewardComponents;   // 드랍, 보상 등 따로 쓰는 컴포넌트가 있으면 여기에 넣기

    void Awake()
    {
        var boss = GetComponent<JangsanbeomBoss>();
        if (boss == null)
        {
            Debug.LogWarning("[BossCloneConfig] JangsanbeomBoss가 없습니다.");
            enabled = false;
            return;
        }

        // 1) HP 다운그레이드
        boss.MaxHealth = Mathf.Max(1, cloneMaxHp);
        boss.CurrentHealth = boss.MaxHealth;

        // 2) 페이즈 전환 봉인
        if (disablePhase2)
        {
            // 체력 임계치를 -1로 내려서 절대 2페이즈 안 가게 함
            boss.Phase2HealthThreshold = -1f;
            // 2페이즈 인트로 관련 부작용 방지
            boss.lockMovementInPhase2 = false;
            boss.enableFakePhase2 = false;
        }

        // 3) 인트로/텔레포트 차단
        if (disablePhase2Intro)
        {
            boss.teleportToCenterOnPhase2 = false;
            boss.phase2IntroInvulnerable = false;
            boss.phase2IntroFallbackDuration = 0f;
        }

        // 4) 분신 소환 차단
        if (disableCloneSpawns)
        {
            boss.maxClones = 0;
            boss.clonePrefab = null;
        }

        // 5) 맵 루트/연출 끊기(있으면)
        if (clearMapRoots)
        {
            boss.Phase1MapRoot = null;
            boss.Phase2IntroMapRoot = null;
            boss.Phase2MapRoot = null;
        }

        // 6) 기즈모 깔끔히
        if (hideGizmosInPlay)
            boss.showAttackGizmos = false;

        // 7) 보상/드랍 컴포넌트 비활성화
        if (disableRewardsOnDeath && rewardComponents != null)
        {
            foreach (var c in rewardComponents)
                if (c) c.enabled = false;
        }
    }
}
