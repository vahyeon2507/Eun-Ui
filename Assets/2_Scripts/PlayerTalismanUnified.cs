using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // ← 조합 이벤트용

public enum TalismanType { Fire, Earth, Water, Metal, Wood }

[DisallowMultipleComponent]
public class PlayerTalismanUnified : MonoBehaviour
{
    [Header("Refs")]
    public PlayerController player;
    public Transform throwOrigin;

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.E;
    public KeyCode cycleKey = KeyCode.Q;
    public int fireMouseButton = 0;

    [Header("Charges")]
    public int maxCharges = 2;
    public float rechargeInterval = 15f;
    public int currentCharges = 2;

    [Header("Projectile Visuals (Optional)")]
    public GameObject[] perElementProjectileVisual = new GameObject[5];
    public GameObject genericProjectileVisual;

    [Header("Field Prefabs (장판)")]
    public GameObject[] perElementField = new GameObject[5];
    public GameObject genericField;

    [Header("Field Animation")]
    [Tooltip("장판 프리팹에 Animator가 있으면 트리거를 쏴서 애니메이션을 재생한다.")]
    public bool fieldUseAnimator = true;
    [Tooltip("요소별 트리거가 비어있으면 이 공통 트리거를 사용")]
    public string fieldAnimatorTrigger = "Spawn";
    [Tooltip("요소별 트리거(선택). 공백이면 공통 트리거 사용")]
    public string[] fieldAnimatorTriggerPerElement = new string[5];

    [Header("Tuning")]
    public float projectileSpeed = 12f;
    public float projectileRadius = 0.15f;
    public float projectileMaxRange = 9f;
    public int projectileDamage = 1;
    public float fireCooldown = 0.25f;

    [Header("Projectile Rotation")]
    public float projectileZRight = -90f;
    public float projectileZLeft = 90f;
    public float projectileSpinXSpeed = 720f;
    public bool spinXReverseOnLeft = true;

    [Header("Layers")]
    public LayerMask enemyLayer;
    public LayerMask terrainLayer;
    public LayerMask playerLayer;

    [Header("Safety")]
    public string noCollisionLayerName = "Ignore Raycast";
    public bool forceNoCollisionLayer = true;

    [Header("Field Combo (hook only)")]
    [Tooltip("장판 겹침 판정에 여유 반경을 더한다.")]
    public float comboCheckExtraRadius = 0.1f;
    [Tooltip("장판-장판 조합이 발생했을 때 호출되는 이벤트 (요소A, 요소B, 위치). 구현은 나중에!")]
    public UnityEvent<TalismanType, TalismanType, Vector2> onFieldCombo;

    [Header("Debug")]
    public bool logEvents = false;

    public bool talismanMode { get; private set; } = false;
    public TalismanType current = TalismanType.Fire;

    float _cooldownTimer = 0f;
    float _rechargeTimer = 0f;
    int _noCollisionLayer = -1;

    class P
    {
        public TalismanType element;
        public Vector2 pos, vel, start;
        public int damage;
        public float radius, maxDist;
        public GameObject visual;
        public float rotZ;       // -90/90
        public float spinX;
        public float spinXSpeed;
    }
    readonly List<P> _projs = new List<P>(16);

    // 활성 장판 관리
    class F
    {
        public TalismanType element;
        public Vector2 pos;
        public float radius;
        public GameObject go;
    }
    readonly List<F> _fields = new List<F>(8);

    void Reset()
    {
        player = GetComponent<PlayerController>();
    }

    void Start()
    {
        if (_noCollisionLayer < 0 && !string.IsNullOrEmpty(noCollisionLayerName))
            _noCollisionLayer = LayerMask.NameToLayer(noCollisionLayerName);

        if (player == null) player = GetComponent<PlayerController>();
        if (throwOrigin == null && player != null && player.attackPoint != null) throwOrigin = player.attackPoint;

        currentCharges = Mathf.Clamp(currentCharges, 0, maxCharges);
    }

