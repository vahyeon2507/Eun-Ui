// BossCloneConfig.cs
using UnityEngine;

/// <summary>
/// ���� �������� "Ŭ��"ó�� ���� ���� ��Ÿ�� ���� ����.
/// ���� ��ũ��Ʈ ������ �ٲ��� �ʰ�, ������/�н�/���� ���� ��Ȱ��ȭ�Ѵ�.
/// </summary>
[DisallowMultipleComponent]
public class BossCloneConfig : MonoBehaviour
{
    [Header("Clone HP")]
    public int cloneMaxHp = 5;

    [Header("Disable Features")]
    public bool disablePhase2 = true;          // ������ ��ȯ ����
    public bool disableCloneSpawns = true;     // �н� ��ȯ ����
    public bool disablePhase2Intro = true;     // 2�� ��Ʈ��/�ڷ���Ʈ ����
    public bool disableRewardsOnDeath = true;  // ���/���� ������Ʈ ����



    [Header("Optional clean-up")]
    public bool clearMapRoots = true;          // �� ��Ʈ ���� ����(���� �� ���� ����)
    public bool hideGizmosInPlay = true;       // �÷��� �� ����� ����

    [Header("Reward/Drop components to disable (optional)")]
    public MonoBehaviour[] rewardComponents;   // ���, ���� �� ���� ���� ������Ʈ�� ������ ���⿡ �ֱ�

    void Awake()
    {
        var boss = GetComponent<JangsanbeomBoss>();
        if (boss == null)
        {
            Debug.LogWarning("[BossCloneConfig] JangsanbeomBoss�� �����ϴ�.");
            enabled = false;
            return;
        }

        // 1) HP �ٿ�׷��̵�
        boss.MaxHealth = Mathf.Max(1, cloneMaxHp);
        boss.CurrentHealth = boss.MaxHealth;

        // 2) ������ ��ȯ ����
        if (disablePhase2)
        {
            // ü�� �Ӱ�ġ�� -1�� ������ ���� 2������ �� ���� ��
            boss.Phase2HealthThreshold = -1f;
            // 2������ ��Ʈ�� ���� ���ۿ� ����
            boss.lockMovementInPhase2 = false;
            boss.enableFakePhase2 = false;
        }

        // 3) ��Ʈ��/�ڷ���Ʈ ����
        if (disablePhase2Intro)
        {
            boss.teleportToCenterOnPhase2 = false;
            boss.phase2IntroInvulnerable = false;
            boss.phase2IntroFallbackDuration = 0f;
        }

        // 4) �н� ��ȯ ����
        if (disableCloneSpawns)
        {
            boss.maxClones = 0;
            boss.clonePrefab = null;
        }

        // 5) �� ��Ʈ/���� ����(������)
        if (clearMapRoots)
        {
            boss.Phase1MapRoot = null;
            boss.Phase2IntroMapRoot = null;
            boss.Phase2MapRoot = null;
        }

        // 6) ����� �����
        if (hideGizmosInPlay)
            boss.showAttackGizmos = false;

        // 7) ����/��� ������Ʈ ��Ȱ��ȭ
        if (disableRewardsOnDeath && rewardComponents != null)
        {
            foreach (var c in rewardComponents)
                if (c) c.enabled = false;
        }
    }
}
