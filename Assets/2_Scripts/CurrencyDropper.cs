using UnityEngine;

public class CurrencyDropper : MonoBehaviour
{
    [Header("재화 드롭 설정")]
    [SerializeField] private GameObject currencyDropPrefab;
    [SerializeField] private int minCurrencyAmount = 5;
    [SerializeField] private int maxCurrencyAmount = 15;
    [SerializeField] private bool useRandomAmount = true;
    [SerializeField] private int fixedCurrencyAmount = 10;

    [Header("드롭 위치")]
    [SerializeField] private Vector2 dropOffset = Vector2.zero;
    [SerializeField] private bool randomizeDropPosition = true;
    [SerializeField] private float randomDropRadius = 0.5f;

    public void DropCurrency()
    {
        if (currencyDropPrefab == null)
        {
            Debug.LogWarning("[CurrencyDropper] CurrencyDrop 프리팹이 설정되지 않았습니다! Inspector에서 Currency Drop Prefab을 할당하세요.");
            return;
        }

        int amount = useRandomAmount 
            ? Random.Range(minCurrencyAmount, maxCurrencyAmount + 1) 
            : fixedCurrencyAmount;

        Vector3 dropPosition = transform.position + (Vector3)dropOffset;
        
        if (randomizeDropPosition)
        {
            Vector2 randomOffset = Random.insideUnitCircle * randomDropRadius;
            dropPosition += (Vector3)randomOffset;
        }

        GameObject drop = Instantiate(currencyDropPrefab, dropPosition, Quaternion.identity);
        
        CurrencyDrop currencyDrop = drop.GetComponent<CurrencyDrop>();
        if (currencyDrop != null)
        {
            currencyDrop.SetCurrencyAmount(amount);
            Debug.Log($"[CurrencyDropper] 재화 드롭 성공! 위치: {dropPosition}, 재화량: {amount}");
        }
        else
        {
            Debug.LogWarning("[CurrencyDropper] CurrencyDrop 프리팹에 CurrencyDrop 스크립트가 없습니다!");
        }
    }

    public void DropCurrency(int amount)
    {
        if (currencyDropPrefab == null)
        {
            Debug.LogWarning("[CurrencyDropper] CurrencyDrop 프리팹이 설정되지 않았습니다!");
            return;
        }

        Vector3 dropPosition = transform.position + (Vector3)dropOffset;
        
        if (randomizeDropPosition)
        {
            Vector2 randomOffset = Random.insideUnitCircle * randomDropRadius;
            dropPosition += (Vector3)randomOffset;
        }

        GameObject drop = Instantiate(currencyDropPrefab, dropPosition, Quaternion.identity);
        
        CurrencyDrop currencyDrop = drop.GetComponent<CurrencyDrop>();
        if (currencyDrop != null)
        {
            currencyDrop.SetCurrencyAmount(amount);
        }
    }
}