    void Update()
    {
        // 충전
        if (currentCharges < maxCharges)
        {
            _rechargeTimer += Time.deltaTime;
            if (_rechargeTimer >= rechargeInterval)
            {
                _rechargeTimer = 0f;
                currentCharges++;
                if (logEvents) Debug.Log($"[Talisman] +1 charge → {currentCharges}/{maxCharges}");
            }
        }
        else _rechargeTimer = 0f;

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        // 토글
        if (Input.GetKeyDown(toggleKey))
        {
            talismanMode = !talismanMode;
            if (player != null) player.ExternalRangedOverride = talismanMode;
            if (logEvents) Debug.Log($"[Talisman] Mode {(talismanMode ? "ON" : "OFF")}");
        }

        if (talismanMode)
        {
            if (Input.GetKeyDown(cycleKey))
            {
                current = (TalismanType)(((int)current + 1) % 5);
                if (logEvents) Debug.Log($"[Talisman] Selected {current}");
            }

            if (Input.GetMouseButtonDown(fireMouseButton))
                TryFire();
        }

        UpdateProjectiles();
        PruneDeadFields(); // 파괴된 장판 정리
    }

    void TryFire()
    {
        if (_cooldownTimer > 0f) return;
        if (currentCharges <= 0)
        {
            if (logEvents) Debug.Log("[Talisman] No charges.");
            return;
        }

        Vector2 origin = (throwOrigin != null ? (Vector2)throwOrigin.position : (Vector2)transform.position);
        origin.y = throwOrigin != null ? origin.y : transform.position.y;
        float dirSign = (transform.localScale.x >= 0f ? 1f : -1f);
        Vector2 vel = new Vector2(dirSign * projectileSpeed, 0f);

        float zRot = (dirSign > 0f) ? projectileZRight : projectileZLeft;
        float xSpinSpeed = projectileSpinXSpeed * (spinXReverseOnLeft && dirSign < 0f ? -1f : 1f);

        var p = new P
        {
            element = current,
            pos = origin,
            start = origin,
            vel = vel,
            damage = projectileDamage,
            radius = projectileRadius,
            maxDist = projectileMaxRange,
            visual = SpawnProjectileVisual(current, origin, dirSign, zRot),
            rotZ = zRot,
            spinX = 0f,
            spinXSpeed = xSpinSpeed
        };
        _projs.Add(p);

        currentCharges--;
        _cooldownTimer = fireCooldown;
        
        // 부적 발사 사운드 초고속 재생
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.talismanFireSFX);
            
