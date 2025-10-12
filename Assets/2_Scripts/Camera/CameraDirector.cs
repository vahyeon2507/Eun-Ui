using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class CameraDirector : MonoBehaviour
{
    [Header("VCams")]
    public CinemachineVirtualCameraBase vFollow;
    public CinemachineVirtualCameraBase vFixed;
    public CinemachineVirtualCameraBase vVertical;

    [Header("Priorities")]
    public int offPriority = 10;
    public int midPriority = 15;
    public int onPriority = 100;

    void Reset()
    {
        if (!vFollow) vFollow = FindByName("VCam_Follow");
        if (!vFixed) vFixed = FindByName("VCam_Fixed");
        if (!vVertical) vVertical = FindByName("VCam_Vertical");
    }

    void Awake() => UseFollow();

    // -------- Switch API --------
    public void UseFollow() => Set(vFollow);
    public void UseFixed() => Set(vFixed);
    public void UseVertical() => Set(vVertical);

    public void SetTargets(Transform follow, Transform lookAt = null)
    {
        if (vFollow) SetTargets(vFollow, follow, lookAt);
        if (vFixed) SetTargets(vFixed, follow, lookAt);
        if (vVertical) SetTargets(vVertical, follow, lookAt);
    }

    // 현재 활성 카메라 (우선순위가 가장 높은 것)
    public CinemachineVirtualCameraBase Current
    {
        get
        {
            CinemachineVirtualCameraBase best = null;
            int bestP = int.MinValue;
            void Try(CinemachineVirtualCameraBase cam)
            {
                if (!cam || !cam.isActiveAndEnabled) return;
                if (cam.Priority > bestP) { best = cam; bestP = cam.Priority; }
            }
            Try(vFollow); Try(vFixed); Try(vVertical);
            return best;
        }
    }

    // -------- Internals --------
    void Set(CinemachineVirtualCameraBase on)
    {
        if (vFollow) vFollow.Priority = (on == vFollow) ? onPriority : offPriority;
        if (vVertical) vVertical.Priority = (on == vVertical) ? onPriority : midPriority;
        if (vFixed) vFixed.Priority = (on == vFixed) ? onPriority : offPriority;
    }

    static void SetTargets(CinemachineVirtualCameraBase cam, Transform follow, Transform lookAt)
    {
        cam.Follow = follow;
        cam.LookAt = lookAt;
    }

    static CinemachineVirtualCameraBase FindByName(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.GetComponent<CinemachineVirtualCameraBase>() : null;
    }
}
