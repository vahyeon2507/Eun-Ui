using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JangsanbeomBoss_SimpleFlip
/// - Flip trigger: child BoxCollider2D (flipTrigger)의 world X를 기준으로 플레이어가 좌/우를 넘나들면 flip.
/// - Visual flip: graphicsRoot.localScale.x 를 ±로 바꿔서 비주얼을 뒤집음 (없으면 spriteRenderer.flipX 사용)
/// - Physics flip: Boss 루트에 있는 PolygonCollider2D들의 path를 캐시해 좌우 미러로 갱신.
/// - 단일 flip 로직만 유지, 머리 전용 로직 삭제. 간단하고 예측 가능.
/// </summary>
[DisallowMultipleComponent]
public class JangsanbeomBoss_SimpleFlip : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public SpriteRenderer spriteRenderer;      // optional, used if graphicsRoot null
    [Tooltip("비주얼(스프라이트, VFX, 머리 등)을 모아놓은 루트. localScale.x로 뒤집습니다.")]
    public Transform graphicsRoot;             // optional

    [Header("Flip trigger (use a child BoxCollider2D)")]
    [Tooltip("플립 기준으로 사용할 BoxCollider2D (보통 꼬리 위치에 둔다). 이 콜라이더는 graphicsRoot의 자식이면 안 됩니다.")]
    public BoxCollider2D flipTrigger;          // assign a child box collider (not under graphicsRoot)

    [Header("Flip tuning")]
    public float flipTriggerHysteresis = 0.06f; // 작은 진동 방지
    public float flipCooldown = 0.12f;         // 연속 flip 방지
    [HideInInspector] public bool facingRight = true;

    [Header("Movement")]
    public float followSpeed = 2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Attack / Hitbox")]
    public GameObject hitboxPrefab;            // prefab containing BoxCollider2D or PolygonCollider2D + optional Hitbox component
    public Vector2 defaultHitboxSize = new Vector2(3f, 1.5f);
    public int defaultHitboxDamage = 2;
    public float defaultHitboxLifetime = 0.12f;

    [Header("Debug")]
    public bool debugFlip = false;
    public bool debugPoly = false;

    // internals
    Vector3 originalLocalScale;
    Vector3 graphicsRootOriginalScale;
    float _lastFlipTime = -10f;
    int _prevTriggerSide = 0; // -1 left, 0 near, +1 right

    // polygon cache for physics colliders (boss root, excluding graphicsRoot children)
    List<PolygonCollider2D> _polyColliders = new List<PolygonCollider2D>();
    List<Vector2[][]> _originalPolyPaths = new List<Vector2[][]>();

    void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Awake()
    {
        originalLocalScale = transform.localScale;
        originalLocalScale.x = Mathf.Abs(originalLocalScale.x);
        transform.localScale = originalLocalScale;

        if (graphicsRoot != null)
        {
            graphicsRootOriginalScale = graphicsRoot.localScale;
            graphicsRootOriginalScale.x = Mathf.Abs(graphicsRootOriginalScale.x);
            var s = graphicsRoot.localScale; s.x = graphicsRootOriginalScale.x; graphicsRoot.localScale = s;
        }
        else
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (flipTrigger != null && graphicsRoot != null && flipTrigger.transform.IsChildOf(graphicsRoot))
        {
            Debug.LogWarning("[Boss] flipTrigger가 graphicsRoot 아래에 있습니다. 트리거는 graphicsRoot의 자식이 아니어야 합니다. (Move it to boss root or scene root.)", this);
        }

        // cache polygon collider paths for flipping physics shapes
        CachePolygonColliderPaths();
    }

    void Start()
    {
        // init previous trigger side
        if (flipTrigger != null && player != null)
        {
            float rel = player.position.x - flipTrigger.bounds.center.x;
            _prevTriggerSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
        }
    }

    void Update()
    {
        if (player == null) return;

        // follow X only
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            Vector3 np = transform.position;
            np.x = Mathf.MoveTowards(transform.position.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = np;
        }

        // single flip logic driven by flipTrigger's world center X
        if (flipTrigger != null && Time.time - _lastFlipTime > flipCooldown)
        {
            float triggerX = flipTrigger.bounds.center.x;
            float rel = player.position.x - triggerX;
            int curSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);

            if (curSide != _prevTriggerSide)
            {
                if (curSide != 0)
                {
                    bool wantFaceRight = (player.position.x > triggerX);

                    // apply flip only when side changed (player crossed trigger)
                    ApplyFlip(wantFaceRight);
                    _lastFlipTime = Time.time;

                    if (debugFlip) Debug.Log($"[Boss] Flip applied -> wantFaceRight={wantFaceRight}, triggerX={triggerX}, playerX={player.position.x}", this);
                }

                _prevTriggerSide = curSide;
            }
        }
    }

    void LateUpdate()
    {
        // ensure root scale remains positive (physics root)
        if (transform.localScale.x != originalLocalScale.x)
        {
            var s = transform.localScale; s.x = originalLocalScale.x; transform.localScale = s;
        }
    }

    // Apply visual flip and physics collider paths
    void ApplyFlip(bool faceRight)
    {
        facingRight = faceRight;

        // Visual flip: prefer graphicsRoot scaling (so child visuals flip together).
        if (graphicsRoot != null)
        {
            var s = graphicsRootOriginalScale;
            s.x = graphicsRootOriginalScale.x * (faceRight ? 1f : -1f);
            graphicsRoot.localScale = s;
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }
        else
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = !faceRight;
        }

        // Physics: flip polygon colliders (mirror X)
        ApplyFlipToPolygonColliders(faceRight);
    }

    // ---------------- Polygon collider path cache & flip ----------------
    void CachePolygonColliderPaths()
    {
        _polyColliders.Clear();
        _originalPolyPaths.Clear();

        // gather polygon colliders that are not under graphicsRoot (those under graphicsRoot will flip by scale)
        var polys = GetComponentsInChildren<PolygonCollider2D>(true);
        foreach (var p in polys)
        {
            if (graphicsRoot != null && p.transform.IsChildOf(graphicsRoot)) continue; // visuals handled by scale
            _polyColliders.Add(p);

            int pc = p.pathCount;
            Vector2[][] paths = new Vector2[pc][];
            for (int i = 0; i < pc; i++)
            {
                Vector2[] path = p.GetPath(i);
                Vector2[] copy = new Vector2[path.Length];
                System.Array.Copy(path, copy, path.Length);
                paths[i] = copy;
            }
            _originalPolyPaths.Add(paths);
        }

        if (debugPoly) Debug.Log($"[Boss] Cached {_polyColliders.Count} PolygonCollider2D(s) for flipping.", this);
    }

    void ApplyFlipToPolygonColliders(bool faceRight)
    {
        if (_polyColliders == null || _originalPolyPaths == null) return;

        for (int k = 0; k < _polyColliders.Count; k++)
        {
            var poly = _polyColliders[k];
            var origPaths = _originalPolyPaths[k];
            if (poly == null) continue;

            poly.pathCount = origPaths.Length;
            bool mirror = !faceRight;

            for (int i = 0; i < origPaths.Length; i++)
            {
                Vector2[] src = origPaths[i];
                Vector2[] dst = new Vector2[src.Length];

                if (!mirror)
                {
                    System.Array.Copy(src, dst, src.Length);
                }
                else
                {
                    for (int j = 0; j < src.Length; j++)
                        dst[j] = new Vector2(-src[j].x, src[j].y);
                    System.Array.Reverse(dst); // keep winding consistent
                }

                poly.SetPath(i, dst);

                if (debugPoly)
                {
                    string s = $"[Boss] Poly[{k}] Path[{i}] points: ";
                    for (int ii = 0; ii < dst.Length; ii++) s += dst[ii].ToString("F3") + ", ";
                    Debug.Log(s, this);
                }
            }
        }
    }

    // ---------------- Attack helper ----------------
    // Call this to spawn an attack hitbox that faces the player (so no need to flip hitbox itself)
    public void SpawnAttackAtPlayer(Vector2 size)
    {
        if (hitboxPrefab == null) return;

        float dir = (player.position.x > transform.position.x) ? 1f : -1f;
        Vector2 center = (Vector2)transform.position + new Vector2(dir * (size.x * 0.5f + 0.08f), 0f);

        GameObject hb = Instantiate(hitboxPrefab, center, Quaternion.identity);

        var bc = hb.GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.offset = Vector2.zero;
            bc.size = size;
        }
        else
        {
            var pc = hb.GetComponent<PolygonCollider2D>();
            if (pc != null)
            {
                pc.isTrigger = true;
                Vector2[] pts = new Vector2[] {
                    new Vector2(-size.x*0.5f, -size.y*0.5f),
                    new Vector2( size.x*0.5f, -size.y*0.5f),
                    new Vector2( size.x*0.5f,  size.y*0.5f),
                    new Vector2(-size.x*0.5f,  size.y*0.5f)
                };
                pc.pathCount = 1;
                pc.SetPath(0, pts);
            }
        }

        var hbComp = hb.GetComponent<Hitbox>();
        if (hbComp != null)
        {
            hbComp.owner = this.gameObject;
            hbComp.damage = defaultHitboxDamage;
            hbComp.targetTag = "Player";
            hbComp.targetLayerMask = hitboxPrefab != null ? hbComp.targetLayerMask : 0;
            hbComp.singleUse = true;
        }

        if (defaultHitboxLifetime > 0f) Destroy(hb, defaultHitboxLifetime + 0.02f);
    }
}
