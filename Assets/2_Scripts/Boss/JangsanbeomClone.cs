using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class JangsanbeomClone : MonoBehaviour, IDamageable
{
    [Header("Owner & Refs")]
    public JangsanbeomBoss owner;      // 본체 참조(콜백용)
    public Transform player;           // 타겟
    public Animator animator;
    public SpriteRenderer sr;
    public Rigidbody2D rb;

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
    public List<JangsanbeomBoss.AttackData> attacks = new(); // 본체가 채워줌
    public bool useAnimationEvents = true; // 본체와 동일하게 운용
    [Tooltip("2페이즈 기준: 페이크도 실체화할지(본체에서 상속)")]
    public bool enableFakePhase2 = true;
    [Range(0f, 1f)] public float fakeChancePhase2 = 0.25f;

    [Header("Layers/Tags")]
    public LayerMask playerLayer;
    public string playerTag = "Player";

    [Header("Health")]
    public int maxHp = 5;
    public int currentHp = 5;
    public bool allowDropRewards = false; // 분신은 보상 X

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
        StartCoroutine(AI());
    }

    void Update()
    {
        if (player == null) return;

        // 이동
        bool isMoving = false;
        float dx = player.position.x - transform.position.x;
        if (!_busy && Mathf.Abs(dx) > followMinDistanceX && Mathf.Abs(dx) < aggroRange)
        {
            float step = followSpeed * Time.deltaTime;
            var p = transform.position;
            p.x = Mathf.MoveTowards(p.x, player.position.x - Mathf.Sign(dx) * followMinDistanceX, step);
            transform.position = p;
            isMoving = Mathf.Abs(step) > 0.0001f;
        }

        if (animator && !string.IsNullOrEmpty(animParam_MoveBool))
            animator.SetBool(animParam_MoveBool, isMoving);

        // 플립
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

        // 이동 속도 파라미터
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
                    // 단순: 첫 슬롯 공격을 확률적으로 사용
                    if (Random.Range(0, 100) < 60) StartAttack(attacks[0]);
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.3f));
        }
    }

    public void FaceRight(bool right)
    {
        facingRight = right;
        if (sr) sr.flipX = !right;
        else
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.flipX = !right;
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

    // 애니메이션 이벤트에서 호출
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

            // 패링 처리 (플레이어 컨트롤러가 있다면)
            var pc = col.GetComponent<PlayerController>() ?? col.GetComponentInParent<PlayerController>() ?? col.GetComponentInChildren<PlayerController>();
            if (pc != null && pc.IsParrying)
            {
                // 보스와 동일한 호환성 리플렉션
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
        // 보상/페이즈 전환 같은 건 없음
        if (owner != null) owner.OnCloneDied(this);
        Destroy(gameObject);
    }
}
