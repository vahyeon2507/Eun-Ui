using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TreeWall : MonoBehaviour
{
    [Header("Lifetime")]
    public float lifeTime = 3f;
    public string growTrigger = "Grow";
    public string breakTrigger = "Break";

    [Header("FX (optional)")]
    public AudioClip spawnSfx;
    public AudioClip breakSfx;

    Animator _anim;
    AudioSource _audio;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        _audio = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (_anim && !string.IsNullOrEmpty(growTrigger)) _anim.SetTrigger(growTrigger);
        if (_audio && spawnSfx) _audio.PlayOneShot(spawnSfx);
        if (lifeTime > 0f) StartCoroutine(SelfDestructTimer(lifeTime));
    }

    IEnumerator SelfDestructTimer(float t)
    {
        yield return new WaitForSeconds(t);
        BreakAndDestroy();
    }

    public void BreakAndDestroy()
    {
        if (_anim && !string.IsNullOrEmpty(breakTrigger))
        {
            _anim.SetTrigger(breakTrigger);
            // 애니 길이가 짧다는 가정. 필요하면 애니 길이 만큼 대기 로직 추가.
        }
        if (_audio && breakSfx) _audio.PlayOneShot(breakSfx);
        Destroy(gameObject, 0.35f);
    }

    // 폴백: 충돌로도 보스 기절 유발(보스가 직접 알림을 보내는 게 기본 설계)
    void OnCollisionEnter2D(Collision2D other)
    {
        var boss = other.collider.GetComponentInParent<BossDueoksiniController>();
        if (boss != null) boss.StunFromTreeWall(this);
    }


}
