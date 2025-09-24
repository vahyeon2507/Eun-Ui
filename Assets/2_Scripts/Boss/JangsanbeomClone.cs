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

    [Header("Visual Flip")]
    public Transform graphicsRoot;          // 있으면 이 루트만 좌우 반전
    [Tooltip("원본 스프라이트가 ‘오른쪽’을 바라보는가? (false면 기본이 왼쪽)")]
    public bool spriteFacesRight = true;

    [Header("Move/AI")]
    public float followSpeed = 2.2f;
    public float followMinDistanceX = 1.2f;
    public float aggroRange = 12f;
    public float flipCooldown = 0.1f;
    public float flipHysteresis = 0.05f;

    [Header("Animator params")]
    public string animParam_MoveBool = "Move";
    public string animParam_MoveSpeed = "Speed";
    public string trig_AttackReal = "ClawExecute";
    public string trig_AttackFake = "ClawFakeExecute";

    [Header("Attacks")]
    public List<JangsanbeomBoss.AttackData> attacks = new();
    public bool useAnimationEvents = true;
    [Tooltip("2페이즈 기준: 페이크도 실체화할지")]
    public bool enableFakePhase2 = true;
    [Range(0f, 1f)] public float fakeChancePhase2 = 0.25f;

    [Header("Layers/Tags")]
    public LayerMask playerLayer;
    public string playerTag = "Player";

    [Header("Health")]
    public int maxHp = 5;
    public int currentHp = 5;
    public bool allowDropRewards = false;

    // state
    bool facingRight = true;
    float _lastFlipTime = -10f;
    bool _busy = false;
    Vector3 _lastPos;

    void Reset()
    {
        animator = GetComponent<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rb) { rb.bodyType = RigidbodyType2D.Kinematic; rb.simulated = true; }
    }

    void Start()
    {
        if (player == null && owner != null) player = owner.player;
        _lastPos = transform.position;

        // 스폰 직후 시선 보정: 플레이어 쪽을 바라보게
        if (player != null) FaceRight(player.position.x >= transform.position.x);

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

        // 플립(지터 방지 쿨다운 포함)
        if (Time.time - _lastFlipTime > flipCooldown)
        {
            float rel = player.position.x - transform.position.x;
            int cur = (rel > flipHysteresis) ? 1 : (rel < -flipHysteresis ? -1 : 0);
            if (cur != 0 && ((cur > 0) != facingRight))
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

    public void FaceRight(bool right)
    {
        facingRight = right;

        if (graphicsRoot != null)
        {
            var s = graphicsRoot.localScale;
            float sign = right ? 1f : -1f;
            // spriteFacesRight=false(=기본이 왼쪽)이면 좌우 반전 방향 한번 더 뒤집음
            s.x = Mathf.Abs(s.x) * (spriteFacesRight ? sign : -sign);
            graphicsRoot.localScale = s;
        }
        else if (sr != null)
        {
            // SpriteRenderer.flipX는 “왼쪽 보이도록 뒤집기”라서 기본 바라보는 방향에 따라 XOR
            bool flipX = spriteFacesRight ? !right : right;
            sr.flipX = flipX;
        }
        else
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.flipX = spriteFacesRight ? !right : right;
        }
    }

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

    // --- Animation Events ---
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
        Vector2 rot = new Vector2(off.x * c - off.y * s, off.x * s + off.y * c);
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
