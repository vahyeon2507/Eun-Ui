using UnityEngine;

[DisallowMultipleComponent]
public class SpriteBoundsAnchor : MonoBehaviour
{
    [Header("Target anchoring")]
    public SpriteRenderer spriteRenderer;   // GfxRoot 아래의 본체 SR
    public Transform anchorWorld;           // FacingPivot/FootAnchor 같은 '고정점'

    [Tooltip("스프라이트 바운즈 안에서 맞출 지점: X=0..1(좌~우) / Y=0..1(하~상)")]
    [Range(0, 1)] public float spriteAnchorX = 0.5f; // 0.5=가로 중앙
    [Range(0, 1)] public float spriteAnchorY = 0.0f; // 0=바닥(발), 1=머리

    [Header("Lock axes")]
    public bool lockX = false;              // 가로도 고정할지(보통 false 권장)
    public bool lockY = true;               // 세로(발) 고정 (true 권장)

    [Header("Fine tune (world units)")]
    public Vector2 extraOffset;             // 미세 보정

    void Reset()
    {
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (!spriteRenderer || !spriteRenderer.sprite || !anchorWorld) return;

        // 현재 스프라이트의 '월드 바운즈'
        var b = spriteRenderer.bounds; // world space
        float ax = Mathf.Lerp(b.min.x, b.max.x, spriteAnchorX);
        float ay = Mathf.Lerp(b.min.y, b.max.y, spriteAnchorY);
        Vector3 spriteAnchorWorld = new Vector3(ax, ay, transform.position.z);

        // 우리가 고정시키고 싶은 월드 위치
        Vector3 desired = anchorWorld.position + (Vector3)extraOffset;

        // 그 차이만큼 GfxRoot를 이동해 보정
        Vector3 delta = desired - spriteAnchorWorld;
        var p = transform.position;
        if (lockX) p.x += delta.x;
        if (lockY) p.y += delta.y;
        transform.position = p;
    }
}
