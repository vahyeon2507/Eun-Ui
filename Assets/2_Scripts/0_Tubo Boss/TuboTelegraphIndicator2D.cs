using UnityEngine;

public class TuboTelegraphIndicator2D : MonoBehaviour
{
    Transform _follow;
    float _remain;

    public void Setup(Transform followTarget, float duration)
    {
        _follow = followTarget;
        _remain = duration;
    }

    void Update()
    {
        if (_follow) transform.position = _follow.position;

        if (_remain > 0f)
        {
            _remain -= Time.deltaTime;
            if (_remain <= 0f) Destroy(gameObject);
        }
    }
}
