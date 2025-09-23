using UnityEngine;
using UnityEngine.UI;

public class UIDim : MonoBehaviour
{
    [SerializeField] private Image dimImage;

    public void ShowDim()
    {
        gameObject.SetActive(true);
        dimImage.CrossFadeAlpha(0.5f, 0.25f, false); // 0.25초 동안 서서히 어두워짐
    }

    public void HideDim()
    {
        dimImage.CrossFadeAlpha(0f, 0.25f, false);
        Invoke(nameof(Disable), 0.3f); // 애니 끝나고 비활성화
    }

    void Disable()
    {
        gameObject.SetActive(false);
    }
}

