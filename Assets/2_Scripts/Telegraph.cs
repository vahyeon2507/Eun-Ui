using UnityEngine;

public class Telegraph : MonoBehaviour
{
    public float duration = 1.5f;
    private float timer;

    void Start()
    {
        timer = duration;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            Destroy(gameObject); // 예고 시간 지나면 사라짐
        }
    }
}
