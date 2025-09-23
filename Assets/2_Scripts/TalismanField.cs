using UnityEngine;

/// <summary>
/// 부적 장판 데이터 컨테이너 + 조합 후킹 자리
/// </summary>
[DisallowMultipleComponent]
public class TalismanField : MonoBehaviour
{
    public TalismanType element = TalismanType.Fire;
    public float duration = 6f;
    public float radius = 1.5f;

    float _timer;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 1, 0, 0.25f);
        Gizmos.DrawSphere(transform.position, radius);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= duration) Destroy(gameObject);
    }

    // === 조합 로직을 여기에 이어붙이면 됨 ===
    // 예) 다른 장판과 겹치면 조합 이벤트 발생:
    // void OnTriggerEnter2D(Collider2D other) { ... }
    //   - other.GetComponent<TalismanField>() 확인
    //   - element 조합표에 따라 효과 발동 -> 이후 Destroy(this/other) 등
}
