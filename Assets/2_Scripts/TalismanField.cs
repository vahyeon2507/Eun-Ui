using UnityEngine;

/// <summary>
/// ���� ���� ������ �����̳� + ���� ��ŷ �ڸ�
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

    // === ���� ������ ���⿡ �̾���̸� �� ===
    // ��) �ٸ� ���ǰ� ��ġ�� ���� �̺�Ʈ �߻�:
    // void OnTriggerEnter2D(Collider2D other) { ... }
    //   - other.GetComponent<TalismanField>() Ȯ��
    //   - element ����ǥ�� ���� ȿ�� �ߵ� -> ���� Destroy(this/other) ��
}
