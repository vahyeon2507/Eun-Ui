using UnityEngine;

public class DestroyAfter : MonoBehaviour
{
    [Tooltip("몇 초 후에 파괴될지 설정")]
    public float lifetime = 1.5f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
