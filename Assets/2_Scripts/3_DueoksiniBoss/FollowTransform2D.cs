using UnityEngine;

public class FollowTransform2D : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public bool followRotation = false;

    void LateUpdate()
    {
        if (!target) return;

        transform.position = target.position + offset;

        if (followRotation)
            transform.rotation = target.rotation;
    }
}