        if (logEvents) Debug.Log($"[Talisman] Fire {current} (remain {currentCharges})");
    }

    GameObject SpawnProjectileVisual(TalismanType t, Vector2 pos, float dirSign, float zRot)
    {
        GameObject template = null; int i = (int)t;
        if (perElementProjectileVisual != null && i < perElementProjectileVisual.Length)
            template = perElementProjectileVisual[i];
        if (template == null) template = genericProjectileVisual;
        if (template == null) return null;

        var go = Instantiate(template, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
        if (forceNoCollisionLayer && _noCollisionLayer >= 0) go.layer = _noCollisionLayer;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = (dirSign < 0f);

        var rb = go.GetComponent<Rigidbody2D>(); if (rb) rb.simulated = false;
        foreach (var col in go.GetComponents<Collider2D>()) col.enabled = false;

        go.transform.rotation = Quaternion.Euler(0f, 0f, zRot);
        return go;
    }

    GameObject GetFieldPrefab(TalismanType t)
    {
        int i = (int)t;
        if (perElementField != null && i < perElementField.Length && perElementField[i] != null)
            return perElementField[i];
        return genericField;
    }

    void UpdateProjectiles()
    {
        for (int idx = _projs.Count - 1; idx >= 0; idx--)
        {
            var p = _projs[idx];

            Vector2 nextPos = p.pos + p.vel * Time.deltaTime;
            Vector2 delta = nextPos - p.pos;
            float dist = delta.magnitude;
            Vector2 dir = dist > 0f ? delta / dist : Vector2.right;

            // 지형
            if (dist > 0f)
            {
                var hitT = Physics2D.CircleCast(p.pos, p.radius, dir, dist, terrainLayer);
                if (hitT.collider != null && !IsPlayerCollider(hitT.collider))
                {
                    ImpactAndSpawnField(hitT.point, p.element);
                    DestroyVisual(p.visual);
                    _projs.RemoveAt(idx);
                    continue;
                }
            }

            // 적
            if (dist > 0f)
            {
                var hitE = Physics2D.CircleCast(p.pos, p.radius, dir, dist, enemyLayer);
                if (hitE.collider != null && !IsPlayerCollider(hitE.collider))
                {
                    var dmg = hitE.collider.GetComponent<IDamageable>()
                              ?? hitE.collider.GetComponentInParent<IDamageable>()
                              ?? hitE.collider.GetComponentInChildren<IDamageable>();
                    if (dmg != null) dmg.TakeDamage(p.damage);

                    // 부적 충돌 사운드 초고속 재생
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFXInstant(AudioManager.Instance.talismanImpactSFX);

                    ImpactAndSpawnField(hitE.point, p.element);
                    DestroyVisual(p.visual);
                    _projs.RemoveAt(idx);
                    continue;
                }
            }

            // 사거리
            if (Vector2.Distance(p.start, nextPos) >= p.maxDist)
            {
                ImpactAndSpawnField(nextPos, p.element);
                DestroyVisual(p.visual);
                _projs.RemoveAt(idx);
                continue;
            }

            // 이동 + 회전
            p.pos = nextPos;
            if (p.visual != null)
            {
                p.visual.transform.position = new Vector3(nextPos.x, nextPos.y, 0f);
                p.spinX += p.spinXSpeed * Time.deltaTime;
                p.visual.transform.rotation = Quaternion.Euler(p.spinX, 0f, p.rotZ);
            }
        }
    }

    bool IsPlayerCollider(Collider2D col)
    {
        if (col == null) return false;
        if (((1 << col.gameObject.layer) & playerLayer) != 0) return true;
        if (player != null && (col.transform == player.transform || col.transform.IsChildOf(player.transform)))
            return true;
        return false;
    }

    void DestroyVisual(GameObject v)
    {
        if (v != null) Destroy(v);
    }

    void ImpactAndSpawnField(Vector2 pos, TalismanType element)
    {
        var fieldPrefab = GetFieldPrefab(element);
        if (fieldPrefab == null) return;

        var go = Instantiate(fieldPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
        if (forceNoCollisionLayer && _noCollisionLayer >= 0) go.layer = _noCollisionLayer;
        foreach (var col in go.GetComponentsInChildren<Collider2D>()) col.isTrigger = true;

        // 애니메이션 재생
        if (fieldUseAnimator)
        {
            var an = go.GetComponentInChildren<Animator>();
            if (an != null)
            {
                string trig = fieldAnimatorTrigger;
                int i = (int)element;
                if (fieldAnimatorTriggerPerElement != null &&
                    i < fieldAnimatorTriggerPerElement.Length &&
                    !string.IsNullOrEmpty(fieldAnimatorTriggerPerElement[i]))
                {
                    trig = fieldAnimatorTriggerPerElement[i];
                }
                if (!string.IsNullOrEmpty(trig)) an.SetTrigger(trig);
            }
        }

        // 장판 데이터 채우기(있다면)
        var tf = go.GetComponent<TalismanField>();
        if (tf != null)
        {
            tf.element = element;
            if (tf.radius <= 0f) tf.radius = 1.5f;
            if (tf.duration <= 0f) tf.duration = 6f;
        }

        // 활성 장판 목록에 등록
        var rec = new F { element = element, pos = pos, radius = (tf != null ? tf.radius : 1.5f), go = go };
        // 기존 장판과 겹침 검사 → 조합 훅 호출
        for (int i = 0; i < _fields.Count; i++)
        {
            var f = _fields[i];
            if (f.go == null) continue;
            if (f.element == rec.element) continue;
            float need = f.radius + rec.radius + comboCheckExtraRadius;
            if (Vector2.Distance(f.pos, rec.pos) <= need)
            {
                if (logEvents) Debug.Log($"[Talisman] COMBO {f.element} + {rec.element} at {rec.pos}");
                onFieldCombo?.Invoke(f.element, rec.element, rec.pos);
                // 실제 조합 효과/파괴는 나중에 구현하도록 남김
            }
        }
        _fields.Add(rec);
    }

    void PruneDeadFields()
    {
        for (int i = _fields.Count - 1; i >= 0; i--)
        {
            if (_fields[i].go == null) _fields.RemoveAt(i);
        }
    }
}
