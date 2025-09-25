using UnityEngine;

/// <summary>
/// 씬별 BGM 자동 재생을 위한 컨트롤러
/// 씬에 배치하여 해당 씬에 맞는 BGM을 자동으로 재생
/// </summary>
public class BGMController : MonoBehaviour
{
    [Header("씬 BGM 설정")]
    [SerializeField] private string bgmTrackName = "GameBGM";
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool fadeIn = true;
    [SerializeField] private float playDelay = 0f;
    
    [Header("보스전 BGM (선택사항)")]
    [SerializeField] private string bossBGMTrackName = "BossBGM";
    [SerializeField] private bool hasBossMusic = false;
    
    private void Start()
    {
        if (playOnStart && AudioManager.Instance != null)
        {
            if (playDelay > 0f)
            {
                Invoke(nameof(PlayBGM), playDelay);
            }
            else
            {
                PlayBGM();
            }
        }
    }
    
    private void PlayBGM()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(bgmTrackName, fadeIn);
        }
    }
    
    /// <summary>
    /// 보스전 BGM으로 전환 (보스 등장 시 호출)
    /// </summary>
    public void PlayBossBGM()
    {
        if (hasBossMusic && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(bossBGMTrackName, true);
        }
    }
    
    /// <summary>
    /// 일반 BGM으로 되돌리기 (보스 처치 후 호출)
    /// </summary>
    public void PlayNormalBGM()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(bgmTrackName, true);
        }
    }
    
    /// <summary>
    /// BGM 정지
    /// </summary>
    public void StopBGM(bool fadeOut = true)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM(fadeOut);
        }
    }
    
    /// <summary>
    /// 외부에서 BGM 트랙 변경
    /// </summary>
    public void ChangeBGM(string trackName, bool fadeIn = true)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(trackName, fadeIn);
        }
    }
}
