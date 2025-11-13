using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpriteBoundsAnchor : MonoBehaviour
{
    [Header("Targets")]
    public SpriteRenderer sprite;          // GfxRoot의 SR
    public Transform anchorWorld;          // FootAnchor 같은 월드 기준점

    [Header("Anchor (normalized, but free input)")]
    public bool clampAnchors01 = true;     // ✔ 기본: 0..1로 자동 클램프
    public float spriteAnchorX = 0.5f;     // 바운즈 내 X 비율(0=왼, 1=오) - 자유 입력
    public float spriteAnchorY = 0.0f;     // 바운즈 내 Y 비율(0=아래, 1=위) - 자유 입력

    [Header("Lock axes")]
    public bool lockX = true;
    public bool lockY = true;

    [Header("Fine tune (world units)")]
    public Vector2 extraOffset = Vector2.zero; // 애니메이션에서 키 가능

    [Header("Flip support")]
    public bool mirrorXOnFlip = true;
    public Transform flipRoot;             // FacingPivot 등

    [System.Serializable]
    public struct ClipOverride
    {
        public AnimationClip clip;

        // 자유 입력(0~1 밖도 가능). 필요시 전역 설정(clampAnchors01) 따라 클램프됨.
        public float anchorX;
        public float anchorY;

        // 이 클립에만 추가 오프셋(월드 단위)
        public Vector2 extraOffset;
    }

    [Header("Per-Clip Overrides (optional)")]
    public List<ClipOverride> perClip = new List<ClipOverride>();

    Animator _anim;

    void Reset()
    {
        sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (!anchorWorld) anchorWorld = transform;
    }

    void Awake()
    {
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (!anchorWorld) anchorWorld = transform;
        _anim = GetComponentInParent<Animator>();
    }

    void LateUpdate()
    {
        if (!sprite || !anchorWorld || !sprite.sprite) return;

        // 1) 기본값
        float ax = spriteAnchorX;
        float ay = spriteAnchorY;
        Vector2 ex = extraOffset;

        // 2) 클립별 오버라이드
        if (_anim != null && perClip != null && perClip.Count > 0)
        {
            var infos = _anim.GetCurrentAnimatorClipInfo(0);
            if (infos != null && infos.Length > 0)
            {
                var playing = infos[0].clip;
                for (int i = 0; i < perClip.Count; i++)
                {
                    if (perClip[i].clip == playing)
                    {
                        ax = perClip[i].anchorX;
                        ay = perClip[i].anchorY;
                        ex += perClip[i].extraOffset;
                        break;
                    }
                }
            }
        }

        // 3) 필요 시 0..1로 클램프
        if (clampAnchors01)
        {
            ax = Mathf.Clamp01(ax);
            ay = Mathf.Clamp01(ay);
        }

        // 4) 좌우 반전 시 미러
        bool flipped = (flipRoot && Mathf.Sign(flipRoot.lossyScale.x) < 0f);
        if (mirrorXOnFlip && flipped) ax = clampAnchors01 ? (1f - ax) : (1f - ax);

        // 5) 현재 프레임 바운즈에서 앵커 지점의 월드 좌표
        Bounds b = sprite.bounds;
        Vector3 anchorInSpriteWorld = new Vector3(
            Mathf.LerpUnclamped(b.min.x, b.max.x, ax),
            Mathf.LerpUnclamped(b.min.y, b.max.y, ay),
            transform.position.z
        );

        // 6) 목표 위치 = FootAnchor + 오프셋
        Vector3 target = anchorWorld.position + (Vector3)ex;

        // 7) 보정 적용
        Vector3 delta = target - anchorInSpriteWorld;
        var p = transform.position;
        if (lockX) p.x += delta.x;
        if (lockY) p.y += delta.y;
        transform.position = p;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!sprite || !sprite.sprite) return;
        float ax = spriteAnchorX, ay = spriteAnchorY;
        if (clampAnchors01) { ax = Mathf.Clamp01(ax); ay = Mathf.Clamp01(ay); }
        if (flipRoot && mirrorXOnFlip && Mathf.Sign(flipRoot.lossyScale.x) < 0f)
            ax = clampAnchors01 ? (1f - ax) : (1f - ax);

        Bounds b = sprite.bounds;
        Vector3 pw = new Vector3(
            Mathf.LerpUnclamped(b.min.x, b.max.x, ax),
            Mathf.LerpUnclamped(b.min.y, b.max.y, ay),
            transform.position.z
        );
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(pw, 0.04f);
        if (anchorWorld)
        {
            Gizmos.color = Color.cyan;
            Vector3 tgt = anchorWorld.position + (Vector3)extraOffset;
            Gizmos.DrawSphere(tgt, 0.05f);
            Gizmos.DrawLine(pw, tgt);
        }
    }
#endif
}
