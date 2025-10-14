using UnityEngine;

/// <summary>
/// 부적 UI 테스트용 간단한 스크립트
/// </summary>
public class SimpleTalismanTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[SimpleTest] Start() 호출됨!");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("[SimpleTest] Tab 키 감지됨!");
        }
    }
    
    [ContextMenu("테스트 로그")]
    public void TestLog()
    {
        Debug.Log("[SimpleTest] Context Menu 테스트 성공!");
    }
}
