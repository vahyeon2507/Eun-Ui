using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeShop : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text upgradeLevelText;
    [SerializeField] private TMP_Text upgradeCostText;
    [SerializeField] private TMP_Text upgradeStatusText;

    [Header("강화 설정")]
    [SerializeField] private int baseCost = 100;
    [SerializeField] private int costIncreasePerLevel = 50;
    [SerializeField] private int maxUpgradeLevel = 10;

    [Header("상점 열기/닫기")]
    [SerializeField] private KeyCode toggleKey = KeyCode.U;
    [SerializeField] private bool canToggle = false;

    private int currentUpgradeLevel = 0;

    void Start()
    {
        if (GameDataManager.Instance != null)
        {
            currentUpgradeLevel = GameDataManager.Instance.GetUpgradeLevel();
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeButtonClick);
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        UpdateUI();
    }

    void Update()
    {
        if (canToggle && Input.GetKeyDown(toggleKey))
        {
            ToggleShop();
        }
    }

    public void ToggleShop()
    {
        if (shopPanel != null)
        {
            bool isActive = shopPanel.activeSelf;
            shopPanel.SetActive(!isActive);
            
            if (!isActive)
            {
                UpdateUI();
            }
        }
    }

    public void OpenShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            UpdateUI();
        }
    }

    public void CloseShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    void OnUpgradeButtonClick()
    {
        TryUpgrade();
    }

    public bool TryUpgrade()
    {
        if (currentUpgradeLevel >= maxUpgradeLevel)
        {
            if (upgradeStatusText != null)
            {
                upgradeStatusText.text = "최대 강화 레벨에 도달했습니다!";
            }
            return false;
        }

        int cost = GetUpgradeCost(currentUpgradeLevel + 1);

        if (GameDataManager.Instance == null)
        {
            Debug.LogError("[UpgradeShop] GameDataManager.Instance가 null입니다!");
            return false;
        }

        if (GameDataManager.Instance.GetCurrency() < cost)
        {
            if (upgradeStatusText != null)
            {
                upgradeStatusText.text = "재화가 부족합니다!";
            }
            return false;
        }

        if (GameDataManager.Instance.SpendCurrency(cost))
        {
            currentUpgradeLevel++;
            GameDataManager.Instance.SetUpgradeLevel(currentUpgradeLevel);

            ApplyUpgradeToPlayer();

            if (upgradeStatusText != null)
            {
                upgradeStatusText.text = $"강화 성공! 레벨 {currentUpgradeLevel}";
            }

            UpdateUI();
            return true;
        }

        return false;
    }

    int GetUpgradeCost(int targetLevel)
    {
        return baseCost + (costIncreasePerLevel * (targetLevel - 1));
    }

    void UpdateUI()
    {
        if (GameDataManager.Instance == null) return;

        int currency = GameDataManager.Instance.GetCurrency();
        currentUpgradeLevel = GameDataManager.Instance.GetUpgradeLevel();

        if (currencyText != null)
        {
            currencyText.text = $"재화: {currency}";
        }

        if (upgradeLevelText != null)
        {
            upgradeLevelText.text = $"현재 강화 레벨: {currentUpgradeLevel} / {maxUpgradeLevel}";
        }

        if (upgradeCostText != null)
        {
            if (currentUpgradeLevel >= maxUpgradeLevel)
            {
                upgradeCostText.text = "최대 강화";
            }
            else
            {
                int cost = GetUpgradeCost(currentUpgradeLevel + 1);
                upgradeCostText.text = $"강화 비용: {cost}";
            }
        }

        if (upgradeButton != null)
        {
            bool canUpgrade = currentUpgradeLevel < maxUpgradeLevel;
            if (canUpgrade)
            {
                int cost = GetUpgradeCost(currentUpgradeLevel + 1);
                canUpgrade = currency >= cost;
            }
            upgradeButton.interactable = canUpgrade;
        }

        if (upgradeStatusText != null)
        {
            if (currentUpgradeLevel >= maxUpgradeLevel)
            {
                upgradeStatusText.text = "최대 강화 레벨에 도달했습니다!";
            }
        }
    }

    public int GetCurrentUpgradeLevel()
    {
        return currentUpgradeLevel;
    }

    public int GetMaxUpgradeLevel()
    {
        return maxUpgradeLevel;
    }

    void ApplyUpgradeToPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ApplyUpgradeBonus();
            playerHealth.currentHealth = playerHealth.maxHealth;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.ApplyUpgradeBonus();
        }
    }
}
