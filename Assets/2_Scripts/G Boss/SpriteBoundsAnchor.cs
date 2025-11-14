using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 스프라이트의 월드 바운즈(SR.bounds) 안에서 앵커(0~1) 지점을 월드 기준점(예: 발목)과 맞춰서
/// GfxRoot(이 컴포넌트가 붙은 오브젝트)를 이동시킨다.
/// - X/Y 락, 플립 대응, 퍼클립 오버라이드 지원
/// - 에디터 프리뷰: 재생 없이 클립/타임을 스크럽하며 즉시 보정 확인 + 버튼으로 오버라이드 저장
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SpriteBoundsAnchor : MonoBehaviour
{
    [Header("Target anchoring")]
    public SpriteRenderer sprite;          // 대상 스프라이트(보통 GfxRoot에 붙은 SR)
    public Transform anchorWorld;          // 월드 기준점(예: FootAnchor)

    [Header("Default anchor (normalized)")]
    [Tooltip("스프라이트 바운즈 내 정규화 앵커 X (0=좌, 1=우). 0~1 밖 값도 허용(오버슈트)")]
    public float anchorX = 0.5f;
    [Tooltip("스프라이트 바운즈 내 정규화 앵커 Y (0=하, 1=상). 0~1 밖 값도 허용(오버슈트)")]
    public float anchorY = 0.0f;

    [Header("Lock axes")]
    public bool lockX = false;
    public bool lockY = false;

    [Header("Fine tune (world units)")]
    public Vector2 extraOffset = Vector2.zero;

    [Header("Flip support")]
    [Tooltip("FlipRoot의 localScale.x가 음수일 때 X오프셋을 반전할지")]
    public bool mirrorXOnFlip = true;
    public Transform flipRoot;             // 없으면 transform 사용

    // -------- Per-clip override --------
    [Serializable]
    public struct ClipOverride
    {
        public AnimationClip clip;
        public float anchorX;
        public float anchorY;
        public Vector2 extraOffset;
    }

    [Header("Per-Clip Overrides (optional)")]
    public List<ClipOverride> perClip = new List<ClipOverride>();

    // -------- Editor Preview --------
#if UNITY_EDITOR
    [Header("Editor Preview (no Play mode)")]
    [Tooltip("체크 시 재생 없이 아래 클립/타임으로 포즈 샘플 후 앵커 보정 적용")]
    public bool editorPreview = false;
    public AnimationClip editorClip;
    [Range(0f, 1f)] public float editorNormalizedTime = 0f;
    public bool editorAutoPlay = false;
    [Min(0f)] public float editorPlaySpeed = 1f;

    // 편하게 저장: 현재 프리뷰 세팅을 해당 클립 오버라이드로 추가/갱신
    [ContextMenu("Per-Clip/Add or Update Override from Preview")]
    public void Editor_AddOrUpdateOverrideFromPreview()
    {
        if (!editorClip)
        {
            Debug.LogWarning("[SpriteBoundsAnchor] editorClip이 비었습니다.");
            return;
        }
        Undo.RecordObject(this, "AddOrUpdate Clip Override");
        int idx = perClip.FindIndex(o => o.clip == editorClip);
        var ov = new ClipOverride
        {
            clip = editorClip,
            anchorX = anchorX,
            anchorY = anchorY,
            extraOffset = extraOffset
        };
        if (idx >= 0) perClip[idx] = ov;
        else perClip.Add(ov);

        EditorUtility.SetDirty(this);
        Debug.Log($"[SpriteBoundsAnchor] '{editorClip.name}' 오버라이드 저장 완료.");
    }
#endif

    // -------- internals --------
    Camera _cam; // 안 써도 유지(확장 대비)

    void Reset()
    {
        sprite = GetComponent<SpriteRenderer>();
        if (!flipRoot) flipRoot = transform;
    }

    void OnEnable()
    {
        if (!flipRoot) flipRoot = transform;
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
#if UNITY_EDITOR
        // 에디터 프리뷰: 재생 아닐 때, 선택된 클립의 해당 타임 포즈를 샘플
        if (!Application.isPlaying && editorPreview && editorClip && sprite)
        {
            float t = Mathf.Clamp01(editorNormalizedTime) * editorClip.length;
            // 스프라이트 변경/키값 적용
            editorClip.SampleAnimation(sprite.gameObject, t);

            if (editorAutoPlay)
            {
                // 씬 뷰에서도 움직이게
                editorNormalizedTime += (Mathf.Approximately(editorClip.length, 0f) ? 0f
                    : (Time.deltaTime * editorPlaySpeed) / editorClip.length);
                if (editorNormalizedTime > 1f) editorNormalizedTime -= Mathf.Floor(editorNormalizedTime);
                SceneView.RepaintAll();
            }
        }
#endif
    }

    void LateUpdate()
    {
        if (sprite == null || anchorWorld == null) return;

        // 현재 활성 클립(프리뷰 중이면 프리뷰 클립, 런타임이면 레이어0 현재 클립)
        AnimationClip activeClip = GetActiveClip();

        // 기본값
        float ax = anchorX;
        float ay = anchorY;
        Vector2 off = extraOffset;

        // 퍼클립 오버라이드 적용
        if (activeClip)
        {
            if (TryGetOverride(activeClip, out var ov))
            {
                ax = ov.anchorX;
                ay = ov.anchorY;
                off = ov.extraOffset;
            }
        }

        ApplyAnchor(ax, ay, off);
    }

    AnimationClip GetActiveClip()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && editorPreview && editorClip) return editorClip;
#endif
        var anim = GetComponent<Animator>();
        if (anim)
        {
            var infos = anim.GetCurrentAnimatorClipInfo(0);
            if (infos != null && infos.Length > 0) return infos[0].clip;
        }
        return null;
    }

    bool TryGetOverride(AnimationClip clip, out ClipOverride ov)
    {
        for (int i = 0; i < perClip.Count; i++)
        {
            if (perClip[i].clip == clip)
            {
                ov = perClip[i];
                return true;
            }
        }
        ov = default;
        return false;
    }

    void ApplyAnchor(float ax, float ay, Vector2 worldOffset)
    {
        // 스프라이트 월드 바운즈
        Bounds b = sprite.bounds;

        // 0~1 밖 값도 허용: LerpUnclamped
        float wx = Mathf.LerpUnclamped(b.min.x, b.max.x, ax);
        float wy = Mathf.LerpUnclamped(b.min.y, b.max.y, ay);
        Vector3 spriteAnchor = new Vector3(wx, wy, transform.position.z);

        // 플립 시 X 오프셋 반전
        float sign = 1f;
        var fr = flipRoot ? flipRoot : transform;
        if (mirrorXOnFlip && fr.localScale.x < 0f) sign = -1f;

        Vector3 target = anchorWorld.position + new Vector3(worldOffset.x * sign, worldOffset.y, 0f);
        Vector3 delta = target - spriteAnchor;

        Vector3 pos = transform.position;
        if (!lockX) pos.x += delta.x;
        if (!lockY) pos.y += delta.y;
        transform.position = pos;
    }
}
