using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFxHooks : MonoBehaviour
{
    [System.Serializable] public class AnchorEntry { public string key = "Feet"; public Transform anchor; }
    [System.Serializable] public class FxEntry { public string id = "JumpDust"; public FxDefinition2D def; }

    [Header("Anchors")] public List<AnchorEntry> anchors = new();
    [Header("Fx Library")] public List<FxEntry> fxList = new();
    [Header("Audio (optional)")] public AudioSource audioSource;

    Dictionary<string, Transform> _anchorMap;
    Dictionary<string, FxDefinition2D> _fxMap;

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

        // 앵커 찾기
        Transform anchor = anchorOverride;
        if (!anchor && !string.IsNullOrEmpty(anchorKey)) _anchorMap.TryGetValue(anchorKey, out anchor);
        if (!anchor) anchor = transform;

        // 생성 위치 계산(월드)
        float sign = (transform.localScale.x >= 0f) ? 1f : -1f;
        Vector2 off = def.offset;
        if (def.signedByFacing) off.x *= sign;
        Vector3 spawnPos = anchor.position + (Vector3)off;

        // 생성
        GameObject go = Instantiate(def.prefab, spawnPos, def.followRotation ? anchor.rotation : Quaternion.identity);

        switch (def.follow)
        {
            case FxDefinition2D.FollowMode.Attach:
                // 자식으로 붙여서 끝까지 따라가기(로컬 오프셋 사용)
                go.transform.SetParent(anchor, worldPositionStays: true);
                // 로컬 오프셋은 부호 곱할 필요 없음(부모 플립에 의해 월드에서 반전됨)
                go.transform.localPosition = def.offset;
                if (def.followRotation) go.transform.localRotation = Quaternion.identity;
                break;

            case FxDefinition2D.FollowMode.SoftFollow:
                // 자식으로 붙이지 않고 duration 동안만 추적
                var flw = go.AddComponent<_FxAnchorFollower>();
                flw.Setup(anchor, def.offset, def.signedByFacing, def.followRotation, def.followDuration, transform);
                break;

            case FxDefinition2D.FollowMode.None:
            default:
                // 아무것도 안 함(생성 좌표 고정)
                break;
        }

        // 정렬(선택)
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
/// 자식으로 붙이지 않고, 지정 시간 동안만 앵커를 따라가게 하는 보조 컴포넌트
/// </summary>
class _FxAnchorFollower : MonoBehaviour
{
    Transform _anchor, _rootForFacing;
    Vector2 _offset;
    bool _signed, _rot;
    float _remain;

    public void Setup(Transform anchor, Vector2 offset, bool signedByFacing, bool followRotation, float duration, Transform rootForFacing)
    {
        _anchor = anchor; _offset = offset; _signed = signedByFacing; _rot = followRotation;
        _remain = Mathf.Max(0f, duration);
        _rootForFacing = rootForFacing; // 보통 플레이어 루트(좌우 플립 판단용)
    }

    void LateUpdate()
    {
        if (!_anchor) { Destroy(this); return; }
        if (_remain <= 0f) { Destroy(this); return; }

        float sign = 1f;
        if (_signed && _rootForFacing) sign = (_rootForFacing.localScale.x >= 0f) ? 1f : -1f;

        Vector3 target = _anchor.position + (Vector3)new Vector2(_offset.x * sign, _offset.y);
        transform.position = target;
        if (_rot) transform.rotation = _anchor.rotation;

        _remain -= Time.deltaTime;
    }
}
