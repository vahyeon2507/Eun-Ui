using UnityEngine;

[CreateAssetMenu(menuName = "VFX/FxDefinition2D")]
public class FxDefinition2D : ScriptableObject
{
    public string fxId = "JumpDust";
    public GameObject prefab;

    [Header("Placement")]
    public Vector2 offset;
    [Tooltip("좌우 플립 시 X 오프셋에 부호를 줄지 여부")]
    public bool signedByFacing = true;

    public enum FollowMode
    {
        None,       // 생성 시점 좌표만 사용(이후 분리)
        Attach,     // 앵커의 자식으로 붙여서 끝까지 따라감
        SoftFollow  // 자식으로 붙이지 않고 duration 동안만 부드럽게 따라감
    }
    [Header("Follow")]
    public FollowMode follow = FollowMode.None;
    [Tooltip("SoftFollow일 때만 사용(초)")]
    public float followDuration = 0.2f;
    [Tooltip("SoftFollow/Attach에서 회전도 따라갈지")]
    public bool followRotation = false;

    [Header("Lifetime")]
    public float autoDestroy = 1.2f;   // 0이면 프리팹이 알아서 파괴

    [Header("Sorting (optional)")]
    public string sortingLayerOverride;
    public int orderInLayerOverride;

    [Header("SFX (optional)")]
    public AudioClip sfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
}
