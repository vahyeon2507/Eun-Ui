using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TeleportGateTrigger2D : MonoBehaviour
{
    [Header("Next Scene")]
    public string nextSceneName;          // 인스펙터에서 지정(또는 Director가 런타임에 덮어씀)
    public bool oneShot = true;

    [Header("Optional SFX/VFX")]
    public AudioSource audioSource;
    public AudioClip enterSfx;
    [Range(0f, 1f)] public float enterSfxVolume = 1f;

    bool _used;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (!col) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_used && oneShot) return;

        // 플레이어 판정(둘 중 하나만 참이어도 통과)
        bool isPlayer = other.GetComponent<PlayerController>() != null || other.CompareTag("Player");
        if (!isPlayer) return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[TeleportGateTrigger2D] nextSceneName이 비었습니다!", this);
            return;
        }

        _used = true;

        if (enterSfx)
        {
            if (audioSource) audioSource.PlayOneShot(enterSfx, enterSfxVolume);
            else AudioSource.PlayClipAtPoint(enterSfx, transform.position, enterSfxVolume);
        }

        // 간단히 즉시 로드(필요하면 페이드/전환 컨트롤러 후킹 가능)
        Time.timeScale = 1f; // 혹시 슬로모 중이었다면 복구
        SceneManager.LoadScene(nextSceneName);
    }
}
