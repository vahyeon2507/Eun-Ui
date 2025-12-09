using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;   // Tilemap 색 변경

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BulgasariFloorHeatSystem : MonoBehaviour
{
    [Header("Heat Level")]
    public int maxHeatLevel = 6;
    [Range(0, 6)]
    public int currentHeatLevel = 0;
    public int heatPerAttack = 1;

    [Header("Color")]
    public Color coolColor = Color.white;
    public Color hotColor = Color.red;

    [Tooltip("스프라이트 기반 바닥 오브젝트(없으면 자동 검색)")]
    public SpriteRenderer[] floorSprites;

    [Tooltip("타일맵 기반 바닥(없으면 자동 검색)")]
    public Tilemap[] floorTilemaps;

    [Header("Max Heat Flicker")]
    public float flickerSpeed = 2f;
    [Range(0f, 1f)]
    public float flickerMinRatio = 0.5f;

    [Header("Player Damage At Max Heat")]
    [Tooltip("바닥 위에 있을 때 틱당 대미지")]
    public int touchDamage = 1;

    [Tooltip("바닥 위에 계속 있을 때 대미지 간격(초)")]
    public float damageInterval = 0.6f;

    [Header("Heat Wave FX (아지랑이)")]
    public GameObject heatWavePrefab;
    public Collider2D floorArea;
    public Vector2 heatWaveOffset = new Vector2(0f, 0.3f);
    public Vector2 heatWaveIntervalRange = new Vector2(0.6f, 1.4f);

    [Header("Debug")]
    public bool debugLog = false;

    // ───────── 내부 상태 ─────────
    bool _playerInside = false;
    IDamageable _playerDamage;
    float _damageTimer = 0f;

    int _playerOverlapCount = 0;   // ★ 플레이어 콜라이더가 몇 개나 겹쳐있는지

    Coroutine _heatWaveCR;
    Color _currentColor;

    void Reset()
    {
        if (floorSprites == null || floorSprites.Length == 0)
            floorSprites = GetComponentsInChildren<SpriteRenderer>();

        if (floorTilemaps == null || floorTilemaps.Length == 0)
            floorTilemaps = GetComponentsInChildren<Tilemap>();

        if (!floorArea)
            floorArea = GetComponent<Collider2D>();

        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        if (floorSprites == null || floorSprites.Length == 0)
            floorSprites = GetComponentsInChildren<SpriteRenderer>();

        if (floorTilemaps == null || floorTilemaps.Length == 0)
            floorTilemaps = GetComponentsInChildren<Tilemap>();

        if (!floorArea)
            floorArea = GetComponent<Collider2D>();

        ApplyColor(coolColor);
    }

    void Update()
    {
        UpdateColor();
        UpdatePlayerDamage();
    }

    // ==== 불가사리 공격에서 호출 ====
    public void AddHeat(int amount)
    {
        if (amount <= 0) return;

        int prev = currentHeatLevel;
        currentHeatLevel = Mathf.Clamp(currentHeatLevel + amount, 0, maxHeatLevel);

        if (debugLog)
            Debug.Log($"[Heat] AddHeat {amount}, now {currentHeatLevel}/{maxHeatLevel}", this);

        if (prev < maxHeatLevel && currentHeatLevel >= maxHeatLevel)
            StartMaxHeatEffects();
    }

    public void ResetHeat()
    {
        currentHeatLevel = 0;
        StopMaxHeatEffects();
        ApplyColor(coolColor);
    }

    // ==== 색상 업데이트 ====
    void UpdateColor()
    {
        if (maxHeatLevel <= 0)
        {
            ApplyColor(coolColor);
            return;
        }

        if (currentHeatLevel < maxHeatLevel)
        {
            float ratio = Mathf.Clamp01((float)currentHeatLevel / maxHeatLevel);
            Color c = Color.Lerp(coolColor, hotColor, ratio);
            ApplyColor(c);
        }
        else
        {
            // 최대 스택: 중간~최대 사이 깜빡임
            float ping = Mathf.PingPong(Time.time * flickerSpeed, 1f);
            float ratio = Mathf.Lerp(flickerMinRatio, 1f, ping);
            ratio = Mathf.Clamp01(ratio);

            Color c = Color.Lerp(coolColor, hotColor, ratio);
            ApplyColor(c);
        }
    }

    void ApplyColor(Color c)
    {
        if (c == _currentColor) return;
        _currentColor = c;

        if (floorSprites != null)
        {
            for (int i = 0; i < floorSprites.Length; i++)
                if (floorSprites[i] != null)
                    floorSprites[i].color = c;
        }

        if (floorTilemaps != null)
        {
            for (int i = 0; i < floorTilemaps.Length; i++)
                if (floorTilemaps[i] != null)
                    floorTilemaps[i].color = c;
        }
    }

    // ==== 플레이어 DOT ====
    void UpdatePlayerDamage()
    {
        if (currentHeatLevel < maxHeatLevel) return;
        if (!_playerInside || _playerDamage == null) return;
        if (touchDamage <= 0) return;

        // damageInterval 간격으로 계속 때리기
        if (damageInterval <= 0f)
        {
            _playerDamage.TakeDamage(touchDamage);
            return;
        }

        _damageTimer -= Time.deltaTime;
        if (_damageTimer <= 0f)
        {
            _damageTimer = damageInterval;
            _playerDamage.TakeDamage(touchDamage);

            if (debugLog)
                Debug.Log("[Heat] Tick damage to PLAYER", this);
        }
    }

    // ==== Player 필터링 ====
    bool IsPlayerObject(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Player"))
                return true;
            t = t.parent;
        }
        return false;
    }

    Transform GetPlayerRoot(Transform t)
    {
        Transform root = t;
        while (root != null)
        {
            if (root.CompareTag("Player"))
                return root;
            root = root.parent;
        }
        return null;
    }

    IDamageable FindPlayerDamageable(Collider2D col)
    {
        Transform root = GetPlayerRoot(col.transform);
        if (!root) return null;
        return root.GetComponent<IDamageable>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerObject(other.transform)) return;

        var dmg = FindPlayerDamageable(other);
        if (dmg == null) return;

        _playerOverlapCount++;
        if (_playerOverlapCount == 1)
        {
            // 첫 겹침 시작
            _playerInside = true;
            _playerDamage = dmg;
            _damageTimer = 0f;   // 들어오자마자 한 번 맞게
        }

        if (debugLog)
            Debug.Log($"[Heat] Player ENTER (count={_playerOverlapCount}) via {other.name}", this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerObject(other.transform)) return;

        var dmg = FindPlayerDamageable(other);
        if (dmg == null) return;

        // 같은 플레이어에 대한 콜라이더만 카운트 감소
        if (_playerDamage != null && _playerDamage != dmg)
            return;

        _playerOverlapCount = Mathf.Max(0, _playerOverlapCount - 1);

        if (_playerOverlapCount == 0)
        {
            _playerInside = false;
            _playerDamage = null;
        }

        if (debugLog)
            Debug.Log($"[Heat] Player EXIT (count={_playerOverlapCount}) via {other.name}", this);
    }

    // ==== 최대 스택 효과 ====
    void StartMaxHeatEffects()
    {
        if (debugLog)
            Debug.Log("[Heat] Reached MAX heat", this);

        if (heatWavePrefab != null && _heatWaveCR == null)
            _heatWaveCR = StartCoroutine(CoHeatWaveLoop());
    }

    void StopMaxHeatEffects()
    {
        if (_heatWaveCR != null)
        {
            StopCoroutine(_heatWaveCR);
            _heatWaveCR = null;
        }
    }

    IEnumerator CoHeatWaveLoop()
    {
        while (currentHeatLevel >= maxHeatLevel)
        {
            SpawnHeatWave();

            float min = Mathf.Min(heatWaveIntervalRange.x, heatWaveIntervalRange.y);
            float max = Mathf.Max(heatWaveIntervalRange.x, heatWaveIntervalRange.y);
            float wait = Random.Range(min, max);
            yield return new WaitForSeconds(wait);
        }
        _heatWaveCR = null;
    }

    void SpawnHeatWave()
    {
        if (!heatWavePrefab) return;

        Vector3 pos = transform.position + (Vector3)heatWaveOffset;

        Collider2D area = floorArea ? floorArea : GetComponent<Collider2D>();
        if (area != null)
        {
            var b = area.bounds;
            float x = Random.Range(b.min.x, b.max.x);
            float y = b.max.y + heatWaveOffset.y;
            pos = new Vector3(x, y, transform.position.z);
        }

        Instantiate(heatWavePrefab, pos, Quaternion.identity);
    }
}
