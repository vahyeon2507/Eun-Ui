using UnityEngine;
using System;

[Serializable]
public class GameData
{
    public int currentLevel = 1;
    public int maxLevelReached = 1;
    public int totalScore = 0;
    public int totalDeaths = 0;
    public float totalPlayTime = 0f;
    public bool[] levelCompleted = new bool[10]; // 최대 10개 레벨
    public int[] levelBestScore = new int[10];
    public float[] levelBestTime = new float[10];
    public bool tutorialCompleted = false;
    public DateTime lastSaveTime;
}

public class GameDataManager : MonoBehaviour
{
    [Header("게임 데이터")]
    [SerializeField] private GameData gameData = new GameData();

    // 싱글톤 패턴
    public static GameDataManager Instance { get; private set; }

    // 이벤트
    public static event Action<GameData> OnDataLoaded;
    public static event Action<GameData> OnDataSaved;

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadGameData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 게임 시작 시간 기록 (이미 로드된 데이터가 있으면 유지)
        if (gameData.totalPlayTime == 0f)
        {
            gameData.totalPlayTime = Time.time;
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGameData();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveGameData();
        }
    }

    void OnDestroy()
    {
        SaveGameData();
    }

    public void LoadGameData()
    {
        try
        {
            string jsonData = PlayerPrefs.GetString("GameData", "");
            if (!string.IsNullOrEmpty(jsonData))
            {
                gameData = JsonUtility.FromJson<GameData>(jsonData);
                Debug.Log("[GameDataManager] 게임 데이터 로드 완료");
            }
            else
            {
                // 새로운 게임 데이터 생성
                gameData = new GameData();
                gameData.lastSaveTime = DateTime.Now;
                Debug.Log("[GameDataManager] 새로운 게임 데이터 생성");
            }

            OnDataLoaded?.Invoke(gameData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataManager] 데이터 로드 실패: {e.Message}");
            gameData = new GameData();
        }
    }

    public void SaveGameData()
    {
        try
        {
            gameData.lastSaveTime = DateTime.Now;
            string jsonData = JsonUtility.ToJson(gameData, true);
            PlayerPrefs.SetString("GameData", jsonData);
            PlayerPrefs.Save();

            Debug.Log("[GameDataManager] 게임 데이터 저장 완료");
            OnDataSaved?.Invoke(gameData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataManager] 데이터 저장 실패: {e.Message}");
        }
    }

    // 게임 진행 관련 메서드들
    public void CompleteLevel(int level, int score, float time)
    {
        if (level < 1 || level > gameData.levelCompleted.Length) return;

        gameData.levelCompleted[level - 1] = true;
        gameData.maxLevelReached = Mathf.Max(gameData.maxLevelReached, level + 1);
        gameData.totalScore += score;

        // 최고 기록 업데이트
        if (score > gameData.levelBestScore[level - 1])
        {
            gameData.levelBestScore[level - 1] = score;
        }

        if (time < gameData.levelBestTime[level - 1] || gameData.levelBestTime[level - 1] == 0)
        {
            gameData.levelBestTime[level - 1] = time;
        }

        SaveGameData();
    }

    public void AddDeath()
    {
        gameData.totalDeaths++;
        SaveGameData();
    }

    public void AddScore(int score)
    {
        gameData.totalScore += score;
        SaveGameData();
    }

    public void CompleteTutorial()
    {
        gameData.tutorialCompleted = true;
        SaveGameData();
    }

    public void SetCurrentLevel(int level)
    {
        gameData.currentLevel = level;
        SaveGameData();
    }

    // 데이터 조회 메서드들
    public GameData GetGameData()
    {
        return gameData;
    }

    public bool IsLevelCompleted(int level)
    {
        if (level < 1 || level > gameData.levelCompleted.Length) return false;
        return gameData.levelCompleted[level - 1];
    }

    public int GetLevelBestScore(int level)
    {
        if (level < 1 || level > gameData.levelBestScore.Length) return 0;
        return gameData.levelBestScore[level - 1];
    }

    public float GetLevelBestTime(int level)
    {
        if (level < 1 || level > gameData.levelBestTime.Length) return 0f;
        return gameData.levelBestTime[level - 1];
    }

    public int GetTotalScore()
    {
        return gameData.totalScore;
    }

    public int GetTotalDeaths()
    {
        return gameData.totalDeaths;
    }

    public int GetMaxLevelReached()
    {
        return gameData.maxLevelReached;
    }

    public bool IsTutorialCompleted()
    {
        return gameData.tutorialCompleted;
    }

    // 게임 데이터 초기화
    public void ResetGameData()
    {
        gameData = new GameData();
        SaveGameData();
        Debug.Log("[GameDataManager] 게임 데이터 초기화 완료");
    }

    // 특정 레벨 데이터만 초기화
    public void ResetLevelData(int level)
    {
        if (level < 1 || level > gameData.levelCompleted.Length) return;

        gameData.levelCompleted[level - 1] = false;
        gameData.levelBestScore[level - 1] = 0;
        gameData.levelBestTime[level - 1] = 0f;
        SaveGameData();
    }
}
