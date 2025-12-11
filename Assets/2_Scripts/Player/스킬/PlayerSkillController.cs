using UnityEngine;

public class PlayerSkillController : MonoBehaviour
{
    [Header("참조 (선택 사항)")]
    [Tooltip("같은 오브젝트에 붙은 PlayerController. 비워두면 자동으로 찾으려고 시도.")]
    public PlayerController playerController;

    [Tooltip("같은 오브젝트에 붙은 PlayerHealth. 비워두면 자동으로 찾음.")]
    public PlayerHealth playerHealth;

    [Header("전체 스킬 온/오프")]
    [Tooltip("튜토리얼 등에서 전체 스킬 잠그고 싶을 때 사용")]
    public bool skillsEnabled = true;

    // =======================
    // 무구 스킬
    // =======================
    [Header("무구 스킬 설정")]
    [Tooltip("스킬을 배웠는지 여부. 다른 시스템에서 이 값을 true로 켜주면 됨.")]
    public bool muguUnlocked = false;

    [Tooltip("무구 스킬 입력 키")]
    public KeyCode muguKey = KeyCode.Alpha1;

    [Tooltip("소환할 무구 프리팹 (애니메이션 + MuguSkill 스크립트 포함)")]
    public GameObject muguPrefab;

    // === 스폰 포인트 ===
    [Header("무구 스폰 포인트")]
    [Tooltip("오른쪽을 보고 있을 때 사용할 스폰 포인트")]
    public Transform muguSpawnRight;

    [Tooltip("왼쪽을 보고 있을 때 사용할 스폰 포인트")]
    public Transform muguSpawnLeft;

    [Tooltip("양쪽 공용 또는 폴백용 스폰 포인트 (둘 다 비어있을 때 사용)")]
    public Transform muguSpawnPoint;

    [Tooltip("무구 스킬 쿨타임(초)")]
    public float muguCooldown = 4f;
    float _muguCooldownTimer = 0f;

    // =======================
    // 무수 스킬
    // =======================
    [Header("무수 스킬 설정")]
    [Tooltip("스킬을 배웠는지 여부")]
    public bool musuUnlocked = false;

    [Tooltip("무수 스킬 입력 키")]
    public KeyCode musuKey = KeyCode.Alpha2;

    [Tooltip("소환할 무수 프리팹 (애니메이션 + MusuShield 스크립트 포함)")]
    public GameObject musuPrefab;

    [Tooltip("무수를 설치할 기준 위치. 비워두면 플레이어 위치에서 스폰")]
    public Transform musuSpawnPoint;

    [Tooltip("무수 스킬 쿨타임(초)")]
    public float musuCooldown = 8f;
    float _musuCooldownTimer = 0f;


    void Awake()
    {
        if (!playerController)
            playerController = GetComponent<PlayerController>();

        if (!playerHealth)
            playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        // 튜토리얼에서 입력 막고 싶을 때
        if (!skillsEnabled) return;
        if (PlayerController.TutorialInputLocked) return;

        if (_muguCooldownTimer > 0f)
            _muguCooldownTimer -= Time.deltaTime;

        if (_musuCooldownTimer > 0f)
            _musuCooldownTimer -= Time.deltaTime;

        HandleMuguInput();
        HandleMusuInput();
    }

    // =======================
    // 무구 로직
    // =======================
    void HandleMuguInput()
    {
        // 잠금 / 세팅 안 됨 / 쿨타임 / 키 입력 체크
        if (!muguUnlocked) return;
        if (!muguPrefab) return;
        if (_muguCooldownTimer > 0f) return;
        if (!Input.GetKeyDown(muguKey)) return;

        // 공격/패링/대시 중에는 사용 막고 싶으면 여길 사용
        if (playerController != null)
        {
            if (playerController.IsDashing || playerController.IsParrying)
                return;
        }

        SpawnMugu();
    }

    // 외부에서 무구 배우기용
    public void UnlockMugu() => muguUnlocked = true;

    public bool CanUseMugu()
    {
        return muguUnlocked && _muguCooldownTimer <= 0f && muguPrefab != null;
    }

    void SpawnMugu()
    {
        // 1) 방향 판단 (localScale.x 기준)
        bool facingRight;
        if (playerController != null)
            facingRight = playerController.transform.localScale.x > 0f;
        else
            facingRight = transform.localScale.x > 0f;

        // 2) 스폰 포인트 선택
        Transform spawnT = null;

        if (facingRight)
        {
            // 오른쪽 보고 있을 때: Right 우선 → 공용 → 자기 위치
            if (muguSpawnRight != null) spawnT = muguSpawnRight;
            else if (muguSpawnPoint != null) spawnT = muguSpawnPoint;
        }
        else
        {
            // 왼쪽 보고 있을 때: Left 우선 → 공용 → 자기 위치
            if (muguSpawnLeft != null) spawnT = muguSpawnLeft;
            else if (muguSpawnPoint != null) spawnT = muguSpawnPoint;
        }

        Vector3 spawnPos = spawnT ? spawnT.position : transform.position;

        // 3) 회전 (좌/우 뒤집기)
        Quaternion rot = facingRight
            ? Quaternion.identity
            : Quaternion.Euler(0f, 180f, 0f);

        // 4) 프리팹 소환
        GameObject obj = Instantiate(muguPrefab, spawnPos, rot);

        // 5) 스킬 소유자 설정(필요하면 쓰라고 남겨둠)
        var mugu = obj.GetComponent<MuguSkill>();
        if (mugu != null)
        {
            mugu.owner = transform;
        }

        // 6) 쿨타임 시작
        _muguCooldownTimer = muguCooldown;
    }

    // =======================
    // 무수 로직
    // =======================
    void HandleMusuInput()
    {
        if (!musuUnlocked) return;
        if (!musuPrefab) return;
        if (_musuCooldownTimer > 0f) return;
        if (!Input.GetKeyDown(musuKey)) return;

        if (playerController != null)
        {
            if (playerController.IsDashing || playerController.IsParrying)
                return;
        }

        SpawnMusu();
    }

    public void UnlockMusu() => musuUnlocked = true;

    public bool CanUseMusu()
    {
        return musuUnlocked && _musuCooldownTimer <= 0f && musuPrefab != null;
    }

    void SpawnMusu()
    {
        // ★ 인스펙터에서 받은 스폰 포인트 우선 사용, 없으면 플레이어 위치
        Vector3 spawnPos = (musuSpawnPoint ? musuSpawnPoint.position : transform.position);

        GameObject obj = Instantiate(musuPrefab, spawnPos, Quaternion.identity);

        var shield = obj.GetComponent<MusuShield>();
        if (shield != null && playerHealth != null)
        {
            shield.Initialize(playerHealth);
        }
        else
        {
            Debug.LogWarning("[PlayerSkillController] MusuShield 또는 PlayerHealth가 없어 Initialize 실패");
        }

        _musuCooldownTimer = musuCooldown;
    }
}
