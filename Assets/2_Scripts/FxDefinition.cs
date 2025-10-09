using UnityEngine;

[CreateAssetMenu(menuName = "VFX/FxDefinition2D")]
public class FxDefinition2D : ScriptableObject
{
    public string fxId = "JumpDust";
    public GameObject prefab;

    [Header("Placement")]
    public Vector2 offset;
    public bool signedByFacing = true;   // X 오프셋에 좌우 부호 적용

    public enum FollowMode { None, Attach, SoftFollow }
    [Header("Follow")]
    public FollowMode follow = FollowMode.None;
    public float followDuration = 0.2f;  // SoftFollow일 때만
    public bool followRotation = false;

    [Header("Facing / Mirroring")]
    public bool mirrorByFacing = true;   // 좌/우에 따라 이펙트를 뒤집을지
    public enum MirrorMode { ScaleX, FlipSpriteRenderer }
    public MirrorMode mirrorMode = MirrorMode.ScaleX;

    [Header("Lifetime")]
    public float autoDestroy = 1.2f;

    [Header("Sorting (optional)")]
    public string sortingLayerOverride;
    public int orderInLayerOverride;

    [Header("SFX (optional)")]
    public AudioClip sfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
}
