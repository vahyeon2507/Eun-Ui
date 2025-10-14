using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFxHooks : MonoBehaviour
{
    [System.Serializable] public class AnchorEntry { public string key = "Feet"; public Transform anchor; }
    [System.Serializable] public class FxEntry { public string id = "JumpDust"; public FxDefinition2D def; }

    [Header("Anchors")] public List<AnchorEntry> anchors = new();
    [Header("Fx Library")] public List<FxEntry> fxList = new();
    [Header("Audio")] public AudioSource audioSource;

    Dictionary<string, Transform> _anchorMap;
    Dictionary<string, FxDefinition2D> _fxMap;
    // === Attack-range FX helpers ===============================================
    static readonly List<Collider2D> _tmpCols = new List<Collider2D>(8);
    // PlayerFxHooks 안에 추가 (public)
    public void PlayFxOnCollider(string fxId, Collider2D col)
    {
        if (!col) return;
        var p = RandomPointInCollider(col);   // 내부 유틸로 콜라이더 안 랜덤 좌표
        PlayFxAtWorld(fxId, p);               // 기존 규칙(미러/정렬/SFX/파괴) 그대로 적용
    }
    [Header("Attack FX")]
    [Tooltip("기본 공격에 사용할 FX id (PlayerFxHooks.fxList의 id)")]
    public string hitFxId = "HitSpark";

    // --- Special 전용 FX (신규) ---
    [Tooltip("패링 스페셜 전용 FX id (PlayerFxHooks.fxList의 id)")]
    public string parrySpecialFxId = "ParryBurst";

    // 스페셜 중복방지(스윙 1회당 1번만 FX) 가드
    bool _attackHitFiredThisSwing = false;
    bool _fxSpawnedThisSwing = false;

    /// <summary>히트박스 배열 중 활성 콜라이더 하나를 랜덤 픽</summary>
    /// 

    Collider2D PickRandomActive(Collider2D[] group)
    {
        _tmpCols.Clear();
        if (group != null)
        {
            for (int i = 0; i < group.Length; i++)
            {
                var c = group[i];
                if (!c) continue;
                if (!c.enabled) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                _tmpCols.Add(c);
            }
        }
        if (_tmpCols.Count == 0) return null;
        return _tmpCols[Random.Range(0, _tmpCols.Count)];
    }

    /// <summary>지정한 Collider2D 안쪽 임의 위치(월드좌표) 샘플링</summary>
    Vector3 RandomPointInCollider(Collider2D col, int maxTries = 10)
    {
        if (!col) return col.bounds.center;

        // 케이스별 빠른 샘플
        var t = col.transform;
        if (col is BoxCollider2D b)
        {
            var local = b.offset + new Vector2(
                Random.Range(-b.size.x * 0.5f, b.size.x * 0.5f),
                Random.Range(-b.size.y * 0.5f, b.size.y * 0.5f));
            return t.TransformPoint(local);
        }
        if (col is CircleCollider2D cir)
        {
            var local = cir.offset + Random.insideUnitCircle * cir.radius;
            return t.TransformPoint(local);
        }
        if (col is CapsuleCollider2D cap)
        {
            for (int i = 0; i < maxTries; i++)
            {
                var local = cap.offset + new Vector2(
                    Random.Range(-cap.size.x * 0.5f, cap.size.x * 0.5f),
                    Random.Range(-cap.size.y * 0.5f, cap.size.y * 0.5f));
                var world = t.TransformPoint(local);
                if (cap.OverlapPoint(world)) return world;
            }
            return t.TransformPoint(cap.offset);
        }
        if (col is PolygonCollider2D poly)
        {
            var bnds = poly.bounds;
            for (int i = 0; i < maxTries; i++)
            {
                var world = new Vector2(
                    Random.Range(bnds.min.x, bnds.max.x),
                    Random.Range(bnds.min.y, bnds.max.y));
                if (poly.OverlapPoint(world)) return world;
            }
            return bnds.center;
        }

        // 불명 타입: AABB 안에서 거절 샘플
        {
            var bnds = col.bounds;
            for (int i = 0; i < maxTries; i++)
            {
                var world = new Vector2(
                    Random.Range(bnds.min.x, bnds.max.x),
                    Random.Range(bnds.min.y, bnds.max.y));
                if (col.OverlapPoint(world)) return world;
            }
            return bnds.center;
        }
    }

    /// <summary>월드 좌표에 FX 즉시 스폰(팔로우/오프셋 무시, 다른 옵션은 그대로 적용)</summary>
    public void PlayFxAtWorld(string fxId, Vector3 worldPos)
    {
        if (!_fxMap.TryGetValue(fxId, out var def) || !def || !def.prefab) return;

        float sign = (transform.localScale.x >= 0f) ? 1f : -1f;

        // 생성
        var go = Instantiate(def.prefab, worldPos, Quaternion.identity);

        // 부모에 붙이지 않는 단발 스폰이므로 parentWillMirror: false
        ApplyMirrorOnce(go, def, sign, parentWillMirror: false);

        // 정렬
        if (!string.IsNullOrEmpty(def.sortingLayerOverride))
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            { sr.sortingLayerName = def.sortingLayerOverride; sr.sortingOrder = def.orderInLayerOverride; }

        // SFX
        if (def.sfx)
        {
            if (audioSource) audioSource.PlayOneShot(def.sfx, def.sfxVolume);
            else AudioSource.PlayClipAtPoint(def.sfx, worldPos, def.sfxVolume);
        }

        if (def.autoDestroy > 0f) Destroy(go, def.autoDestroy);
    }

    /// <summary>
    /// 현재 공격 히트박스 그룹 내부 임의 위치에 FX 스폰(1회)
    /// </summary>
    public void PlayFxInHitboxGroup(string fxId, Collider2D[] hitboxGroup)
    {
        var col = PickRandomActive(hitboxGroup);
        if (!col) { PlayFx(fxId); return; } // 히트박스 없으면 기존 앵커 스폰으로 폴백
        var p = RandomPointInCollider(col);
        PlayFxAtWorld(fxId, p);
    }

    void Awake()
    {
        _anchorMap = new();
        foreach (var a in anchors) if (a != null && !string.IsNullOrEmpty(a.key) && a.anchor) _anchorMap[a.key] = a.anchor;

        _fxMap = new();
        foreach (var f in fxList) if (f != null && !string.IsNullOrEmpty(f.id) && f.def) _fxMap[f.id] = f.def;

        if (!audioSource) audioSource = GetComponent<AudioSource>();
    }

    public void PlayFx(string fxId, string anchorKey = null, Transform anchorOverride = null)
    {
        if (!_fxMap.TryGetValue(fxId, out var def) || !def || !def.prefab) return;

        // 앵커
        Transform anchor = anchorOverride;
        if (!anchor && !string.IsNullOrEmpty(anchorKey)) _anchorMap.TryGetValue(anchorKey, out anchor);
        if (!anchor) anchor = transform;

        // 좌/우 부호
        float sign = (transform.localScale.x >= 0f) ? 1f : -1f;

        // 월드 스폰 위치
        Vector2 off = def.offset;
        if (def.signedByFacing) off.x *= sign;
        Vector3 spawnPos = anchor.position + (Vector3)off;

        // 생성
        var go = Instantiate(def.prefab, spawnPos, def.followRotation ? anchor.rotation : Quaternion.identity);

        // ====== 미러링 적용 ======
        ApplyMirrorOnce(go, def, sign, parentWillMirror: def.follow == FxDefinition2D.FollowMode.Attach);

        // ====== 붙임/팔로우 모드 ======
        switch (def.follow)
        {
            case FxDefinition2D.FollowMode.Attach:
                go.transform.SetParent(anchor, worldPositionStays: true);
                // 부모가 좌우 플립을 가져가므로 로컬 오프셋만 그대로 주면 됨
                go.transform.localPosition = def.offset;
                if (def.followRotation) go.transform.localRotation = Quaternion.identity;
                break;

            case FxDefinition2D.FollowMode.SoftFollow:
                var flw = go.AddComponent<_FxAnchorFollower>();
                flw.Setup(anchor, def.offset, def.signedByFacing, def.followRotation,
                          def.followDuration, transform,
                          def.mirrorByFacing, def.mirrorMode);
                break;

            case FxDefinition2D.FollowMode.None:
            default:
                break;
        }

        // 정렬
        if (!string.IsNullOrEmpty(def.sortingLayerOverride))
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            { sr.sortingLayerName = def.sortingLayerOverride; sr.sortingOrder = def.orderInLayerOverride; }

        // SFX
        if (def.sfx)
        {
            if (audioSource) audioSource.PlayOneShot(def.sfx, def.sfxVolume);
            else AudioSource.PlayClipAtPoint(def.sfx, go.transform.position, def.sfxVolume);
        }

        if (def.autoDestroy > 0f) Destroy(go, def.autoDestroy);
    }

    public void Anim_PlayFx(string fxId) => PlayFx(fxId);
    public void Anim_PlayFxAt(string fxId, string anchorKey) => PlayFx(fxId, anchorKey);

    // --- helper: 한 번만 미러링 적용(Attach가 아닌 경우) ---
    void ApplyMirrorOnce(GameObject go, FxDefinition2D def, float sign, bool parentWillMirror)
    {
        if (!def.mirrorByFacing) return;

        // Attach면 부모(Player)가 좌우 플립을 가져가므로 자식에서 따로 뒤집을 필요 없음
        if (parentWillMirror) return;

        if (sign >= 0f) return; // 오른쪽 보고 있으면 그대로

        if (def.mirrorMode == FxDefinition2D.MirrorMode.ScaleX)
        {
            var s = go.transform.localScale;
            s.x = Mathf.Abs(s.x) * -1f;
            go.transform.localScale = s;
        }
        else // FlipSpriteRenderer
        {
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.flipX = !sr.flipX; // 기본이 오른쪽 바라보는 에셋 가정
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (anchors == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        foreach (var a in anchors) if (a != null && a.anchor)
            { Gizmos.DrawWireSphere(a.anchor.position, 0.06f); UnityEditor.Handles.Label(a.anchor.position + Vector3.up * 0.08f, a.key); }
    }
#endif
}

/// <summary>
/// 자식으로 붙이지 않고, 지정 시간 동안만 앵커를 따라가며
/// 좌/우 뒤집기도 매 프레임 갱신
/// </summary>
class _FxAnchorFollower : MonoBehaviour
{
    Transform _anchor, _rootForFacing;
    Vector2 _offset;
    bool _signed, _rot;
    float _remain;

    bool _mirror;
    FxDefinition2D.MirrorMode _mirrorMode;
    Vector3 _initialScale;
    SpriteRenderer[] _srs;

    public void Setup(Transform anchor, Vector2 offset, bool signedByFacing, bool followRotation,
                      float duration, Transform rootForFacing,
                      bool mirrorByFacing, FxDefinition2D.MirrorMode mirrorMode)
    {
        _anchor = anchor; _offset = offset; _signed = signedByFacing; _rot = followRotation;
        _remain = Mathf.Max(0f, duration);
        _rootForFacing = rootForFacing;

        _mirror = mirrorByFacing;
        _mirrorMode = mirrorMode;
        _initialScale = transform.localScale;
        _srs = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void LateUpdate()
    {
        if (!_anchor) { Destroy(this); return; }
        if (_remain <= 0f) { Destroy(this); return; }

        float sign = 1f;
        if (_signed && _rootForFacing) sign = (_rootForFacing.localScale.x >= 0f) ? 1f : -1f;

        // 위치 추적
        Vector3 target = _anchor.position + (Vector3)new Vector2(_offset.x * sign, _offset.y);
        transform.position = target;
        if (_rot) transform.rotation = _anchor.rotation;

        // 좌/우 미러링 갱신
        if (_mirror && _rootForFacing)
        {
            if (_mirrorMode == FxDefinition2D.MirrorMode.ScaleX)
            {
                var s = _initialScale;
                s.x *= (_rootForFacing.localScale.x >= 0f) ? 1f : -1f;
                transform.localScale = s;
            }
            else
            {
                bool flip = (_rootForFacing.localScale.x < 0f);
                for (int i = 0; i < _srs.Length; i++) if (_srs[i]) _srs[i].flipX = flip;
            }
        }

        _remain -= Time.deltaTime;


    }


}
