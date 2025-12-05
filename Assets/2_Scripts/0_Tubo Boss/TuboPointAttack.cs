using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TuboPointAttack : MonoBehaviour, ITuboAttack
{
    [Header("Owner")]
    public BossTuboController owner;

    [Header("Hitbox")]
    public Collider2D hitbox;              // isTrigger 권장
    public LayerMask playerLayer;

    [Header("Damage")]
    public int damage = 1;
    public float activeTime = 0.15f;       // 실제 유효 시간
    public float extraParryGrace = 0.15f;  // 끝나고도 약간 패링 인정

    [Header("FX/SFX (optional)")]
    public string spawnFxId;
    public string hitFxId;

    bool _active = false;
    bool _consumed = false;
    Coroutine _lifeCR;

    void Reset()
    {
        hitbox = GetComponent<Collider2D>();
        if (hitbox) hitbox.isTrigger = true;
    }

    void OnEnable()
    {
        if (!owner) owner = GetComponentInParent<BossTuboController>();
        if (owner) owner.RegisterAttack(this);

        // 그로기 중에 스폰되면 즉시 폐기
        if (owner && !owner.AttacksEnabled) { Destroy(gameObject); return; }

        _lifeCR = StartCoroutine(CoLife());
    }

    void OnDisable()
    {
        if (owner) owner.UnregisterAttack(this);
    }

    IEnumerator CoLife()
    {
        _active = true;

        // 패링 윈도우 알림(원하면)
        if (owner) owner.NotifyAttackWindow(true, activeTime + extraParryGrace);

        yield return new WaitForSeconds(activeTime);

        _active = false;
        if (owner) owner.NotifyAttackWindow(false);

        // 한 틱 뒤 안전 제거
        Destroy(gameObject, 0.02f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_active || _consumed) return;
        if (owner && !owner.AttacksEnabled) return; // 그로기 가드

        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        var pc = other.GetComponentInParent<PlayerController>() ?? other.GetComponent<PlayerController>();
        if (!pc) return;

        _consumed = true;

        // 플레이어가 패링 중이면 PlayerController 내부 로직이 소비/보상 처리
        pc.TakeDamage(damage);

        if (hitbox) hitbox.enabled = false;
    }

    // === ITuboAttack ===
    public void CancelAttack()
    {
        _active = false;
        if (hitbox) hitbox.enabled = false;
        if (_lifeCR != null) { StopCoroutine(_lifeCR); _lifeCR = null; }
        Destroy(gameObject);
    }
}
