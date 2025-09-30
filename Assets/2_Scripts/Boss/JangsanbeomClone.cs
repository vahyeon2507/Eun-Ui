using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomClone : MonoBehaviour, IDamageable
{
    [Header("Owner & Refs")]
    public JangsanbeomBoss owner;
    public Transform player;
    public Animator animator;
    public SpriteRenderer sr;
    public Rigidbody2D rb;

    [Header("Visual Flip (optional)")]
    public Transform graphicsRoot; // 여기에 애니메이션 키 찍으면 안전
    public bool spriteFacesRight = true;
    public bool enforceVisualFlip = true;

    [Header("Flip Trigger / Pivot (본체와 동일)")]
    public BoxCollider2D flipTrigger;
    public Transform flipPivot;
    public float flipPivotDeadzone = 0.6f;
    public float flipTriggerHysteresis = 0.05f;
    public float flipCooldown = 0.12f;

    [Header("Move/AI")]
    public float followSpeed = 2.2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;

    [Header("Animator params")]
    public string animParam_MoveBool = "Move";
    public string animParam_MoveSpeed = "Speed";
    public string trig_AttackReal = "ClawExecute";
    public string trig_AttackFake = "ClawFakeExecute";

    [Header("Attacks")]
    public List<JangsanbeomBoss.AttackData> attacks = new();
    public bool useAnimationEvents = true;
    public bool enableFakePhase2 = true;
    [Range(0f, 1f)] public float fakeChancePhase2 = 0.25f;

    [Header("Layers/Tags")]
    public LayerMask playerLayer;
    public string playerTag = "Player";

    [Header("Health")]
    public int maxHp = 5;
    public int currentHp = 5;
    public bool allowDropRewards = false;

    // --- state ---
    bool facingRight = true;
    float _lastFlipTime = -10f;
    bool _busy = false;
    Vector3 _lastPos;

    Vector3 _rootOriginalScale, _graphicsOriginalScale;
    readonly List<PolygonCollider2D> _polys = new();
    readonly List<Vector2[][]> _polyPaths = new();

    float _flipTriggerOriginalLocalX = 0f;
    bool _flipTriggerIsChild = false;
    int _prevTriggerSide = 0;

    bool _animLockMove, _animLockFlip, _invuln;

    public void EVT_DashStart() { _animLockMove = true; _animLockFlip = true; }
    public void EVT_DashEnd() { _animLockMove = false; _animLockFlip = false; }

    public void EVT_MoveBy(string spec)
    {
        // 보스랑 동일 규칙으로 이동시키고 싶으면 보스의 파서를 재사용하거나,
        // 여기서 간단히 처리해도 됩니다. 가장 간단한 버전:
        // spec: "dx,dy[,local|world][,face]"
        if (string.IsNullOrEmpty(spec)) return;
        var p = spec.Split(',');
        if (p.Length < 2) return;
        float dx = float.Parse(p[0]); float dy = float.Parse(p[1]);
        bool useLocal = false, useFacing = false;
        for (int i = 2; i < p.Length; i++)
        {
            var s = p[i].Trim().ToLowerInvariant();
            if (s == "local") useLocal = true;
            else if (s == "world") useLocal = false;
            else if (s == "face" || s == "signed") useFacing = true;
        }
        if (useFacing) dx *= (/*clone의 바라보는 방향*/ transform.localScale.x >= 0 ? 1f : -1f);
        var delta = new Vector3(dx, dy, 0);
        if (useLocal) transform.position = transform.TransformPoint(delta);
        else transform.position += delta;
    }

    public void EVT_DashHit()
    {
        // ★ 보스의 대쉬 히트 로직을 그대로 재사용 (원점은 클론)
        owner?.PerformAttackOnceFrom(owner.dashAttack, this.transform, true);
    }

    // (옵션) 필요하면 락/무적도 구현
    public void EVT_SetMoveLock(bool on) { _animLockMove = on; }
    public void EVT_SetFlipLock(bool on) { _animLockFlip = on; }
    public void EVT_Invuln(bool on) { _invuln = on; }

    void Reset()
    {
        animator = GetComponent<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rb) { rb.bodyType = RigidbodyType2D.Kinematic; rb.simulated = true; }
    }

    void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        _rootOriginalScale = transform.localScale; _rootOriginalScale.x = Mathf.Abs(_rootOriginalScale.x);
        transform.localScale = _rootOriginalScale;

        if (graphicsRoot != null)
        {
            _graphicsOriginalScale = graphicsRoot.localScale;
            _graphicsOriginalScale.x = Mathf.Abs(_graphicsOriginalScale.x);
            var s = graphicsRoot.localScale; s.x = Mathf.Abs(s.x); graphicsRoot.localScale = s;
        }

        if (flipTrigger)
        {
            _flipTriggerIsChild = flipTrigger.transform.IsChildOf(transform);
            _flipTriggerOriginalLocalX = _flipTriggerIsChild ? flipTrigger.transform.localPosition.x : 0f;
        }

        CachePolyPaths();
    }

    void Start()
    {
        if (player == null && owner != null) player = owner.player;
        _lastPos = transform.position;

        if (player != null) FaceRight(player.position.x >= transform.position.x);

        if (player != null)
        {
            float rel = player.position.x - GetFlipReferenceX();
            _prevTriggerSide = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
        }

        StartCoroutine(AI());
    }

    void Update()
    {
        if (player == null) return;

        bool isMoving = false;
        float dx = player.position.x - transform.position.x;
        if (!_busy && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            var p = transform.position;
            p.x = Mathf.MoveTowards(p.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = p;
            isMoving = Mathf.Abs(p.x - _lastPos.x) > 0.0001f;
        }
        if (animator && !string.IsNullOrEmpty(animParam_MoveBool))
            animator.SetBool(animParam_MoveBool, isMoving);

        if (Time.time - _lastFlipTime > flipCooldown)
        {
            float refX = GetFlipReferenceX();
            float rel = player.position.x - refX;

            int cur = 0;
            if (flipTrigger) cur = (rel > flipTriggerHysteresis) ? 1 : (rel < -flipTriggerHysteresis ? -1 : 0);
            else if (flipPivot) cur = (rel > flipPivotDeadzone) ? 1 : (rel < -flipPivotDeadzone ? -1 : 0);
            else cur = (rel > 0f) ? 1 : (rel < 0f ? -1 : 0);

            if (cur != _prevTriggerSide && cur != 0)
            {
                FaceRight(rel > 0f);
                _lastFlipTime = Time.time;
            }
        }

        if (animator && !string.IsNullOrEmpty(animParam_MoveSpeed))
        {
            float dt = Time.deltaTime;
            float vx = dt > 0f ? (transform.position.x - _lastPos.x) / dt : 0f;
            animator.SetFloat(animParam_MoveSpeed, Mathf.Abs(vx));
        }
        _lastPos = transform.position;
    }

    void LateUpdate()
    {
        // ❌ 루트 X 스케일 강제 복구 제거
        // if (!Mathf.Approximately(transform.localScale.x, _rootOriginalScale.x))
        // { var s = transform.localScale; s.x = _rootOriginalScale.x; transform.localScale = s; }

        if (enforceVisualFlip) ApplyVisualFlipNow();
    }

    IEnumerator AI()
    {
        while (true)
        {
            if (!_busy && player != null && attacks != null && attacks.Count > 0)
            {
                float d = Mathf.Abs(player.position.x - transform.position.x);
                if (d <= aggroRange)
                {
                    if (Random.Range(0, 100) < 60) StartAttack(attacks[0]);
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.3f));
        }
    }

    float GetFlipReferenceX()
    {
        if (flipTrigger)
        {
            if (_flipTriggerIsChild)
            {
                float localX = _flipTriggerOriginalLocalX * (facingRight ? 1f : -1f);
                Vector3 world = transform.TransformPoint(new Vector3(localX, flipTrigger.transform.localPosition.y, 0f));
                return world.x;
            }
            return flipTrigger.bounds.center.x;
        }
        if (flipPivot) return flipPivot.position.x;
        return transform.position.x;
    }

    public void FaceRight(bool right)
    {
        facingRight = right;
        // 루트 스케일은 덮어쓰지 않음 (애니값 보존)

        if (graphicsRoot != null)
        {
            var s = graphicsRoot.localScale;
            float sign = right ? 1f : -1f;
            s.x = Mathf.Abs(s.x) * (spriteFacesRight ? sign : -sign);
            graphicsRoot.localScale = s;
            if (sr) sr.flipX = false;
        }
        else if (sr != null)
        {
            bool flipX = spriteFacesRight ? !right : right;
            sr.flipX = flipX;
        }
        else
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.flipX = spriteFacesRight ? !right : right;
        }

        ApplyFlipToPolys(right);

        if (flipTrigger && _flipTriggerIsChild)
        {
            var lp = flipTrigger.transform.localPosition;
            lp.x = _flipTriggerOriginalLocalX * (right ? 1f : -1f);
            flipTrigger.transform.localPosition = lp;
        }

        float relAfter = player ? (player.position.x - GetFlipReferenceX()) : 0f;
        _prevTriggerSide = (relAfter > flipTriggerHysteresis) ? 1 : (relAfter < -flipTriggerHysteresis ? -1 : 0);
    }

    void ApplyVisualFlipNow()
    {
        if (graphicsRoot != null)
        {
            var s = graphicsRoot.localScale;
            float wantSign = (spriteFacesRight ? (facingRight ? 1f : -1f) : (facingRight ? -1f : 1f));
            if (Mathf.Sign(s.x) != wantSign) { s.x = Mathf.Abs(s.x) * wantSign; graphicsRoot.localScale = s; }
        }
        else
        {
            if (sr != null) sr.flipX = spriteFacesRight ? !facingRight : facingRight;
            else foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                    r.flipX = spriteFacesRight ? !facingRight : facingRight;
        }
    }

    void CachePolyPaths()
    {
        _polys.Clear(); _polyPaths.Clear();
        foreach (var p in GetComponentsInChildren<PolygonCollider2D>(true))
        {
            _polys.Add(p);
            int pc = p.pathCount;
            var paths = new Vector2[pc][];
            for (int i = 0; i < pc; i++)
            {
                var src = p.GetPath(i);
                var cp = new Vector2[src.Length];
                System.Array.Copy(src, cp, src.Length);
                paths[i] = cp;
            }
            _polyPaths.Add(paths);
        }
    }
    void ApplyFlipToPolys(bool faceRight)
    {
        for (int k = 0; k < _polys.Count; k++)
        {
            var poly = _polys[k]; if (!poly) continue;
            var orig = _polyPaths[k]; poly.pathCount = orig.Length;
            bool mirror = !faceRight;
            for (int i = 0; i < orig.Length; i++)
            {
                var src = orig[i];
                var dst = new Vector2[src.Length];
                if (!mirror) System.Array.Copy(src, dst, src.Length);
                else { for (int j = 0; j < src.Length; j++) dst[j] = new Vector2(-src[j].x, src[j].y); System.Array.Reverse(dst); }
                poly.SetPath(i, dst);
            }
        }
    }

    // -------- Attack / Events --------
    void StartAttack(JangsanbeomBoss.AttackData atk)
    {
        if (_busy) return;
        StartCoroutine(AttackRoutine(atk));
    }

    IEnumerator AttackRoutine(JangsanbeomBoss.AttackData atk)
    {
        _busy = true;

        bool visualFake = false;
        bool applyDamage = true;

        if (enableFakePhase2)
        {
            visualFake = Random.value < fakeChancePhase2 || atk.DefaultFake;
            applyDamage = visualFake ? atk.Phase2_FakeDealsDamage : true;
        }

        if (animator) animator.SetTrigger(visualFake ? trig_AttackFake : trig_AttackReal);

        if (!useAnimationEvents)
        {
            if (visualFake && atk.FakeSprite != null && sr != null)
                StartCoroutine(TempShow(atk.FakeSprite, atk.FakeSpriteDuration));
            yield return new WaitForSeconds(visualFake ? 0.06f : 0.08f);
            PerformHit(atk, applyDamage);
        }

        if (atk.TelegraphTime > 0f) yield return new WaitForSeconds(atk.TelegraphTime);
        yield return new WaitForSeconds(0.01f);
        _busy = false;
    }

    public void OnCloneHitFrame()
    {
        if (attacks == null || attacks.Count == 0) return;
        PerformHit(attacks[0], true);
    }
    public void OnCloneFakeHitFrame()
    {
        if (attacks == null || attacks.Count == 0) return;
        var a = attacks[0];
        PerformHit(a, a.Phase2_FakeDealsDamage);
    }

    void PerformHit(JangsanbeomBoss.AttackData atk, bool applyDamage)
    {
        if (!applyDamage) return;
        if (playerLayer == 0) return;

        Vector2 center = GetAttackWorldPos(atk);
        float dirSign = facingRight ? 1f : -1f;
        float ang = atk.Angle * dirSign;
        var hits = Physics2D.OverlapBoxAll(center, atk.Size, ang, playerLayer);
        var set = new HashSet<Collider2D>();

        foreach (var col in hits)
        {
            if (!col || set.Contains(col)) continue;
            set.Add(col);
            if (!string.IsNullOrEmpty(playerTag) && !col.CompareTag(playerTag)) continue;

            var pc = col.GetComponent<PlayerController>() ?? col.GetComponentInParent<PlayerController>() ?? col.GetComponentInChildren<PlayerController>();
            if (pc != null && pc.IsParrying)
            {
                TryAskPlayerToConsumeParry(pc, atk.Damage);
                continue;
            }

            var dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>() ?? col.GetComponentInChildren<IDamageable>();
            if (dmg != null) { dmg.TakeDamage(atk.Damage); continue; }

            var pHealth = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
            if (pHealth != null) { pHealth.TakeDamage(atk.Damage); continue; }

            var mb = col.GetComponent<MonoBehaviour>() ?? col.GetComponentInParent<MonoBehaviour>();
            if (mb != null)
            {
                var mi = mb.GetType().GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(mb, new object[] { atk.Damage });
            }
        }
    }

    Vector2 GetAttackWorldPos(JangsanbeomBoss.AttackData atk)
    {
        float dir = facingRight ? 1f : -1f;
        Vector2 origin = (atk.OriginOverride ? (Vector2)atk.OriginOverride.position : (Vector2)transform.position);
        Vector2 off = atk.Offset; off.x *= dir;
        float rad = (atk.Angle * dir) * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        Vector2 rot = new(off.x * c - off.y * s, off.x * s + off.y * c);
        return origin + rot;
    }

    bool TryAskPlayerToConsumeParry(PlayerController pc, int dmg)
    {
        if (pc == null) return false;
        string[] names = { "ConsumeHitboxIfParrying", "ConsumeParryHit", "ConsumeParry", "OnParriedByHitbox", "ConsumeHitIfParry" };
        var t = pc.GetType();
        foreach (var n in names)
        {
            var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) continue;
            var ps = m.GetParameters();
            try
            {
                object r = null;
                if (ps.Length == 0) r = m.Invoke(pc, null);
                else if (ps.Length == 1) r = m.Invoke(pc, new object[] { dmg });
                else r = m.Invoke(pc, new object[] { dmg, this });
                if (r is bool b) return b;
                return true;
            }
            catch { }
        }
        return false;
    }

    IEnumerator TempShow(Sprite s, float dur)
    {
        if (!sr) yield break;
        var prev = sr.sprite; sr.sprite = s;
        yield return new WaitForSeconds(dur);
        if (sr) sr.sprite = prev;
    }

    // ---- IDamageable ----
    public void TakeDamage(int amount)
    {
        currentHp -= Mathf.Max(1, amount);
        if (currentHp <= 0) Die();
    }

    void Die()
    {
        if (owner != null) owner.OnCloneDied(this);
        Destroy(gameObject);
    }
}
