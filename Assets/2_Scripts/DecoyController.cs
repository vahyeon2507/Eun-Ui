using UnityEngine;

public class DecoyController : MonoBehaviour
{
    private bool isReal;

    public void Setup(bool isReal)
    {
        this.isReal = isReal;

        // 예: 진짜는 하얀색, 가짜는 회색
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = isReal ? Color.white : Color.gray;
        }
    }

    public bool IsReal()
    {
        return isReal;
    }
}
