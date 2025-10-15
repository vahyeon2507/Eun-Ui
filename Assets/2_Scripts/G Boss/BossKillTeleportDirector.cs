using UnityEngine;

public class BossKillTeleportDirector : MonoBehaviour
{
    public static BossKillTeleportDirector Instance { get; private set; }

    [Header("Gate Spawn")]
    public Transform spawnPoint;                 // 게이트가 생길 위치(필수)
    public GameObject gatePrefab;                // TeleportGateTrigger2D 달린 프리팹
    public GameObject preplacedGate;             // 미리 배치해둔 게이트(선택). 있으면 이걸 활성화해서 사용
    public bool hidePreplacedOnStart = true;     // 시작 시 미리 배치된 게이트 숨기기

    [Header("Next Scene")]
    public string nextSceneName = "NextScene";   // 기본 다음 씬(필요 시 런타임에 override 가능)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (preplacedGate && hidePreplacedOnStart) preplacedGate.SetActive(false);
    }

    // 보스 코드에서 호출해도 되고, 이벤트로 연결해도 됨
    public static void SignalBossDied() { if (Instance) Instance.OnBossDied(); }

    public void OnBossDied()
    {
        ActivateGate(nextSceneName);
    }

    public void ActivateGate(string nextSceneOverride = null)
    {
        if (!spawnPoint)
        {
            Debug.LogError("[BossKillTeleportDirector] spawnPoint가 비었습니다!", this);
            return;
        }

        string sceneToLoad = string.IsNullOrEmpty(nextSceneOverride) ? nextSceneName : nextSceneOverride;

        GameObject gate = null;
        if (preplacedGate)
        {
            gate = preplacedGate;
            gate.transform.position = spawnPoint.position;
            preplacedGate.SetActive(true);
        }
        else
        {
            if (!gatePrefab)
            {
                Debug.LogError("[BossKillTeleportDirector] gatePrefab이 비었습니다!", this);
                return;
            }
            gate = Instantiate(gatePrefab, spawnPoint.position, spawnPoint.rotation);
        }

        var trig = gate.GetComponent<TeleportGateTrigger2D>();
        if (!trig)
        {
            Debug.LogError("[BossKillTeleportDirector] 게이트에 TeleportGateTrigger2D가 없습니다!", gate);
            return;
        }
        trig.nextSceneName = sceneToLoad;

        Debug.Log($"[BossKillTeleportDirector] Gate 활성화됨 @ {spawnPoint.position}, 다음 씬: {sceneToLoad}");
    }
}
